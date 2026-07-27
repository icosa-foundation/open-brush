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
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class AndroidStorageManager : MonoBehaviour
    {
        private const string kStartupPromptDismissedKey = "GooglePlayStorage.StartupPromptDismissed";
        private const string kPendingTransfersKey = "GooglePlayStorage.PendingTransfers";

        // The Android SAF picker is modal: while it has focus, users cannot initiate another
        // storage operation through the Open Brush UI. Keep at most one continuation (normally the
        // action that opened the picker). API or background callers are deliberately not queued once
        // that slot is occupied.
        private static Action m_PendingAction;
        private static Action m_PendingCanceledAction;
        private static bool m_RequestInProgress;
        private static bool m_RequestIsStartupPrompt;
        private static bool m_StartupPromptShown;
        private static bool m_PendingTransfersLoaded;
        private static bool m_FileDescriptorProbeRun;
        private static AndroidStorageManager m_Instance;
        private static readonly List<PendingTransferRetry> m_PendingTransferRetries =
            new List<PendingTransferRetry>();
        private static readonly List<PersistentPendingTransfer> m_PersistentPendingTransfers =
            new List<PersistentPendingTransfer>();
        private static readonly HashSet<string> m_PendingLocalPaths =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [Serializable]
        private class PendingTransferStore
        {
            public List<PersistentPendingTransfer> Transfers = new List<PersistentPendingTransfer>();
        }

        [Serializable]
        private class PersistentPendingTransfer
        {
            public string Label;
            public string LocalPath;
            public string RelativePath;
        }

        private class PendingTransferRetry
        {
            public string Label;
            public string LocalPath;
            public Action RetryAction;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                return;
            }

            LoadPendingTransfers();

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
                RunFileDescriptorProbeOnce();
                yield return RecoverTransactions(() =>
                    OpenBrushStorage.SyncSharedUserContentToLocalCache(RetryPendingTransfers));
                yield break;
            }

            if (!m_StartupPromptShown &&
                PlayerPrefs.GetInt(kStartupPromptDismissedKey, 0) == 0 &&
                !AndroidSafStorage.HasOpenBrushFolder())
            {
                m_StartupPromptShown = true;
                RequireSharedFolderFor("shared storage", null, null, true);
            }
        }

        public static bool RequireSharedFolderFor(string featureName, Action onReady)
        {
            return RequireSharedFolderFor(featureName, onReady, null);
        }

        public static bool RequireSharedFolderFor(
            string featureName, Action onReady, Action onCanceled)
        {
            return RequireSharedFolderFor(featureName, onReady, onCanceled, false);
        }

        private static bool RequireSharedFolderFor(
            string featureName, Action onReady, Action onCanceled, bool isStartupPrompt)
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
                    m_PendingCanceledAction = onCanceled;
                }
                ControllerConsoleScript.m_Instance?.AddNewLine(
                    $"Waiting for Open Brush folder selection before {featureName}.");
                return false;
            }

            m_PendingAction = onReady;
            m_PendingCanceledAction = onCanceled;
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

            RunFileDescriptorProbeOnce();
            StartCoroutine(RecoverTransactions(() =>
            {
                OpenBrushStorage.SyncSharedUserContentToLocalCache(() =>
                {
                    RetryPendingTransfers();

                    Action pendingAction = m_PendingAction;
                    m_PendingAction = null;
                    m_PendingCanceledAction = null;
                    pendingAction?.Invoke();
                });
            }));
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
            Action pendingCanceledAction = m_PendingCanceledAction;
            m_PendingAction = null;
            m_PendingCanceledAction = null;
            string message =
                "Open Brush folder selection canceled. Shared-storage features remain unavailable.";
            ControllerConsoleScript.m_Instance?.AddNewLine(message);
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush, message, fPopScalar: 0.5f);
            pendingCanceledAction?.Invoke();
        }

        private static void RunFileDescriptorProbeOnce()
        {
            if (!Debug.isDebugBuild || m_FileDescriptorProbeRun)
            {
                return;
            }

            m_FileDescriptorProbeRun = true;
            bool success = AndroidSafStorage.RunFileDescriptorProbe(out string report);
            if (success)
            {
                Debug.Log($"SAF_FD {report}");
            }
            else
            {
                Debug.LogError($"SAF_FD {report}");
            }
        }

        private IEnumerator RecoverTransactions(Action onComplete)
        {
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework ||
                !UserStorage.Backend.IsReady)
            {
                onComplete?.Invoke();
                yield break;
            }

            var future = new Future<SafRecoveryReport>(
                () =>
                {
                    SafRecoveryReport transactionReport =
                        SafTransactionRecovery.RecoverAll(
                            UserStorage.Backend, default);
                    SafRecoveryReport publicationReport =
                        SafStagedOutputPublisher.RecoverAll(
                            UserStorage.Backend, default);
                    transactionReport.Recovered += publicationReport.Recovered;
                    transactionReport.Pending += publicationReport.Pending;
                    transactionReport.Errors.AddRange(publicationReport.Errors);
                    return transactionReport;
                },
                longRunning: true);
            SafRecoveryReport report;
            while (!future.TryGetResult(out report))
            {
                yield return null;
            }
            future.Close();

            if (report.Recovered > 0)
            {
                Debug.Log($"SAF_STORAGE Recovered {report.Recovered} storage transaction(s).");
            }
            if (report.Pending > 0)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE {report.Pending} storage transaction(s) require recovery.");
            }
            SketchCatalog.m_Instance?.GetSet(SketchSetType.User)?.RequestRefresh();
            SketchCatalog.m_Instance?.GetSet(SketchSetType.SavedStrokes)?.RequestRefresh();
            onComplete?.Invoke();
        }

        public static void StartStorageOperation(
            string label,
            Func<SafPublicationResult> operation,
            Action<bool, string> onComplete)
        {
            if (m_Instance == null)
            {
                onComplete?.Invoke(false, "Android storage manager is not ready.");
                return;
            }
            m_Instance.StartCoroutine(
                m_Instance.RunStorageOperation(label, operation, onComplete));
        }

        private IEnumerator RunStorageOperation(
            string label,
            Func<SafPublicationResult> operation,
            Action<bool, string> onComplete)
        {
            var future = new Future<SafPublicationResult>(operation, longRunning: true);
            SafPublicationResult result;
            while (!future.TryGetResult(out result))
            {
                yield return null;
            }
            future.Close();
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE {label} publication failed: {result.Error}");
            }
            onComplete?.Invoke(result.Success, result.Error);
        }

        public static void StartTransfer(
            string label,
            Func<int> startJob,
            string localPath,
            string relativePath,
            Action<bool, string> onComplete,
            Action retryAction)
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                onComplete?.Invoke(true, null);
                return;
            }

            AddPersistentPendingTransfer(label, localPath, relativePath);

            if (m_Instance == null)
            {
                onComplete?.Invoke(false, "Android storage manager is not ready.");
                return;
            }

            m_Instance.StartCoroutine(m_Instance.TransferCoroutine(
                label,
                "shared storage",
                startJob,
                localPath,
                relativePath,
                onComplete,
                retryAction));
        }

        public static void StartInboundTransfer(
            string label,
            Func<int> startJob,
            Action<bool, string> onComplete)
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

            m_Instance.StartCoroutine(m_Instance.TransferCoroutine(
                label, "local cache", startJob, null, null, onComplete, null));
        }

        private IEnumerator TransferCoroutine(
            string label,
            string destination,
            Func<int> startJob,
            string localPath,
            string relativePath,
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
                RegisterFailedTransfer(label, localPath, relativePath, retryAction, error);
                onComplete?.Invoke(false, error);
                yield break;
            }

            ControllerConsoleScript.m_Instance?.AddNewLine($"Copying {label} to {destination}.");
            float nextProgressMessage = Time.realtimeSinceStartup + 2f;

            while (!AndroidSafStorage.IsTransferJobDone(jobId))
            {
                if (Time.realtimeSinceStartup >= nextProgressMessage)
                {
                    ControllerConsoleScript.m_Instance?.AddNewLine(
                        FormatTransferProgress(label, destination, jobId));
                    nextProgressMessage = Time.realtimeSinceStartup + 3f;
                }
                yield return null;
            }

            bool success = AndroidSafStorage.DidTransferJobSucceed(jobId);
            string errorMessage = success ? null : AndroidSafStorage.GetTransferJobError(jobId);
            AndroidSafStorage.ClearTransferJob(jobId);

            if (success)
            {
                if (!string.IsNullOrEmpty(localPath))
                {
                    RemovePersistentPendingTransfer(localPath, relativePath);
                }
                ControllerConsoleScript.m_Instance?.AddNewLine($"Finished copying {label}.");
            }
            else
            {
                string error = string.IsNullOrEmpty(errorMessage)
                    ? $"Failed to copy {label} to {destination}."
                    : errorMessage;
                if (retryAction != null)
                {
                    RegisterFailedTransfer(label, localPath, relativePath, retryAction, error);
                }
                else
                {
                    Debug.LogWarning($"SAF_CACHE_SYNC {error}");
                }
            }

            onComplete?.Invoke(success, errorMessage);
        }

        private static string FormatTransferProgress(string label, string destination, int jobId)
        {
            long done = AndroidSafStorage.GetTransferJobBytesDone(jobId);
            long total = AndroidSafStorage.GetTransferJobBytesTotal(jobId);
            if (total <= 0)
            {
                return $"Copying {label} to {destination}.";
            }

            float percent = Mathf.Clamp01((float)done / total) * 100f;
            return $"Copying {label} to {destination}: {percent:0}%";
        }

        private static void RegisterFailedTransfer(
            string label,
            string localPath,
            string relativePath,
            Action retryAction,
            string error)
        {
            if (retryAction != null)
            {
                RegisterPendingTransfer(label, localPath, relativePath, retryAction);
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

        public static void RegisterPendingTransfer(
            string label, string localPath, string relativePath, Action retryAction)
        {
            string fullPath = string.IsNullOrEmpty(localPath)
                ? null
                : Path.GetFullPath(localPath);
            if (fullPath != null)
            {
                AddPersistentPendingTransfer(label, fullPath, relativePath);
                m_PendingTransferRetries.RemoveAll(retry =>
                    string.Equals(retry.LocalPath, fullPath, StringComparison.OrdinalIgnoreCase));
            }
            m_PendingTransferRetries.Add(new PendingTransferRetry
            {
                Label = label,
                LocalPath = fullPath,
                RetryAction = retryAction
            });
        }

        public static string[] GetPendingLocalPaths(string localDirectory)
        {
            LoadPendingTransfers();
            string fullDirectory = Path.GetFullPath(localDirectory)
                .TrimEnd(Path.DirectorySeparatorChar);
            string directoryPrefix = fullDirectory + Path.DirectorySeparatorChar;
            return m_PendingLocalPaths
                .Where(path => path.Equals(fullDirectory, StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        private static void RetryPendingTransfers()
        {
            LoadPendingTransfers();
            if (m_PendingTransferRetries.Count == 0 && m_PersistentPendingTransfers.Count == 0)
            {
                return;
            }

            var retries = new List<PendingTransferRetry>(m_PendingTransferRetries);
            m_PendingTransferRetries.Clear();
            var retriedPaths = new HashSet<string>(
                retries.Where(retry => !string.IsNullOrEmpty(retry.LocalPath))
                    .Select(retry => retry.LocalPath),
                StringComparer.OrdinalIgnoreCase);
            var persistentRetries = m_PersistentPendingTransfers
                .Where(transfer => !retriedPaths.Contains(transfer.LocalPath))
                .Select(transfer => new PersistentPendingTransfer
                {
                    Label = transfer.Label,
                    LocalPath = transfer.LocalPath,
                    RelativePath = transfer.RelativePath
                })
                .ToList();
            ControllerConsoleScript.m_Instance?.AddNewLine(
                $"Retrying {retries.Count + persistentRetries.Count} pending shared-storage copy operation(s).");
            foreach (var retry in retries)
            {
                retry.RetryAction?.Invoke();
            }
            foreach (var retry in persistentRetries)
            {
                RetryPersistentTransfer(retry);
            }
        }

        private static void RetryPersistentTransfer(PersistentPendingTransfer transfer)
        {
            if (!File.Exists(transfer.LocalPath) && !Directory.Exists(transfer.LocalPath))
            {
                RemovePersistentPendingTransfer(transfer.LocalPath, transfer.RelativePath);
                return;
            }

            OpenBrushStorage.PublishLocalPathToSharedStorageAsync(
                transfer.RelativePath,
                transfer.LocalPath,
                transfer.Label,
                (success, error) =>
                {
                    if (!success)
                    {
                        Debug.LogWarning(
                            $"SAF_PENDING Failed to retry '{transfer.RelativePath}': {error}");
                    }
                });
        }

        private static void LoadPendingTransfers()
        {
            if (m_PendingTransfersLoaded)
            {
                return;
            }

            m_PendingTransfersLoaded = true;
            string json = PlayerPrefs.GetString(kPendingTransfersKey, "");
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            try
            {
                PendingTransferStore store = JsonUtility.FromJson<PendingTransferStore>(json);
                if (store?.Transfers == null)
                {
                    return;
                }

                foreach (PersistentPendingTransfer transfer in store.Transfers)
                {
                    if (string.IsNullOrEmpty(transfer.LocalPath) ||
                        string.IsNullOrEmpty(transfer.RelativePath))
                    {
                        continue;
                    }

                    transfer.LocalPath = Path.GetFullPath(transfer.LocalPath);
                    if (m_PersistentPendingTransfers.Any(existing =>
                            PendingTransferMatches(
                                existing, transfer.LocalPath, transfer.RelativePath)))
                    {
                        continue;
                    }

                    m_PersistentPendingTransfers.Add(transfer);
                    m_PendingLocalPaths.Add(transfer.LocalPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"SAF_PENDING Failed to load pending transfers: {e.Message}");
            }
        }

        private static void AddPersistentPendingTransfer(
            string label, string localPath, string relativePath)
        {
            if (string.IsNullOrEmpty(localPath) || string.IsNullOrEmpty(relativePath))
            {
                return;
            }

            LoadPendingTransfers();
            string fullPath = Path.GetFullPath(localPath);
            PersistentPendingTransfer existing = m_PersistentPendingTransfers.FirstOrDefault(
                transfer => PendingTransferMatches(transfer, fullPath, relativePath));
            if (existing != null)
            {
                existing.Label = label;
            }
            else
            {
                m_PersistentPendingTransfers.Add(new PersistentPendingTransfer
                {
                    Label = label,
                    LocalPath = fullPath,
                    RelativePath = relativePath
                });
            }
            m_PendingLocalPaths.Add(fullPath);
            SavePendingTransfers();
        }

        private static void RemovePersistentPendingTransfer(
            string localPath, string relativePath)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                return;
            }

            LoadPendingTransfers();
            string fullPath = Path.GetFullPath(localPath);
            m_PersistentPendingTransfers.RemoveAll(transfer =>
                PendingTransferMatches(transfer, fullPath, relativePath));
            if (!m_PersistentPendingTransfers.Any(transfer =>
                    string.Equals(
                        transfer.LocalPath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                m_PendingLocalPaths.Remove(fullPath);
            }
            SavePendingTransfers();
        }

        private static bool PendingTransferMatches(
            PersistentPendingTransfer transfer, string localPath, string relativePath)
        {
            return string.Equals(
                    transfer.LocalPath, localPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    transfer.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase);
        }

        private static void SavePendingTransfers()
        {
            if (m_PersistentPendingTransfers.Count == 0)
            {
                PlayerPrefs.DeleteKey(kPendingTransfersKey);
            }
            else
            {
                var store = new PendingTransferStore
                {
                    Transfers = m_PersistentPendingTransfers
                };
                PlayerPrefs.SetString(kPendingTransfersKey, JsonUtility.ToJson(store));
            }
            PlayerPrefs.Save();
        }
    }
}
