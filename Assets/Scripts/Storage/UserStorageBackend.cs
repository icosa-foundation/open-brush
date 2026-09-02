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

namespace TiltBrush
{
    public enum StorageBackendKind
    {
        Local,
        StorageAccessFramework,
    }

    public enum StorageArea
    {
        Sketches,
        SavedStrokes,
        MediaLibraryImages,
        MediaLibraryBackgroundImages,
        MediaLibraryModels,
        MediaLibraryVideos,
        Snapshots,
        Videos,
        VrVideos,
        Exports,
        Scripts,
        Plugins,
        Fonts,
    }

    public enum StorageResultCode
    {
        Success,
        NotFound,
        NotReady,
        PermissionDenied,
        Cancelled,
        ProviderUnavailable,
        InvalidPath,
        Failed,
    }

    public enum MaterializationScope
    {
        File,
        DependencyTree,
    }

    /// An opaque backend-owned document identity. Display paths must never be used in its place.
    public readonly struct StorageDocumentId : IEquatable<StorageDocumentId>
    {
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public StorageDocumentId(string value)
        {
            Value = value;
        }

        public bool Equals(StorageDocumentId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StorageDocumentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value ?? "";
        }
    }

    public sealed class StorageDocument
    {
        private const long kFlagSupportsWrite = 1L << 1;
        private const long kFlagSupportsDelete = 1L << 2;
        private const long kFlagSupportsRename = 1L << 6;
        private const long kFlagSupportsRemove = 1L << 10;

        public StorageDocumentId DocumentId { get; }
        public StorageDocumentId ParentDocumentId { get; }
        public string DisplayName { get; }
        public string MimeType { get; }
        public bool IsDirectory { get; }
        public long? Size { get; }
        public DateTime? LastModified { get; }
        public long ProviderFlags { get; }
        public string RelativeDisplayPath { get; }
        public bool SupportsWrite => (ProviderFlags & kFlagSupportsWrite) != 0;
        public bool SupportsDelete => (ProviderFlags & kFlagSupportsDelete) != 0;
        public bool SupportsRename => (ProviderFlags & kFlagSupportsRename) != 0;
        public bool SupportsRemove => (ProviderFlags & kFlagSupportsRemove) != 0;

        public StorageDocument(
            StorageDocumentId documentId,
            StorageDocumentId parentDocumentId,
            string displayName,
            string mimeType,
            bool isDirectory,
            long? size,
            DateTime? lastModified,
            long providerFlags,
            string relativeDisplayPath)
        {
            DocumentId = documentId;
            ParentDocumentId = parentDocumentId;
            DisplayName = displayName;
            MimeType = mimeType;
            IsDirectory = isDirectory;
            Size = size;
            LastModified = lastModified;
            ProviderFlags = providerFlags;
            RelativeDisplayPath = relativeDisplayPath;
        }
    }

    public sealed class StorageDirectoryResult
    {
        public StorageResultCode Code { get; }
        public IReadOnlyList<StorageDocument> Documents { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        private StorageDirectoryResult(
            StorageResultCode code, IReadOnlyList<StorageDocument> documents, string error)
        {
            Code = code;
            Documents = documents ?? Array.Empty<StorageDocument>();
            Error = error;
        }

        public static StorageDirectoryResult Succeeded(IReadOnlyList<StorageDocument> documents)
        {
            return new StorageDirectoryResult(StorageResultCode.Success, documents, null);
        }

        public static StorageDirectoryResult Failed(StorageResultCode code, string error)
        {
            if (code == StorageResultCode.Success)
            {
                throw new ArgumentException("A failed result cannot use the success code.", nameof(code));
            }
            return new StorageDirectoryResult(code, null, error);
        }
    }

    public sealed class StorageTreeQuery
    {
        public bool Recursive { get; }
        public bool IncludeDirectories { get; }
        public IReadOnlyCollection<string> IncludeExtensions { get; }
        public IReadOnlyCollection<string> ExcludeExtensions { get; }
        public int MaximumDepth { get; }
        public int MaximumItemCount { get; }

