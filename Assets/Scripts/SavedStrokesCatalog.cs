// Copyright 2024 The Tilt Brush Authors
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
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    // A thin wrapper around SketchSet to conform to the interface needed by reference panel tabs
    public class SavedStrokesCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        static public SavedStrokesCatalog Instance { get; private set; }
        [SerializeField] private string[] m_DefaultSavedStrokes;
        private FileWatcher m_FileWatcher;
        private string m_CurrentSavedStrokesDirectory;
        public string CurrentSavedStrokesDirectory => m_CurrentSavedStrokesDirectory;
        private List<SavedStrokeFile> m_SavedStrokeFiles;
        private bool m_ScanningDirectory;
        private bool m_DirectoryScanRequired;
        private HashSet<string> m_ChangedFiles;
        private bool m_WaitingForSketchSetUpdate;
        private bool m_SketchSetSubscribed;
        private bool m_SeedingSafDefaults;
        private string m_SafSeedAttemptedRootIdentity;
        private const string kSafSeedPreference =
            "GooglePlayStorage.SeededDefaultSavedStrokesFdV1";

        private bool IsSafStorage =>
            UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework;

        public bool IsScanning => m_ScanningDirectory;

        private void Awake()
        {
            Instance = this;
            Init();
        }

        private void Init()
        {
            if (!IsSafStorage)
            {
                App.InitMediaLibraryPath();
                App.InitSavedStrokesLibraryPath(m_DefaultSavedStrokes);
            }
            ChangeDirectory(HomeDirectory);
        }

        public void ChangeDirectory(string newPath)
        {
            StopWatchingCurrentDirectory();
            m_CurrentSavedStrokesDirectory = newPath;
            m_SavedStrokeFiles = new List<SavedStrokeFile>();
            m_ChangedFiles = new HashSet<string>();

            EnsureSketchSetSubscription();
            SketchCatalog.m_Instance?.GetSet(SketchSetType.SavedStrokes)?.RequestRefresh();
            StartCoroutine(ScanReferenceDirectory());

            if (!IsSafStorage && Directory.Exists(m_CurrentSavedStrokesDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentSavedStrokesDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
                m_FileWatcher.FileChanged += OnDirectoryChanged;
                m_FileWatcher.FileCreated += OnDirectoryChanged;
                m_FileWatcher.FileDeleted += OnDirectoryChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
        }

        public string HomeDirectory => App.SavedStrokesPath();
        public bool IsHomeDirectory() => m_CurrentSavedStrokesDirectory == HomeDirectory;

        public bool IsSubDirectoryOfHome()
        {
            return IsPathWithinDirectory(HomeDirectory, m_CurrentSavedStrokesDirectory);
        }

        internal static bool IsNavigableDirectory(string path)
        {
            return !path.EndsWith(
                SaveLoadScript.TILT_SUFFIX, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsNavigableLocalDirectory(string path)
        {
            try
            {
                return IsNavigableDirectory(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static bool IsPathWithinDirectory(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return fullPath.Equals(fullRoot, comparison) ||
                fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                    comparison);
        }

        internal static bool IsDirectChildPath(string directory, string path)
        {
            return string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(path) ?? "").TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(directory).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        public string GetCurrentDirectory()
        {
            return m_CurrentSavedStrokesDirectory;
        }

        public event Action CatalogChanged;
        public int ItemCount
        {
            get { return m_SavedStrokeFiles.Count; }
        }

        private void OnDestroy()
        {
            StopWatchingCurrentDirectory();

            // Clean up event subscription if still active
            if (m_WaitingForSketchSetUpdate || m_SketchSetSubscribed)
            {
                var sketchSet = SketchCatalog.m_Instance?.GetSet(SketchSetType.SavedStrokes);
                if (sketchSet != null)
                {
                    sketchSet.OnChanged -= OnFileSketchSetChanged;
                }
            }
        }

        public SavedStrokeFile GetSavedStrokeFileAtIndex(int index)
        {
            if (index < m_SavedStrokeFiles.Count && index >= 0)
            {
                return m_SavedStrokeFiles[index];
            }
            throw new ArgumentException(
                $"Saved Strokes Catalog has {m_SavedStrokeFiles.Count} files. File {index} requested.");
        }

        // Directory scanning works in the following manner:
        // Scanning is triggered when the directory scan required flag is set, and no scanning is
        // currently in progress. A Filewatcher watches the directory for changes and will set the scan
        // required flag if it sees a change. If a file has changed, then it adds it to a list of changed
        // files, so that it will force a rescan of that file, rather than ignoring it as a file it
        // has already scanned.
        private void Update()
        {
            EnsureSketchSetSubscription();
            if (IsSafStorage)
            {
                if (!m_SeedingSafDefaults &&
                    UserStorage.Backend.IsReady &&
                    m_SafSeedAttemptedRootIdentity !=
                        UserStorage.Backend.RootIdentity &&
                    PlayerPrefs.GetInt(
                        kSafSeedPreference,
                        0) == 0)
                {
                    StartCoroutine(SeedSafDefaults());
                }
            }
            if (m_DirectoryScanRequired)
            {
                ForceCatalogScan();
            }
        }

        public void ForceCatalogScan()
        {
            if (!m_ScanningDirectory)
            {
                m_DirectoryScanRequired = false;
                StartCoroutine(ScanReferenceDirectory());
            }
        }

        private void OnDirectoryChanged(object source, FileSystemEventArgs e)
        {
            if (!ReferenceEquals(source, m_FileWatcher)) { return; }
            m_DirectoryScanRequired = true;
        }

        private void StopWatchingCurrentDirectory()
        {
            if (m_FileWatcher == null) { return; }
            m_FileWatcher.EnableRaisingEvents = false;
            m_FileWatcher.FileChanged -= OnDirectoryChanged;
            m_FileWatcher.FileCreated -= OnDirectoryChanged;
            m_FileWatcher.FileDeleted -= OnDirectoryChanged;
            m_FileWatcher.Dispose();
            m_FileWatcher = null;
        }

        public void NotifyFileCreated(string fullpath)
        {
            if (IsSafStorage)
            {
                NotifyStorageChanged();
                return;
            }
            if (IsPathWithinDirectory(m_CurrentSavedStrokesDirectory, fullpath))
            {
                // Don't scan immediately - wait for FileSketchSet to process the file
                if (!m_WaitingForSketchSetUpdate && !m_SketchSetSubscribed)
                {
                    var sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
                    if (sketchSet != null)
                    {
                        sketchSet.OnChanged += OnFileSketchSetChanged;
                        m_WaitingForSketchSetUpdate = true;
                    }
                }
            }
        }

        public void NotifyFileChanged(string fullpath)
        {
            // Same logic as NotifyFileCreated
            NotifyFileCreated(fullpath);
        }

        public void NotifyFileDeleted(string fullpath)
        {
            // Same logic as NotifyFileCreated
            NotifyFileCreated(fullpath);
        }

        public void NotifyStorageChanged()
        {
            SketchSet sketchSet =
                SketchCatalog.m_Instance?.GetSet(SketchSetType.SavedStrokes);
            sketchSet?.RequestRefresh();
            EnsureSketchSetSubscription();
        }

        private void OnFileSketchSetChanged()
        {
            m_DirectoryScanRequired = true;

            if (m_SketchSetSubscribed)
            {
                return;
            }

            // FileSketchSet has processed files, so the path catalog is safe to scan.
            var sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
            if (sketchSet != null)
            {
                sketchSet.OnChanged -= OnFileSketchSetChanged;
            }
            m_WaitingForSketchSetUpdate = false;
        }

        private IEnumerator<object> ScanReferenceDirectory()
        {
            if (m_ScanningDirectory)
            {
                yield break; // Already scanning, skip
            }
            m_ScanningDirectory = true;
            m_SavedStrokeFiles.Clear();
            var catalog = SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
            if (!catalog.IsReadyForAccess)
            {
                catalog.Init();
            }
            for (int i = 0; i < catalog.NumSketches; i++)
            {
                var sketchFileInfo = catalog.GetSketchSceneFileInfo(i);
                if (!IsInCurrentDirectory(sketchFileInfo))
                {
                    continue;
                }
                catalog.GetSketchIcon(i, out var icon, out _, out _);
                var savedStrokeFile = new SavedStrokeFile(i, sketchFileInfo, icon);
                m_SavedStrokeFiles.Add(savedStrokeFile);
            }

            m_ScanningDirectory = false;
            CatalogChanged?.Invoke();
        }

        // The sketch set is a library-wide index; the reference panel is a
        // folder page. Keep only direct children of the selected folder here.
        private bool IsInCurrentDirectory(SceneFileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                return false;
            }
            if (!IsSafStorage)
            {
                return IsDirectChildPath(
                    m_CurrentSavedStrokesDirectory, fileInfo.FullPath);
            }

            if (!(fileInfo is SafSceneFileInfo safInfo) ||
                !OpenBrushStorage.TryGetSharedMediaLibraryRelativePath(
                    m_CurrentSavedStrokesDirectory, out string sharedPath) ||
                !OpenBrushStorage.TryResolveStorageDestination(
                    sharedPath, out StorageArea area, out string relativeDirectory) ||
                area != StorageArea.SavedStrokes)
            {
                return false;
            }
            string logicalPath = safInfo.Document.RelativeDisplayPath
                .Replace('\\', '/').Trim('/');
            string logicalParent = relativeDirectory.Replace('\\', '/').Trim('/');
            int separator = logicalPath.LastIndexOf('/');
            string parentPath = separator < 0 ? "" : logicalPath.Substring(0, separator);
            return string.Equals(parentPath, logicalParent, StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureSketchSetSubscription()
        {
            if (m_SketchSetSubscribed || SketchCatalog.m_Instance == null)
            {
                return;
            }
            SketchSet sketchSet =
                SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
            if (sketchSet != null)
            {
                if (!m_WaitingForSketchSetUpdate) { sketchSet.OnChanged += OnFileSketchSetChanged; }
                m_WaitingForSketchSetUpdate = false;
                m_SketchSetSubscribed = true;
            }
        }

        private IEnumerator<object> SeedSafDefaults()
        {
            m_SeedingSafDefaults = true;
            string seedRootIdentity = UserStorage.Backend.RootIdentity;
            m_SafSeedAttemptedRootIdentity = seedRootIdentity;
            StorageDirectoryResult listing = UserStorage.Backend.List(
                StorageArea.SavedStrokes, "", default);
            if (!listing.Success && listing.Code != StorageResultCode.NotFound)
            {
                m_SeedingSafDefaults = false;
                yield break;
            }
            if (listing.Code == StorageResultCode.NotFound ||
                listing.Documents.Count == 0)
            {
                foreach (string resourcePath in m_DefaultSavedStrokes)
                {
                    TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                    if (resource == null)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Missing default saved stroke: {resourcePath}");
                        continue;
                    }

                    string displayName = Path.GetFileName(resourcePath);
                    string seedError = null;
                    try
                    {
                        using (IStorageWriteTransaction transaction =
                            UserStorage.Backend.BeginWrite(
                                StorageArea.SavedStrokes,
                                displayName,
                                TiltFile.TILT_MIME_TYPE,
                                default))
                        {
                            using (Stream stream = transaction.OpenWrite())
                            {
                                stream.Write(resource.bytes, 0, resource.bytes.Length);
                            }
                            StorageMutationResult commit = transaction.Commit();
                            if (!commit.Success)
                            {
                                seedError = commit.Error;
                            }
                        }
                    }
                    catch (Exception e) when (
                        e is IOException ||
                        e is UnauthorizedAccessException ||
                        e is InvalidOperationException)
                    {
                        seedError = e.Message;
                    }
                    finally
                    {
                        Resources.UnloadAsset(resource);
                    }
                    if (seedError != null)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Failed to seed {displayName}: {seedError}");
                        m_SeedingSafDefaults = false;
                        yield break;
                    }
                    yield return null;
                }
            }

            if (!string.Equals(
                    seedRootIdentity,
                    UserStorage.Backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                m_SeedingSafDefaults = false;
                yield break;
            }
            PlayerPrefs.SetInt(
                kSafSeedPreference,
                1);
            PlayerPrefs.Save();
            m_SeedingSafDefaults = false;
            NotifyStorageChanged();
        }
    }
}
