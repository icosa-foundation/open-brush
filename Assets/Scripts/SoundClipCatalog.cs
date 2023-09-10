// Copyright 2023 The Open Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class SoundClipCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        static public SoundClipCatalog Instance { get; private set; }
        [SerializeField] private string[] m_DefaultSoundClips;
        [SerializeField] private bool m_DebugOutput;
        [SerializeField] private string[] m_supportedSoundClipExtensions;

        private FileWatcher m_FileWatcher;
        private List<SoundClip> m_SoundClips;
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
            App.InitSoundClipLibraryPath(m_DefaultSoundClips);

            m_SoundClips = new List<SoundClip>();
            m_ChangedFiles = new HashSet<string>();

            StartCoroutine(ScanReferenceDirectory());

            if (Directory.Exists(App.SoundClipLibraryPath()))
            {
                m_FileWatcher = new FileWatcher(App.SoundClipLibraryPath());
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnDirectoryChanged;
                m_FileWatcher.FileCreated += OnDirectoryChanged;
                m_FileWatcher.FileDeleted += OnDirectoryChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
        }

        public event Action CatalogChanged;
        public int ItemCount
        {
            get { return m_SoundClips.Count; }
        }

        private void OnDestroy()
        {
            foreach (var clip in m_SoundClips)
            {
                clip.Dispose();
            }
            m_FileWatcher.EnableRaisingEvents = false;
        }

        public SoundClip GetSoundClipAtIndex(int index)
        {
            if (index < m_SoundClips.Count && index >= 0)
            {
                return m_SoundClips[index];
            }
            throw new ArgumentException(
                $"Sound Clip Catalog has {m_SoundClips.Count} soundclips. Clip {index} requested.");
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
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                lock (m_ChangedFiles)
                {
                    m_ChangedFiles.Add(e.FullPath);
                }
            }
        }

        private IEnumerator<object> ScanReferenceDirectory()
        {
            m_ScanningDirectory = true;
            HashSet<string> changedSet = null;
            // We do a switcheroo on the changed list here so that there isn't a conflict with it
            // if a filewatch callback happens.
            lock (m_ChangedFiles)
            {
                changedSet = m_ChangedFiles;
                m_ChangedFiles = new HashSet<string>();
            }

            var existing = new HashSet<string>(m_SoundClips.Select(x => x.AbsolutePath));
            var detected = new HashSet<string>(
                Directory.GetFiles(App.SoundClipLibraryPath(), "*.*", SearchOption.AllDirectories).Where(x => m_supportedSoundClipExtensions.Contains(Path.GetExtension(x))));
            var toDelete = existing.Except(detected).Concat(changedSet).ToArray();
            var toScan = detected.Except(existing).Concat(changedSet).ToArray();

            // Remove deleted sound clips from the list. Currently playing clips may continue to play, but will
            // not appear in the reference panel.
            m_SoundClips.RemoveAll(x => toDelete.Contains(x.AbsolutePath));

            var newSoundClips = new List<SoundClip>();
            foreach (var filePath in toScan)
            {
                SoundClip clipRef = new SoundClip(filePath);
                newSoundClips.Add(clipRef);
                m_SoundClips.Add(clipRef);
            }

            // If we have a lot of clips, they may take a while to create thumbnails. Make sure we refresh
            // every few seconds so the user sees progress if they go straight to the reference panel.
            TimeSpan interval = TimeSpan.FromSeconds(4);
            DateTime nextRefresh = DateTime.Now + interval;
            foreach (var clipRef in newSoundClips)
            {
                if (DateTime.Now > nextRefresh)
                {
                    CatalogChanged?.Invoke();
                    nextRefresh = DateTime.Now + interval;
                }
                yield return clipRef.Initialize();
            }

            m_ScanningDirectory = false;
            CatalogChanged?.Invoke();
            if (m_DebugOutput)
            {
                DebugListSoundClips();
            }
        }

        /// Gets a clip form the catalog, given its filename. Returns null if no such clip is found.
        public SoundClip GetSoundClipByPersistentPath(string path)
        {
            return m_SoundClips.FirstOrDefault(x => x.PersistentPath == path);
        }


        public void DebugListSoundClips()
        {
            foreach (var clip in m_SoundClips)
            {
                Debug.Log(clip);
            }
        }

    }
}
