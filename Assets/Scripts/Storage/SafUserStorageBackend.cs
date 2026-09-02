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
            string rootId = RootIdentity;

            StorageDirectoryResult result = AndroidSafStorage.QueryDirectory(
                CombinePath(GetAreaPath(area), relativeDirectory));
            if (cancellationToken.IsCancellationRequested)
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.Cancelled, "Directory listing was cancelled.");
            }
            if (!string.Equals(rootId, RootIdentity, StringComparison.Ordinal))
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.Cancelled,
                    "The selected Open Brush folder changed during the directory query.");
            }
            if (result.Success)
            {
                RecordLocations(rootId, area, relativeDirectory, result.Documents);
            }
            return result;
        }

        public StorageTreeResult EnumerateTree(
            StorageArea area,
            string relativeDirectory,
            StorageTreeQuery query,
            CancellationToken cancellationToken)
        {
            return StorageTreeEnumerator.Enumerate(
                this, area, relativeDirectory, query, cancellationToken);
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
                throw new IOException("The selected SAF document is not seekable.");
            }
            return stream;
        }

        public IStorageWriteTransaction BeginWrite(
            StorageArea area,
            string relativePath,
            string mimeType,
            CancellationToken cancellationToken,
            StorageDocumentId targetDocumentId = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsReady)
            {
                throw new IOException("Open Brush shared folder is unavailable.");
            }
            return new SafFileWriteTransaction(
                area,
                relativePath,
                mimeType,
                targetDocumentId,
                cancellationToken);
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
                if (location != null)
                {
                    string directory = GetLogicalDirectory(location.RelativePath);
                    StorageDirectoryResult listing = List(
                        location.Area, directory, cancellationToken);
                    if (!listing.Success)
                    {
                        return new StorageMutationResult(
                            listing.Code, documentId, listing.Error);
                    }
                    StorageDocument conflict = listing.Documents.FirstOrDefault(document =>
                        !document.DocumentId.Equals(documentId) &&
                        string.Equals(
                            document.DisplayName,
                            newDisplayName,
                            StringComparison.OrdinalIgnoreCase));
                    if (conflict != null)
                    {
                        return new StorageMutationResult(
                            StorageResultCode.Failed,
                            documentId,
                            "A document with that name already exists.");
                    }
                }
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
                throw new IOException(
                    "The selected SAF document is not part of the active catalog.");
            }

            StorageDocumentId materializationGroupId = location.Document.DocumentId;
            string path = MaterializeFile(
                location, materializationGroupId, cancellationToken);
            if (scope == MaterializationScope.DependencyTree &&
                location.Area == StorageArea.MediaLibraryModels)
            {
                MaterializeModelDependencies(
                    location,
                    path,
                    materializationGroupId,
                    cancellationToken);
            }
            EvictMaterializationCache(
                GetMaterializationGroupRoot(location.Area, materializationGroupId));
            return path;
        }

        public string GetMaterializationPath(StorageDocumentId documentId)
        {
            DocumentLocation location = GetLocation(documentId);
            if (location == null)
            {
                throw new IOException(
                    "The selected SAF document is not part of the active catalog.");
            }
            return GetMaterializationPath(
                location, location.Document.DocumentId);
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
                case StorageArea.Scripts: return "Scripts";
                case StorageArea.Plugins: return "Plugins";
                case StorageArea.Fonts: return "Fonts";
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
            DocumentLocation location = GetLocation(documentId);
            return AndroidSafStorage.DeleteDocument(
                documentId, location?.Document.ParentDocumentId ?? default);
        }

        private void RecordLocations(
            string rootId,
            StorageArea area,
            string relativeDirectory,
            IReadOnlyList<StorageDocument> documents)
        {
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
            string rootId = RootIdentity;
            lock (m_LocationGate)
            {
                ResetLocationsForRootLocked(rootId);
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
            DocumentLocation location,
            StorageDocumentId materializationGroupId,
            CancellationToken cancellationToken)
        {
            if (location.Document.IsDirectory)
            {
                MaterializeDirectory(
                    location.Area,
                    location.RelativePath,
                    materializationGroupId,
                    cancellationToken);
                return GetMaterializationPath(location, materializationGroupId);
            }

            string destination = GetMaterializationPath(
                location, materializationGroupId);
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
            StorageArea area,
            string relativeDirectory,
            StorageDocumentId materializationGroupId,
            CancellationToken cancellationToken)
        {
            DocumentLocation directoryLocation = FindLocationByPath(area, relativeDirectory);
            if (directoryLocation != null)
            {
                Directory.CreateDirectory(GetMaterializationPath(
                    directoryLocation, materializationGroupId));
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
                    MaterializeDirectory(
                        area,
                        child.RelativePath,
                        materializationGroupId,
                        cancellationToken);
                }
                else
                {
                    MaterializeFile(
                        child, materializationGroupId, cancellationToken);
                }
            }
        }

        private DocumentLocation FindLocationByPath(StorageArea area, string relativePath)
        {
            string rootId = RootIdentity;
            lock (m_LocationGate)
            {
                ResetLocationsForRootLocked(rootId);
                return m_Locations.Values.FirstOrDefault(location =>
                    location.Area == area &&
                    string.Equals(
                        location.RelativePath,
                        relativePath,
                        StringComparison.OrdinalIgnoreCase));
            }
        }

        private void ResetLocationsForRootLocked(string rootId)
        {
            if (m_MappedRootId == rootId)
            {
                return;
            }
            m_Locations.Clear();
            m_MappedRootId = rootId;
        }

        private void MaterializeModelDependencies(
            DocumentLocation model,
            string localModelPath,
            StorageDocumentId materializationGroupId,
            CancellationToken cancellationToken)
        {
            if (model.Document.IsDirectory)
            {
                return;
            }

            var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string extension = Path.GetExtension(localModelPath).ToLowerInvariant();
            string modelDirectory = GetLogicalDirectory(model.RelativePath);
            try
            {
                if (extension == ".gltf" || extension == ".gltf2")
                {
                    JToken json = JToken.Parse(File.ReadAllText(localModelPath));
                    foreach (JToken uri in json.SelectTokens("$..uri"))
                    {
                        AddLocalDependency(
                            dependencies, modelDirectory, uri.ToString());
                    }
                }
                else if (extension == ".obj")
                {
                    foreach (string line in File.ReadLines(localModelPath))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                        {
                            AddLocalDependency(
                                dependencies,
                                modelDirectory,
                                trimmed.Substring(7).Trim());
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
                                dependencies,
                                modelDirectory,
                                line.Substring(start + 1, end - start - 1));
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

            foreach (string dependency in dependencies.ToArray())
            {
                DocumentLocation dependencyLocation = FindByRelativePath(
                    model.Area,
                    dependency,
                    cancellationToken);
                if (dependencyLocation == null || dependencyLocation.Document.IsDirectory)
                {
                    continue;
                }
                string dependencyPath = MaterializeFile(
                    dependencyLocation,
                    materializationGroupId,
                    cancellationToken);
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
                            AddLocalDependency(
                                dependencies,
                                GetLogicalDirectory(dependency),
                                parts[parts.Length - 1]);
                        }
                    }
                }
            }

            foreach (string dependency in dependencies)
            {
                DocumentLocation dependencyLocation = FindByRelativePath(
                    model.Area,
                    dependency,
                    cancellationToken);
                if (dependencyLocation != null && !dependencyLocation.Document.IsDirectory)
                {
                    MaterializeFile(
                        dependencyLocation,
                        materializationGroupId,
                        cancellationToken);
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

        private static void AddLocalDependency(
            HashSet<string> dependencies, string baseDirectory, string uri)
        {
            if (string.IsNullOrWhiteSpace(uri) ||
                uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                return;
            }
            string normalized = Uri.UnescapeDataString(uri).Replace('\\', '/');
            var segments = new List<string>();
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                segments.AddRange(baseDirectory.Replace('\\', '/').Split('/'));
            }
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == ".")
                {
                    continue;
                }
                if (segment == "..")
                {
                    if (segments.Count == 0)
                    {
                        return;
                    }
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                segments.Add(segment);
            }
            if (segments.Count > 0)
            {
                dependencies.Add(string.Join("/", segments));
            }
        }

        private string GetMaterializationPath(
            DocumentLocation location, StorageDocumentId materializationGroupId)
        {
            string root = GetMaterializationGroupRoot(
                location.Area, materializationGroupId);
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

        private static string GetMaterializationGroupRoot(
            StorageArea area, StorageDocumentId materializationGroupId)
        {
            if (!materializationGroupId.IsValid)
            {
                throw new IOException("Materialization group identity is empty.");
            }
            return Path.Combine(
                GetMaterializationAreaRoot(area),
                SafTransactionJournal.GetRootNamespaceId(
                    materializationGroupId.Value));
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
            string protectedRoot = Path.GetFullPath(protectedPath).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string protectedPrefix = protectedRoot + Path.DirectorySeparatorChar;
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
                        file.FullName, protectedRoot, StringComparison.OrdinalIgnoreCase) ||
                    file.FullName.StartsWith(
                        protectedPrefix, StringComparison.OrdinalIgnoreCase))
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
