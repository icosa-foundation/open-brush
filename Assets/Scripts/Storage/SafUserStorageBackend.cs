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
using System.IO;
using System.Threading;

namespace TiltBrush
{
    /// Direct access to the user-selected Open Brush document tree.
    public sealed class SafUserStorageBackend : IUserStorageBackend
    {
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
            throw new NotSupportedException("SAF write transactions are not initialized.");
        }

        public StorageMutationResult Rename(
            StorageDocumentId documentId,
            string newDisplayName,
            CancellationToken cancellationToken)
        {
            return new StorageMutationResult(
                StorageResultCode.Failed,
                documentId,
                "SAF rename is not initialized.");
        }

        public StorageMutationResult Delete(
            StorageDocumentId documentId, CancellationToken cancellationToken)
        {
            return new StorageMutationResult(
                StorageResultCode.Failed,
                documentId,
                "SAF delete is not initialized.");
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
