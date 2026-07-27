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
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Direct access to the user-selected Open Brush document tree.
    public sealed class SafUserStorageBackend : IUserStorageBackend
    {
        private sealed class DocumentLocation
        {
            public StorageArea Area;
            public string RelativePath;
            public StorageDocument Document;
        }

        private readonly object m_LocationGate = new object();
        private readonly Dictionary<StorageDocumentId, DocumentLocation> m_Locations =
            new Dictionary<StorageDocumentId, DocumentLocation>();
        private string m_MappedRootId;

        public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
        public bool IsReady => AndroidSafStorage.HasOpenBrushFolder();
        public string RootIdentity => AndroidSafStorage.GetSelectedRootIdentity();

        public StorageDirectoryResult List(
            StorageArea area, string relativeDirectory, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.Cancelled, "Directory listing was cancelled.");
            }
            if (!IsReady)
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.NotReady, "Open Brush shared folder is unavailable.");
            }

            StorageDirectoryResult result = AndroidSafStorage.QueryDirectory(
                CombinePath(GetAreaPath(area), relativeDirectory));
            if (cancellationToken.IsCancellationRequested)
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.Cancelled, "Directory listing was cancelled.");
            }
            if (result.Success)
            {
                RecordLocations(area, relativeDirectory, result.Documents);
            }
            return result;
        }

        public Stream OpenRead(
            StorageDocumentId documentId,
            bool requireSeekable,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!documentId.IsValid)
            {
                throw new ArgumentException("Storage document identity is empty.", nameof(documentId));
            }
            if (!AndroidSafStorage.TryOpenSeekableReadStream(
                    documentId, out FileStream stream, out string error))
            {
                throw new IOException(error);
            }
            if (requireSeekable && !stream.CanSeek)
            {
                stream.Dispose();
                throw new IOException($"SAF document is not seekable: {documentId}");
            }
            return stream;
        }

        public IStorageWriteTransaction BeginWrite(
            StorageArea area,
            string relativePath,
            string mimeType,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsReady)
            {
                throw new IOException("Open Brush shared folder is unavailable.");
            }
            return new SafFileWriteTransaction(
                area, relativePath, mimeType, cancellationToken);
        }

        public StorageMutationResult Rename(
            StorageDocumentId documentId,
            string newDisplayName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsReady)
            {
                return new StorageMutationResult(
                    StorageResultCode.NotReady,
                    documentId,
                    "Open Brush shared folder is unavailable.");
            }
            DocumentLocation location = GetLocation(documentId);
            string rootId = RootIdentity;
            string oldKey = location == null
                ? $"{rootId}\nunknown\n{documentId}"
                : SafDestinationLocks.GetDestinationKey(
                    rootId, location.Area, location.RelativePath);
            string newRelativePath = location == null
                ? newDisplayName
                : CombineLogicalPath(
                    GetLogicalDirectory(location.RelativePath), newDisplayName);
            string newKey = location == null
                ? $"{rootId}\nunknown-name\n{newDisplayName}"
                : SafDestinationLocks.GetDestinationKey(
                    rootId, location.Area, newRelativePath);
            using (SafDestinationLocks.AcquireMany(
                new[] { oldKey, newKey }, cancellationToken))
            {
                StorageMutationResult result = RenameWithoutLock(documentId, newDisplayName);
                if (result.Success && location != null)
                {
                    lock (m_LocationGate)
                    {
                        m_Locations.Remove(documentId);
                        location.RelativePath = newRelativePath;
                        m_Locations[result.DocumentId] = location;
                    }
                }
                return result;
            }
        }

        public StorageMutationResult Delete(
            StorageDocumentId documentId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsReady)
            {
                return new StorageMutationResult(
                    StorageResultCode.NotReady,
                    documentId,
                    "Open Brush shared folder is unavailable.");
            }
            DocumentLocation location = GetLocation(documentId);
            string rootId = RootIdentity;
            string key = location == null
                ? $"{rootId}\nunknown\n{documentId}"
                : SafDestinationLocks.GetDestinationKey(
                    rootId, location.Area, location.RelativePath);
            using (SafDestinationLocks.Acquire(key, cancellationToken))
            {
                StorageMutationResult result = DeleteWithoutLock(documentId);
                if (result.Success || result.Code == StorageResultCode.NotFound)
                {
                    lock (m_LocationGate)
                    {
                        m_Locations.Remove(documentId);
                    }
                }
                return result;
            }
        }

        public string Materialize(
            StorageDocumentId documentId,
            MaterializationScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DocumentLocation location = GetLocation(documentId);
            if (location == null)
            {
                throw new IOException($"Unknown SAF document identity: {documentId}");
            }

            string path = MaterializeFile(location, cancellationToken);
            if (scope == MaterializationScope.DependencyTree &&
                location.Area == StorageArea.MediaLibraryModels)
            {
                MaterializeModelDependencies(location, path, cancellationToken);
            }
            EvictMaterializationCache(path);
            return path;
        }

        public string GetMaterializationPath(StorageDocumentId documentId)
        {
            DocumentLocation location = GetLocation(documentId);
            if (location == null)
            {
                throw new IOException($"Unknown SAF document identity: {documentId}");
            }
            return GetMaterializationPath(location);
        }

        internal static string GetAreaPath(StorageArea area)
        {
            switch (area)
            {
                case StorageArea.Sketches: return "Sketches";
                case StorageArea.SavedStrokes: return "Saved Strokes";
                case StorageArea.MediaLibraryImages: return "Media Library/Images";
                case StorageArea.MediaLibraryBackgroundImages:
                    return "Media Library/BackgroundImages";
                case StorageArea.MediaLibraryModels: return "Media Library/Models";
                case StorageArea.MediaLibraryVideos: return "Media Library/Videos";
                case StorageArea.Snapshots: return "Snapshots";
                case StorageArea.Videos: return "Videos";
                case StorageArea.VrVideos: return "VRVideos";
                case StorageArea.Exports: return "Exports";
                default: throw new ArgumentOutOfRangeException(nameof(area), area, null);
            }
        }

        internal StorageMutationResult RenameWithoutLock(
            StorageDocumentId documentId, string newDisplayName)
        {
            return AndroidSafStorage.RenameDocument(documentId, newDisplayName);
        }

        internal StorageMutationResult DeleteWithoutLock(StorageDocumentId documentId)
        {
            return AndroidSafStorage.DeleteDocument(documentId);
        }

        private void RecordLocations(
            StorageArea area,
            string relativeDirectory,
            IReadOnlyList<StorageDocument> documents)
        {
            string rootId = RootIdentity;
            lock (m_LocationGate)
            {
                if (m_MappedRootId != rootId)
                {
                    m_Locations.Clear();
                    m_MappedRootId = rootId;
                }
                foreach (StorageDocument document in documents)
                {
                    m_Locations[document.DocumentId] = new DocumentLocation
                    {
                        Area = area,
                        RelativePath = CombineLogicalPath(
                            relativeDirectory, document.DisplayName),
                        Document = document,
                    };
                }
            }
        }

        private DocumentLocation GetLocation(StorageDocumentId documentId)
        {
            lock (m_LocationGate)
            {
                m_Locations.TryGetValue(documentId, out DocumentLocation location);
                return location;
            }
        }

        private static string GetLogicalDirectory(string relativePath)
        {
            int separator = relativePath?.LastIndexOf('/') ?? -1;
            return separator < 0 ? "" : relativePath.Substring(0, separator);
        }

        private static string CombineLogicalPath(string directory, string name)
        {
            return string.IsNullOrEmpty(directory)
                ? name
                : $"{directory.TrimEnd('/', '\\')}/{name}";
        }

        private string MaterializeFile(
            DocumentLocation location, CancellationToken cancellationToken)
        {
            if (location.Document.IsDirectory)
            {
                MaterializeDirectory(location.Area, location.RelativePath, cancellationToken);
                return GetMaterializationPath(location);
            }

            string destination = GetMaterializationPath(location);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary = $"{destination}.obtmp-{Guid.NewGuid():N}";
            try
            {
                using (Stream input = OpenRead(
                    location.Document.DocumentId, requireSeekable: false, cancellationToken))
                using (var output = new FileStream(
                    temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(flushToDisk: true);
                }
                if (File.Exists(destination))
                {
                    File.Replace(temporary, destination, null);
                }
                else
                {
                    File.Move(temporary, destination);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            if (location.Document.LastModified.HasValue)
            {
                File.SetLastWriteTime(destination, location.Document.LastModified.Value);
            }
            File.SetLastAccessTimeUtc(destination, DateTime.UtcNow);
            return destination;
        }

        private void MaterializeDirectory(
            StorageArea area, string relativeDirectory, CancellationToken cancellationToken)
        {
            DocumentLocation directoryLocation = FindLocationByPath(area, relativeDirectory);
            if (directoryLocation != null)
            {
                Directory.CreateDirectory(GetMaterializationPath(directoryLocation));
            }
            StorageDirectoryResult listing = List(
                area, relativeDirectory, cancellationToken);
            if (!listing.Success)
            {
                throw new IOException(listing.Error);
            }
            foreach (StorageDocument document in listing.Documents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DocumentLocation child = GetLocation(document.DocumentId);
                if (document.IsDirectory)
                {
                    MaterializeDirectory(area, child.RelativePath, cancellationToken);
                }
                else
                {
                    MaterializeFile(child, cancellationToken);
                }
            }
        }

        private DocumentLocation FindLocationByPath(StorageArea area, string relativePath)
        {
            lock (m_LocationGate)
            {
                return m_Locations.Values.FirstOrDefault(location =>
                    location.Area == area &&
                    string.Equals(
                        location.RelativePath,
                        relativePath,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private void MaterializeModelDependencies(
            DocumentLocation model,
            string localModelPath,
            CancellationToken cancellationToken)
        {
            if (model.Document.IsDirectory)
            {
                return;
            }

            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string extension = Path.GetExtension(localModelPath).ToLowerInvariant();
            try
            {
                if (extension == ".gltf" || extension == ".gltf2")
                {
                    JToken json = JToken.Parse(File.ReadAllText(localModelPath));
                    foreach (JToken uri in json.SelectTokens("$..uri"))
                    {
                        AddLocalDependency(dependencies, uri.ToString());
                    }
                }
                else if (extension == ".obj")
                {
                    foreach (string line in File.ReadLines(localModelPath))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                        {
                            AddLocalDependency(dependencies, trimmed.Substring(7).Trim());
                        }
                    }
                }
                else if (extension == ".usda")
                {
                    foreach (string line in File.ReadLines(localModelPath))
                    {
                        int start = line.IndexOf('@');
                        int end = start < 0 ? -1 : line.IndexOf('@', start + 1);
                        if (start >= 0 && end > start)
                        {
                            AddLocalDependency(
                                dependencies, line.Substring(start + 1, end - start - 1));
                        }
                    }
                }
            }
            catch (Exception e) when (
                e is IOException ||
                e is Newtonsoft.Json.JsonException)
            {
                Debug.LogWarning(
                    $"SAF_STORAGE Could not inspect model dependencies for " +
                    $"{model.RelativePath}: {e.Message}");
            }

            string modelDirectory = GetLogicalDirectory(model.RelativePath);
            foreach (string dependency in dependencies.ToArray())
            {
                DocumentLocation dependencyLocation = FindByRelativePath(
                    model.Area,
                    CombineLogicalPath(modelDirectory, dependency),
                    cancellationToken);
                if (dependencyLocation == null || dependencyLocation.Document.IsDirectory)
                {
                    continue;
                }
                string dependencyPath = MaterializeFile(
                    dependencyLocation, cancellationToken);
                if (Path.GetExtension(dependencyPath).Equals(
                        ".mtl", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (string line in File.ReadLines(dependencyPath))
                    {
                        string[] parts = line.Trim().Split(
                            new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 &&
                            (parts[0].StartsWith("map_", StringComparison.OrdinalIgnoreCase) ||
                             parts[0].Equals("bump", StringComparison.OrdinalIgnoreCase)))
                        {
                            AddLocalDependency(dependencies, parts[parts.Length - 1]);
                        }
                    }
                }
            }

            foreach (string dependency in dependencies)
            {
                DocumentLocation dependencyLocation = FindByRelativePath(
                    model.Area,
                    CombineLogicalPath(modelDirectory, dependency),
                    cancellationToken);
                if (dependencyLocation != null && !dependencyLocation.Document.IsDirectory)
                {
                    MaterializeFile(dependencyLocation, cancellationToken);
                }
            }
        }

        private DocumentLocation FindByRelativePath(
            StorageArea area,
            string relativePath,
            CancellationToken cancellationToken)
        {
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            string directory = GetLogicalDirectory(normalized);
            string name = Path.GetFileName(normalized);
            StorageDirectoryResult listing = List(area, directory, cancellationToken);
            if (!listing.Success)
            {
                return null;
            }
            StorageDocument document = listing.Documents.FirstOrDefault(candidate =>
                string.Equals(candidate.DisplayName, name, StringComparison.OrdinalIgnoreCase));
            return document == null ? null : GetLocation(document.DocumentId);
        }

        private static void AddLocalDependency(HashSet<string> dependencies, string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) ||
                uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                return;
            }
            string normalized = Uri.UnescapeDataString(uri).Replace('\\', '/');
            if (normalized.Split('/').Any(segment =>
                    string.IsNullOrEmpty(segment) || segment == "." || segment == ".."))
            {
                return;
            }
            dependencies.Add(normalized);
        }

        private string GetMaterializationPath(DocumentLocation location)
        {
            string root = GetMaterializationAreaRoot(location.Area);
            string fullRoot = Path.GetFullPath(root);
            string destination = Path.GetFullPath(
                Path.Combine(fullRoot, location.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = fullRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Materialization path escapes its cache area.");
            }
            return destination;
        }

        private static string GetMaterializationAreaRoot(StorageArea area)
        {
            switch (area)
            {
                case StorageArea.MediaLibraryImages: return App.ReferenceImagePath();
                case StorageArea.MediaLibraryBackgroundImages:
                    return App.BackgroundImagesLibraryPath();
                case StorageArea.MediaLibraryModels: return App.ModelLibraryPath();
                case StorageArea.MediaLibraryVideos: return App.VideoLibraryPath();
                default:
                    return Path.Combine(
                        Application.persistentDataPath,
                        "OpenBrushSafMaterialized",
                        SafTransactionJournal.GetRootNamespaceId(
                            UserStorage.Backend.RootIdentity),
                        area.ToString());
            }
        }

        private static void EvictMaterializationCache(string protectedPath)
        {
            const long maxBytes = 512L * 1024L * 1024L;
            string cacheRoot = Path.Combine(
                Application.persistentDataPath,
                "OpenBrushSafMaterialized",
                SafTransactionJournal.GetRootNamespaceId(
                    UserStorage.Backend.RootIdentity));
            if (!Directory.Exists(cacheRoot))
            {
                return;
            }
            FileInfo[] files = new DirectoryInfo(cacheRoot)
                .GetFiles("*", SearchOption.AllDirectories)
                .Where(file => !file.Name.Contains(".obtmp-"))
                .OrderBy(file => file.LastAccessTimeUtc)
                .ToArray();
            long total = files.Sum(file => file.Length);
            foreach (FileInfo file in files)
            {
                if (total <= maxBytes)
                {
                    break;
                }
                if (string.Equals(
                        file.FullName, protectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                long length = file.Length;
                try
                {
                    file.Delete();
                    total -= length;
                }
                catch (IOException)
                {
                    // An importer may currently hold this cache entry.
                }
            }
        }

        private static string CombinePath(string root, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return root;
            }
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArgumentException("Storage path escapes its logical area.");
                }
            }
            return $"{root}/{normalized}";
        }
    }
}
