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

        private FileWatcher m_FileWatcher;
        private string m_CurrentSavedStrokesDirectory;
        public string CurrentSavedStrokesDirectory => m_CurrentSavedStrokesDirectory;
        private List<SavedStrokeFile> m_SavedStrokeFiles;
        private bool m_ScanningDirectory;
        private bool m_DirectoryScanRequired;
        private HashSet<string> m_ChangedFiles;

        public bool IsScanning => m_ScanningDirectory;

        private void Awake()
        {
            Instance = this;
            Init();
        }

        private void Init()
        {
            App.InitMediaLibraryPath();
            ChangeDirectory(HomeDirectory);
        }

        public void ChangeDirectory(string newPath)
        {
            m_CurrentSavedStrokesDirectory = newPath;
            m_SavedStrokeFiles = new List<SavedStrokeFile>();
            m_ChangedFiles = new HashSet<string>();

            StartCoroutine(ScanReferenceDirectory());

            if (Directory.Exists(m_CurrentSavedStrokesDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentSavedStrokesDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
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
            m_FileWatcher.EnableRaisingEvents = false;
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
            m_DirectoryScanRequired = true;
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
                if (!sketchFileInfo.FullPath.StartsWith(m_CurrentSavedStrokesDirectory)) continue;
                catalog.GetSketchIcon(i, out var icon, out _, out _);
                var savedStrokeFile = new SavedStrokeFile(i, sketchFileInfo, icon);
                m_SavedStrokeFiles.Add(savedStrokeFile);
            }

            m_ScanningDirectory = false;
            CatalogChanged?.Invoke();
        }
    }
}
