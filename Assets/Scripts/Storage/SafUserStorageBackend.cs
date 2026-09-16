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
            StorageArea area,
            string relativePath,
            bool requireSeekable,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = CombinePath(GetAreaPath(area), relativePath);
            if (!AndroidSafStorage.TryOpenSeekableReadStream(
                    path, out FileStream stream, out string error))
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

        public bool Exists(StorageArea area, string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }
            // Resolved through the parent listing rather than by opening a descriptor, so an
            // existence check does not cost a file-descriptor round trip through the provider.
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            int separator = normalized.LastIndexOf('/');
            string directory = separator < 0 ? string.Empty : normalized.Substring(0, separator);
            string name = separator < 0 ? normalized : normalized.Substring(separator + 1);
            StorageDirectoryResult listing = List(area, directory, CancellationToken.None);
            if (!listing.Success)
            {
                return false;
            }
            foreach (StorageDocument document in listing.Documents)
            {
                if (!document.IsDirectory &&
                    string.Equals(document.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
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

        private static StorageMutationResult StaleRootMutation(StorageDocumentId documentId)
        {
            return new StorageMutationResult(
                StorageResultCode.Cancelled,
                documentId,
                "The document belongs to a previously selected Open Brush folder.");
        }




        internal static string GetAreaPath(StorageArea area)
        {
            switch (area)
            {
                case StorageArea.UserRoot: return "";
                case StorageArea.Sketches: return "Sketches";
                case StorageArea.SavedStrokes: return "Media Library/Saved Strokes";
                case StorageArea.MediaLibraryImages: return "Media Library/Images";
                case StorageArea.MediaLibraryBackgroundImages:
                    return "Media Library/BackgroundImages";
                case StorageArea.MediaLibraryModels: return "Media Library/Models";
                case StorageArea.MediaLibraryVideos: return "Media Library/Videos";
                case StorageArea.MediaLibrarySoundClips: return "Media Library/Sound Clips";
                case StorageArea.MediaLibraryQuill: return "Media Library/Quill";
                case StorageArea.Music: return "Music";
                case StorageArea.Snapshots: return "Snapshots";
                case StorageArea.Videos: return "Videos";
                case StorageArea.VrVideos: return "VRVideos";
                case StorageArea.Exports: return "Exports";
                case StorageArea.SplatPoses: return "SplatPoses";
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



        internal static string GetMaterialTexturePath(string line)
        {
            var tokens = System.Text.RegularExpressions.Regex.Matches(line, @"\S+");
            if (tokens.Count < 2 ||
                !(tokens[0].Value.StartsWith("map_", StringComparison.OrdinalIgnoreCase) ||
                  tokens[0].Value.Equals("bump", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
            int index = 1;
            while (index < tokens.Count && tokens[index].Value.StartsWith("-", StringComparison.Ordinal))
            {
                string option = tokens[index++].Value.ToLowerInvariant();
                if (option == "-o" || option == "-s" || option == "-t")
                {
                    int count = 0;
                    while (index < tokens.Count && count < 3 && double.TryParse(tokens[index].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                    {
                        index++;
                        count++;
                    }
                    if (count == 0) { return null; }
                }
                else
                {
                    int count;
                    switch (option)
                    {
                        case "-mm": count = 2; break;
                        case "-blendu": case "-blendv": case "-boost": case "-texres":
                        case "-clamp": case "-bm": case "-imfchan": case "-type":
                        case "-cc": case "-colorspace": count = 1; break;
                        default: return null;
                    }
                    index += count;
                }
            }
            return index < tokens.Count ? line.Substring(tokens[index].Index).Trim().Trim('"') : null;
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
                throw new IOException(listing.Error);
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
