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

        public bool IsScanning => m_ScanningDirectory;

        private void Awake()
        {
            Instance = this;
            Init();
        }

        private void Init()
        {
            App.InitMediaLibraryPath();
            App.InitSavedStrokesLibraryPath(m_DefaultSavedStrokes);
            ChangeDirectory(HomeDirectory);
        }

        public void ChangeDirectory(string newPath)
        {
            StopWatchingCurrentDirectory();
            m_CurrentSavedStrokesDirectory = newPath;
            m_SavedStrokeFiles = new List<SavedStrokeFile>();
            m_ChangedFiles = new HashSet<string>();

            // The sketch set is a library-wide tree index, so ask it to rebuild before
            // this folder page is populated from it.
            SketchCatalog.m_Instance?.GetSet(SketchSetType.SavedStrokes)?.RequestRefresh();
            StartCoroutine(ScanReferenceDirectory());

            if (Directory.Exists(m_CurrentSavedStrokesDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentSavedStrokesDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite
                    | NotifyFilters.FileName | NotifyFilters.DirectoryName;
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

        /// True when path is root or lives beneath it. Compares whole path segments, so
        /// a sibling whose name merely starts with root's is not treated as contained.
        internal static bool IsPathWithinDirectory(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return fullPath.Equals(fullRoot, comparison) ||
                fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison);
        }

        /// True when path is a direct child of directory, one level down and no further.
        internal static bool IsDirectChildPath(string directory, string path)
        {
            return string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(path) ?? "").TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(directory).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
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
            if (m_WaitingForSketchSetUpdate)
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
            // A watcher replaced by a directory change can still deliver an event.
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
            if (IsPathWithinDirectory(m_CurrentSavedStrokesDirectory, fullpath))
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

        private void OnFileSketchSetChanged()
        {
            // FileSketchSet has processed files, now safe to scan
            m_DirectoryScanRequired = true;

            // Unsubscribe - we only need this once per notification
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

        // The sketch set indexes the whole Saved Strokes tree, because saved strokes may
        // be organised into subfolders. This panel is a folder page, so it shows the
        // direct children of the selected folder only.
        private bool IsInCurrentDirectory(SceneFileInfo fileInfo)
        {
            if (fileInfo == null)
            {
                return false;
            }
            return IsDirectChildPath(m_CurrentSavedStrokesDirectory, fileInfo.FullPath);
        }
    }
}
