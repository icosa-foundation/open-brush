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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class VideoCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        static public VideoCatalog Instance { get; private set; }
        [SerializeField] private string[] m_DefaultVideos;
        [SerializeField] private bool m_DebugOutput;
        [SerializeField] private string[] m_supportedVideoExtensions;

        private List<FileWatcher> m_FileWatchers = new List<FileWatcher>();
        private string m_CurrentVideoDirectory;
        public string CurrentVideoDirectory => m_CurrentVideoDirectory;
        private List<ReferenceVideo> m_Videos;
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
            App.InitVideoLibraryPath(m_DefaultVideos);
            ChangeDirectory(HomeDirectory);
        }

        public void ChangeDirectory(string newPath)
        {
            m_CurrentVideoDirectory = newPath;
            m_Videos = new List<ReferenceVideo>();
            m_ChangedFiles = new HashSet<string>();

            StartCoroutine(ScanReferenceDirectory());

            DisposeFileWatchers();
            foreach (var directory in ScanDirectories())
            {
                var fileWatcher = new FileWatcher(directory);
                fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                fileWatcher.FileChanged += OnDirectoryChanged;
                fileWatcher.FileCreated += OnDirectoryChanged;
                fileWatcher.FileDeleted += OnDirectoryChanged;
                fileWatcher.EnableRaisingEvents = true;
                m_FileWatchers.Add(fileWatcher);
            }
        }

        /// The directories a scan covers: at the top level that is every configured video root,
        /// otherwise just the directory the user has browsed into. Roots that do not exist on this
        /// machine are skipped, so a config shared between machines does not have to match them all.
        private List<string> ScanDirectories()
        {
            var directories = IsHomeDirectory()
                ? App.GetAllVideoRoots()
                : new List<string> { m_CurrentVideoDirectory };
            return directories.Where(Directory.Exists).ToList();
        }

        private void DisposeFileWatchers()
        {
            foreach (var fileWatcher in m_FileWatchers)
            {
                fileWatcher.EnableRaisingEvents = false;
                fileWatcher.Dispose();
            }
            m_FileWatchers.Clear();
        }

        public string HomeDirectory => App.VideoLibraryPath();
        public bool IsHomeDirectory() => m_CurrentVideoDirectory == HomeDirectory;

        public bool IsSubDirectoryOfHome()
        {
            return m_CurrentVideoDirectory.StartsWith(HomeDirectory);
        }

        public string GetCurrentDirectory()
        {
            return m_CurrentVideoDirectory;
        }

        public event Action CatalogChanged;
        public int ItemCount
        {
            get { return m_Videos.Count; }
        }

        private void OnDestroy()
        {
            foreach (var video in m_Videos)
            {
                video.Dispose();
            }
            DisposeFileWatchers();
        }

        public ReferenceVideo GetVideoAtIndex(int index)
        {
            if (index < m_Videos.Count && index >= 0)
            {
                return m_Videos[index];
            }
            throw new ArgumentException(
                $"Reference Video Catalog has {m_Videos.Count} videos. Video {index} requested.");
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

            var existing = new HashSet<string>(m_Videos.Select(x => x.AbsolutePath));
            var detected = new HashSet<string>(
                ScanDirectories()
                    .SelectMany(x => Directory.GetFiles(x, "*.*", SearchOption.AllDirectories))
                    .Where(x => m_supportedVideoExtensions.Contains(Path.GetExtension(x))));
            var toDelete = existing.Except(detected).Concat(changedSet).ToArray();
            var toScan = detected.Except(existing).Concat(changedSet).ToArray();

            // Remove deleted videos from the list. Currently playing videos may continue to play, but will
            // not appear in the reference panel.
            m_Videos.RemoveAll(x => toDelete.Contains(x.AbsolutePath));

            var newVideos = new List<ReferenceVideo>();
            foreach (var filePath in toScan)
            {
                ReferenceVideo videoRef = new ReferenceVideo(filePath);
                newVideos.Add(videoRef);
                m_Videos.Add(videoRef);
            }

            // If we have a lot of videos, they may take a while to create thumbnails. Make sure we refresh
            // every few seconds so the user sees progress if they go straight to the reference panel.
            TimeSpan interval = TimeSpan.FromSeconds(4);
            DateTime nextRefresh = DateTime.Now + interval;
            foreach (var videoRef in newVideos)
            {
                if (DateTime.Now > nextRefresh)
                {
                    CatalogChanged?.Invoke();
                    nextRefresh = DateTime.Now + interval;
                }
                yield return videoRef.Initialize();
            }

            m_ScanningDirectory = false;
            CatalogChanged?.Invoke();
            if (m_DebugOutput)
            {
                DebugListVideos();
            }
        }

        /// Gets a video form the catalog, given its filename. Returns null if no such video is found.
        ///
        /// A sketch can name a video the catalog has not got to yet, either because the scan is
        /// still running or because the file sits in a configured root below a directory the user
        /// has browsed away from. Rather than report it missing, resolve it against the roots and
        /// adopt it into the catalog if the file is really there.
        public ReferenceVideo GetVideoByPersistentPath(string path)
        {
            var video = m_Videos.FirstOrDefault(x => x.PersistentPath == path);
            if (video != null) { return video; }

            if (string.IsNullOrEmpty(path)) { return null; }
            string resolvedPath;
            try
            {
                resolvedPath = App.ResolveMediaPath(App.GetAllVideoRoots(), path);
            }
            catch (Exception)
            {
                return null;
            }
            if (!File.Exists(resolvedPath)) { return null; }

            video = new ReferenceVideo(resolvedPath);
            m_Videos.Add(video);
            StartCoroutine(InitializeAdoptedVideo(video));
            return video;
        }

        private IEnumerator<object> InitializeAdoptedVideo(ReferenceVideo video)
        {
            yield return video.Initialize();
            CatalogChanged?.Invoke();
        }


        public void DebugListVideos()
        {
            foreach (var video in m_Videos)
            {
                Debug.Log(video);
            }
        }

    }
}
