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

    public class ReferenceImageCatalog : MonoBehaviour, IReferenceItemCatalog
    {
        const int IMAGE_LOAD_PER_FRAME = 4;
        const int IMAGE_LOAD_PER_FRAME_COMPOSITOR = 8;
        public const int TEXTURE_CREATIONS_PER_FRAME = 1;
        public const int MAX_ICON_TEX_DIMENSION = 256;

        static public ReferenceImageCatalog m_Instance;

        public event Action CatalogChanged;
        private int m_TexturesCreatedThisFrame;

        protected FileWatcher m_FileWatcher;
        protected string m_CurrentImagesDirectory;
        public string CurrentImagesDirectory => m_CurrentImagesDirectory;

        protected List<ReferenceImage> m_Images;
        protected Stack<int> m_RequestedLoads; // it's okay if this contains duplicates
        private bool m_DirNeedsProcessing;
        private string m_ChangedFile;
        private int m_InCompositorLoad;
        private bool m_RunningImageCacheCoroutine;
        private bool m_ResetImageEnumeration;
        private bool m_SafQueryInProgress;
        private bool m_SeedingSafDefaults;
        private string m_SafSeedAttemptedRootIdentity;
        private const string kSafSeedPreference =
            "GooglePlayStorage.SeededDefaultReferenceImagesFdV1";

        [SerializeField] private Texture2D m_ErrorImage;
        [SerializeField] protected string[] m_DefaultImages;

        public bool IsScanning => m_RunningImageCacheCoroutine || m_SafQueryInProgress;

        public Texture2D ErrorImage { get { return m_ErrorImage; } }
        public int TexturesCreatedThisFrame
        {
            get { return m_TexturesCreatedThisFrame; }
            set { m_TexturesCreatedThisFrame = value; }
        }

        public void ForceCatalogScan()
        {
            ProcessReferenceDirectory(false);
        }

        void Awake()
        {
            m_Instance = this;
            m_RequestedLoads = new Stack<int>();

            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                App.InitMediaLibraryPath();
                App.InitReferenceImagePath(m_DefaultImages);
            }
            ImageCache.DeleteObsoleteCaches();
            ChangeDirectory(HomeDirectory);
        }

        public virtual void ChangeDirectory(string newPath)
        {
            m_CurrentImagesDirectory = newPath;

            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework &&
                Directory.Exists(m_CurrentImagesDirectory))
            {
                m_FileWatcher = new FileWatcher(m_CurrentImagesDirectory);
                m_FileWatcher.NotifyFilter = NotifyFilters.LastWrite;
                m_FileWatcher.FileChanged += OnChanged;
                m_FileWatcher.FileCreated += OnChanged;
                m_FileWatcher.FileDeleted += OnChanged;
                m_FileWatcher.EnableRaisingEvents = true;
            }

            m_Images = new List<ReferenceImage>();
            ProcessReferenceDirectory(userOverlay: false);
        }

        public virtual string HomeDirectory => App.ReferenceImagePath();
        protected virtual StorageArea StorageAreaKind => StorageArea.MediaLibraryImages;
        protected virtual string SafSeedPreferenceKey => kSafSeedPreference;

        public virtual bool IsHomeDirectory()
        {
            return m_CurrentImagesDirectory == HomeDirectory;
        }

        public virtual bool IsSubDirectoryOfHome()
        {
            return m_CurrentImagesDirectory.StartsWith(HomeDirectory);
        }

        public virtual string GetCurrentDirectory()
        {
            return m_CurrentImagesDirectory;
        }

        // This is not persistent state; it avoids allocating a transient Stack every frame
        private Stack<int> Update__temporarystack = new Stack<int>();

        void Update()
        {
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework &&
                UserStorage.Backend.IsReady &&
                !m_SeedingSafDefaults &&
                m_SafSeedAttemptedRootIdentity !=
                    UserStorage.Backend.RootIdentity &&
                PlayerPrefs.GetInt(
                    OpenBrushStorage.GetSafRootScopedPreferenceKey(
                        SafSeedPreferenceKey),
                    0) == 0)
            {
                StartCoroutine(SeedSafDefaults());
            }

            // Safest not to interfere with LoadAllImages().
            // This code can mutate m_Images or mutate entries in m_Images.
            // LoadAllImages() can cause hitchy loads, which if processed here can
            // interfere with the compositor/progress bar fade-in and fade-out, etc.
            if (m_InCompositorLoad > 0)
            {
                return;
            }

            // If our folder was tampered with, reset the directory
            if (m_DirNeedsProcessing)
            {
                ProcessReferenceDirectory();
            }

            m_TexturesCreatedThisFrame = 0;
            // Grab a few units of work
            var working = Update__temporarystack;
            Debug.Assert(working.Count == 0);
            for (int i = 0; i < IMAGE_LOAD_PER_FRAME && m_RequestedLoads.Count > 0; ++i)
            {
                working.Push(m_RequestedLoads.Pop());
            }

            // Process work (perhaps generating future work)
            while (working.Count > 0)
            {
                int iImage = working.Pop();
                if (!m_Images[iImage].RequestLoad())
                {
                    m_RequestedLoads.Push(iImage);
                }
            }
        }

        protected virtual byte[] LoadSafDefaultBytes(string resourcePath)
        {
            string loadPath = resourcePath.Substring(0, resourcePath.IndexOf('.'));
            Texture2D texture = Resources.Load<Texture2D>(loadPath);
            if (texture == null)
            {
                return null;
            }
            try
            {
                return texture.EncodeToPNG();
            }
            finally
            {
                Resources.UnloadAsset(texture);
            }
        }

        private IEnumerator<object> SeedSafDefaults()
        {
            m_SeedingSafDefaults = true;
            IUserStorageBackend backend = UserStorage.Backend;
            string seedRootIdentity = backend.RootIdentity;
            m_SafSeedAttemptedRootIdentity = seedRootIdentity;
            var listingFuture = new Future<StorageDirectoryResult>(
                () => backend.List(StorageAreaKind, "", CancellationToken.None),
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
                        $"SAF_STORAGE Could not inspect default media destination: " +
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
                foreach (string resourcePath in m_DefaultImages)
                {
                    byte[] bytes = LoadSafDefaultBytes(resourcePath);
                    if (bytes == null)
                    {
                        Debug.LogWarning(
                            $"SAF_STORAGE Missing default media resource: {resourcePath}");
                        continue;
                    }
                    string displayName = Path.GetFileName(resourcePath);
                    string mimeType = GetImageMimeType(displayName);
                    var writeFuture = new Future<StorageMutationResult>(
                        () => WriteSafDefault(
                            backend, StorageAreaKind, displayName, mimeType, bytes),
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
                    SafSeedPreferenceKey, seedRootIdentity),
                1);
            PlayerPrefs.Save();
            m_SeedingSafDefaults = false;
            ForceCatalogScan();
        }

        private static StorageMutationResult WriteSafDefault(
            IUserStorageBackend backend,
            StorageArea area,
            string displayName,
            string mimeType,
            byte[] bytes)
        {
            using (IStorageWriteTransaction transaction = backend.BeginWrite(
                area, displayName, mimeType, CancellationToken.None))
            {
                using (Stream output = transaction.OpenWrite())
                {
                    output.Write(bytes, 0, bytes.Length);
                }
                return transaction.Commit();
            }
        }

        private static string GetImageMimeType(string displayName)
        {
            switch (Path.GetExtension(displayName).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".svg":
                    return "image/svg+xml";
                case ".hdr":
                    return "image/vnd.radiance";
                case ".txt":
                    return "text/plain";
                default:
                    return "image/png";
            }
        }

        // It is possible for m_Images to change while this is happening. When that happens, the
        // m_ResetImageEnumeration flag is set, and the enumeration is reset, starting again from the
        // beginning.
        private IEnumerator<Null> LoadAvailableImageCaches()
        {
            Debug.Assert(m_RunningImageCacheCoroutine, "Caller must set this");
            try
            {
                // We can't set it ourselves because there's a frame of latency between
                // caller calling StartCoroutine and us getting control.
                yield return null; // Give the compositor time to spool up
            restart:
                m_ResetImageEnumeration = false;
                foreach (var image in m_Images)
                {
                    image.RequestLoadIconCache();
                    yield return null;
                    if (m_ResetImageEnumeration)
                    {
                        goto restart;
                    }
                }
            }
            finally
            {
                m_RunningImageCacheCoroutine = false;
            }
        }

        /// Load all not-already-loaded images in parallel,
        /// allowing as much main thread usage as desired
        public IEnumerator<Null> LoadAllImagesCoroutine()
        {
            // Want this to happen right away. The returned coroutine won't be pumped
            // until after the compositor fade-in
            m_InCompositorLoad += 1;
            return LoadAllImagesCoroutineImpl();
        }

        IEnumerator<Null> LoadAllImagesCoroutineImpl()
        {
            // This pretty much recreates the Update() loading loop, except it
            // uses allowMainThread=true, and has a bit of logic for progress updates.
            try
            {
                var toLoad = m_Images.Where(im => !im.RequestLoad(allowMainThread: true)).ToList();
                while (true)
                {
                    int finished = 0;
                    int pumped = 0;
                    foreach (var image in toLoad)
                    {
                        m_TexturesCreatedThisFrame = 0;
                        if (image.RequestLoad())
                        {
                            finished += 1;
                        }
                        else
                        {
                            pumped += 1;
                        }
                        // For sanity, limit the number of outstanding requests we have
                        if (pumped >= IMAGE_LOAD_PER_FRAME_COMPOSITOR)
                        {
                            break;
                        }
                    }

                    if (finished == toLoad.Count)
                    {
                        break;
                    }
                    else
                    {
                        OverlayManager.m_Instance.UpdateProgress((float)(finished + 1) / toLoad.Count);
                        yield return null;
                    }
                }
            }
            finally
            {
                m_InCompositorLoad -= 1;
            }
        }

        public void UnloadAllImages()
        {
            for (int i = 0; i < m_Images.Count; ++i)
            {
                m_Images[i].Unload();
            }
            Resources.UnloadUnusedAssets();

            // CatalogChanged is used here to tell the single client (the ReferencePanel) to refresh
            // its icons.  This may be an overload of CatalogChanged, but because it only has one
            // client right now, it's reasonable.
            if (CatalogChanged != null)
            {
                CatalogChanged();
            }
        }

        public bool AnyImageValid()
        {
            for (int i = 0; i < m_Images.Count; ++i)
            {
                if (m_Images[i].Valid)
                {
                    return true;
                }
            }
            return false;
        }

        protected void OnChanged(object source, FileSystemEventArgs e)
        {
            m_DirNeedsProcessing = true;

            // If a file was changed, store the name so we can refresh it.
            if (e.ChangeType == WatcherChangeTypes.Changed)
            {
                m_ChangedFile = e.FullPath;
            }
            else
            {
                m_ChangedFile = null;
            }
        }

        /// Returns a handle to the specified catalog entry, or null if the index is invalid.
        public ReferenceImage IndexToImage(int index)
        {
            if (0 <= index && index < m_Images.Count)
            {
                return m_Images[index];
            }
            else
            {
                return null;
            }
        }

        public int FilenameToIndex(string filename)
        {
            for (var i = 0; i < m_Images.Count; i++)
            {
                var img = m_Images[i];
                if (img.FileName == filename)
                {
                    return i;
                }
            }
            return -1;
        }

        /// Returns an index to a catalog entry, or -1 if the handle is invalid.
        /// The inverse of IndexToHandle.
        /// Indices are not durable, so use the index immediately and do not keep it around.
        public int ImageToIndex(ReferenceImage image)
        {
            for (int i = 0; i < m_Images.Count; ++i)
            {
                if (m_Images[i] == image)
                {
                    return i;
                }
            }
            return -1;
        }

        public int ItemCount
        {
            get { return m_Images.Count; }
        }

        // TODO: Look into making this append image requests instead of replacing
        // them.
        public void RequestLoadImage(ReferenceImage referenceImage)
        {
            Debug.Assert(referenceImage != null);
            int index = ImageToIndex(referenceImage);
            RequestLoadImages(index, index + 1);
        }

        /// Requests that the specified range be loaded.
        /// Range is half-open on the right.
        public void RequestLoadImages(int iMin, int iMax)
        {
            iMin = Mathf.Max(0, iMin);
            iMax = Mathf.Min(m_Images.Count, iMax);

            var newRequests = m_RequestedLoads
                .Concat(Enumerable.Range(iMin, iMax - iMin))
                .Distinct()
                .OrderBy(i => m_Images[i].Running ? 0 : 1)
                .ThenBy(i => (iMin <= i && i < iMax) ? 0 : 1);
            m_RequestedLoads = new Stack<int>(newRequests.Reverse());
            Resources.UnloadUnusedAssets();
        }

        /// Returns a Texture2D that may be not be full-resolution.
        /// Ownership does not transfer, so do not mutate or destroy the texture.
        /// The Texture data may disappear.
        /// The Texture2D will usually be square, but the aspect ratio may not be.
        public Texture2D GetImageIcon(int index, out float aspect)
        {
            if (0 <= index && index < m_Images.Count)
            {
                aspect = m_Images[index].ImageAspect;
                return m_Images[index].Icon;
            }
            else
            {
                aspect = 1;
                return null;
            }
        }

        protected virtual void ProcessReferenceDirectory(bool userOverlay = true)
        {
            _ProcessReferenceDirectory_Impl(m_CurrentImagesDirectory, userOverlay);
        }

        // Update m_Images with latest contents of reference directory.
        // Preserves items if they're still in the directory.
        protected void _ProcessReferenceDirectory_Impl(string imageDir, bool userOverlay = true)
        {
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                ProcessSafReferenceDirectory(imageDir, userOverlay);
                return;
            }

            m_DirNeedsProcessing = false;
            var oldImagesByPath = m_Images.ToDictionary(image => image.CatalogIdentity);

            // If we changed a file, pretend like we don't have it.
            if (m_ChangedFile != null)
            {
                if (oldImagesByPath.ContainsKey(m_ChangedFile))
                {
                    oldImagesByPath.Remove(m_ChangedFile);
                }
                m_ChangedFile = null;
            }
            m_Images.Clear();

            // Changed file may be deleted from the directory so indices are invalidated.
            m_RequestedLoads.Clear();

            //look for .jpg or .png files
            try
            {
                // GetFiles returns full paths, surprisingly enough.
                foreach (var filePath in Directory.GetFiles(imageDir))
                {
                    string ext = Path.GetExtension(filePath).ToLower();
                    if (!ValidExtension(ext)) { continue; }
                    try
                    {
                        m_Images.Add(oldImagesByPath[filePath]);
                        oldImagesByPath.Remove(filePath);
                    }
                    catch (KeyNotFoundException)
                    {
                        m_Images.Add(new ReferenceImage(filePath));
                    }
                }
            }
            catch (DirectoryNotFoundException) { }

            if (oldImagesByPath.Count > 0)
            {
                foreach (var entry in oldImagesByPath)
                {
                    entry.Value.Unload();
                }
                Resources.UnloadUnusedAssets();
            }

            if (m_RunningImageCacheCoroutine)
            {
                m_ResetImageEnumeration = true;
            }
            else
            {
                m_RunningImageCacheCoroutine = true;
                if (userOverlay)
                {
                    StartCoroutine(
                        OverlayManager.m_Instance.RunInCompositor(
                            OverlayType.LoadImages,
                            LoadAvailableImageCaches(),
                            fadeDuration: 0.25f));
                }
                else
                {
                    StartCoroutine(LoadAvailableImageCaches());
                }
            }

            if (CatalogChanged != null)
            {
                CatalogChanged();
            }
        }

        private void ProcessSafReferenceDirectory(string imageDir, bool userOverlay)
        {
            if (m_SafQueryInProgress)
            {
                m_DirNeedsProcessing = true;
                return;
            }
            m_DirNeedsProcessing = false;
            StartCoroutine(QuerySafReferenceDirectory(imageDir, userOverlay));
        }

        private IEnumerator<object> QuerySafReferenceDirectory(
            string imageDir, bool userOverlay)
        {
            m_SafQueryInProgress = true;
            string queryRootIdentity = UserStorage.Backend.RootIdentity;
            string relativeDirectory;
            if (!TryGetRelativeDirectory(HomeDirectory, imageDir, out relativeDirectory))
            {
                Debug.LogError($"SAF_CATALOG Image directory is outside its storage area: {imageDir}");
                m_SafQueryInProgress = false;
                yield break;
            }

            IUserStorageBackend backend = UserStorage.Backend;
            var query = new Future<StorageDirectoryResult>(
                () => backend.List(
                    StorageAreaKind, relativeDirectory, CancellationToken.None),
                cleanupFunction: null,
                longRunning: true);
            StorageDirectoryResult listing = null;
            while (true)
            {
                bool finished;
                try
                {
                    finished = query.TryGetResult(out listing);
                }
                catch (FutureFailed e)
                {
                    Debug.LogWarning(
                        $"SAF_CATALOG Image query failed; retaining the previous catalog: " +
                        $"{e.InnerException?.Message ?? e.Message}");
                    m_SafQueryInProgress = false;
                    yield break;
                }
                if (finished)
                {
                    break;
                }
                yield return null;
            }
            if (!listing.Success)
            {
                Debug.LogWarning(
                    $"SAF_CATALOG Image query failed; retaining the previous catalog: " +
                    $"{listing.Code} {listing.Error}");
                m_SafQueryInProgress = false;
                yield break;
            }
            if (!string.Equals(
                    queryRootIdentity,
                    backend.RootIdentity,
                    StringComparison.Ordinal))
            {
                m_SafQueryInProgress = false;
                m_DirNeedsProcessing = true;
                yield break;
            }

            var oldImages = m_Images.ToDictionary(image => image.CatalogIdentity);
            var nextImages = new List<ReferenceImage>();
            foreach (StorageDocument document in listing.Documents)
            {
                if (document.IsDirectory ||
                    !ValidExtension(Path.GetExtension(document.DisplayName).ToLowerInvariant()))
                {
                    continue;
                }

                string catalogIdentity =
                    $"{document.DocumentId.Value}|{document.LastModified:o}|{document.Size}";
                if (oldImages.TryGetValue(catalogIdentity, out ReferenceImage existing))
                {
                    nextImages.Add(existing);
                    oldImages.Remove(catalogIdentity);
                    continue;
                }

                StorageDocumentId documentId = document.DocumentId;
                string displayPath = backend.GetMaterializationPath(documentId);
                nextImages.Add(new ReferenceImage(
                    displayPath,
                    catalogIdentity,
                    () => backend.OpenRead(
                        documentId, requireSeekable: false, CancellationToken.None),
                    () => backend.Materialize(
                        documentId, MaterializationScope.File, CancellationToken.None),
                    document.Size));
            }

            foreach (ReferenceImage removed in oldImages.Values)
            {
                removed.Unload();
            }
            if (oldImages.Count > 0)
            {
                Resources.UnloadUnusedAssets();
            }

            m_Images = nextImages;
            m_RequestedLoads.Clear();
            m_SafQueryInProgress = false;
            StartImageCacheLoading(userOverlay);
            CatalogChanged?.Invoke();
        }

        private void StartImageCacheLoading(bool userOverlay)
        {
            if (m_RunningImageCacheCoroutine)
            {
                m_ResetImageEnumeration = true;
                return;
            }

            m_RunningImageCacheCoroutine = true;
            if (userOverlay)
            {
                StartCoroutine(
                    OverlayManager.m_Instance.RunInCompositor(
                        OverlayType.LoadImages,
                        LoadAvailableImageCaches(),
                        fadeDuration: 0.25f));
            }
            else
            {
                StartCoroutine(LoadAvailableImageCaches());
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

        protected virtual bool ValidExtension(string ext)
        {
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".svg";
        }

        public ReferenceImage RelativePathToImage(string relativePath)
        {
            // Protect against path traversal below HomeDirectory
            string fullPath = Path.GetFullPath(Path.Combine(HomeDirectory, relativePath));
            if (!fullPath.StartsWith(HomeDirectory, StringComparison.OrdinalIgnoreCase)) return null;

            // TODO change to a dictionary to avoid O(n) lookup
            var refImage = m_Images.FirstOrDefault(x => x.FileFullPath == fullPath);
            if (refImage == null)
            {
                refImage = new ReferenceImage(fullPath);
                m_Images.Add(refImage);
            }
            return refImage;
        }

        // Pass a file name with no path components. Matching is purely based on name.
        // Returns null on error.

        public ReferenceImage FileNameToImage(string name)
        {
            // This function used to be vague about its arguments.
            // Catch anyone who is still doing the wrong thing.
            if (name != Path.GetFileName(name))
            {
                Debug.LogErrorFormat("Got image name with path components: {0}", name);
                name = Path.GetFileName(name);
            }

            // TODO: do something better than O(n)?
            for (int i = 0; i < m_Images.Count; ++i)
            {
                if (m_Images[i].FileName == name)
                {
                    return m_Images[i];
                }
            }
            return null;
        }
    }
} // namespace TiltBrush
