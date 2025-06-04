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
        private AsyncOperation m_AsyncOperation;
        private double m_AsyncOperationStartTime;

        override public void SetPopupCommandParameters(int commandParam, int commandParam2)
        {
            if (commandParam2 != (int)SketchSetType.Drive
                && commandParam2 != (int)SketchSetType.Curated
                && commandParam2 != (int)SketchSetType.Liked)
            {
                return;
            }

            m_SketchSetType = (SketchSetType)commandParam2;
            m_SketchIndex = commandParam;

            var set = SketchCatalog.m_Instance.GetSet(m_SketchSetType);
            m_SceneFileInfo = set.GetSketchSceneFileInfo(m_SketchIndex);

            if (m_SceneFileInfo.Available)
            {
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
                StartCoroutine(DownloadTiltCoroutine());
            }
        }

        private IEnumerator DownloadTiltCoroutine()
        {
            bool notifyOnError = true;
            void NotifyCreateError(IcosaSceneFileInfo sceneFileInfo, string type, Exception ex)
            {
                string error = $"Error downloading {type} file for {sceneFileInfo.HumanName}.";
                ControllerConsoleScript.m_Instance.AddNewLine(error, notifyOnError);
                notifyOnError = false;
                Debug.LogException(ex);
                Debug.LogError($"{sceneFileInfo.HumanName} {sceneFileInfo.TiltPath}");
            }

            void NotifyWriteError(IcosaSceneFileInfo sceneFileInfo, string type, UnityWebRequest www)
            {
                string error = $"Error downloading {type} file for {sceneFileInfo.HumanName}.\n" +
                    "Out of disk space?";
                ControllerConsoleScript.m_Instance.AddNewLine(error, notifyOnError);
                notifyOnError = false;
                Debug.LogError($"{www.error} {sceneFileInfo.HumanName} {sceneFileInfo.TiltPath}");
            }

            const int kDownloadBufferSize = 1024 * 1024;
            byte[] downloadBuffer = new byte[kDownloadBufferSize];
            var info = m_SceneFileInfo as IcosaSceneFileInfo;
            if (info.TiltDownloaded)
            {
                yield break;
            }

            if (System.IO.File.Exists(info.TiltPath))
            {
                info.TiltDownloaded = true;
                yield break;
            }

            using (m_WebRequest = UnityWebRequest.Get(info.TiltFileUrl))
            {
                try
                {
                    m_WebRequest.downloadHandler =
                        new DownloadHandlerFastFile(info.TiltPath, downloadBuffer);
                }
                catch (Exception ex)
                {
                    NotifyCreateError(info, "sketch", ex);
                    yield break;
                }

                m_AsyncOperationStartTime = Time.realtimeSinceStartupAsDouble;
                m_AsyncOperation = m_WebRequest.SendWebRequest();
                while (!m_AsyncOperation.isDone)
                {
                    if (m_DownloadTask.Cts.IsCancellationRequested)
                    {
                        m_WebRequest.Abort();
                        m_WebRequest = null;
                        m_AsyncOperation = null;
                        yield break;
                    }
                    yield return null;
                }

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
            m_WebRequest = null;
            m_AsyncOperation = null;
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
                if (m_AsyncOperation == null)
                {
                    // Will only be null if the request is already complete.
                    progress = 1.0f;
                }
                else
                {
                    // Make the bar go up while request is in-flight so the user
                    // knows something is happening.
                    const float kRequestProportion = 0.2f;
                    const float kRequestTime = 1.0f;
                    var opProgress = m_AsyncOperation.progress;
                    var downloadProgress = (1 - kRequestProportion) * opProgress;
                    var requestProgress = kRequestProportion;
                    if (m_AsyncOperation.progress == 0)
                    {
                        var delta = Time.realtimeSinceStartupAsDouble - m_AsyncOperationStartTime;
                        requestProgress *= Mathf.Clamp01((float)delta / kRequestTime);
                    }
                    progress = downloadProgress + requestProgress;
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
    }
} // namespace TiltBrush
