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
using System.Threading;

namespace TiltBrush
{
    /// Direct access to the user-selected Open Brush document tree.
    public sealed class SafUserStorageBackend : IUserStorageBackend
    {
        private sealed class DocumentLocation
        {
            public StorageArea Area;
            public string RelativePath;
        }

        private readonly object m_LocationGate = new object();
        private readonly Dictionary<StorageDocumentId, DocumentLocation> m_Locations =
            new Dictionary<StorageDocumentId, DocumentLocation>();
        private string m_MappedRootId;

        public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
        public bool IsReady => AndroidSafStorage.HasOpenBrushFolder();

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
            string rootId = AndroidSafStorage.GetSelectedRootIdentity();
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
            string rootId = AndroidSafStorage.GetSelectedRootIdentity();
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
            throw new NotSupportedException("SAF materialization is not initialized.");
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
            string rootId = AndroidSafStorage.GetSelectedRootIdentity();
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
