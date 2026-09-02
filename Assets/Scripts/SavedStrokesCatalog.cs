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
        private bool m_SafSubscribed;
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
            if (m_FileWatcher != null)
            {
                m_FileWatcher.EnableRaisingEvents = false;
                m_FileWatcher = null;
            }
            m_CurrentSavedStrokesDirectory = IsSafStorage ? HomeDirectory : newPath;
            m_SavedStrokeFiles = new List<SavedStrokeFile>();
            m_ChangedFiles = new HashSet<string>();

            StartCoroutine(ScanReferenceDirectory());

            if (!IsSafStorage && Directory.Exists(m_CurrentSavedStrokesDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentSavedStrokesDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnDirectoryChanged;
                m_FileWatcher.FileCreated += OnDirectoryChanged;
                m_FileWatcher.FileDeleted += OnDirectoryChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
        }

        public string HomeDirectory => IsSafStorage ? "Saved Strokes" : App.SavedStrokesPath();
        public bool IsHomeDirectory() => m_CurrentSavedStrokesDirectory == HomeDirectory;

        public bool IsSubDirectoryOfHome()
        {
            return m_CurrentSavedStrokesDirectory.StartsWith(HomeDirectory);
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
            if (m_FileWatcher != null)
            {
                m_FileWatcher.EnableRaisingEvents = false;
            }

            // Clean up event subscription if still active
            if (m_WaitingForSketchSetUpdate || m_SafSubscribed)
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
            if (IsSafStorage)
            {
                EnsureSafSubscription();
                if (!m_SeedingSafDefaults &&
                    UserStorage.Backend.IsReady &&
                    m_SafSeedAttemptedRootIdentity !=
                        UserStorage.Backend.RootIdentity &&
                    PlayerPrefs.GetInt(
                        OpenBrushStorage.GetSafRootScopedPreferenceKey(
                            kSafSeedPreference),
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
            m_DirectoryScanRequired = true;
        }

        public void NotifyFileCreated(string fullpath)
        {
            if (IsSafStorage)
            {
                NotifyStorageChanged();
                return;
            }
            if (fullpath.StartsWith(m_CurrentSavedStrokesDirectory))
            {
                // Don't scan immediately - wait for FileSketchSet to process the file
                if (!m_WaitingForSketchSetUpdate)
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
            EnsureSafSubscription();
        }

        private void OnFileSketchSetChanged()
        {
            m_DirectoryScanRequired = true;

            if (IsSafStorage)
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
                if (!IsSafStorage &&
                    !sketchFileInfo.FullPath.StartsWith(m_CurrentSavedStrokesDirectory))
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

        private void EnsureSafSubscription()
        {
            if (!IsSafStorage || m_SafSubscribed || SketchCatalog.m_Instance == null)
            {
                return;
            }
            SketchSet sketchSet =
                SketchCatalog.m_Instance.GetSet(SketchSetType.SavedStrokes);
            if (sketchSet != null)
            {
                sketchSet.OnChanged += OnFileSketchSetChanged;
                m_SafSubscribed = true;
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
                OpenBrushStorage.GetSafRootScopedPreferenceKey(
                    kSafSeedPreference, seedRootIdentity),
                1);
            PlayerPrefs.Save();
            m_SeedingSafDefaults = false;
            NotifyStorageChanged();
        }
    }
}
