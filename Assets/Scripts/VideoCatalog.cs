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
using System.Threading;
using UnityEngine;

namespace TiltBrush
{
    public class VideoCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        private const string kSafSeedPreference =
            "GooglePlayStorage.SeededDefaultVideosFdV1";
        static public VideoCatalog Instance { get; private set; }
        [SerializeField] private string[] m_DefaultVideos;
        [SerializeField] private bool m_DebugOutput;
        [SerializeField] private string[] m_supportedVideoExtensions;

        private FileWatcher m_FileWatcher;
        private string m_CurrentVideoDirectory;
        public string CurrentVideoDirectory => m_CurrentVideoDirectory;
        private List<ReferenceVideo> m_Videos;
        private bool m_ScanningDirectory;
        private bool m_DirectoryScanRequired;
        private HashSet<string> m_ChangedFiles;
        private bool m_SeedingSafDefaults;
        private string m_SafSeedAttemptedRootIdentity;

        public bool IsScanning => m_ScanningDirectory;

        private void Awake()
        {
            Instance = this;
            Init();
        }

        private void Init()
        {
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                App.InitMediaLibraryPath();
                App.InitVideoLibraryPath(m_DefaultVideos);
            }
            ChangeDirectory(HomeDirectory);
        }

        public void ChangeDirectory(string newPath)
        {
            m_CurrentVideoDirectory = newPath;
            m_Videos = new List<ReferenceVideo>();
            m_ChangedFiles = new HashSet<string>();

            if (m_ScanningDirectory)
            {
                m_DirectoryScanRequired = true;
            }
            else
            {
                StartCoroutine(ScanReferenceDirectory());
            }

            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework &&
                Directory.Exists(m_CurrentVideoDirectory))
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
            if (m_FileWatcher != null)
            {
                m_FileWatcher.EnableRaisingEvents = false;
            }
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
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework &&
                UserStorage.Backend.IsReady &&
                !m_SeedingSafDefaults &&
                m_SafSeedAttemptedRootIdentity !=
                    UserStorage.Backend.RootIdentity &&
                PlayerPrefs.GetInt(
                    OpenBrushStorage.GetSafRootScopedPreferenceKey(
                        kSafSeedPreference),
                    0) == 0)
            {
                StartCoroutine(SeedSafDefaults());
            }
            if (m_DirectoryScanRequired)
            {
                ForceCatalogScan();
            }
        }

        private IEnumerator<object> SeedSafDefaults()
        {
            m_SeedingSafDefaults = true;
            IUserStorageBackend backend = UserStorage.Backend;
            string seedRootIdentity = backend.RootIdentity;
            m_SafSeedAttemptedRootIdentity = seedRootIdentity;
            var listingFuture = new Future<StorageDirectoryResult>(
                () => backend.List(
                    StorageArea.MediaLibraryVideos, "", CancellationToken.None),
                cleanupFunction: null,
                longRunning: true);
            StorageDirectoryResult listing = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = listingFuture.TryGetResult(out listing);
                }
                catch (FutureFailed e)
                {
                    Debug.LogWarning(
                        $"SAF_STORAGE Could not inspect default video destination: " +
                        $"{e.InnerException?.Message ?? e.Message}");
                    m_SeedingSafDefaults = false;
                    yield break;
                }
                if (finished)
                {
                    break;
                }
                yield return null;
            }
            if (!listing.Success && listing.Code != StorageResultCode.NotFound)
            {
                m_SeedingSafDefaults = false;
                yield break;
            }

            if (listing.Code == StorageResultCode.NotFound ||
                listing.Documents.Count == 0)
            {
                foreach (string resourcePath in m_DefaultVideos)
                {
                    TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                    if (resource == null)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Missing default video: {resourcePath}");
                        continue;
                    }
                    byte[] bytes = resource.bytes;
                    Resources.UnloadAsset(resource);
                    string displayName = Path.GetFileName(resourcePath);
                    var writeFuture = new Future<StorageMutationResult>(
                        () => WriteSafDefaultVideo(
                            backend, displayName, bytes),
                        cleanupFunction: null,
                        longRunning: true);
                    StorageMutationResult result;
                    while (true)
                    {
                        bool finished;
                        try
                        {
                            finished = writeFuture.TryGetResult(out result);
                        }
                        catch (FutureFailed e)
                        {
                            Debug.LogWarning(
                                $"SAF_STORAGE Failed to seed {displayName}: " +
                                $"{e.InnerException?.Message ?? e.Message}");
                            m_SeedingSafDefaults = false;
                            yield break;
                        }
                        if (finished)
                        {
                            break;
                        }
                        yield return null;
                    }
                    if (!result.Success)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Failed to seed {displayName}: {result.Error}");
                        m_SeedingSafDefaults = false;
                        yield break;
                    }
                }
            }

            if (!string.Equals(
                    seedRootIdentity,
                    backend.RootIdentity,
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
            ForceCatalogScan();
        }

        private static StorageMutationResult WriteSafDefaultVideo(
            IUserStorageBackend backend, string displayName, byte[] bytes)
        {
            using (IStorageWriteTransaction transaction = backend.BeginWrite(
                StorageArea.MediaLibraryVideos,
                displayName,
                "video/mp4",
                CancellationToken.None))
            {
                using (Stream output = transaction.OpenWrite())
                {
                    output.Write(bytes, 0, bytes.Length);
                }
                return transaction.Commit();
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
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                foreach (object item in ScanSafReferenceDirectory())
                {
                    yield return item;
                }
                yield break;
            }

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
                Directory.GetFiles(m_CurrentVideoDirectory, "*.*", SearchOption.AllDirectories).Where(x => m_supportedVideoExtensions.Contains(Path.GetExtension(x))));
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

        private IEnumerable<object> ScanSafReferenceDirectory()
        {
            IUserStorageBackend backend = UserStorage.Backend;
            string scanRootIdentity = backend.RootIdentity;
            string relativeDirectory;
            if (!TryGetRelativeDirectory(
                    HomeDirectory, m_CurrentVideoDirectory, out relativeDirectory))
            {
                Debug.LogError(
                    $"SAF_CATALOG Video directory is outside its storage area: " +
                    $"{m_CurrentVideoDirectory}");
                m_ScanningDirectory = false;
                yield break;
            }

            var listingFuture = new Future<List<StorageDocument>>(
                () => ListSafFilesRecursively(
                    backend, StorageArea.MediaLibraryVideos, relativeDirectory),
                cleanupFunction: null,
                longRunning: true);
            List<StorageDocument> documents = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = listingFuture.TryGetResult(out documents);
                }
                catch (FutureFailed e)
                {
                    Debug.LogWarning(
                        $"SAF_CATALOG Video query failed; retaining the previous catalog: " +
                        $"{e.InnerException?.Message ?? e.Message}");
                    m_ScanningDirectory = false;
                    yield break;
                }
                if (finished)
                {
                    break;
                }
                yield return null;
            }

            if (!string.Equals(
                    scanRootIdentity,
                    backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                m_ScanningDirectory = false;
                m_DirectoryScanRequired = true;
                yield break;
            }

            var oldVideos = m_Videos.ToDictionary(video => video.CatalogIdentity);
            var nextVideos = new List<ReferenceVideo>();
            var newVideos = new List<ReferenceVideo>();
            foreach (StorageDocument document in documents)
            {
                if (!m_supportedVideoExtensions.Contains(
                        Path.GetExtension(document.DisplayName)))
                {
                    continue;
                }

                string identity =
                    $"{document.DocumentId.Value}|{document.LastModified:o}|{document.Size}";
                if (oldVideos.TryGetValue(identity, out ReferenceVideo existing))
                {
                    nextVideos.Add(existing);
                    oldVideos.Remove(identity);
                    continue;
                }

                StorageDocumentId documentId = document.DocumentId;
                string path = backend.GetMaterializationPath(documentId);
                var video = new ReferenceVideo(
                    path,
                    identity,
                    () => backend.Materialize(
                        documentId, MaterializationScope.File, CancellationToken.None));
                nextVideos.Add(video);
                newVideos.Add(video);
            }

            foreach (ReferenceVideo removed in oldVideos.Values)
            {
                removed.Dispose();
            }
            m_Videos = nextVideos;

            TimeSpan interval = TimeSpan.FromSeconds(4);
            DateTime nextRefresh = DateTime.Now + interval;
            foreach (ReferenceVideo video in newVideos)
            {
                if (DateTime.Now > nextRefresh)
                {
                    CatalogChanged?.Invoke();
                    nextRefresh = DateTime.Now + interval;
                }
                yield return video.Initialize();
            }

            m_ScanningDirectory = false;
            CatalogChanged?.Invoke();
        }

        private static List<StorageDocument> ListSafFilesRecursively(
            IUserStorageBackend backend, StorageArea area, string relativeDirectory)
        {
            var files = new List<StorageDocument>();
            StorageDirectoryResult listing = backend.List(
                area, relativeDirectory, CancellationToken.None);
            if (!listing.Success)
            {
                throw new IOException($"{listing.Code}: {listing.Error}");
            }
            foreach (StorageDocument document in listing.Documents)
            {
                if (document.IsDirectory)
                {
                    string childDirectory = string.IsNullOrEmpty(relativeDirectory)
                        ? document.DisplayName
                        : $"{relativeDirectory}/{document.DisplayName}";
                    files.AddRange(ListSafFilesRecursively(
                        backend, area, childDirectory));
                }
                else
                {
                    files.Add(document);
                }
            }
            return files;
        }

        private static bool TryGetRelativeDirectory(
            string root, string directory, out string relativeDirectory)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullDirectory = Path.GetFullPath(directory).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(fullRoot, fullDirectory, StringComparison.OrdinalIgnoreCase))
            {
                relativeDirectory = "";
                return true;
            }
            string prefix = fullRoot + Path.DirectorySeparatorChar;
            if (!fullDirectory.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                relativeDirectory = null;
                return false;
            }
            relativeDirectory = fullDirectory.Substring(prefix.Length).Replace('\\', '/');
            return true;
        }

        /// Gets a video form the catalog, given its filename. Returns null if no such video is found.
        public ReferenceVideo GetVideoByPersistentPath(string path)
        {
            return m_Videos.FirstOrDefault(x => x.PersistentPath == path);
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
