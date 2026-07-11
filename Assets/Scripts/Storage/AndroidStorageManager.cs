// Copyright 2026 The Open Brush Authors
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
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class AndroidStorageManager : MonoBehaviour
    {
        private const string kStartupPromptDismissedKey = "GooglePlayStorage.StartupPromptDismissed";

        // The Android SAF picker is modal: while it has focus, users cannot initiate another
        // storage operation through the Open Brush UI. Keep at most one continuation (normally the
        // action that opened the picker). API or background callers are deliberately not queued once
        // that slot is occupied.
        private static Action m_PendingAction;
        private static bool m_RequestInProgress;
        private static bool m_RequestIsStartupPrompt;
        private static bool m_StartupPromptShown;
        private static AndroidStorageManager m_Instance;
        private static readonly List<PendingTransferRetry> m_PendingTransferRetries =
            new List<PendingTransferRetry>();

        private class PendingTransferRetry
        {
            public string Label;
            public Action RetryAction;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                return;
            }

            var existing = GameObject.Find(nameof(AndroidStorageManager));
            if (existing != null)
            {
                return;
            }

            var gameObject = new GameObject(nameof(AndroidStorageManager));
            gameObject.AddComponent<AndroidStorageManager>();
            DontDestroyOnLoad(gameObject);
        }

        private void Awake()
        {
            m_Instance = this;
        }

        private IEnumerator Start()
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                yield break;
            }

            while (App.CurrentState != App.AppState.Standard)
            {
                yield return null;
            }

            yield return null;

            if (AndroidSafStorage.HasOpenBrushFolder())
            {
                OpenBrushStorage.SyncSharedUserContentToLocalCache();
                yield break;
            }

            if (!m_StartupPromptShown &&
                PlayerPrefs.GetInt(kStartupPromptDismissedKey, 0) == 0 &&
                !AndroidSafStorage.HasOpenBrushFolder())
            {
                m_StartupPromptShown = true;
                RequireSharedFolderFor("shared storage", null, true);
            }
        }

        public static bool RequireSharedFolderFor(string featureName, Action onReady)
        {
            return RequireSharedFolderFor(featureName, onReady, false);
        }

        private static bool RequireSharedFolderFor(
            string featureName, Action onReady, bool isStartupPrompt)
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode || AndroidSafStorage.HasOpenBrushFolder())
            {
                return true;
            }

            if (m_RequestInProgress)
            {
                if (m_PendingAction == null && onReady != null)
                {
                    m_PendingAction = onReady;
                }
                ControllerConsoleScript.m_Instance?.AddNewLine(
                    $"Waiting for Open Brush folder selection before {featureName}.");
                return false;
            }

            m_PendingAction = onReady;
            m_RequestInProgress = true;
            m_RequestIsStartupPrompt = isStartupPrompt;
            string message =
                $"Choose an Open Brush folder to enable {featureName}. You can cancel and continue without shared storage.";
            ControllerConsoleScript.m_Instance?.AddNewLine(message);
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush, message, fPopScalar: 0.5f);
            AndroidSafStorage.RequestOpenBrushFolder();
            return false;
        }

        public void OnOpenBrushFolderSelected(string uriString)
        {
            m_RequestInProgress = false;
            m_RequestIsStartupPrompt = false;
            PlayerPrefs.DeleteKey(kStartupPromptDismissedKey);

            OpenBrushStorage.SyncSharedUserContentToLocalCache();
            RetryPendingTransfers();

            Action pendingAction = m_PendingAction;
            m_PendingAction = null;
            pendingAction?.Invoke();
        }

        public void OnOpenBrushFolderCanceled(string unused)
        {
            m_RequestInProgress = false;
            if (m_RequestIsStartupPrompt)
            {
                PlayerPrefs.SetInt(kStartupPromptDismissedKey, 1);
                PlayerPrefs.Save();
            }
            m_RequestIsStartupPrompt = false;
            m_PendingAction = null;
            string message =
                "Open Brush folder selection canceled. Shared-storage features remain unavailable.";
            ControllerConsoleScript.m_Instance?.AddNewLine(message);
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush, message, fPopScalar: 0.5f);
        }

        public static void StartTransfer(
            string label,
            Func<int> startJob,
            Action<bool, string> onComplete,
            Action retryAction)
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                onComplete?.Invoke(true, null);
                return;
            }

            if (m_Instance == null)
            {
                onComplete?.Invoke(false, "Android storage manager is not ready.");
                return;
            }

            m_Instance.StartCoroutine(
                m_Instance.TransferCoroutine(label, startJob, onComplete, retryAction));
        }

        private IEnumerator TransferCoroutine(
            string label,
            Func<int> startJob,
            Action<bool, string> onComplete,
            Action retryAction)
        {
            int jobId;
            try
            {
                jobId = startJob();
            }
            catch (Exception e)
            {
                string error = $"Failed to start {label}: {e.Message}";
                RegisterFailedTransfer(label, retryAction, error);
                onComplete?.Invoke(false, error);
                yield break;
            }

            ControllerConsoleScript.m_Instance?.AddNewLine($"Copying {label} to shared storage.");
            float nextProgressMessage = Time.realtimeSinceStartup + 2f;

            while (!AndroidSafStorage.IsTransferJobDone(jobId))
            {
                if (Time.realtimeSinceStartup >= nextProgressMessage)
                {
                    ControllerConsoleScript.m_Instance?.AddNewLine(
                        FormatTransferProgress(label, jobId));
                    nextProgressMessage = Time.realtimeSinceStartup + 3f;
                }
                yield return null;
            }

            bool success = AndroidSafStorage.DidTransferJobSucceed(jobId);
            string errorMessage = success ? null : AndroidSafStorage.GetTransferJobError(jobId);
            AndroidSafStorage.ClearTransferJob(jobId);

            if (success)
            {
                ControllerConsoleScript.m_Instance?.AddNewLine($"Finished copying {label}.");
            }
            else
            {
                string error = string.IsNullOrEmpty(errorMessage)
                    ? $"Failed to copy {label} to shared storage."
                    : errorMessage;
                RegisterFailedTransfer(label, retryAction, error);
            }

            onComplete?.Invoke(success, errorMessage);
        }

        private static string FormatTransferProgress(string label, int jobId)
        {
            long done = AndroidSafStorage.GetTransferJobBytesDone(jobId);
            long total = AndroidSafStorage.GetTransferJobBytesTotal(jobId);
            if (total <= 0)
            {
                return $"Copying {label} to shared storage.";
            }

            float percent = Mathf.Clamp01((float)done / total) * 100f;
            return $"Copying {label}: {percent:0}%";
        }

        private static void RegisterFailedTransfer(string label, Action retryAction, string error)
        {
            if (retryAction != null)
            {
                m_PendingTransferRetries.Add(new PendingTransferRetry
                {
                    Label = label,
                    RetryAction = retryAction
                });
                AndroidSafStorage.ClearOpenBrushFolder();
            }

            string message = string.IsNullOrEmpty(error)
                ? $"Failed to copy {label}. The local copy was kept."
                : $"Failed to copy {label}: {error}. The local copy was kept.";
            ControllerConsoleScript.m_Instance?.AddNewLine(message);
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush,
                message + " Choose the Open Brush folder again to retry pending copies.",
                fPopScalar: 0.5f);
        }

        private static void RetryPendingTransfers()
        {
            if (m_PendingTransferRetries.Count == 0)
            {
                return;
            }

            var retries = new List<PendingTransferRetry>(m_PendingTransferRetries);
            m_PendingTransferRetries.Clear();
            ControllerConsoleScript.m_Instance?.AddNewLine(
                $"Retrying {retries.Count} pending shared-storage copy operation(s).");
            foreach (var retry in retries)
            {
                retry.RetryAction?.Invoke();
            }
        }
    }
}
