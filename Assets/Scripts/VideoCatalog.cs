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

        private FileWatcher m_FileWatcher;
        private string m_CurrentVideoDirectory;
        public string CurrentVideoDirectory => m_CurrentVideoDirectory;
        private List<ReferenceVideo> m_Videos;
        private bool m_ScanningDirectory;
        private int m_ScanGeneration;
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
            DisposeFileWatcher();
            m_CurrentVideoDirectory = newPath;
            m_Videos = new List<ReferenceVideo>();
            m_ChangedFiles = new HashSet<string>();

            StartCoroutine(ScanReferenceDirectory());

            if (Directory.Exists(m_CurrentVideoDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentVideoDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnDirectoryChanged;
                m_FileWatcher.FileCreated += OnDirectoryChanged;
                m_FileWatcher.FileDeleted += OnDirectoryChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
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
            DisposeFileWatcher();
        }

        /// Releases the watcher for the previous folder. Navigating away must not leave a
        /// live watcher behind: its callbacks would keep firing, and would drive scans of
        /// the folder now on screen.
        private void DisposeFileWatcher()
        {
            if (m_FileWatcher == null) return;
            m_FileWatcher.EnableRaisingEvents = false;
            m_FileWatcher.FileChanged -= OnDirectoryChanged;
            m_FileWatcher.FileCreated -= OnDirectoryChanged;
            m_FileWatcher.FileDeleted -= OnDirectoryChanged;
            m_FileWatcher.Dispose();
            m_FileWatcher = null;
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
            // Numbered so a scan only ever clears the flag it set. ChangeDirectory can start a
            // replacement while this one is still running, and an unconditional clear in the
            // finally below would release the flag on the newer scan's behalf - permitting
            // overlapping rescans and reporting completion before the replacement had finished.
            int generation = ++m_ScanGeneration;
            m_ScanningDirectory = true;
            try
            {
                HashSet<string> changedSet = null;
                // We do a switcheroo on the changed list here so that there isn't a conflict with it
                // if a filewatch callback happens.
                lock (m_ChangedFiles)
                {
                    changedSet = m_ChangedFiles;
                    m_ChangedFiles = new HashSet<string>();
                }

                var existing = new HashSet<string>(m_Videos.Select(x => x.AbsolutePath));
                HashSet<string> detected;
                try
                {
                    detected = new HashSet<string>(
                        Directory.GetFiles(m_CurrentVideoDirectory, "*.*", SearchOption.TopDirectoryOnly).Where(x => m_supportedVideoExtensions.Contains(Path.GetExtension(x), StringComparer.OrdinalIgnoreCase)));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                                          e is ArgumentException || e is NotSupportedException)
                {
                    // An unreadable or missing folder gives an empty catalog rather than
                    // ending the scan, so the panel stays usable and recovers by itself.
                    Debug.LogWarning(
                        $"CATALOG_SCAN Could not scan video folder {m_CurrentVideoDirectory}: {e.Message}");
                    detected = new HashSet<string>();
                }
                StringComparer pathComparer = Path.DirectorySeparatorChar == '\\'
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;
                // The watcher covers subdirectories and reports files of any type, so a changed
                // path is only a member of this folder's catalog if it is a direct child of the
                // folder being shown and is a supported video that still exists.
                var changedDetected = CatalogChangeSet.GetChangedDetectedPaths(
                    changedSet.Where(x => IsDirectChildSupportedPath(
                        m_CurrentVideoDirectory, x, m_supportedVideoExtensions)),
                    detected, pathComparer);
                var toDelete = existing.Except(detected, pathComparer)
                    .Concat(changedDetected).Distinct(pathComparer).ToArray();
                var toScan = detected.Except(existing, pathComparer)
                    .Concat(changedDetected).Distinct(pathComparer).ToArray();

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
            }
            finally
            {
                // Rescans are gated on this flag, so it must be cleared however the scan
                // ends. Leaving it set stops the catalog refreshing for the rest of the
                // session. Only the current scan may clear it; see the generation above.
                if (generation == m_ScanGeneration)
                {
                    m_ScanningDirectory = false;
                }
            }

            CatalogChanged?.Invoke();
            if (m_DebugOutput)
            {
                DebugListVideos();
            }
        }

        /// True when path is a direct child of directory and has a supported video
        /// extension. Changed paths come from a watcher that covers subdirectories and
        /// every file type, so they need this before joining a folder's catalog.
        internal static bool IsDirectChildSupportedPath(
            string directory, string path, IEnumerable<string> supportedExtensions)
        {
            try
            {
                string fullDirectory = Path.GetFullPath(directory).TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(path);
                return string.Equals(Path.GetDirectoryName(fullPath), fullDirectory,
                        Path.DirectorySeparatorChar == '\\'
                            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
                    supportedExtensions.Contains(Path.GetExtension(fullPath),
                        StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException ||
                e is PathTooLongException)
            {
                return false;
            }
        }

        /// Gets a video from the catalog, given its saved library-relative path.
        ///
        /// Falls back to resolving the path against the library root. The catalog only
        /// lists the folder the panel is showing, but a sketch records where its videos
        /// are relative to the library, so restoring one must not depend on panel state.
        public ReferenceVideo GetVideoByPersistentPath(string path)
        {
            // The listed entry is only preferred while its file is still there. The catalog
            // can outlive a deletion, and returning a stale entry here would bypass the
            // validating resolver below rather than falling through to it.
            ReferenceVideo listed = m_Videos.FirstOrDefault(x => x.PersistentPath == path);
            if (listed != null && File.Exists(listed.AbsolutePath))
            {
                return listed;
            }
            return ResolveVideoByPersistentPath(
                HomeDirectory, path, m_supportedVideoExtensions);
        }

        /// Resolves a saved library path independently of the folder shown in the panel.
        /// Returns null when the path escapes the library, is not a supported video, or
        /// does not exist.
        internal static ReferenceVideo ResolveVideoByPersistentPath(
            string libraryPath, string path, IEnumerable<string> supportedExtensions)
        {
            if (string.IsNullOrWhiteSpace(path)) { return null; }
            try
            {
                string normalized = path.Replace('\\', '/');
                if (Path.IsPathRooted(normalized) || normalized.Contains(":")) { return null; }
                string root = Path.GetFullPath(libraryPath);
                string absolutePath = Path.GetFullPath(Path.Combine(root, normalized));
                string prefix = $"{root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)}{Path.DirectorySeparatorChar}";
                StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
                if (!absolutePath.StartsWith(prefix, comparison) ||
                    !supportedExtensions.Contains(
                        Path.GetExtension(absolutePath), StringComparer.OrdinalIgnoreCase))
                {
                    // Matched the way discovery matches. Comparing case-sensitively here
                    // made a nested clip.MP4 visible in the panel but impossible to restore.
                    return null;
                }

                return File.Exists(absolutePath) ? new ReferenceVideo(absolutePath) : null;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                                      e is ArgumentException || e is NotSupportedException)
            {
                return null;
            }
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
