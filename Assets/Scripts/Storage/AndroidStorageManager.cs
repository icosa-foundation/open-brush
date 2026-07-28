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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{
    public class AndroidStorageManager : MonoBehaviour
    {
        private const string kLegacyStartupPromptDismissedKey =
            "GooglePlayStorage.StartupPromptDismissed";
        // Pre-release mirrored-cache builds used this key. Payloads are deliberately retained on
        // disk, but the obsolete retry records must not drive the FD-backed backend.
        private const string kPendingTransfersKey = "GooglePlayStorage.PendingTransfers";

        // The Android SAF picker is modal: while it has focus, users cannot initiate another
        // storage operation through the Open Brush UI. Keep at most one continuation (normally the
        // action that opened the picker). API or background callers are deliberately not queued once
        // that slot is occupied.
        private static Action m_PendingAction;
        private static Action m_PendingCanceledAction;
        private static bool m_RequestInProgress;
        private static bool m_StartupPromptShown;
        private static string m_FileDescriptorProbeRootIdentity;
        private static string m_ActiveRootIdentity;
        private static AndroidStorageManager m_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                return;
            }

            ReportObsoletePendingTransferState();
            if (PlayerPrefs.HasKey(kLegacyStartupPromptDismissedKey))
            {
                PlayerPrefs.DeleteKey(kLegacyStartupPromptDismissedKey);
                PlayerPrefs.Save();
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

        private void OnDestroy()
        {
            if (m_Instance == this)
            {
                m_Instance = null;
            }
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
                yield return RecoverTransactions(null);
                yield break;
            }

            if (!m_StartupPromptShown &&
                !AndroidSafStorage.HasOpenBrushFolder())
            {
                m_StartupPromptShown = true;
                RequireSharedFolderFor("shared storage", null, null);
            }
        }

        public static bool RequireSharedFolderFor(string featureName, Action onReady)
        {
            return RequireSharedFolderFor(featureName, onReady, null);
        }

        public static bool RequireSharedFolderFor(
            string featureName, Action onReady, Action onCanceled)
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
            string message =
                $"Choose an Open Brush folder to enable {featureName}. You can cancel and continue without shared storage.";
            ControllerConsoleScript.m_Instance?.AddNewLine(message);
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush, message, fPopScalar: 0.5f);
            AndroidSafStorage.RequestOpenBrushFolder();
            return false;
        }

        public static void ReselectSharedFolder()
        {
            if (!OpenBrushStorage.IsGooglePlayStorageMode)
            {
                return;
            }
            if (m_RequestInProgress)
            {
                ControllerConsoleScript.m_Instance?.AddNewLine(
                    "Open Brush folder selection is already in progress.");
                return;
            }

            m_PendingAction = null;
            m_PendingCanceledAction = null;
            m_RequestInProgress = true;
            AndroidSafStorage.RequestOpenBrushFolder();
        }

        public void OnOpenBrushFolderSelected(string uriString)
        {
            m_RequestInProgress = false;
            AndroidSafStorage.InvalidateReadiness();

            RunFileDescriptorProbeOnce();
            StartCoroutine(RecoverTransactions(() =>
            {
                Action pendingAction = m_PendingAction;
                m_PendingAction = null;
                m_PendingCanceledAction = null;
                pendingAction?.Invoke();
            }));
        }

        public void OnOpenBrushFolderCanceled(string unused)
        {
            m_RequestInProgress = false;
            Action pendingCanceledAction = m_PendingCanceledAction;
            m_PendingAction = null;
            m_PendingCanceledAction = null;
            bool existingFolderRemainsAvailable = AndroidSafStorage.HasOpenBrushFolder();
            string message = existingFolderRemainsAvailable
                ? "Open Brush folder selection canceled. The existing folder remains selected."
                : "Open Brush folder selection canceled. Shared-storage features remain unavailable.";
            ControllerConsoleScript.m_Instance?.AddNewLine(message);
            OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                InputManager.ControllerName.Brush, message, fPopScalar: 0.5f);
            pendingCanceledAction?.Invoke();
        }

        private static void RunFileDescriptorProbeOnce()
        {
            string rootIdentity = AndroidSafStorage.GetSelectedRootIdentity();
            if (!Debug.isDebugBuild ||
                string.IsNullOrEmpty(rootIdentity) ||
                string.Equals(
                    m_FileDescriptorProbeRootIdentity,
                    rootIdentity,
                    StringComparison.Ordinal))
            {
                return;
            }

            m_FileDescriptorProbeRootIdentity = rootIdentity;
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
                    RecoverAutosave(transactionReport);
                    return transactionReport;
                },
                longRunning: true);
            SafRecoveryReport report = null;
            string recoveryError = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = future.TryGetResult(out report);
                }
                catch (FutureFailed e)
                {
                    recoveryError = e.InnerException?.Message ?? e.Message;
                    break;
                }
                if (finished)
                {
                    break;
                }
                yield return null;
            }
            future.Close();

            if (recoveryError != null)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE Transaction recovery failed: {recoveryError}");
            }
            else if (report != null && report.Recovered > 0)
            {
                Debug.Log($"SAF_STORAGE Recovered {report.Recovered} storage transaction(s).");
            }
            if (report != null && report.Pending > 0)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE {report.Pending} storage transaction(s) require recovery.");
            }
            if (report != null && report.AutosaveRecovered)
            {
                App.Instance.AutosaveRestoreFileExists = false;
                OutputWindowScript.m_Instance?.CreateInfoCardAtController(
                    InputManager.ControllerName.Wand,
                    "The last autosave was recovered into your shared sketchbook.");
            }
            yield return RefreshRuntimeContent();
            RefreshSharedCatalogs();
            if (App.DriveSync?.SyncEnabled == true)
            {
                App.DriveSync.SyncLocalFilesAsync().AsAsyncVoid();
            }
            onComplete?.Invoke();
        }

        // External changes to Scripts/Plugins/Fonts are picked up here at startup, after folder
        // selection, and on application resume. There is no live change observation: mobile
        // platform configs disable file watching, so a resume-time refresh matches Quest behavior.
        private IEnumerator RefreshRuntimeContent()
        {
            yield return SeedRuntimeContent();
            foreach (StorageArea area in new[]
            {
                StorageArea.Scripts,
                StorageArea.Plugins,
                StorageArea.Fonts,
            })
            {
                Task<RuntimeProjectionResult> refresh =
                    UserRuntimeContent.Instance.EnsureCurrentAsync(
                        area, CancellationToken.None);
                while (!refresh.IsCompleted)
                {
                    yield return null;
                }
                if (refresh.IsFaulted)
                {
                    string error = refresh.Exception?.GetBaseException().Message ??
                        "Unknown runtime-content refresh failure.";
                    Debug.LogWarning(
                        $"SAF_PROJECTION {area} refresh failed: {error}");
                    continue;
                }
                if (refresh.IsCanceled)
                {
                    Debug.LogWarning(
                        $"SAF_PROJECTION {area} refresh was canceled.");
                    continue;
                }
                RuntimeProjectionResult result = refresh.Result;
                if (!result.Success)
                {
                    Debug.LogWarning(
                        $"SAF_PROJECTION {area} refresh retained previous content: " +
                        $"{result.Error}");
                }
            }
        }

        private IEnumerator SeedRuntimeContent()
        {
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework ||
                !UserStorage.Backend.IsReady)
            {
                yield break;
            }
            var seeds = new List<RuntimeContentSeed>();
            foreach (TextAsset library in Resources.LoadAll<TextAsset>("LuaModules"))
            {
                seeds.Add(new RuntimeContentSeed(
                    StorageArea.Plugins,
                    $"LuaModules/{library.name}.lua",
                    "text/x-lua",
                    library.bytes));
            }
            Task<RuntimeContentSeedResult> task = Task.Run(() =>
                RuntimeContentSeeder.SeedMissing(
                    UserStorage.Backend, seeds, CancellationToken.None));
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE Runtime content seeding failed: " +
                    $"{task.Exception?.GetBaseException().Message}");
                yield break;
            }
            if (task.IsCanceled)
            {
                Debug.LogWarning("SAF_STORAGE Runtime content seeding was canceled.");
                yield break;
            }
            RuntimeContentSeedResult result = task.Result;
            if (!result.Success)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE Runtime content seeding failed: {result.Error}");
            }
            else if (result.SeededCount > 0)
            {
                Debug.Log(
                    $"SAF_STORAGE Seeded {result.SeededCount} runtime content file(s).");
            }
        }

        private static void RecoverAutosave(SafRecoveryReport report)
        {
            if (!App.Config.m_AutosaveRestoreEnabled ||
                App.Instance == null ||
                !App.Instance.AutosaveRestoreFileExists)
            {
                return;
            }

            string autosavePath = SaveLoadScript.m_Instance?.MostRecentAutosaveFile();
            if (string.IsNullOrEmpty(autosavePath) || !File.Exists(autosavePath))
            {
                report.Pending++;
                report.Errors.Add(
                    "SAF_RECOVERY Autosave marker exists but no autosave file was found.");
                return;
            }

            try
            {
                IUserStorageBackend backend = UserStorage.Backend;
                StorageDirectoryResult listing = backend.List(
                    StorageArea.Sketches, "", default);
                if (!listing.Success && listing.Code != StorageResultCode.NotFound)
                {
                    throw new IOException(listing.Error);
                }
                var existingNames = new HashSet<string>(
                    listing.Documents.Select(document => document.DisplayName),
                    StringComparer.OrdinalIgnoreCase);
                string timestamp = File.GetLastWriteTime(autosavePath)
                    .ToString("yyyy-MM-dd HH-mm-ss");
                string baseName = $"Recovered Autosave {timestamp}";
                string displayName = $"{baseName}{SaveLoadScript.TILT_SUFFIX}";
                for (int suffix = 2; existingNames.Contains(displayName); ++suffix)
                {
                    displayName =
                        $"{baseName} ({suffix}){SaveLoadScript.TILT_SUFFIX}";
                }

                using (IStorageWriteTransaction transaction = backend.BeginWrite(
                    StorageArea.Sketches,
                    displayName,
                    TiltFile.TILT_MIME_TYPE,
                    default))
                {
                    using (Stream input = new FileStream(
                        autosavePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (Stream output = transaction.OpenWrite())
                    {
                        input.CopyTo(output);
                    }
                    StorageMutationResult commit = transaction.Commit();
                    if (!commit.Success)
                    {
                        throw new IOException(commit.Error);
                    }
                }
                report.AutosaveRecovered = true;
                report.Recovered++;
                Debug.Log("SAF_RECOVERY Autosave committed to the shared sketchbook.");
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException ||
                e is InvalidOperationException)
            {
                report.Pending++;
                string error = $"SAF_RECOVERY Autosave remains local: {e.Message}";
                report.Errors.Add(error);
                Debug.LogWarning(error);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!paused &&
                UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework &&
                UserStorage.Backend.IsReady)
            {
                StartCoroutine(RefreshRuntimeContentAfterResume());
            }
        }

        private IEnumerator RefreshRuntimeContentAfterResume()
        {
            yield return RefreshRuntimeContent();
            RefreshSharedCatalogs();
            if (App.DriveSync?.SyncEnabled == true)
            {
                App.DriveSync.SyncLocalFilesAsync().AsAsyncVoid();
            }
        }

        private static void RefreshSharedCatalogs()
        {
            string rootIdentity = UserStorage.Backend.RootIdentity;
            bool rootChanged = !string.Equals(
                m_ActiveRootIdentity, rootIdentity, StringComparison.Ordinal);
            m_ActiveRootIdentity = rootIdentity;

            SketchCatalog.m_Instance?.GetSet(SketchSetType.User)?.RequestRefresh();
            SketchCatalog.m_Instance?.GetSet(SketchSetType.SavedStrokes)?.RequestRefresh();
            if (rootChanged)
            {
                if (ReferenceImageCatalog.m_Instance != null)
                {
                    ReferenceImageCatalog.m_Instance.ChangeDirectory(
                        ReferenceImageCatalog.m_Instance.HomeDirectory);
                }
                if (BackgroundImageCatalog.m_Instance != null)
                {
                    BackgroundImageCatalog.m_Instance.ChangeDirectory(
                        BackgroundImageCatalog.m_Instance.HomeDirectory);
                }
                if (ModelCatalog.m_Instance != null)
                {
                    ModelCatalog.m_Instance.ChangeDirectory(
                        ModelCatalog.m_Instance.HomeDirectory);
                }
                if (VideoCatalog.Instance != null)
                {
                    VideoCatalog.Instance.ChangeDirectory(
                        VideoCatalog.Instance.HomeDirectory);
                }
            }
            else
            {
                ReferenceImageCatalog.m_Instance?.ForceCatalogScan();
                BackgroundImageCatalog.m_Instance?.ForceCatalogScan();
                ModelCatalog.m_Instance?.ForceCatalogScan();
                VideoCatalog.Instance?.ForceCatalogScan();
            }
        }

        private static void ReportObsoletePendingTransferState()
        {
            if (!PlayerPrefs.HasKey(kPendingTransfersKey))
            {
                return;
            }
            Debug.LogWarning(
                "SAF_STORAGE Obsolete pre-release mirror retry records were found. " +
                "They are ignored by the SAF backend and retained for explicit cleanup.");
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
            SafPublicationResult result = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = future.TryGetResult(out result);
                }
                catch (FutureFailed e)
                {
                    result = new SafPublicationResult(
                        StorageResultCode.Failed,
                        e.InnerException?.Message ?? e.Message);
                    break;
                }
                if (finished)
                {
                    break;
                }
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

    }
}
