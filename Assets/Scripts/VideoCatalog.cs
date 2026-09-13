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
        private int m_ScanGeneration;
        private volatile bool m_DirectoryScanRequired;
        private readonly CatalogChangeQueue m_ChangedFiles = new CatalogChangeQueue();
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
            DisposeFileWatcher();
            m_CurrentVideoDirectory = newPath;
            ++m_ScanGeneration;
            if (m_Videos != null)
            {
                foreach (ReferenceVideo video in m_Videos) { video.ReleaseThumbnail(); }
            }
            m_Videos = new List<ReferenceVideo>();
            m_ChangedFiles.Clear();

            if (m_ScanningDirectory)
            {
                m_DirectoryScanRequired = true;
            }
            else
            {
                ForceCatalogScan();
            }

            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework &&
                Directory.Exists(m_CurrentVideoDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentVideoDirectory, "*.*");
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite |
                    NotifyFilters.FileName | NotifyFilters.DirectoryName;
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
            ++m_ScanGeneration;
            foreach (var video in m_Videos)
            {
                video.Dispose();
            }
            DisposeFileWatcher();
        }

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
                StartCoroutine(ScanReferenceDirectory(
                    m_CurrentVideoDirectory, ++m_ScanGeneration));
            }
        }

        private void OnDirectoryChanged(object source, FileSystemEventArgs e)
        {
            if (!ReferenceEquals(source, m_FileWatcher)) return;
            if (e.ChangeType == WatcherChangeTypes.Changed &&
                IsDirectChildSupportedPath(
                    m_CurrentVideoDirectory, e.FullPath, m_supportedVideoExtensions))
            {
                m_ChangedFiles.Add(e.FullPath);
            }
            m_DirectoryScanRequired = true;
        }

        private IEnumerator<object> ScanReferenceDirectory(string directory, int generation)
        {
            m_ScanningDirectory = true;
            try
            {
                using (var scan = ScanReferenceDirectoryImpl(directory, generation))
                {
                    while (scan.MoveNext()) { yield return scan.Current; }
                }
            }
            finally
            {
                m_ScanningDirectory = false;
            }
        }

        private IEnumerator<object> ScanReferenceDirectoryImpl(string directory, int generation)
        {
            List<ReferenceVideo> videos = m_Videos;
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                foreach (object item in ScanSafReferenceDirectory(directory, generation))
                {
                    yield return item;
                }
                yield break;
            }

            string[] changedSet = m_ChangedFiles.Drain();

            StringComparer pathComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var existing = new HashSet<string>(videos.Select(x => x.AbsolutePath), pathComparer);
            HashSet<string> detected;
            try
            {
                detected = new HashSet<string>(
                    Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly).Where(
                        x => IsSupportedVideoExtension(x, m_supportedVideoExtensions)));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException ||
                e is ArgumentException || e is NotSupportedException)
            {
                Debug.LogWarning($"CATALOG_SCAN Could not scan video folder {directory}: {e.Message}");
                yield break;
            }
            var changed = changedSet.Where(x => IsDirectChildSupportedPath(
                directory, x, m_supportedVideoExtensions));
            var changedDetected = CatalogChangeSet.GetChangedDetectedPaths(
                changed, detected, pathComparer);
            var toDelete = existing.Except(detected, pathComparer)
                .Concat(changedDetected).Distinct(pathComparer).ToArray();
            var toScan = detected.Except(existing, pathComparer)
                .Concat(changedDetected).Distinct(pathComparer).ToArray();

            // Remove deleted videos from the list. Currently playing videos may continue to play, but will
            // not appear in the reference panel.
            foreach (ReferenceVideo retired in videos.Where(x => toDelete.Contains(x.AbsolutePath, pathComparer)))
            {
                retired.ReleaseThumbnail();
            }
            videos.RemoveAll(x => toDelete.Contains(
                x.AbsolutePath, pathComparer));

            var newVideos = new List<ReferenceVideo>();
            foreach (var filePath in toScan)
            {
                ReferenceVideo videoRef = new ReferenceVideo(filePath);
                newVideos.Add(videoRef);
                videos.Add(videoRef);
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
                if (generation != m_ScanGeneration ||
                    !string.Equals(directory, m_CurrentVideoDirectory,
                        StringComparison.Ordinal))
                {
                    videoRef.ReleaseThumbnail();
                    yield break;
                }
            }

            if (generation != m_ScanGeneration || !string.Equals(
                    directory, m_CurrentVideoDirectory, StringComparison.Ordinal))
            {
                foreach (var video in newVideos) video.ReleaseThumbnail();
                yield break;
            }
            CatalogChanged?.Invoke();
            if (m_DebugOutput)
            {
                DebugListVideos();
            }
        }

        private IEnumerable<object> ScanSafReferenceDirectory(string directory, int generation)
        {
            IUserStorageBackend backend = UserStorage.Backend;
            string scanRootIdentity = backend.RootIdentity;
            StringComparer pathComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            string relativeDirectory;
            if (!TryGetRelativeDirectory(
                    HomeDirectory, directory, out relativeDirectory))
            {
                Debug.LogError(
                    $"SAF_CATALOG Video directory is outside its storage area: " +
                    $"{directory}");
                yield break;
            }

            var listingFuture = new Future<List<StorageDocument>>(
                () => ListSafFiles(
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
                m_DirectoryScanRequired = true;
                yield break;
            }

            if (!CatalogScanGuard.IsCurrent(
                    generation, m_ScanGeneration, backend, UserStorage.Backend,
                    scanRootIdentity, backend.RootIdentity,
                    directory, m_CurrentVideoDirectory, pathComparer))
            {
                m_DirectoryScanRequired = true;
                yield break;
            }

            var oldVideos = m_Videos.ToDictionary(video => video.CatalogIdentity);
            var nextVideos = new List<ReferenceVideo>();
            var newVideos = new List<ReferenceVideo>();
            foreach (StorageDocument document in documents)
            {
                if (!IsSupportedVideoExtension(
                        document.DisplayName, m_supportedVideoExtensions))
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
                        documentId, MaterializationScope.File, CancellationToken.None),
                    document.RelativeDisplayPath);
                nextVideos.Add(video);
                newVideos.Add(video);
            }

            foreach (ReferenceVideo removed in oldVideos.Values)
            {
                removed.ReleaseThumbnail();
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
                if (!CatalogScanGuard.IsCurrent(
                        generation, m_ScanGeneration, backend, UserStorage.Backend,
                        scanRootIdentity, backend.RootIdentity,
                        directory, m_CurrentVideoDirectory, pathComparer))
                {
                    video.ReleaseThumbnail();
                    m_DirectoryScanRequired = true;
                    yield break;
                }
            }

            if (!CatalogScanGuard.IsCurrent(
                    generation, m_ScanGeneration, backend, UserStorage.Backend,
                    scanRootIdentity, backend.RootIdentity,
                    directory, m_CurrentVideoDirectory, pathComparer))
            {
                m_DirectoryScanRequired = true;
                yield break;
            }
            CatalogChanged?.Invoke();
        }

        internal static List<StorageDocument> ListSafFiles(
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
                if (!document.IsDirectory)
                {
                    files.Add(new StorageDocument(
                        document.DocumentId, document.ParentDocumentId, document.DisplayName,
                        document.MimeType, document.IsDirectory, document.Size,
                        document.LastModified, document.ProviderFlags,
                        string.IsNullOrEmpty(relativeDirectory)
                            ? document.DisplayName
                            : $"{relativeDirectory}/{document.DisplayName}"));
                }
            }
            return files;
        }

        internal static bool IsSupportedVideoExtension(
            string path, IEnumerable<string> supportedExtensions)
        {
            string extension = Path.GetExtension(path);
            return supportedExtensions.Any(supportedExtension =>
                string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase));
        }

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
                    IsSupportedVideoExtension(fullPath, supportedExtensions);
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException ||
                e is PathTooLongException)
            {
                return false;
            }
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

        /// Resolves a saved library path independently of the folder shown in the panel.
        public ReferenceVideo GetVideoByPersistentPath(string path)
        {
            return m_Videos.FirstOrDefault(x => x.PersistentPath == path) ??
                ResolveVideoByPersistentPath(
                    UserStorage.Backend, HomeDirectory, path, m_supportedVideoExtensions);
        }

        internal static ReferenceVideo ResolveVideoByPersistentPath(
            IUserStorageBackend backend, string libraryPath, string path,
            IEnumerable<string> supportedExtensions)
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
                    ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                if (!absolutePath.StartsWith(prefix, comparison) ||
                    !IsSupportedVideoExtension(absolutePath, supportedExtensions)) { return null; }

                string relativePath = Path.GetRelativePath(root, absolutePath).Replace('\\', '/');
                if (backend.Kind == StorageBackendKind.StorageAccessFramework)
                {
                    // The provider is authoritative; an old materialization is not a fallback
                    // for a missing shared file or a revoked grant.
                    var source = new OpenBrushStorage.MediaSource(
                        backend, StorageArea.MediaLibraryVideos, relativePath);
                    return new ReferenceVideo(source.LocalPath, source.Identity,
                        () => source.Materialize(MaterializationScope.File), relativePath);
                }
                return File.Exists(absolutePath)
                    ? new ReferenceVideo(absolutePath, absolutePath, null, relativePath) : null;
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