        public StorageTreeQuery(
            bool recursive = true,
            bool includeDirectories = false,
            IEnumerable<string> includeExtensions = null,
            IEnumerable<string> excludeExtensions = null,
            int maximumDepth = 32,
            int maximumItemCount = 10000)
        {
            if (maximumDepth < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumDepth), "Maximum depth cannot be negative.");
            }
            if (maximumItemCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumItemCount), "Maximum item count must be positive.");
            }
            Recursive = recursive;
            IncludeDirectories = includeDirectories;
            IncludeExtensions = NormalizeExtensions(includeExtensions);
            ExcludeExtensions = NormalizeExtensions(excludeExtensions);
            MaximumDepth = maximumDepth;
            MaximumItemCount = maximumItemCount;
        }

        internal bool IncludesFile(string displayName)
        {
            string extension = Path.GetExtension(displayName).ToLowerInvariant();
            return (IncludeExtensions.Count == 0 || IncludeExtensions.Contains(extension)) &&
                !ExcludeExtensions.Contains(extension);
        }

        private static IReadOnlyCollection<string> NormalizeExtensions(
            IEnumerable<string> extensions)
        {
            if (extensions == null)
            {
                return Array.Empty<string>();
            }
            var normalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string extension in extensions)
            {
                if (string.IsNullOrWhiteSpace(extension))
                {
                    continue;
                }
                string value = extension.Trim();
                normalized.Add(value.StartsWith(".", StringComparison.Ordinal)
                    ? value.ToLowerInvariant()
                    : $".{value.ToLowerInvariant()}");
            }
            return normalized;
        }
    }

    public sealed class StorageTreeResult
    {
        public StorageResultCode Code { get; }
        public IReadOnlyList<StorageDocument> Entries { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        private StorageTreeResult(
            StorageResultCode code, IReadOnlyList<StorageDocument> entries, string error)
        {
            Code = code;
            Entries = entries ?? Array.Empty<StorageDocument>();
            Error = error;
        }

        public static StorageTreeResult Succeeded(IReadOnlyList<StorageDocument> entries)
        {
            return new StorageTreeResult(StorageResultCode.Success, entries, null);
        }

        public static StorageTreeResult Failed(StorageResultCode code, string error)
        {
            if (code == StorageResultCode.Success)
            {
                throw new ArgumentException(
                    "A failed result cannot use the success code.", nameof(code));
            }
            return new StorageTreeResult(code, null, error);
        }
    }

    public readonly struct StorageMutationResult
    {
        public StorageResultCode Code { get; }
        public StorageDocumentId DocumentId { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        public StorageMutationResult(
            StorageResultCode code, StorageDocumentId documentId, string error = null)
        {
            Code = code;
            DocumentId = documentId;
            Error = error;
        }
    }

    public interface IStorageWriteTransaction : IDisposable
    {
        StorageDocumentId TargetDocumentId { get; }
        StorageDocumentId TemporaryDocumentId { get; }
        Stream OpenWrite();
        StorageMutationResult Commit();
        void Rollback();
    }

    public interface IUserStorageBackend
    {
        StorageBackendKind Kind { get; }
        bool IsReady { get; }
        string RootIdentity { get; }

        StorageDirectoryResult List(
            StorageArea area, string relativeDirectory, CancellationToken cancellationToken);
        StorageTreeResult EnumerateTree(
            StorageArea area,
            string relativeDirectory,
            StorageTreeQuery query,
            CancellationToken cancellationToken);
        Stream OpenRead(
            StorageDocumentId documentId,
            bool requireSeekable,
            CancellationToken cancellationToken);
        IStorageWriteTransaction BeginWrite(
            StorageArea area,
            string relativePath,
            string mimeType,
            CancellationToken cancellationToken,
            StorageDocumentId targetDocumentId = default);
        StorageMutationResult Rename(
            StorageDocumentId documentId,
            string newDisplayName,
            CancellationToken cancellationToken);
        StorageMutationResult Delete(
            StorageDocumentId documentId, CancellationToken cancellationToken);
        string Materialize(
            StorageDocumentId documentId,
            MaterializationScope scope,
            CancellationToken cancellationToken);
        string GetMaterializationPath(StorageDocumentId documentId);
    }

    public static class UserStorage
    {
        private static IUserStorageBackend sm_Backend;

        public static IUserStorageBackend Backend
        {
            get
            {
                if (sm_Backend == null)
                {
                    sm_Backend = CreateBackend();
                }
                return sm_Backend;
            }
        }

        public static void SetBackendForTests(IUserStorageBackend backend)
        {
            sm_Backend = backend;
        }

        private static IUserStorageBackend CreateBackend()
        {
#if UNITY_ANDROID && OPEN_BRUSH_GOOGLE_PLAY
            if (OpenBrushStorage.IsGooglePlayStorageMode)
            {
                return new SafUserStorageBackend();
            }
#endif
            return new LocalUserStorageBackend();
        }
    }

    /// Preserves the existing System.IO behavior for every non-SAF platform.
    public sealed class LocalUserStorageBackend : IUserStorageBackend
    {
        private readonly Func<StorageArea, string> m_AreaRoot;

        public StorageBackendKind Kind => StorageBackendKind.Local;
        public bool IsReady => true;
        public string RootIdentity => "";

        public LocalUserStorageBackend()
            : this(GetAreaRoot)
        {
        }

        public LocalUserStorageBackend(Func<StorageArea, string> areaRoot)
        {
            m_AreaRoot = areaRoot ?? throw new ArgumentNullException(nameof(areaRoot));
        }

        public StorageDirectoryResult List(
            StorageArea area, string relativeDirectory, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string root = m_AreaRoot(area);
                string directory = ResolveRelativePath(root, relativeDirectory);
                if (!Directory.Exists(directory))
                {
                    return StorageDirectoryResult.Failed(
                        StorageResultCode.NotFound, $"Directory does not exist: {directory}");
                }

                var parentId = new StorageDocumentId(directory);
                var documents = new List<StorageDocument>();
                foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    bool isDirectory = Directory.Exists(path);
                    var info = isDirectory
                        ? (FileSystemInfo)new DirectoryInfo(path)
                        : new FileInfo(path);
                    long? size = isDirectory ? null : ((FileInfo)info).Length;
                    documents.Add(new StorageDocument(
                        new StorageDocumentId(path),
                        parentId,
                        info.Name,
                        isDirectory
                            ? "vnd.android.document/directory"
                            : StorageMimeTypes.ForPath(path),
                        isDirectory,
                        size,
                        info.LastWriteTime,
                        0,
                        CombineDisplayPath(relativeDirectory, info.Name)));
                }
                return StorageDirectoryResult.Succeeded(documents);
            }
            catch (OperationCanceledException)
            {
                return StorageDirectoryResult.Failed(
                    StorageResultCode.Cancelled, "Directory listing was cancelled.");
            }
            catch (UnauthorizedAccessException e)
            {
                return StorageDirectoryResult.Failed(StorageResultCode.PermissionDenied, e.Message);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                return StorageDirectoryResult.Failed(StorageResultCode.Failed, e.Message);
            }
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
            EnsureValidDocumentId(documentId);
            var stream = new FileStream(
                documentId.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (requireSeekable && !stream.CanSeek)
            {
                stream.Dispose();
                throw new IOException($"Storage document is not seekable: {documentId}");
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
            string targetPath = ResolveRelativePath(m_AreaRoot(area), relativePath);
            return new LocalWriteTransaction(targetPath);
        }

        public StorageMutationResult Rename(
            StorageDocumentId documentId,
            string newDisplayName,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureValidDocumentId(documentId);
                ValidateDisplayName(newDisplayName);
                string newPath = Path.Combine(Path.GetDirectoryName(documentId.Value), newDisplayName);
                if (Directory.Exists(documentId.Value))
                {
                    Directory.Move(documentId.Value, newPath);
                }
                else
                {
                    File.Move(documentId.Value, newPath);
                }
                return new StorageMutationResult(
                    StorageResultCode.Success, new StorageDocumentId(newPath));
            }
            catch (OperationCanceledException e)
            {
                return new StorageMutationResult(StorageResultCode.Cancelled, documentId, e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                return new StorageMutationResult(
                    StorageResultCode.PermissionDenied, documentId, e.Message);
            }
            catch (Exception e) when (
                e is IOException || e is ArgumentException || e is NotSupportedException)
            {
                return new StorageMutationResult(StorageResultCode.Failed, documentId, e.Message);
            }
        }

        public StorageMutationResult Delete(
            StorageDocumentId documentId, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureValidDocumentId(documentId);
                if (Directory.Exists(documentId.Value))
                {
                    Directory.Delete(documentId.Value, true);
                }
                else if (File.Exists(documentId.Value))
                {
                    File.Delete(documentId.Value);
                }
                else
                {
                    return new StorageMutationResult(
                        StorageResultCode.NotFound, documentId, "Storage document does not exist.");
                }
                return new StorageMutationResult(StorageResultCode.Success, documentId);
            }
            catch (OperationCanceledException e)
            {
                return new StorageMutationResult(StorageResultCode.Cancelled, documentId, e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                return new StorageMutationResult(
                    StorageResultCode.PermissionDenied, documentId, e.Message);
            }
            catch (Exception e) when (e is IOException || e is ArgumentException)
            {
                return new StorageMutationResult(StorageResultCode.Failed, documentId, e.Message);
            }
        }

        public string Materialize(
            StorageDocumentId documentId,
            MaterializationScope scope,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureValidDocumentId(documentId);
            return documentId.Value;
        }

        public string GetMaterializationPath(StorageDocumentId documentId)
        {
            EnsureValidDocumentId(documentId);
            return documentId.Value;
        }

        internal static string GetAreaRoot(StorageArea area)
        {
            switch (area)
            {
                case StorageArea.Sketches: return App.UserSketchPath();
                case StorageArea.SavedStrokes: return App.SavedStrokesPath();
                case StorageArea.MediaLibraryImages: return App.ReferenceImagePath();
                case StorageArea.MediaLibraryBackgroundImages:
                    return App.BackgroundImagesLibraryPath();
                case StorageArea.MediaLibraryModels: return App.ModelLibraryPath();
                case StorageArea.MediaLibraryVideos: return App.VideoLibraryPath();
                case StorageArea.Snapshots: return App.SnapshotPath();
                case StorageArea.Videos: return App.VideosPath();
                case StorageArea.VrVideos: return App.VrVideosPath();
                case StorageArea.Exports: return App.UserExportPath();
                case StorageArea.Scripts: return Path.Combine(App.UserPath(), "Scripts");
                case StorageArea.Plugins: return Path.Combine(App.UserPath(), "Plugins");
                case StorageArea.Fonts: return Path.Combine(App.UserPath(), "Fonts");
                default: throw new ArgumentOutOfRangeException(nameof(area), area, null);
            }
        }

        private static string ResolveRelativePath(string root, string relativePath)
        {
            string fullRoot = Path.GetFullPath(root);
            string combined = string.IsNullOrEmpty(relativePath)
                ? fullRoot
                : Path.GetFullPath(Path.Combine(fullRoot, relativePath));
            string rootWithSeparator = fullRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            StringComparison comparison = Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(combined, fullRoot, comparison) &&
                !combined.StartsWith(rootWithSeparator, comparison))
            {
                throw new ArgumentException("Storage path escapes its logical area.");
            }
            return combined;
        }

        private static string CombineDisplayPath(string directory, string name)
        {
            return string.IsNullOrEmpty(directory)
                ? name
                : $"{directory.TrimEnd('/', '\\')}/{name}";
        }

        private static void EnsureValidDocumentId(StorageDocumentId documentId)
        {
            if (!documentId.IsValid)
            {
                throw new ArgumentException("Storage document identity is empty.");
            }
        }

        private static void ValidateDisplayName(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) ||
                displayName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                displayName.Contains("/") ||
                displayName.Contains("\\"))
            {
                throw new ArgumentException("Storage display name is invalid.", nameof(displayName));
            }
        }

        private sealed class LocalWriteTransaction : IStorageWriteTransaction
        {
            private readonly string m_TargetPath;
            private readonly string m_TemporaryPath;
            private FileStream m_Stream;
            private bool m_Finished;

            public StorageDocumentId TargetDocumentId => new StorageDocumentId(m_TargetPath);
            public StorageDocumentId TemporaryDocumentId =>
                new StorageDocumentId(m_TemporaryPath);

            public LocalWriteTransaction(string targetPath)
            {
                m_TargetPath = targetPath;
                m_TemporaryPath = $"{targetPath}.obtmp-{Guid.NewGuid():N}";
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
            }

            public Stream OpenWrite()
            {
                if (m_Finished)
                {
                    throw new InvalidOperationException("Storage transaction is already finished.");
                }
                if (m_Stream != null)
                {
                    throw new InvalidOperationException("Storage transaction stream is already open.");
                }
                m_Stream = new FileStream(
                    m_TemporaryPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                return m_Stream;
            }

            public StorageMutationResult Commit()
            {
                if (m_Finished)
                {
                    throw new InvalidOperationException("Storage transaction is already finished.");
                }

                try
                {
                    CloseStream();
                    if (!File.Exists(m_TemporaryPath))
                    {
                        throw new IOException("Storage transaction has no completed payload.");
                    }

                    string backupPath = $"{m_TargetPath}.obbackup-{Guid.NewGuid():N}";
                    bool hadTarget = File.Exists(m_TargetPath);
                    if (hadTarget)
                    {
                        File.Move(m_TargetPath, backupPath);
                    }
                    try
                    {
                        File.Move(m_TemporaryPath, m_TargetPath);
                    }
                    catch
                    {
                        if (hadTarget && File.Exists(backupPath) && !File.Exists(m_TargetPath))
                        {
                            File.Move(backupPath, m_TargetPath);
                        }
                        throw;
                    }
                    if (hadTarget)
                    {
                        File.Delete(backupPath);
                    }

                    m_Finished = true;
                    return new StorageMutationResult(
                        StorageResultCode.Success, new StorageDocumentId(m_TargetPath));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    return new StorageMutationResult(
                        e is UnauthorizedAccessException
                            ? StorageResultCode.PermissionDenied
                            : StorageResultCode.Failed,
                        TargetDocumentId,
                        e.Message);
                }
            }

            public void Rollback()
            {
                if (m_Finished)
                {
                    return;
                }
                CloseStream();
                if (File.Exists(m_TemporaryPath))
                {
                    File.Delete(m_TemporaryPath);
                }
                m_Finished = true;
            }

            public void Dispose()
            {
                if (!m_Finished)
                {
                    Rollback();
                }
            }

            private void CloseStream()
            {
                if (m_Stream == null)
                {
                    return;
                }
                try
                {
                    m_Stream.Flush();
                    m_Stream.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // The caller already closed and flushed the transaction stream.
                }
                finally
                {
                    m_Stream = null;
                }
            }
        }
    }

    internal static class StorageMimeTypes
    {
        public static string ForPath(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".tilt": return TiltFile.TILT_MIME_TYPE;
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".mp4": return "video/mp4";
                case ".json": return "application/json";
                case ".txt": return "text/plain";
                case ".html":
                case ".htm": return "text/html";
                case ".lua": return "text/x-lua";
                case ".ttf": return "font/ttf";
                case ".otf": return "font/otf";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                default: return "application/octet-stream";
            }
        }
    }

    internal static class StorageTreeEnumerator
    {
        private readonly struct PendingDirectory
        {
            public string RelativePath { get; }
            public int Depth { get; }

            public PendingDirectory(string relativePath, int depth)
            {
                RelativePath = relativePath;
                Depth = depth;
            }
        }

        public static StorageTreeResult Enumerate(
            IUserStorageBackend backend,
            StorageArea area,
            string relativeDirectory,
            StorageTreeQuery query,
            CancellationToken cancellationToken)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }
            query = query ?? new StorageTreeQuery();
            string rootIdentity = backend.RootIdentity;
            string initialDirectory = NormalizeDirectory(relativeDirectory);
            var pending = new Stack<PendingDirectory>();
            pending.Push(new PendingDirectory(initialDirectory, 0));
            var entries = new List<StorageDocument>();
            int observedItemCount = 0;

            try
            {
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.Equals(
                            rootIdentity, backend.RootIdentity, StringComparison.Ordinal))
                    {
                        return StorageTreeResult.Failed(
                            StorageResultCode.Cancelled,
                            "The selected storage root changed during tree enumeration.");
                    }

                    PendingDirectory current = pending.Pop();
                    StorageDirectoryResult listing = backend.List(
                        area, current.RelativePath, cancellationToken);
                    if (!listing.Success)
                    {
                        if (current.Depth == 0 &&
                            listing.Code == StorageResultCode.NotFound)
                        {
                            return StorageTreeResult.Succeeded(Array.Empty<StorageDocument>());
                        }
                        return StorageTreeResult.Failed(listing.Code, listing.Error);
                    }

                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (StorageDocument document in listing.Documents)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!names.Add(document.DisplayName))
                        {
                            return StorageTreeResult.Failed(
                                StorageResultCode.Failed,
                                $"Storage directory contains duplicate child name: " +
                                $"{CombinePath(current.RelativePath, document.DisplayName)}");
                        }
                        ++observedItemCount;
                        if (observedItemCount > query.MaximumItemCount)
                        {
                            return StorageTreeResult.Failed(
                                StorageResultCode.Failed,
                                $"Storage tree exceeds the {query.MaximumItemCount} item limit.");
                        }

                        string childPath = CombinePath(
                            current.RelativePath, document.DisplayName);
                        StorageDocument normalized = WithRelativePath(document, childPath);
                        if (document.IsDirectory)
                        {
                            if (query.IncludeDirectories)
                            {
                                entries.Add(normalized);
                            }
                            if (query.Recursive)
                            {
                                if (current.Depth >= query.MaximumDepth)
                                {
                                    return StorageTreeResult.Failed(
                                        StorageResultCode.Failed,
                                        $"Storage tree exceeds the {query.MaximumDepth} level " +
                                        $"depth limit at {childPath}.");
                                }
                                pending.Push(new PendingDirectory(
                                    childPath, current.Depth + 1));
                            }
                        }
                        else if (query.IncludesFile(document.DisplayName))
                        {
                            entries.Add(normalized);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return StorageTreeResult.Failed(
                    StorageResultCode.Cancelled, "Storage tree enumeration was cancelled.");
            }
            catch (ArgumentException e)
            {
                return StorageTreeResult.Failed(StorageResultCode.InvalidPath, e.Message);
            }

            if (!string.Equals(rootIdentity, backend.RootIdentity, StringComparison.Ordinal))
            {
                return StorageTreeResult.Failed(
                    StorageResultCode.Cancelled,
                    "The selected storage root changed during tree enumeration.");
            }
            entries.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                left.RelativeDisplayPath, right.RelativeDisplayPath));
            return StorageTreeResult.Succeeded(entries);
        }

        private static StorageDocument WithRelativePath(
            StorageDocument document, string relativePath)
        {
            return new StorageDocument(
                document.DocumentId,
                document.ParentDocumentId,
                document.DisplayName,
                document.MimeType,
                document.IsDirectory,
                document.Size,
                document.LastModified,
                document.ProviderFlags,
                relativePath);
        }

        private static string NormalizeDirectory(string relativeDirectory)
        {
            if (string.IsNullOrEmpty(relativeDirectory))
            {
                return "";
            }
            if (Path.IsPathRooted(relativeDirectory))
            {
                throw new ArgumentException("Storage tree path must be relative.");
            }
            string normalized = relativeDirectory.Replace('\\', '/').Trim('/');
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArgumentException("Storage tree path escapes its logical area.");
                }
            }
            return normalized;
        }

        private static string CombinePath(string directory, string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) ||
                displayName.Contains("/") ||
                displayName.Contains("\\") ||
                displayName == "." ||
                displayName == "..")
            {
                throw new ArgumentException("Storage provider returned an invalid display name.");
            }
            return string.IsNullOrEmpty(directory)
                ? displayName
                : $"{directory}/{displayName}";
        }
    }
}
