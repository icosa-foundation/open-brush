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
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TiltBrush
{
    public class SoundClipCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        private const string kSafSeedPreference = "SeededDefaultSoundClips";
        static public SoundClipCatalog Instance { get; private set; }
        [SerializeField] private string[] m_DefaultSoundClips;
        [SerializeField] private bool m_DebugOutput;
        [SerializeField] private string[] m_supportedSoundClipExtensions;

        private FileWatcher m_FileWatcher;
        private bool m_ScanningDirectory;
        private bool m_DirectoryScanRequired;
        private int m_ScanGeneration;
        private HashSet<string> m_ChangedFiles;

        private List<SoundClip> m_SoundClips;
        private string m_CurrentSoundClipDirectory;
        public string CurrentSoundClipDirectory => m_CurrentSoundClipDirectory;
        private List<SoundClip> m_SoundClip;

        public bool IsScanning => m_ScanningDirectory;

        private void Awake()
        {
            Instance = this;
            Init();
        }

        private void Init()
        {
            App.InitMediaLibraryPath();
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                App.InitDirectoryAtPath(HomeDirectory);
            }
            else
            {
                App.InitSoundClipLibraryPath(m_DefaultSoundClips);
            }
            ChangeDirectory(HomeDirectory);
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
            if (m_ScanningDirectory &&
                UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                m_DirectoryScanRequired = true;
            }
            if (!m_ScanningDirectory)
            {
                StartCatalogScan();
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

        private void StartCatalogScan()
        {
            m_DirectoryScanRequired = false;
            m_ScanningDirectory = true;
            int generation = ++m_ScanGeneration;

            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                StartCoroutine(ScanSafReferenceDirectory(
                    m_CurrentSoundClipDirectory, m_SoundClips, generation));
                return;
            }

            // We do a switcheroo on the changed list here so that there isn't a conflict with it
            // if a filewatch callback happens.
            HashSet<string> changedSet;
            lock (m_ChangedFiles)
            {
                changedSet = m_ChangedFiles;
                m_ChangedFiles = new HashSet<string>();
            }

            StartCoroutine(ScanReferenceDirectory(
                m_CurrentSoundClipDirectory, m_SoundClips, changedSet, generation));
        }

        private IEnumerator<object> ScanReferenceDirectory(
            string directory, List<SoundClip> soundClips, HashSet<string> changedSet, int generation)
        {

            var existing = new HashSet<string>(soundClips.Select(x => x.AbsolutePath));
            var detected = new HashSet<string>(
                Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly).Where(
                    x => m_supportedSoundClipExtensions.Contains(
                        Path.GetExtension(x), StringComparer.OrdinalIgnoreCase)));
            var toDelete = existing.Except(detected).Concat(changedSet).ToArray();
            var toScan = detected.Except(existing).Concat(changedSet).ToArray();

            // Remove deleted sound clips from the list. Currently playing clips may continue to play, but will
            // not appear in the reference panel.
            var retiredSoundClips = soundClips.Where(x => toDelete.Contains(x.AbsolutePath)).ToArray();
            soundClips.RemoveAll(x => toDelete.Contains(x.AbsolutePath));
            foreach (var soundClip in retiredSoundClips)
            {
                soundClip.ReleaseThumbnail();
            }

            var newSoundClips = new List<SoundClip>();
            foreach (var filePath in toScan)
            {
                SoundClip clipRef = new SoundClip(filePath);
                newSoundClips.Add(clipRef);
                soundClips.Add(clipRef);
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
                if (generation != m_ScanGeneration)
                {
                    clipRef.ReleaseThumbnail();
                    // The replacement scan owns m_ScanningDirectory. A stale scan must not clear
                    // it while the replacement may still be running.
                    yield break;
                }
            }

            m_ScanningDirectory = false;
            CatalogChanged?.Invoke();
            if (m_DebugOutput)
            {
                DebugListSoundClips();
            }
        }

        private IEnumerator<object> ScanSafReferenceDirectory(
            string directory, List<SoundClip> soundClips, int generation)
        {
            IUserStorageBackend backend = UserStorage.Backend;
            string rootIdentity = backend.RootIdentity;
            try
            {
                if (!backend.IsReady)
                {
                    yield break;
                }
                string relativeDirectory = Path.GetRelativePath(HomeDirectory, directory)
                    .Replace('\\', '/');
                if (relativeDirectory == ".") relativeDirectory = "";
                if (relativeDirectory == ".." || relativeDirectory.StartsWith("../") ||
                    Path.IsPathRooted(relativeDirectory))
                {
                    Debug.LogError($"[SAF_SOUND] Directory is outside the sound library: {directory}");
                    yield break;
                }

                string seedKey = OpenBrushStorage.GetSafRootScopedPreferenceKey(
                    kSafSeedPreference, rootIdentity);
                Dictionary<string, byte[]> defaults = null;
                if (PlayerPrefs.GetInt(seedKey, 0) == 0)
                {
                    defaults = new Dictionary<string, byte[]>();
                    foreach (string resourcePath in m_DefaultSoundClips ?? Array.Empty<string>())
                    {
                        TextAsset resource = Resources.Load<TextAsset>(resourcePath);
                        if (resource == null)
                        {
                            Debug.LogWarning($"[SAF_SOUND] Missing default sound: {resourcePath}");
                            continue;
                        }
                        defaults[Path.GetFileName(resourcePath)] = resource.bytes;
                        Resources.UnloadAsset(resource);
                    }
                }
                Task<StorageTreeResult> query = Task.Run(() => QuerySafSoundClips(
                    backend, relativeDirectory, m_supportedSoundClipExtensions, defaults));
                while (!query.IsCompleted)
                {
                    yield return null;
                }
                if (generation != m_ScanGeneration || rootIdentity != backend.RootIdentity)
                {
                    yield break;
                }
                if (query.IsFaulted || query.IsCanceled)
                {
                    Debug.LogWarning($"[SAF_SOUND] Query failed; retaining the catalog: " +
                        $"{query.Exception?.GetBaseException().Message}");
                    yield break;
                }
                StorageTreeResult result = query.Result;
                if (!result.Success && result.Code != StorageResultCode.NotFound)
                {
                    Debug.LogWarning($"[SAF_SOUND] Query failed; retaining the catalog: {result.Error}");
                    yield break;
                }
                if (defaults != null)
                {
                    PlayerPrefs.SetInt(seedKey, 1);
                    PlayerPrefs.Save();
                }

                var previous = soundClips.ToDictionary(clip => clip.CatalogIdentity);
                var next = new List<SoundClip>();
                var added = new List<SoundClip>();
                foreach (StorageDocument document in result.Entries)
                {
                    string identity = GetSafCatalogIdentity(backend, document);
                    if (!previous.TryGetValue(identity, out SoundClip clip))
                    {
                        clip = CreateSafSoundClip(backend, document);
                    }
                    if (!clip.IsInitialized) added.Add(clip);
                    previous.Remove(identity);
                    next.Add(clip);
                }
                foreach (SoundClip retired in previous.Values) retired.ReleaseThumbnail();
                soundClips.Clear();
                soundClips.AddRange(next);
                CatalogChanged?.Invoke();
                foreach (SoundClip clip in added)
                {
                    yield return clip.Initialize();
                    if (generation != m_ScanGeneration || rootIdentity != backend.RootIdentity)
                    {
                        clip.ReleaseThumbnail();
                        yield break;
                    }
                }
                CatalogChanged?.Invoke();
            }
            finally
            {
                // A replacement scan owns this flag after a folder change.
                if (generation == m_ScanGeneration) m_ScanningDirectory = false;
            }
        }

        internal static StorageTreeResult QuerySafSoundClips(
            IUserStorageBackend backend, string relativeDirectory, string[] extensions,
            IReadOnlyDictionary<string, byte[]> defaults)
        {
            string rootIdentity = backend.RootIdentity;
            if (defaults != null)
            {
                StorageDirectoryResult existing = backend.List(
                    StorageArea.MediaLibrarySoundClips, "", CancellationToken.None);
                if (!existing.Success && existing.Code != StorageResultCode.NotFound)
                {
                    return StorageTreeResult.Failed(existing.Code, existing.Error);
                }
                // Match the local library: seed a new/empty library, not an existing user's library.
                if (existing.Documents.Count == 0)
                {
                    foreach (var seed in defaults)
                    {
                        if (rootIdentity != backend.RootIdentity)
                        {
                            return StorageTreeResult.Failed(
                                StorageResultCode.Cancelled, "The shared folder changed.");
                        }
                        using (IStorageWriteTransaction transaction = backend.BeginWrite(
                            StorageArea.MediaLibrarySoundClips, seed.Key,
                            StorageMimeTypes.ForPath(seed.Key), CancellationToken.None))
                        {
                            // A provider file may have appeared since the initial listing.
                            if (backend.Kind == StorageBackendKind.StorageAccessFramework &&
                                transaction.TargetDocumentId.IsValid)
                            {
                                continue;
                            }
                            using (Stream output = transaction.OpenWrite())
                            {
                                output.Write(seed.Value, 0, seed.Value.Length);
                            }
                            StorageMutationResult write = transaction.Commit();
                            if (!write.Success) return StorageTreeResult.Failed(write.Code, write.Error);
                        }
                    }
                }
            }
            return backend.EnumerateTree(StorageArea.MediaLibrarySoundClips, relativeDirectory,
                new StorageTreeQuery(includeExtensions: extensions), CancellationToken.None);
        }

        private static string GetSafCatalogIdentity(IUserStorageBackend backend, StorageDocument document)
        {
            return $"{backend.RootIdentity}|{document.DocumentId.Value}|{document.LastModified:o}|{document.Size}";
        }

        internal static SoundClip CreateSafSoundClip(IUserStorageBackend backend, StorageDocument document)
        {
            string rootIdentity = backend.RootIdentity;
            return new SoundClip(backend.GetMaterializationPath(document.DocumentId),
                document.RelativeDisplayPath, GetSafCatalogIdentity(backend, document), () =>
                {
                    if (rootIdentity != backend.RootIdentity)
                    {
                        throw new IOException("The shared sound library changed.");
                    }
                    return backend.Materialize(document.DocumentId,
                        MaterializationScope.File, CancellationToken.None);
                });
        }

        /// Gets a clip form the catalog, given its filename. Returns null if no such clip is found.
        public SoundClip GetSoundClipByPersistentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            SoundClip soundClip = m_SoundClips.FirstOrDefault(x => x.PersistentPath == path);
            if (soundClip != null)
            {
                return soundClip;
            }

            // The catalog contains only the directory currently shown in the reference panel.
            // Resolve saved paths directly so clips in subdirectories can be restored without the
            // user first navigating to their directory.
            if (Path.IsPathRooted(path))
            {
                return null;
            }

            try
            {
                string libraryPath = Path.GetFullPath(App.SoundClipLibraryPath());
                string normalizedPath = path
                    .Replace('\\', Path.DirectorySeparatorChar)
                    .Replace('/', Path.DirectorySeparatorChar);
                string absolutePath = Path.GetFullPath(Path.Combine(libraryPath, normalizedPath));
                string libraryPathWithSeparator = libraryPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                StringComparison pathComparison = Path.DirectorySeparatorChar == '\\'
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                if (!absolutePath.StartsWith(libraryPathWithSeparator, pathComparison) ||
                    !m_supportedSoundClipExtensions.Contains(
                        Path.GetExtension(absolutePath), StringComparer.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
                {
                    string relativePath = Path.GetRelativePath(libraryPath, absolutePath).Replace('\\', '/');
                    StorageTreeResult listing = UserStorage.Backend.EnumerateTree(
                        StorageArea.MediaLibrarySoundClips,
                        Path.GetDirectoryName(relativePath)?.Replace('\\', '/') ?? "",
                        new StorageTreeQuery(recursive: false,
                            includeExtensions: m_supportedSoundClipExtensions),
                        CancellationToken.None);
                    StorageDocument document = listing.Success
                        ? listing.Entries.FirstOrDefault(item => !item.IsDirectory &&
                            item.DisplayName == Path.GetFileName(relativePath))
                        : null;
                    if (document != null) return CreateSafSoundClip(UserStorage.Backend, document);
                    // Preserve access to locally extracted audio in the working library.
                }
                if (!File.Exists(absolutePath)) return null;

                return new SoundClip(absolutePath);
            }
            catch (Exception e) when (
                e is ArgumentException ||
                e is NotSupportedException ||
                e is PathTooLongException)
            {
                return null;
            }
        }

        public void DebugListSoundClips()
        {
            foreach (var clip in m_SoundClips)
            {
                Debug.Log(clip);
            }
        }

        public void ChangeDirectory(string newPath)
        {
            DisposeFileWatcher();
            ++m_ScanGeneration;
            m_DirectoryScanRequired = false;
            if (m_SoundClips != null)
            {
                foreach (var soundClip in m_SoundClips)
                {
                    soundClip.ReleaseThumbnail();
                }
            }
            m_CurrentSoundClipDirectory = newPath;
            m_SoundClips = new List<SoundClip>();
            m_ChangedFiles = new HashSet<string>();

            StartCatalogScan();

            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework &&
                Directory.Exists(m_CurrentSoundClipDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentSoundClipDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnDirectoryChanged;
                m_FileWatcher.FileCreated += OnDirectoryChanged;
                m_FileWatcher.FileDeleted += OnDirectoryChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }
        }

        public string HomeDirectory => App.SoundClipLibraryPath();
        public bool IsHomeDirectory() => m_CurrentSoundClipDirectory == HomeDirectory;

        public bool IsSubDirectoryOfHome()
        {
            return m_CurrentSoundClipDirectory.StartsWith(HomeDirectory);
        }
        public string GetCurrentDirectory()
        {
            return m_CurrentSoundClipDirectory;
        }

    }
}
