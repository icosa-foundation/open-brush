// Copyright 2026 The Open Brush Authors
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
    public static class OpenBrushStorage
    {
        public static bool IsScopedStorageMode
        {
            get
            {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
                return Application.platform == RuntimePlatform.Android;
#else
                return false;
#endif
            }
        }

        private static string sm_PersistentDataPath;
        private static string sm_TemporaryCachePath;

        /// Unity application paths are main-thread only, and storage paths are wanted from worker
        /// threads - transaction recovery hit exactly that and failed with "can only be called
        /// from the main thread". The values are fixed for the process, so they are captured before
        /// the scene loads and read from the cache thereafter.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CaptureApplicationPaths()
        {
            sm_PersistentDataPath = Application.persistentDataPath;
            sm_TemporaryCachePath = Application.temporaryCachePath;
        }

        public static string PersistentDataPath
        {
            get
            {
                // A test or an editor path can reach this before the hook has run.
                return sm_PersistentDataPath ??= Application.persistentDataPath;
            }
        }

        public static string LocalUserPathRoot
        {
            get
            {
                return Path.Combine(OpenBrushStorage.PersistentDataPath, "OpenBrushWorkingCache");
            }
        }

        public static string LocalExportStagingPath
        {
            get
            {
                return Path.Combine(LocalStagingPath, "Exports");
            }
        }

        public static string LocalStagingPath =>
            Path.Combine(
                sm_TemporaryCachePath ??= Application.temporaryCachePath,
                "OpenBrushSafStaging");

        public static string LocalSnapshotStagingPath =>
            Path.Combine(LocalStagingPath, "Snapshots");

        public static string LocalVideoStagingPath =>
            Path.Combine(LocalStagingPath, "Videos");

        public static string LocalVrVideoStagingPath =>
            Path.Combine(LocalStagingPath, "VRVideos");

        public static string LocalSplatPoseStagingPath =>
            Path.Combine(LocalStagingPath, "SplatPoses");

        /// A logical anchor, not a directory: nothing is written here and nothing creates it.
        /// The media catalogs are written against local paths, so under scoped storage they are
        /// given this prefix and the SAF-relative directory is recovered by subtracting it again.
        ///
        /// Deliberately not scoped by root. It used to embed a hash of the root identity, left
        /// over from when media really was materialized per root. That made the anchor change the
        /// moment a folder was granted - before the grant the identity is empty, so the prefix
        /// hashed the empty string - and every directory captured beforehand then failed to match
        /// it, leaving each catalog reporting its own home as "outside its storage area". The
        /// root is fixed for the life of an installation and SafRootChangeGuard discards derived
        /// state if it ever is not, so there is nothing for the scoping to protect.
        public static string MediaLibraryAnchorPath
        {
            get
            {
                return Path.Combine(
                    OpenBrushStorage.PersistentDataPath,
                    "OpenBrushSafMediaLibrary",
                    "Media Library");
            }
        }

        public static string SharedExportDisplayPath
        {
            get { return "Open Brush/Exports"; }
        }

        internal sealed class MediaSource
        {
            private readonly IUserStorageBackend m_Backend;
            private readonly string m_Root;
            private readonly string m_Identity;
            public StorageDocument Document { get; }
            public string Identity => m_Identity;
            public bool HasVerifiableRevision =>
                Document.LastModified.HasValue || Document.Size.HasValue;

            public MediaSource(IUserStorageBackend backend, StorageArea area, string relativePath)
            {
                m_Backend = backend;
                m_Root = backend.RootIdentity;
                Document = ResolveMediaDocument(backend, area, relativePath);
                m_Identity = GetMediaRevisionIdentity(m_Root, Document);
                CheckRoot();
            }

            private void CheckRoot()
            {
                if (m_Root != m_Backend.RootIdentity) { throw new IOException("Selected media folder changed."); }
            }

            public Stream OpenRead()
            {
                CheckRoot();
                return m_Backend.OpenRead(Document.DocumentId, false, CancellationToken.None);
            }

        }

        internal static string GetMediaRevisionIdentity(
            string rootIdentity, StorageDocument document)
        {
            string identity =
                $"{rootIdentity}:{document.DocumentId.Value}|" +
                $"{document.LastModified:o}|{document.Size}";
            if (document.LastModified.HasValue || document.Size.HasValue)
            {
                return identity;
            }

            // DocumentsProvider permits both revision fields to be null. A durable document URI
            // identifies the document, not its current contents, so it cannot safely validate an
            // image cache by itself. The nonce deliberately prevents reuse across catalog queries.
            return $"{identity}|unverifiable:{Guid.NewGuid():N}";
        }

        internal static StorageDocument ResolveMediaDocument(
            IUserStorageBackend backend, StorageArea area, string relativePath)
        {
            string normalized = (relativePath ?? "").Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.Split('/').Any(part => part == "..") ||
                normalized.Contains(":"))
            {
                throw new ArgumentException($"Invalid media path: {relativePath}");
            }
            normalized = string.Join("/", normalized.Split('/').Where(part => part != "." && part.Length != 0));
            if (normalized.Length == 0) { throw new ArgumentException("A media filename is required."); }
            string root = backend.RootIdentity;
            StorageDirectoryResult listing = backend.List(area,
                Path.GetDirectoryName(normalized)?.Replace('\\', '/') ?? "", CancellationToken.None);
            if (!listing.Success) { throw new IOException($"Could not resolve {relativePath}: {listing.Error}"); }
            StorageDocument document = listing.Documents.SingleOrDefault(item => !item.IsDirectory &&
                item.DisplayName.Equals(Path.GetFileName(normalized), StringComparison.OrdinalIgnoreCase));
            if (root != backend.RootIdentity) { throw new IOException("Selected media folder changed."); }
            return document ?? throw new FileNotFoundException($"Media file not found: {relativePath}");
        }

        private static readonly Dictionary<string, HashSet<string>> sm_CaptureReservations =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        internal static string ReserveCaptureName(IUserStorageBackend backend, StorageArea area,
            string directory, string format, Func<string, bool> localExists)
        {
            lock (sm_CaptureReservations)
            {
                string key = $"{backend.RootIdentity}\n{area}\n{directory}";
                if (!sm_CaptureReservations.TryGetValue(key, out var names))
                {
                    names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    sm_CaptureReservations.Add(key, names);
                }
                if (backend.IsReady)
                {
                    StorageDirectoryResult listing = backend.List(area, directory, CancellationToken.None);
                    if (!listing.Success && listing.Code != StorageResultCode.NotFound)
                    {
                        throw new IOException($"Could not reserve capture name: {listing.Error}");
                    }
                    foreach (StorageDocument document in listing.Documents) { names.Add(document.DisplayName); }
                }
                for (int index = 0; ; ++index)
                {
                    string candidate = string.Format(format, index);
                    string stem = Path.GetFileNameWithoutExtension(candidate);
                    bool used = localExists(candidate);
                    foreach (string name in names)
                    {
                        if (Path.GetFileNameWithoutExtension(name).Equals(stem, StringComparison.OrdinalIgnoreCase) ||
                            name.StartsWith($"{stem}_", StringComparison.OrdinalIgnoreCase)) { used = true; break; }
                    }
                    if (used) { continue; }
                    names.Add(candidate);
                    return candidate;
                }
            }
        }

        public static bool TryGetSharedGeneratedFileRelativePath(
            string localPath, out string relativePath)
        {
            relativePath = null;

            if (string.IsNullOrEmpty(localPath))
            {
                return false;
            }

            if (TryGetRelativePath(App.SnapshotPath(), localPath, out string snapshotPath))
            {
                relativePath = Path.Combine("Snapshots", snapshotPath);
                return true;
            }

            if (TryGetRelativePath(App.VideosPath(), localPath, out string videoPath))
            {
                relativePath = Path.Combine("Videos", videoPath);
                return true;
            }

            if (TryGetRelativePath(App.VrVideosPath(), localPath, out string vrVideoPath))
            {
                relativePath = Path.Combine("VRVideos", vrVideoPath);
                return true;
            }

            return false;
        }

        public static bool TryGetSharedMediaLibraryRelativePath(
            string localPath, out string relativePath)
        {
            relativePath = null;

            if (string.IsNullOrEmpty(localPath))
            {
                return false;
            }

            if (TryGetRelativePath(App.MediaLibraryPath(), localPath, out string mediaPath))
            {
                relativePath = Path.Combine("Media Library", mediaPath);
                return true;
            }

            return false;
        }


        public static void WriteGeneratedFile(string path, Action<Stream> write)
        {
            if (!IsScopedStorageMode)
            {
                using (var output = new FileStream(path, FileMode.Create)) { write(output); }
                return;
            }
            var (area, relativePath) = GetGeneratedDestination(path);
            WriteSharedFile(UserStorage.Backend, area, relativePath, write);
        }

        internal static (StorageArea area, string relativePath) GetGeneratedDestination(string path)
        {
            if (!TryGetSharedGeneratedFileRelativePath(path, out string sharedPath) ||
                !TryResolveStorageDestination(sharedPath, out StorageArea area, out string relativePath))
            {
                throw new IOException($"Capture destination is outside shared storage: {path}");
            }
            return (area, relativePath);
        }

        public static string ReadGeneratedText(string path)
        {
            if (!IsScopedStorageMode) { return File.ReadAllText(path); }
            var (area, relativePath) = GetGeneratedDestination(path);
            var source = new MediaSource(UserStorage.Backend, area, relativePath);
            using var reader = new StreamReader(source.OpenRead());
            return reader.ReadToEnd();
        }

        internal static void DeleteSharedFiles(IUserStorageBackend backend, StorageArea area,
            string directory, ISet<string> filenames)
        {
            StorageDirectoryResult listing = backend.List(area, directory, CancellationToken.None);
            if (listing.Code == StorageResultCode.NotFound) { return; }
            if (!listing.Success) { throw new IOException(listing.Error); }
            foreach (StorageDocument file in listing.Documents)
            {
                if (file.IsDirectory || !filenames.Contains(file.DisplayName)) { continue; }
                StorageMutationResult result = backend.Delete(file.DocumentId, CancellationToken.None);
                if (!result.Success && result.Code != StorageResultCode.NotFound)
                {
                    throw new IOException(result.Error);
                }
            }
        }

        public static void WriteGeneratedBytes(string path, byte[] bytes)
        {
            if (!IsScopedStorageMode) { File.WriteAllBytes(path, bytes); return; }
            WriteGeneratedFile(path, output => output.Write(bytes, 0, bytes.Length));
        }

        internal static void WriteSharedFile(
            IUserStorageBackend backend, StorageArea area, string relativePath, Action<Stream> write)
        {
            using (IStorageWriteTransaction transaction = backend.BeginWrite(
                area, relativePath, StorageMimeTypes.ForPath(relativePath), CancellationToken.None))
            {
                using (Stream output = transaction.OpenWrite()) { write(output); }
                StorageMutationResult result = transaction.Commit();
                if (!result.Success)
                {
                    throw new IOException($"Could not save shared file '{relativePath}': {result.Error}");
                }
            }
        }

        public static void PublishUserRootFileToSharedStorageAsync(
            string localPath, string displayName, string label,
            Action<bool, string> onComplete)
        {
            if (!IsScopedStorageMode)
            {
                onComplete?.Invoke(true, null);
                return;
            }
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                onComplete?.Invoke(false, "SAF storage backend is unavailable.");
                return;
            }
            AndroidStorageManager.StartStorageOperation(
                label,
                () => SafStagedOutputPublisher.Publish(
                    UserStorage.Backend,
                    StorageArea.UserRoot,
                    displayName,
                    localPath,
                    transactionOwnsPayload: true,
                    CancellationToken.None),
                onComplete);
        }

        /// Resolves a local path to its shared destination and publishes it, or reports success
        /// when there is nothing to publish. The resolver decides which tree the path belongs to.
        private static void PublishSinglePathAsync(
            string localPath,
            string label,
            TryResolveSharedPath resolve,
            bool transactionOwnsPayload,
            Action<bool, string> onComplete)
        {
            if (!IsScopedStorageMode || !resolve(localPath, out string relativePath))
            {
                onComplete?.Invoke(true, null);
                return;
            }
            PublishPathToSharedStorageAsync(
                relativePath, localPath, label, transactionOwnsPayload, onComplete);
        }

        private delegate bool TryResolveSharedPath(string localPath, out string relativePath);

        public static void PublishMediaLibraryPathToSharedStorageAsync(
            string localPath, string label, Action<bool, string> onComplete)
        {
            // Media-library content stays where it is locally; the copy is additive.
            PublishSinglePathAsync(
                localPath, label, TryGetSharedMediaLibraryRelativePath,
                transactionOwnsPayload: false, onComplete);
        }

        internal static string GetUniqueImportPath(IUserStorageBackend backend, StorageArea area,
            string relativePath, Func<string, bool> localExists = null)
        {
            string directory = (Path.GetDirectoryName(relativePath) ?? "").Replace('\\', '/');
            StorageDirectoryResult listing = backend.List(area, directory, CancellationToken.None);
            if (!listing.Success && listing.Code != StorageResultCode.NotFound)
            {
                throw new IOException($"Could not check imported media destination: {listing.Error}");
            }
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (listing.Success)
            {
                foreach (StorageDocument document in listing.Documents) { names.Add(document.DisplayName); }
            }
            string filename = Path.GetFileName(relativePath);
            string candidate = filename;
            int version = 0;
            while (names.Contains(candidate) || (localExists?.Invoke(candidate) ?? false))
            {
                candidate = $"{Path.GetFileNameWithoutExtension(filename)} ({++version}){Path.GetExtension(filename)}";
            }
            return string.IsNullOrEmpty(directory) ? candidate : $"{directory}/{candidate}";
        }

        internal static SafPublicationResult PublishImportedMedia(
            IUserStorageBackend backend, StorageArea area, string relativePath, string localPath,
            bool prepareLocalImport, out string publishedLocalPath,
            bool preserveDestination = false, bool replaceDestination = false)
        {
            publishedLocalPath = null;
            if (preserveDestination && replaceDestination)
            {
                throw new ArgumentException(
                    "An imported-media publication cannot both reject and replace collisions.");
            }
            // Serialize API name selection and publication, including delayed picker continuations.
            using (SafDestinationLocks.Acquire($"api-import:{backend.RootIdentity}:{area}", CancellationToken.None))
            {
                string localDirectory = Path.GetDirectoryName(localPath);
                string destination = replaceDestination
                    ? relativePath
                    : GetUniqueImportPath(backend, area, relativePath,
                        candidate => !string.Equals(candidate, Path.GetFileName(localPath),
                            StringComparison.OrdinalIgnoreCase) &&
                            File.Exists(Path.Combine(localDirectory, candidate)));
                if (preserveDestination && !string.Equals(destination, relativePath, StringComparison.Ordinal))
                {
                    return new SafPublicationResult(StorageResultCode.Failed,
                        $"The reserved import destination already exists: {relativePath}. Staged content was preserved.");
                }
                SafPublicationResult result = replaceDestination
                    ? SafStagedOutputPublisher.PublishReplacing(
                        backend, area, destination, localPath,
                        transactionOwnsPayload: false, CancellationToken.None)
                    : SafStagedOutputPublisher.Publish(
                        backend, area, destination, localPath,
                        transactionOwnsPayload: false, CancellationToken.None);
                if (result.Success && prepareLocalImport)
                {
                    // The importing widget needs both the final logical name and its local bytes.
                    publishedLocalPath = Path.Combine(localDirectory, Path.GetFileName(destination));
                    if (!string.Equals(localPath, publishedLocalPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(localPath, publishedLocalPath);
                    }
                }
                return result;
            }
        }


        public static void PublishExportToSharedStorageAsync(
            string localExportDirectory, Action<bool, string> onComplete)
        {
            if (!IsScopedStorageMode)
            {
                onComplete?.Invoke(true, null);
                return;
            }
            if (!Directory.EnumerateFileSystemEntries(localExportDirectory).Any())
            {
                // GLB-only exports have no local payload to publish or retain.
                Directory.Delete(localExportDirectory);
                onComplete?.Invoke(true, null);
                return;
            }
            IUserStorageBackend backend = UserStorage.Backend;
            AndroidStorageManager.StartStorageOperation(
                $"export {Path.GetFileName(localExportDirectory)}",
                () => SafStagedOutputPublisher.PublishExport(
                    backend, localExportDirectory, CancellationToken.None), onComplete);
        }

        public static void PublishGaussianCaptureToSharedStorageAsync(
            string localCaptureDirectory, Action<bool, string> onComplete)
        {
            if (!IsScopedStorageMode)
            {
                onComplete?.Invoke(true, null);
                return;
            }
            string captureName = Path.GetFileName(localCaptureDirectory);
            if (UserStorage.Backend.Kind == StorageBackendKind.StorageAccessFramework)
            {
                IUserStorageBackend backend = UserStorage.Backend;
                AndroidStorageManager.StartStorageOperation(
                    $"Gaussian capture {captureName}",
                    () => SafStagedOutputPublisher.PublishUniqueDirectory(
                        backend, StorageArea.SplatPoses, localCaptureDirectory,
                        transactionOwnsPayload: true, CancellationToken.None),
                    onComplete);
                return;
            }
            PublishPathToSharedStorageAsync(
                Path.Combine("SplatPoses", captureName),
                localCaptureDirectory,
                $"Gaussian capture {captureName}",
                transactionOwnsPayload: true,
                onComplete);
        }

        private static void PublishPathToSharedStorageAsync(
            string relativePath,
            string localPath,
            string label,
            bool transactionOwnsPayload,
            Action<bool, string> onComplete)
        {
            if (UserStorage.Backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                onComplete?.Invoke(false, "SAF storage backend is unavailable.");
                return;
            }
            if (!TryResolveStorageDestination(
                    relativePath, out StorageArea area, out string areaRelativePath))
            {
                onComplete?.Invoke(
                    false, $"Unsupported shared-storage destination: {relativePath}");
                return;
            }
            AndroidStorageManager.StartStorageOperation(
                label,
                () => SafStagedOutputPublisher.Publish(
                    UserStorage.Backend,
                    area,
                    areaRelativePath,
                    localPath,
                    transactionOwnsPayload,
                    CancellationToken.None),
                onComplete);
        }


        internal static bool TryResolveStorageDestination(
            string sharedRelativePath,
            out StorageArea area,
            out string areaRelativePath)
        {
            string normalized = (sharedRelativePath ?? "").Replace('\\', '/').Trim('/');
            (string prefix, StorageArea area)[] mappings =
            {
                ("Media Library/BackgroundImages", StorageArea.MediaLibraryBackgroundImages),
                ("Media Library/Images", StorageArea.MediaLibraryImages),
                ("Media Library/Models", StorageArea.MediaLibraryModels),
                ("Media Library/Videos", StorageArea.MediaLibraryVideos),
                ("Media Library/Sound Clips", StorageArea.MediaLibrarySoundClips),
                ("Media Library/Quill", StorageArea.MediaLibraryQuill),
                ("Music", StorageArea.Music),
                ("Media Library/Saved Strokes", StorageArea.SavedStrokes),
                ("Sketches", StorageArea.Sketches),
                ("Snapshots", StorageArea.Snapshots),
                ("VRVideos", StorageArea.VrVideos),
                ("Videos", StorageArea.Videos),
                ("Exports", StorageArea.Exports),
                ("SplatPoses", StorageArea.SplatPoses),
            };
            foreach ((string prefix, StorageArea mappedArea) in mappings)
            {
                if (normalized == prefix)
                {
                    area = mappedArea;
                    areaRelativePath = "";
                    return true;
                }
                string prefixWithSeparator = $"{prefix}/";
                if (normalized.StartsWith(
                        prefixWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    area = mappedArea;
                    areaRelativePath = normalized.Substring(prefixWithSeparator.Length);
                    return true;
                }
            }
            area = default;
            areaRelativePath = null;
            return false;
        }

        private static bool TryGetRelativePath(string root, string path, out string relativePath)
        {
            relativePath = null;

            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(path);
            if (fullPath == fullRoot)
            {
                relativePath = "";
                return true;
            }

            string rootWithSeparator = fullRoot + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootWithSeparator))
            {
                return false;
            }

            relativePath = fullPath.Substring(rootWithSeparator.Length);
            return true;
        }

    }
}
