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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public sealed class RuntimeProjectionResult
    {
        public StorageResultCode Code { get; }
        public string RuntimePath { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        private RuntimeProjectionResult(
            StorageResultCode code, string runtimePath, string error)
        {
            Code = code;
            RuntimePath = runtimePath;
            Error = error;
        }

        public static RuntimeProjectionResult Succeeded(string runtimePath)
        {
            return new RuntimeProjectionResult(
                StorageResultCode.Success, runtimePath, null);
        }

        public static RuntimeProjectionResult Failed(
            StorageResultCode code, string runtimePath, string error)
        {
            if (code == StorageResultCode.Success)
            {
                throw new ArgumentException(
                    "A failed result cannot use the success code.", nameof(code));
            }
            return new RuntimeProjectionResult(code, runtimePath, error);
        }
    }

    public interface IUserRuntimeContent
    {
        string GetRuntimePath(StorageArea area);
        Task<RuntimeProjectionResult> EnsureCurrentAsync(
            StorageArea area, CancellationToken cancellationToken);
        event Action<StorageArea> Refreshed;
    }

    public sealed class RuntimeContentWriteResult
    {
        public StorageResultCode Code { get; }
        public bool Created { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        public RuntimeContentWriteResult(
            StorageResultCode code, bool created, string error = null)
        {
            Code = code;
            Created = created;
            Error = error;
        }
    }

    public static class UserRuntimeContent
    {
        private static readonly object sm_Gate = new object();
        private static IUserStorageBackend sm_Backend;
        private static IUserRuntimeContent sm_Content;

        public static IUserRuntimeContent Instance
        {
            get
            {
                IUserStorageBackend backend = UserStorage.Backend;
                lock (sm_Gate)
                {
                    if (sm_Content == null || !ReferenceEquals(sm_Backend, backend))
                    {
                        sm_Backend = backend;
                        sm_Content = backend.Kind == StorageBackendKind.StorageAccessFramework
                            ? (IUserRuntimeContent)new SafUserRuntimeContent(backend)
                            : new LocalUserRuntimeContent();
                    }
                    return sm_Content;
                }
            }
        }

        public static void SetForTests(IUserRuntimeContent content)
        {
            lock (sm_Gate)
            {
                sm_Backend = UserStorage.Backend;
                sm_Content = content;
            }
        }

        public static async Task<RuntimeContentWriteResult> PublishIfMissingAsync(
            StorageArea area,
            string relativePath,
            string mimeType,
            byte[] data,
            CancellationToken cancellationToken)
        {
            LocalUserRuntimeContent.EnsureRuntimeArea(area);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            string normalized = NormalizeRelativePath(relativePath);
            string directory = GetLogicalDirectory(normalized);
            string displayName = Path.GetFileName(normalized);
            IUserStorageBackend backend = UserStorage.Backend;
            RuntimeContentWriteResult write = await Task.Run(() =>
            {
                StorageDirectoryResult listing = backend.List(
                    area, directory, cancellationToken);
                if (!listing.Success && listing.Code != StorageResultCode.NotFound)
                {
                    return new RuntimeContentWriteResult(
                        listing.Code, created: false, listing.Error);
                }
                if (listing.Success && listing.Documents.Any(document =>
                        string.Equals(
                            document.DisplayName,
                            displayName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return new RuntimeContentWriteResult(
                        StorageResultCode.Success, created: false);
                }
                try
                {
                    using (IStorageWriteTransaction transaction = backend.BeginWrite(
                        area,
                        normalized,
                        mimeType ?? StorageMimeTypes.ForPath(normalized),
                        cancellationToken))
                    {
                        if (backend.Kind == StorageBackendKind.StorageAccessFramework &&
                            transaction.TargetDocumentId.IsValid)
                        {
                            return new RuntimeContentWriteResult(
                                StorageResultCode.Success, created: false);
                        }
                        using (Stream output = transaction.OpenWrite())
                        {
                            output.Write(data, 0, data.Length);
                        }
                        StorageMutationResult commit = transaction.Commit();
                        return new RuntimeContentWriteResult(
                            commit.Code, commit.Success, commit.Error);
                    }
                }
                catch (OperationCanceledException e)
                {
                    return new RuntimeContentWriteResult(
                        StorageResultCode.Cancelled, created: false, e.Message);
                }
                catch (UnauthorizedAccessException e)
                {
                    return new RuntimeContentWriteResult(
                        StorageResultCode.PermissionDenied, created: false, e.Message);
                }
                catch (Exception e) when (
                    e is IOException || e is ArgumentException || e is InvalidOperationException)
                {
                    return new RuntimeContentWriteResult(
                        StorageResultCode.Failed, created: false, e.Message);
                }
            }, cancellationToken);
            if (!write.Success || !write.Created)
            {
                return write;
            }
            RuntimeProjectionResult refresh =
                await Instance.EnsureCurrentAsync(area, cancellationToken);
            return refresh.Success
                ? write
                : new RuntimeContentWriteResult(
                    refresh.Code,
                    created: true,
                    $"Content was committed but runtime refresh failed: {refresh.Error}");
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Runtime content path must be relative.");
            }
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArgumentException(
                        "Runtime content path escapes its storage area.");
                }
            }
            return normalized;
        }

        private static string GetLogicalDirectory(string relativePath)
        {
            int separator = relativePath.LastIndexOf('/');
            return separator < 0 ? "" : relativePath.Substring(0, separator);
        }
    }

    public sealed class LocalUserRuntimeContent : IUserRuntimeContent
    {
        public event Action<StorageArea> Refreshed;

        public string GetRuntimePath(StorageArea area)
        {
            EnsureRuntimeArea(area);
            return LocalUserStorageBackend.GetAreaRoot(area);
        }

        public Task<RuntimeProjectionResult> EnsureCurrentAsync(
            StorageArea area, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRuntimeArea(area);
            string path = LocalUserStorageBackend.GetAreaRoot(area);
            Directory.CreateDirectory(path);
            Refreshed?.Invoke(area);
            return Task.FromResult(RuntimeProjectionResult.Succeeded(path));
        }

        internal static void EnsureRuntimeArea(StorageArea area)
        {
            if (area != StorageArea.Scripts &&
                area != StorageArea.Plugins &&
                area != StorageArea.Fonts)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(area), area, "Area is not projected runtime content.");
            }
        }
    }

    public sealed class SafUserRuntimeContent : IUserRuntimeContent
    {
        private const int kManifestVersion = 1;
        private const int kMigrationVersion = 1;
        private const int kMaximumDepth = 32;
        private const int kMaximumItemCount = 5000;
        private const long kMaximumFileBytes = 64L * 1024L * 1024L;
        private const long kMaximumAreaBytes = 256L * 1024L * 1024L;
        private const string kPointerFileName = "current.json";
        private const string kManifestFileName = "manifest.json";
        private const string kContentDirectoryName = "content";

        [Serializable]
        private sealed class ProjectionPointer
        {
            public int Version;
            public string RootNamespace;
            public string Area;
            public string Generation;
        }

        [Serializable]
        private sealed class ProjectionManifest
        {
            public int Version;
            public string RootNamespace;
            public string Area;
            public string Generation;
            public List<ProjectionEntry> Entries = new List<ProjectionEntry>();
        }

        [Serializable]
        private sealed class ProjectionEntry
        {
            public string RelativePath;
            public string DocumentId;
            public long Size;
            public long? LastModifiedTicks;
            public string Sha256;
        }

        [Serializable]
        private sealed class MigrationRecord
        {
            public int Version;
            public string RootNamespace;
            public string Area;
            public string StartedUtc;
            public bool CanonicalCopiesComplete;
            public bool LocalCleanupComplete;
            public List<MigrationItem> Items = new List<MigrationItem>();
        }

        [Serializable]
        private sealed class MigrationItem
        {
            public string SourceRelativePath;
            public string SourceSha256;
            public string DestinationRelativePath;
            public bool CanonicalCopyComplete;
            public bool LocalCleanupComplete;
        }

        private readonly IUserStorageBackend m_Backend;
        private readonly string m_BasePath;
        private readonly Func<StorageArea, string> m_LegacyRoot;
        private readonly Dictionary<StorageArea, SemaphoreSlim> m_AreaGates =
            new Dictionary<StorageArea, SemaphoreSlim>();

        public event Action<StorageArea> Refreshed;

        public SafUserRuntimeContent(
            IUserStorageBackend backend,
            string basePath = null,
            Func<StorageArea, string> legacyRoot = null)
        {
            m_Backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (backend.Kind != StorageBackendKind.StorageAccessFramework)
            {
                throw new ArgumentException(
                    "SAF runtime content requires an SAF backend.", nameof(backend));
            }
            m_BasePath = basePath ?? Path.Combine(
                Application.persistentDataPath, "OpenBrushSafRuntime");
            m_LegacyRoot = legacyRoot ?? LocalUserStorageBackend.GetAreaRoot;
            foreach (StorageArea area in RuntimeAreas)
            {
                m_AreaGates.Add(area, new SemaphoreSlim(1, 1));
            }
        }

        public string GetRuntimePath(StorageArea area)
        {
            LocalUserRuntimeContent.EnsureRuntimeArea(area);
            string rootIdentity = m_Backend.RootIdentity;
            if (string.IsNullOrEmpty(rootIdentity))
            {
                return GetUnavailablePath(area);
            }
            string areaRoot = GetAreaRoot(rootIdentity, area);
            ProjectionPointer pointer = ReadPointer(areaRoot);
            if (IsValidPointer(pointer, rootIdentity, area))
            {
                string contentPath = GetGenerationContentPath(
                    areaRoot, pointer.Generation);
                if (Directory.Exists(contentPath))
                {
                    return contentPath;
                }
            }
            return GetUnavailablePath(area);
        }

        public async Task<RuntimeProjectionResult> EnsureCurrentAsync(
            StorageArea area, CancellationToken cancellationToken)
        {
            LocalUserRuntimeContent.EnsureRuntimeArea(area);
            SemaphoreSlim areaGate = m_AreaGates[area];
            await areaGate.WaitAsync(cancellationToken);
            RuntimeProjectionResult result;
            try
            {
                result = await Task.Run(
                    () => Refresh(area, cancellationToken), cancellationToken);
            }
            finally
            {
                areaGate.Release();
            }
            if (result.Success)
            {
                try
                {
                    Refreshed?.Invoke(area);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"SAF_PROJECTION {area} refresh listener failed: {e.Message}");
                }
            }
            return result;
        }

        private RuntimeProjectionResult Refresh(
            StorageArea area, CancellationToken cancellationToken)
        {
            string rootIdentity = m_Backend.RootIdentity;
            if (!m_Backend.IsReady || string.IsNullOrEmpty(rootIdentity))
            {
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.NotReady,
                    GetUnavailablePath(area),
                    "Open Brush shared folder is unavailable.");
            }

            var query = new StorageTreeQuery(
                recursive: true,
                includeDirectories: false,
                maximumDepth: kMaximumDepth,
                maximumItemCount: kMaximumItemCount);
            StorageTreeResult tree = m_Backend.EnumerateTree(
                area, "", query, cancellationToken);
            if (!tree.Success)
            {
                string retainedPath = GetRuntimePath(area);
                Debug.LogWarning(
                    $"SAF_PROJECTION {area} query failed; retaining generation: {tree.Error}");
                return RuntimeProjectionResult.Failed(
                    tree.Code, retainedPath, tree.Error);
            }
            if (!RootMatches(rootIdentity))
            {
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.Cancelled,
                    GetUnavailablePath(area),
                    "The selected Open Brush folder changed during projection refresh.");
            }

            string areaRoot = GetAreaRoot(rootIdentity, area);
            RuntimeProjectionResult migrationResult = MigrateLegacyContent(
                area,
                rootIdentity,
                areaRoot,
                tree,
                cancellationToken);
            if (!migrationResult.Success)
            {
                return migrationResult;
            }
            MigrationRecord migration = ReadMigrationRecord(areaRoot);
            if (migration != null &&
                migration.CanonicalCopiesComplete &&
                !migration.LocalCleanupComplete &&
                migration.Items.Count > 0)
            {
                tree = m_Backend.EnumerateTree(
                    area, "", query, cancellationToken);
                if (!tree.Success)
                {
                    return RuntimeProjectionResult.Failed(
                        tree.Code, GetRuntimePath(area), tree.Error);
                }
            }

            string generation = Guid.NewGuid().ToString("N");
            string generationRoot = GetGenerationRoot(areaRoot, generation);
            string contentRoot = Path.Combine(generationRoot, kContentDirectoryName);
            var manifest = new ProjectionManifest
            {
                Version = kManifestVersion,
                RootNamespace = GetRootNamespace(rootIdentity),
                Area = area.ToString(),
                Generation = generation,
            };
            long totalBytes = 0;

            try
            {
                Directory.CreateDirectory(contentRoot);
                foreach (StorageDocument document in tree.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (document.IsDirectory)
                    {
                        continue;
                    }
                    if (document.Size.HasValue && document.Size.Value > kMaximumFileBytes)
                    {
                        throw new IOException(
                            $"{document.RelativeDisplayPath} exceeds the " +
                            $"{kMaximumFileBytes} byte runtime-content file limit.");
                    }
                    string destination = GetSafeContentPath(
                        contentRoot, document.RelativeDisplayPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    ProjectionEntry entry = CopyDocument(
                        document,
                        destination,
                        ref totalBytes,
                        cancellationToken);
                    manifest.Entries.Add(entry);
                }
                manifest.Entries.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(
                        left.RelativePath, right.RelativePath));
                WriteJsonAtomically(
                    Path.Combine(generationRoot, kManifestFileName), manifest);

                if (!RootMatches(rootIdentity))
                {
                    throw new OperationCanceledException(
                        "The selected Open Brush folder changed during projection refresh.");
                }
                ProjectionPointer previous = ReadPointer(areaRoot);
                var pointer = new ProjectionPointer
                {
                    Version = kManifestVersion,
                    RootNamespace = GetRootNamespace(rootIdentity),
                    Area = area.ToString(),
                    Generation = generation,
                };
                WriteJsonAtomically(Path.Combine(areaRoot, kPointerFileName), pointer);
                try
                {
                    CleanupMigratedLocalContent(area, areaRoot, migration);
                    CleanupOldGenerations(areaRoot, generation, previous?.Generation);
                }
                catch (Exception e) when (
                    e is IOException ||
                    e is UnauthorizedAccessException ||
                    e is JsonException ||
                    e is CryptographicException)
                {
                    Debug.LogWarning(
                        $"SAF_PROJECTION {area} committed with cleanup pending: {e.Message}");
                }
                Debug.Log(
                    $"SAF_PROJECTION {area} committed {manifest.Entries.Count} file(s), " +
                    $"{totalBytes} byte(s).");
                return RuntimeProjectionResult.Succeeded(contentRoot);
            }
            catch (OperationCanceledException e)
            {
                DeleteOwnedGeneration(generationRoot);
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.Cancelled, GetRuntimePath(area), e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                DeleteOwnedGeneration(generationRoot);
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.PermissionDenied, GetRuntimePath(area), e.Message);
            }
            catch (Exception e) when (
                e is IOException ||
                e is ArgumentException ||
                e is CryptographicException ||
                e is JsonException)
            {
                DeleteOwnedGeneration(generationRoot);
                Debug.LogWarning(
                    $"SAF_PROJECTION {area} refresh failed; retaining generation: {e.Message}");
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.Failed, GetRuntimePath(area), e.Message);
            }
        }

        private RuntimeProjectionResult MigrateLegacyContent(
            StorageArea area,
            string rootIdentity,
            string areaRoot,
            StorageTreeResult tree,
            CancellationToken cancellationToken)
        {
            string journalPath = GetMigrationJournalPath(areaRoot);
            MigrationRecord record = ReadMigrationRecord(areaRoot);
            if (record != null)
            {
                if (!IsValidMigrationRecord(record, rootIdentity, area))
                {
                    return RuntimeProjectionResult.Failed(
                        StorageResultCode.Failed,
                        GetRuntimePath(area),
                        $"Runtime content migration record for {area} has an unknown " +
                        $"or mismatched format.");
                }
                if (record.CanonicalCopiesComplete)
                {
                    return RuntimeProjectionResult.Succeeded(GetRuntimePath(area));
                }
            }
            else
            {
                record = CreateMigrationRecord(area, rootIdentity);
                WriteJsonAtomically(journalPath, record);
            }

            string legacyRoot = m_LegacyRoot(area);
            if (!Directory.Exists(legacyRoot))
            {
                record.CanonicalCopiesComplete = true;
                record.LocalCleanupComplete = true;
                WriteJsonAtomically(journalPath, record);
                return RuntimeProjectionResult.Succeeded(GetRuntimePath(area));
            }

            if (record.Items.Count == 0)
            {
                long legacyBytes = 0;
                foreach (string path in Directory.EnumerateFiles(
                    legacyRoot, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relativePath = Path.GetRelativePath(legacyRoot, path)
                        .Replace('\\', '/');
                    string safePath = GetSafeContentPath(legacyRoot, relativePath);
                    if (!string.Equals(
                            Path.GetFullPath(path),
                            safePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException(
                            "Legacy runtime content resolved outside its source directory.");
                    }
                    var file = new FileInfo(path);
                    legacyBytes += file.Length;
                    if (record.Items.Count >= kMaximumItemCount ||
                        legacyBytes > kMaximumAreaBytes)
                    {
                        return RuntimeProjectionResult.Failed(
                            StorageResultCode.Failed,
                            GetRuntimePath(area),
                            $"Legacy {area} content exceeds the runtime-content migration limit.");
                    }
                    if (file.Length > kMaximumFileBytes)
                    {
                        return RuntimeProjectionResult.Failed(
                            StorageResultCode.Failed,
                            GetRuntimePath(area),
                            $"{relativePath} exceeds the {kMaximumFileBytes} byte " +
                            $"runtime-content migration limit.");
                    }
                    record.Items.Add(new MigrationItem
                    {
                        SourceRelativePath = relativePath,
                        SourceSha256 = ComputeFileHash(path, cancellationToken),
                    });
                }
                record.Items.Sort((left, right) =>
                    StringComparer.OrdinalIgnoreCase.Compare(
                        left.SourceRelativePath, right.SourceRelativePath));
                WriteJsonAtomically(journalPath, record);
            }

            var remoteByPath = tree.Entries
                .Where(entry => !entry.IsDirectory)
                .ToDictionary(
                    entry => entry.RelativeDisplayPath,
                    entry => entry,
                    StringComparer.OrdinalIgnoreCase);
            var reservedPaths = new HashSet<string>(
                remoteByPath.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (MigrationItem existing in record.Items)
            {
                if (!string.IsNullOrEmpty(existing.DestinationRelativePath))
                {
                    reservedPaths.Add(existing.DestinationRelativePath);
                }
            }

            foreach (MigrationItem item in record.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.CanonicalCopyComplete)
                {
                    continue;
                }
                string sourcePath = GetSafeContentPath(
                    legacyRoot, item.SourceRelativePath);
                if (!File.Exists(sourcePath))
                {
                    return RuntimeProjectionResult.Failed(
                        StorageResultCode.NotFound,
                        GetRuntimePath(area),
                        $"Legacy runtime content disappeared during migration: " +
                        $"{item.SourceRelativePath}");
                }
                string currentHash = ComputeFileHash(sourcePath, cancellationToken);
                if (!string.Equals(
                        currentHash, item.SourceSha256, StringComparison.Ordinal))
                {
                    return RuntimeProjectionResult.Failed(
                        StorageResultCode.Failed,
                        GetRuntimePath(area),
                        $"Legacy runtime content changed during migration: " +
                        $"{item.SourceRelativePath}");
                }

                string destinationPath = item.SourceRelativePath;
                if (!string.IsNullOrEmpty(item.DestinationRelativePath) &&
                    remoteByPath.TryGetValue(
                        item.DestinationRelativePath, out StorageDocument recordedRemote))
                {
                    string recordedHash = ComputeDocumentHash(
                        recordedRemote, cancellationToken);
                    if (string.Equals(
                            recordedHash, item.SourceSha256, StringComparison.Ordinal))
                    {
                        item.CanonicalCopyComplete = true;
                        WriteJsonAtomically(journalPath, record);
                        continue;
                    }
                }
                if (remoteByPath.TryGetValue(
                        item.SourceRelativePath, out StorageDocument remote))
                {
                    string remoteHash = ComputeDocumentHash(remote, cancellationToken);
                    if (string.Equals(
                            remoteHash, item.SourceSha256, StringComparison.Ordinal))
                    {
                        item.DestinationRelativePath = item.SourceRelativePath;
                        item.CanonicalCopyComplete = true;
                        WriteJsonAtomically(journalPath, record);
                        continue;
                    }
                    destinationPath = GetConflictPath(
                        item.SourceRelativePath,
                        record.StartedUtc,
                        reservedPaths);
                }

                item.DestinationRelativePath = destinationPath;
                reservedPaths.Add(destinationPath);
                WriteJsonAtomically(journalPath, record);
                RuntimeProjectionResult publication = PublishLegacyFile(
                    area,
                    sourcePath,
                    item,
                    cancellationToken);
                if (!publication.Success)
                {
                    return publication;
                }
                item.CanonicalCopyComplete = true;
                WriteJsonAtomically(journalPath, record);
            }

            record.CanonicalCopiesComplete = true;
            record.LocalCleanupComplete = record.Items.Count == 0;
            WriteJsonAtomically(journalPath, record);
            Debug.Log(
                $"SAF_MIGRATION {area} preserved {record.Items.Count} legacy file(s).");
            return RuntimeProjectionResult.Succeeded(GetRuntimePath(area));
        }

        private RuntimeProjectionResult PublishLegacyFile(
            StorageArea area,
            string sourcePath,
            MigrationItem item,
            CancellationToken cancellationToken)
        {
            try
            {
                using (IStorageWriteTransaction transaction = m_Backend.BeginWrite(
                    area,
                    item.DestinationRelativePath,
                    StorageMimeTypes.ForPath(item.DestinationRelativePath),
                    cancellationToken))
                {
                    string copiedHash;
                    using (Stream destination = transaction.OpenWrite())
                    {
                        copiedHash = CopyFileAndHash(
                            sourcePath, destination, cancellationToken);
                    }
                    if (!string.Equals(
                            copiedHash, item.SourceSha256, StringComparison.Ordinal))
                    {
                        transaction.Rollback();
                        return RuntimeProjectionResult.Failed(
                            StorageResultCode.Failed,
                            GetRuntimePath(area),
                            $"Legacy runtime content changed while being published: " +
                            $"{item.SourceRelativePath}");
                    }
                    StorageMutationResult commit = transaction.Commit();
                    if (!commit.Success)
                    {
                        return RuntimeProjectionResult.Failed(
                            commit.Code, GetRuntimePath(area), commit.Error);
                    }
                }
                return RuntimeProjectionResult.Succeeded(GetRuntimePath(area));
            }
            catch (OperationCanceledException e)
            {
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.Cancelled, GetRuntimePath(area), e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.PermissionDenied, GetRuntimePath(area), e.Message);
            }
            catch (Exception e) when (
                e is IOException ||
                e is ArgumentException ||
                e is CryptographicException)
            {
                return RuntimeProjectionResult.Failed(
                    StorageResultCode.Failed, GetRuntimePath(area), e.Message);
            }
        }

        private void CleanupMigratedLocalContent(
            StorageArea area, string areaRoot, MigrationRecord record)
        {
            if (record == null ||
                !record.CanonicalCopiesComplete ||
                record.LocalCleanupComplete)
            {
                return;
            }
            string legacyRoot = m_LegacyRoot(area);
            foreach (MigrationItem item in record.Items)
            {
                if (item.LocalCleanupComplete)
                {
                    continue;
                }
                string sourcePath = GetSafeContentPath(
                    legacyRoot, item.SourceRelativePath);
                try
                {
                    if (File.Exists(sourcePath) &&
                        string.Equals(
                            ComputeFileHash(sourcePath, CancellationToken.None),
                            item.SourceSha256,
                            StringComparison.Ordinal))
                    {
                        File.Delete(sourcePath);
                    }
                    item.LocalCleanupComplete = !File.Exists(sourcePath);
                }
                catch (Exception e) when (
                    e is IOException ||
                    e is UnauthorizedAccessException ||
                    e is CryptographicException)
                {
                    Debug.LogWarning(
                        $"SAF_MIGRATION {area} local cleanup pending for " +
                        $"{item.SourceRelativePath}: {e.Message}");
                }
            }
            record.LocalCleanupComplete =
                record.Items.All(item => item.LocalCleanupComplete);
            try
            {
                WriteJsonAtomically(GetMigrationJournalPath(areaRoot), record);
            }
            catch (Exception e) when (
                e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                Debug.LogWarning(
                    $"SAF_MIGRATION {area} cleanup state remains pending: {e.Message}");
            }
        }

        private static MigrationRecord CreateMigrationRecord(
            StorageArea area, string rootIdentity)
        {
            return new MigrationRecord
            {
                Version = kMigrationVersion,
                RootNamespace = GetRootNamespace(rootIdentity),
                Area = area.ToString(),
                StartedUtc = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"),
            };
        }

        private static MigrationRecord ReadMigrationRecord(string areaRoot)
        {
            string path = GetMigrationJournalPath(areaRoot);
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<MigrationRecord>(
                    File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException || e is JsonException)
            {
                Debug.LogWarning(
                    $"SAF_MIGRATION Could not read migration record: {e.Message}");
                return new MigrationRecord { Version = -1 };
            }
        }

        private static bool IsValidMigrationRecord(
            MigrationRecord record, string rootIdentity, StorageArea area)
        {
            return record != null &&
                record.Version == kMigrationVersion &&
                record.RootNamespace == GetRootNamespace(rootIdentity) &&
                record.Area == area.ToString() &&
                !string.IsNullOrEmpty(record.StartedUtc) &&
                record.Items != null;
        }

        private static string GetMigrationJournalPath(string areaRoot)
        {
            return Path.Combine(areaRoot, "migration.json");
        }

        private string ComputeDocumentHash(
            StorageDocument document, CancellationToken cancellationToken)
        {
            using (Stream stream = m_Backend.OpenRead(
                document.DocumentId, requireSeekable: false, cancellationToken))
            {
                return ComputeStreamHash(stream, cancellationToken);
            }
        }

        private static string ComputeFileHash(
            string path, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ComputeStreamHash(stream, cancellationToken);
            }
        }

        private static string ComputeStreamHash(
            Stream stream, CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[64 * 1024];
            using (SHA256 sha256 = SHA256.Create())
            {
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(sha256.Hash);
            }
        }

        private static string CopyFileAndHash(
            string sourcePath,
            Stream destination,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[64 * 1024];
            using (var source = new FileStream(
                sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (SHA256 sha256 = SHA256.Create())
            {
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    destination.Write(buffer, 0, read);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                destination.Flush();
                return ToHex(sha256.Hash);
            }
        }

        private static string GetConflictPath(
            string sourceRelativePath,
            string startedUtc,
            HashSet<string> reservedPaths)
        {
            string directory = GetLogicalDirectory(sourceRelativePath);
            string fileName = Path.GetFileName(sourceRelativePath);
            string extension = Path.GetExtension(fileName);
            string stem = Path.GetFileNameWithoutExtension(fileName);
            string baseName = $"{stem}.local-recovered-{startedUtc}";
            for (int suffix = 0; suffix < 10000; ++suffix)
            {
                string candidateName = suffix == 0
                    ? $"{baseName}{extension}"
                    : $"{baseName}-{suffix}{extension}";
                string candidate = string.IsNullOrEmpty(directory)
                    ? candidateName
                    : $"{directory}/{candidateName}";
                if (!reservedPaths.Contains(candidate))
                {
                    return candidate;
                }
            }
            throw new IOException(
                $"Could not reserve a migration conflict name for {sourceRelativePath}.");
        }

        private static string GetLogicalDirectory(string relativePath)
        {
            int separator = relativePath.LastIndexOf('/');
            return separator < 0 ? "" : relativePath.Substring(0, separator);
        }

        private ProjectionEntry CopyDocument(
            StorageDocument document,
            string destination,
            ref long totalBytes,
            CancellationToken cancellationToken)
        {
            long fileBytes = 0;
            byte[] buffer = new byte[64 * 1024];
            using (Stream input = m_Backend.OpenRead(
                document.DocumentId, requireSeekable: false, cancellationToken))
            using (var output = new FileStream(
                destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (SHA256 sha256 = SHA256.Create())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fileBytes += read;
                    totalBytes += read;
                    if (fileBytes > kMaximumFileBytes)
                    {
                        throw new IOException(
                            $"{document.RelativeDisplayPath} exceeds the " +
                            $"{kMaximumFileBytes} byte runtime-content file limit.");
                    }
                    if (totalBytes > kMaximumAreaBytes)
                    {
                        throw new IOException(
                            $"Runtime content exceeds the {kMaximumAreaBytes} byte area limit.");
                    }
                    sha256.TransformBlock(buffer, 0, read, null, 0);
                    output.Write(buffer, 0, read);
                }
                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                output.Flush(flushToDisk: true);
                if (document.LastModified.HasValue)
                {
                    File.SetLastWriteTime(destination, document.LastModified.Value);
                }
                return new ProjectionEntry
                {
                    RelativePath = document.RelativeDisplayPath,
                    DocumentId = document.DocumentId.Value,
                    Size = fileBytes,
                    LastModifiedTicks = document.LastModified?.ToUniversalTime().Ticks,
                    Sha256 = ToHex(sha256.Hash),
                };
            }
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private bool RootMatches(string rootIdentity)
        {
            return m_Backend.IsReady &&
                string.Equals(
                    rootIdentity, m_Backend.RootIdentity, StringComparison.Ordinal);
        }

        private string GetAreaRoot(string rootIdentity, StorageArea area)
        {
            return Path.Combine(
                m_BasePath, GetRootNamespace(rootIdentity), area.ToString());
        }

        private string GetUnavailablePath(StorageArea area)
        {
            string path = Path.Combine(m_BasePath, "Unavailable", area.ToString());
            Directory.CreateDirectory(path);
            return path;
        }

        private static string GetGenerationRoot(string areaRoot, string generation)
        {
            return Path.Combine(areaRoot, "generations", generation);
        }

        private static string GetGenerationContentPath(string areaRoot, string generation)
        {
            return Path.Combine(
                GetGenerationRoot(areaRoot, generation), kContentDirectoryName);
        }

        private static string GetSafeContentPath(string contentRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Runtime content path must be relative.");
            }
            string fullRoot = Path.GetFullPath(contentRoot);
            string destination = Path.GetFullPath(Path.Combine(
                fullRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = fullRoot.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Runtime content path escapes its projection.");
            }
            return destination;
        }

        private static string GetRootNamespace(string rootIdentity)
        {
            return SafTransactionJournal.GetRootNamespaceId(rootIdentity);
        }

        private static ProjectionPointer ReadPointer(string areaRoot)
        {
            string path = Path.Combine(areaRoot, kPointerFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<ProjectionPointer>(
                    File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException || e is JsonException)
            {
                Debug.LogWarning(
                    $"SAF_PROJECTION Could not read current generation pointer: {e.Message}");
                return null;
            }
        }

        private static bool IsValidPointer(
            ProjectionPointer pointer, string rootIdentity, StorageArea area)
        {
            return pointer != null &&
                pointer.Version == kManifestVersion &&
                pointer.RootNamespace == GetRootNamespace(rootIdentity) &&
                pointer.Area == area.ToString() &&
                !string.IsNullOrEmpty(pointer.Generation) &&
                pointer.Generation.Length == 32 &&
                pointer.Generation.All(character =>
                    (character >= '0' && character <= '9') ||
                    (character >= 'a' && character <= 'f'));
        }

        private static void WriteJsonAtomically<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = $"{path}.obtmp-{Guid.NewGuid():N}";
            try
            {
                File.WriteAllText(
                    temporary, JsonConvert.SerializeObject(value, Formatting.Indented));
                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        private static void CleanupOldGenerations(
            string areaRoot, string currentGeneration, string previousGeneration)
        {
            string generationsRoot = Path.Combine(areaRoot, "generations");
            if (!Directory.Exists(generationsRoot))
            {
                return;
            }
            foreach (string directory in Directory.EnumerateDirectories(generationsRoot))
            {
                string generation = Path.GetFileName(directory);
                if (generation == currentGeneration || generation == previousGeneration)
                {
                    continue;
                }
                string manifestPath = Path.Combine(directory, kManifestFileName);
                ProjectionManifest manifest = ReadManifest(manifestPath);
                if (manifest == null ||
                    manifest.Version != kManifestVersion ||
                    manifest.Generation != generation)
                {
                    continue;
                }
                DeleteOwnedGeneration(directory);
            }
        }

        private static ProjectionManifest ReadManifest(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<ProjectionManifest>(
                    File.ReadAllText(path));
            }
            catch (Exception e) when (e is IOException || e is JsonException)
            {
                Debug.LogWarning(
                    $"SAF_PROJECTION Could not read generation manifest: {e.Message}");
                return null;
            }
        }

        private static void DeleteOwnedGeneration(string generationRoot)
        {
            if (string.IsNullOrEmpty(generationRoot) ||
                !Directory.Exists(generationRoot))
            {
                return;
            }
            try
            {
                Directory.Delete(generationRoot, recursive: true);
            }
            catch (IOException)
            {
                // A runtime loader may still have a file open in the old generation.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup will be retried after a later successful generation.
            }
        }

        private static readonly StorageArea[] RuntimeAreas =
        {
            StorageArea.Scripts,
            StorageArea.Plugins,
            StorageArea.Fonts,
        };
    }
}
