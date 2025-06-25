// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    public class DownloadPopupWindow : PopUpWindow
    {
        private int m_SketchIndex;
        [SerializeField] private Renderer m_ProgressBar;

        private SceneFileInfo m_SceneFileInfo;
        private SketchSetType m_SketchSetType;

        private TaskAndCts m_DownloadTask;
        private UnityWebRequest m_WebRequest;
        private double m_WebRequestStartTime;

        override public void SetPopupCommandParameters(int commandParam, int commandParam2)
        {
            if (commandParam2 != (int)SketchSetType.Drive
                && commandParam2 != (int)SketchSetType.Curated
                && commandParam2 != (int)SketchSetType.Liked)
            {
                Debug.LogWarning("Download popup window created for a " +
                    "sketch in a non-cloud sketch set. Should still work but " +
                    "it would be quicker to directly load the file.");
                return;
            }

            m_SketchSetType = (SketchSetType)commandParam2;
            m_SketchIndex = commandParam;

            var set = SketchCatalog.m_Instance.GetSet(m_SketchSetType);
            m_SceneFileInfo = set.GetSketchSceneFileInfo(m_SketchIndex);

            if (m_SceneFileInfo == null)
            {
                Debug.LogWarning("Sketch file cannot be found for " +
                    "download. Maybe the sketch set has refreshed.");
                return;
            }

            if (m_SceneFileInfo.Available)
            {
                Debug.LogWarning("Download popup window created for an " +
                    "already available sketch. Should still work but " +
                    "it would be quicker to directly load the file.");
                return;
            }

            m_ProgressBar.material.SetFloat("_Ratio", 0);

            m_DownloadTask = new TaskAndCts();
            if (m_SketchSetType == SketchSetType.Drive)
            {
                m_DownloadTask.Task = (m_SceneFileInfo as GoogleDriveSketchSet.GoogleDriveFileInfo)
                    .DownloadAsync(m_DownloadTask.Token);
            }
            else if (m_SketchSetType == SketchSetType.Curated
                     || m_SketchSetType == SketchSetType.Liked)
            {
                StartCoroutine(RetryDownloadTiltCoroutine());
            }
        }

        private IEnumerator RetryDownloadTiltCoroutine()
        {
            const int kDownloadBufferSize = 1024 * 1024;
            byte[] downloadBuffer = new byte[kDownloadBufferSize];
            var info = m_SceneFileInfo as IcosaSceneFileInfo;
            if (info == null)
            {
                Debug.LogWarning("Unexpected file info type.");
                yield break;
            }

            const int kRetryAttempts = 3;
            for (int i = 0; i < kRetryAttempts; i++)
            {
                if (info.TiltDownloaded || m_DownloadTask.Cts.IsCancellationRequested)
                {
                    break;
                }

                yield return DownloadTiltCoroutine(info, downloadBuffer);
                yield return null;
            }
        }

        private IEnumerator DownloadTiltCoroutine(IcosaSceneFileInfo info, byte[] buffer)
        {
            bool notifyOnError = true;
            void NotifyCreateError(IcosaSceneFileInfo sceneFileInfo, string type, Exception ex)
            {
                string error = $"Error downloading {type} file for {sceneFileInfo.HumanName}.";
                ControllerConsoleScript.m_Instance.AddNewLine(error, notifyOnError);
                notifyOnError = false;
                Debug.LogWarning($"{ex} {sceneFileInfo.HumanName} {sceneFileInfo.TiltPath}");
            }

            void NotifyWriteError(IcosaSceneFileInfo sceneFileInfo, string type, UnityWebRequest www)
            {
                string error = $"Error downloading {type} file for {sceneFileInfo.HumanName}.\n" +
                    "Out of disk space?";
                ControllerConsoleScript.m_Instance.AddNewLine(error, notifyOnError);
                notifyOnError = false;
                Debug.LogWarning($"{www.error} {sceneFileInfo.HumanName} {sceneFileInfo.TiltPath}");
            }

            using (m_WebRequest = UnityWebRequest.Get(info.TiltFileUrl))
            {
                try
                {
                    m_WebRequest.downloadHandler = new DownloadHandlerFastFile(info.TiltPath, buffer);
                }
                catch (Exception ex)
                {
                    NotifyCreateError(info, "sketch", ex);
                    yield break;
                }

                // Do request and wait until done.
                m_WebRequestStartTime = Time.realtimeSinceStartupAsDouble;
                var op = m_WebRequest.SendWebRequest();
                while (!op.isDone)
                {
                    // Be careful here. The coroutine may be stopped at any
                    // time, never coming back to this point. If execution does
                    // not make it to the end of the using block, file handles
                    // etc will never be released, so this must be done manually
                    // wherever the coroutine is stopped.
                    yield return null;
                    if (m_DownloadTask.Cts.IsCancellationRequested)
                    {
                        break;
                    }
                }

                if (m_WebRequest.isDone && !m_DownloadTask.Cts.IsCancellationRequested)
                {
                    if (m_WebRequest.isNetworkError
                        || m_WebRequest.responseCode >= 400
                        || !string.IsNullOrEmpty(m_WebRequest.error))
                    {
                        NotifyWriteError(info, "sketch", m_WebRequest);
                    }
                    else
                    {
                        info.TiltDownloaded = true;
                    }
                }
            }
            m_WebRequest = null;
        }

        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();
            if (m_SceneFileInfo == null)
            {
                return;
            }

            var progress = 0.0f;
            if (m_SketchSetType == SketchSetType.Drive)
            {
                progress = (m_SceneFileInfo as GoogleDriveSketchSet.GoogleDriveFileInfo)
                    .Progress;
            }
            else if (m_SketchSetType == SketchSetType.Curated
                     || m_SketchSetType == SketchSetType.Liked)
            {
                // Make the bar go up while request is in-flight so the user
                // knows something is happening.
                const float kRequestProportion = 0.3f;
                const float kDownloadProportion = 1 - kRequestProportion;
                const float kRequestTime = 1.5f;
                if (m_WebRequest == null || m_WebRequest.downloadProgress == 0)
                {
                    var delta = Time.realtimeSinceStartupAsDouble - m_WebRequestStartTime;
                    var deltaProportion = Mathf.Clamp01((float)delta / kRequestTime);
                    progress = kRequestProportion * deltaProportion;
                }
                else
                {
                    progress = kRequestProportion + kDownloadProportion * m_WebRequest.downloadProgress;
                }
            }

            m_ProgressBar.material.SetFloat("_Ratio", progress);
        }

        protected override void BaseUpdate()
        {
            base.BaseUpdate();
            if (m_SceneFileInfo == null || !m_SceneFileInfo.Available)
            {
                return;
            }

            if (m_ParentPanel)
            {
                m_ParentPanel.ResolveDelayedButtonCommand(true);
            }
        }

        public override bool RequestClose(bool bForceClose = false)
        {
            bool close = base.RequestClose(bForceClose);
            if (close)
            {
                m_DownloadTask.Cts.Cancel();
            }
            return close;
        }

        private void OnDestroy()
        {

            if (m_DownloadTask != null)
            {
                m_DownloadTask.Cts.Cancel();
                m_DownloadTask.Cts?.Dispose();
                m_DownloadTask = null;
            }

            // If we are destroyed while in the middle of a coroutine, the
            // coroutine will not finish, and 'using' blocks will not dispose
            // their arguments. Do it manually if we never reached the end of
            // the coroutine.
            StopAllCoroutines();
            if (m_WebRequest != null)
            {
                m_WebRequest.Dispose();
                m_WebRequest = null;
            }

            if (m_SceneFileInfo is IcosaSceneFileInfo icosaSceneFileInfo
                && !icosaSceneFileInfo.TiltDownloaded)
            {
                // If anything goes wrong we may be left with a partial download
                // at TiltPath. Attempt to clean it up to prevent failed loads
                // later.
                try
                {
                    File.Delete(icosaSceneFileInfo.TiltPath);
                }
                catch (Exception e)
                {
                    // No big deal if we couldn't delete it.
                    Debug.LogWarning($"Could not clean up failed download: {e}");
                }
            }
        }
    }
} // namespace TiltBrush
