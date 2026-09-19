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
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    internal enum SafTransactionState
    {
        CreatingTemporary,
        WritingTemporary,
        TemporaryComplete,
        OriginalBackedUp,
        ReplacementInstalled,
        BackupCleanupPending,
        Complete,
        RollbackRequired,
    }

    /// In-memory bookkeeping for one write transaction, and for one interrupted write that
    /// recovery reconstructs from the sidecars it left behind. It was a serialized journal
    /// record once, which is why the fields that survive are the ones two different callers
    /// both need - the names and ids of the four documents a commit juggles. The schema that
    /// only mattered across process restarts is gone: a version number, the root the record
    /// belonged to, when it was created, how many attempts it had survived, and the state it
    /// had reached.
    internal sealed class SafTransactionRecord
    {
        public int MarkerVersion = 1;
        public string TransactionId;
        public string RootId;
        public string Kind = "tilt-replacement";
        public StorageArea Area;
        public string RelativePath;
        public string TargetDisplayName;
        public string TargetDocumentId;
        public string TemporaryDisplayName;
        public string TemporaryDocumentId;
        public bool TemporaryWriteCompleted;
        public string BackupDisplayName;
        public string BackupDocumentId;
        public string InvalidDisplayName;
    }

    /// Where app-private state for a root lives, and how to fold an arbitrary id into a path
    /// segment. Transaction markers and publication recovery records share this namespace.
    internal static class SafPrivatePaths
    {
        public static string GetRecoveryRootDirectory(string rootId)
        {
            return Path.Combine(
                OpenBrushStorage.PersistentDataPath,
                "OpenBrushSafRecovery",
                GetStableId(rootId));
        }

        /// A stable, filesystem-safe id for an arbitrary string. Named for roots originally,
        /// but the Drive ledger and conflict-copy naming use it to fold account ids, device
        /// folders and Drive file ids into path segments too.
        public static string GetStableId(string source)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(
                    Encoding.UTF8.GetBytes(source ?? ""));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes)
                {
                    result.Append(value.ToString("x2"));
                }
                return result.ToString();
            }
        }





    }

    /// A single app-private intent marker distinguishes transaction sidecars from user files
    /// that happen to use a reserved-looking suffix. Unlike the former state journal, this is
    /// written once before shared storage is touched rather than fsynced at every rename step.
    internal static class SafTransactionMarker
    {
        private const int kVersion = 1;

        private static string GetDirectory(string rootId) => Path.Combine(
            SafPrivatePaths.GetRecoveryRootDirectory(rootId), "transactions");

        private static string GetPath(SafTransactionRecord record) => Path.Combine(
            GetDirectory(record.RootId), $"{record.TransactionId}.json");

        public static void Persist(SafTransactionRecord record)
        {
            string path = GetPath(record);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + ".tmp";
            byte[] json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(
                JsonConvert.SerializeObject(record, Formatting.Indented));
            using (var stream = new FileStream(
                temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(json, 0, json.Length);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }

        public static void Delete(SafTransactionRecord record)
        {
            string path = GetPath(record);
            if (File.Exists(path)) { File.Delete(path); }
        }

        public static List<SafTransactionRecord> Load(
            string rootId, out List<string> errors)
        {
            errors = new List<string>();
            var records = new List<SafTransactionRecord>();
            string directory = GetDirectory(rootId);
            if (!Directory.Exists(directory)) { return records; }

            foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
            {
                try
                {
                    SafTransactionRecord record =
                        JsonConvert.DeserializeObject<SafTransactionRecord>(
                            File.ReadAllText(path));
                    if (record == null ||
                        record.MarkerVersion != kVersion ||
                        string.IsNullOrEmpty(record.TransactionId) ||
                        !string.Equals(record.RootId, rootId, StringComparison.Ordinal) ||
                        string.IsNullOrEmpty(record.RelativePath) ||
                        string.IsNullOrEmpty(record.TargetDisplayName))
                    {
                        errors.Add($"Unsupported or malformed SAF transaction marker: {path}");
                        continue;
                    }
                    records.Add(record);
                }
                catch (Exception e) when (
                    e is IOException ||
                    e is UnauthorizedAccessException ||
                    e is JsonException)
                {
                    errors.Add($"Failed to read SAF transaction marker {path}: {e.Message}");
                }
            }
            return records;
        }
    }

    internal static class SafDestinationLocks
    {
        private sealed class Lease : IDisposable
        {
            private SemaphoreSlim m_Semaphore;

            public Lease(SemaphoreSlim semaphore)
            {
                m_Semaphore = semaphore;
            }

            public void Dispose()
            {
                SemaphoreSlim semaphore = Interlocked.Exchange(ref m_Semaphore, null);
                semaphore?.Release();
            }
        }

        private sealed class CompositeLease : IDisposable
        {
            private List<IDisposable> m_Leases;

            public CompositeLease(List<IDisposable> leases)
            {
                m_Leases = leases;
            }

            public void Dispose()
            {
                List<IDisposable> leases = Interlocked.Exchange(ref m_Leases, null);
                if (leases == null)
                {
                    return;
                }
                for (int i = leases.Count - 1; i >= 0; --i)
                {
                    leases[i].Dispose();
                }
            }
        }

        private static readonly object sm_Gate = new object();
        private static readonly Dictionary<string, SemaphoreSlim> sm_Locks =
            new Dictionary<string, SemaphoreSlim>(StringComparer.OrdinalIgnoreCase);

        public static IDisposable Acquire(string key, CancellationToken cancellationToken)
        {
            SemaphoreSlim semaphore;
            lock (sm_Gate)
            {
                if (!sm_Locks.TryGetValue(key, out semaphore))
                {
                    semaphore = new SemaphoreSlim(1, 1);
                    sm_Locks.Add(key, semaphore);
                }
            }
            semaphore.Wait(cancellationToken);
            return new Lease(semaphore);
        }

        public static string GetDestinationKey(
            string rootId, StorageArea area, string relativePath)
        {
            return $"{rootId}\n{area}\n{relativePath}".ToLowerInvariant();
        }

        public static IDisposable AcquireMany(
            IEnumerable<string> keys, CancellationToken cancellationToken)
        {
            var orderedKeys = new SortedSet<string>(
                keys, StringComparer.OrdinalIgnoreCase);
            var leases = new List<IDisposable>(orderedKeys.Count);
            try
            {
                foreach (string key in orderedKeys)
                {
                    leases.Add(Acquire(key, cancellationToken));
                }
                return new CompositeLease(leases);
            }
            catch
            {
                for (int i = leases.Count - 1; i >= 0; --i)
                {
                    leases[i].Dispose();
                }
                throw;
            }
        }
    }

    internal sealed class SafFileWriteTransaction : IStorageWriteTransaction
    {
        private readonly StorageArea m_Area;
        private readonly string m_RelativeDirectory;
        private readonly string m_MimeType;
        private readonly SafTransactionRecord m_Record;
        private IDisposable m_DestinationLock;
        private Stream m_Stream;
        private bool m_Finished;
        private bool m_NamespaceMutationStarted;

        public StorageDocumentId TargetDocumentId =>
            new StorageDocumentId(m_Record.TargetDocumentId);
        public StorageDocumentId TemporaryDocumentId =>
            new StorageDocumentId(m_Record.TemporaryDocumentId);

        public SafFileWriteTransaction(
            StorageArea area,
            string relativePath,
            string mimeType,
            StorageDocumentId targetDocumentId,
            CancellationToken cancellationToken)
        {
            m_Area = area;
            m_MimeType = string.IsNullOrEmpty(mimeType)
                ? "application/octet-stream"
                : mimeType;
            SplitRelativePath(relativePath, out m_RelativeDirectory, out string targetName);
            string rootId = AndroidSafStorage.GetSelectedRootIdentity();
            if (string.IsNullOrEmpty(rootId))
            {
                throw new IOException("Open Brush shared folder is unavailable.");
            }

            string transactionId = Guid.NewGuid().ToString("N");
            m_Record = new SafTransactionRecord
            {
                TransactionId = transactionId,
                RootId = rootId,
                Kind = m_MimeType == TiltFile.TILT_MIME_TYPE
                    ? "tilt-replacement"
                    : "file-replacement",
                Area = area,
                RelativePath = relativePath.Replace('\\', '/'),
                TargetDisplayName = targetName,
                TargetDocumentId = targetDocumentId.Value,
                // Named after the target rather than the transaction, so a sidecar left behind
                // by an interrupted save says which document it belongs to. Recovery can then
                // work from the directory alone.
                TemporaryDisplayName = $"{targetName}.ob-tmp",
                BackupDisplayName = $"{targetName}.ob-bak",
                InvalidDisplayName = $"{targetName}.ob-invalid",
            };
            string destinationKey = SafDestinationLocks.GetDestinationKey(
                rootId, area, m_Record.RelativePath);
            m_DestinationLock = SafDestinationLocks.Acquire(
                destinationKey, cancellationToken);
            try
            {
                FindExistingTarget(cancellationToken);
                SafTransactionMarker.Persist(m_Record);
                Debug.Log(
                    $"SAF_TRANSACTION {m_Record.TransactionId} " +
                    $"{SafTransactionState.CreatingTemporary}");
            }
            catch
            {
                ReleaseLock();
                throw;
            }
        }

        public Stream OpenWrite()
        {
            if (m_Finished)
            {
                throw new InvalidOperationException("Storage transaction is already finished.");
            }
            if (m_Stream != null || !string.IsNullOrEmpty(m_Record.TemporaryDocumentId))
            {
                throw new InvalidOperationException("Storage transaction stream is already open.");
            }

            string providerDirectory = CombineProviderDirectory();
            if (!AndroidSafStorage.TryCreateNamedFileStream(
                    providerDirectory,
                    m_Record.TemporaryDisplayName,
                    m_MimeType,
                    out m_Stream,
                    out StorageDocumentId temporaryId,
                    out string error))
            {
                Fail(SafTransactionState.RollbackRequired, error);
                throw new IOException(error);
            }
            m_Record.TemporaryDocumentId = temporaryId.Value;
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
                if (!TemporaryDocumentId.IsValid)
                {
                    throw new IOException("Storage transaction has no temporary document.");
                }
                using (Stream validationStream = OpenTemporaryRead())
                {
                    if (!ValidatePayload(validationStream))
                    {
                        throw new IOException("Completed temporary document is invalid.");
                    }
                }

                m_Record.TemporaryWriteCompleted = true;
                Transition(SafTransactionState.TemporaryComplete);
                if (TargetDocumentId.IsValid)
                {
                    m_NamespaceMutationStarted = true;
                    StorageMutationResult backup = AndroidSafStorage.RenameDocument(
                        TargetDocumentId, m_Record.BackupDisplayName);
                    if (!backup.Success)
                    {
                        return CommitFailed(backup.Error);
                    }
                    m_Record.BackupDocumentId = backup.DocumentId.Value;
                    Transition(SafTransactionState.OriginalBackedUp);
                }

                m_NamespaceMutationStarted = true;
                StorageMutationResult install = AndroidSafStorage.RenameDocument(
                    TemporaryDocumentId, m_Record.TargetDisplayName);
                if (!install.Success)
                {
                    return CommitFailed(install.Error);
                }
                m_Record.TargetDocumentId = install.DocumentId.Value;
                m_Record.TemporaryDocumentId = null;
                Transition(SafTransactionState.ReplacementInstalled);

                if (!string.IsNullOrEmpty(m_Record.BackupDocumentId))
                {
                    Transition(SafTransactionState.BackupCleanupPending);
                    StorageMutationResult cleanup = AndroidSafStorage.DeleteDocument(
                        new StorageDocumentId(m_Record.BackupDocumentId));
                    if (!cleanup.Success)
                    {
                        m_Finished = true;
                        ReleaseLock();
                        Debug.LogWarning(
                            $"SAF_STORAGE Replacement committed; backup cleanup pending: " +
                            $"{cleanup.Error}");
                        return new StorageMutationResult(
                            StorageResultCode.Success,
                            new StorageDocumentId(m_Record.TargetDocumentId),
                            cleanup.Error);
                    }
                    m_Record.BackupDocumentId = null;
                }

                Transition(SafTransactionState.Complete);
                DeleteMarkerBestEffort();
                m_Finished = true;
                ReleaseLock();
                return new StorageMutationResult(
                    StorageResultCode.Success,
                    new StorageDocumentId(m_Record.TargetDocumentId));
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException ||
                e is InvalidOperationException)
            {
                return CommitFailed(e.Message);
            }
        }

        public void Rollback()
        {
            if (m_Finished)
            {
                return;
            }
            CloseStream();
            if (m_NamespaceMutationStarted)
            {
                Fail(
                    SafTransactionState.RollbackRequired,
                    "Namespace mutation requires startup recovery.");
            }
            else
            {
                if (TemporaryDocumentId.IsValid)
                {
                    StorageMutationResult cleanup =
                        AndroidSafStorage.DeleteDocument(TemporaryDocumentId);
                    if (!cleanup.Success && cleanup.Code != StorageResultCode.NotFound)
                    {
                        Fail(
                            SafTransactionState.RollbackRequired,
                            $"Temporary document cleanup failed: {cleanup.Error}");
                        m_Finished = true;
                        ReleaseLock();
                        return;
                    }
                    m_Record.TemporaryDocumentId = null;
                }
                DeleteMarkerBestEffort();
            }
            m_Finished = true;
            ReleaseLock();
        }

        public void Dispose()
        {
            if (!m_Finished)
            {
                Rollback();
            }
        }

        private void FindExistingTarget(CancellationToken cancellationToken)
        {
            StorageDirectoryResult listing = AndroidSafStorage.QueryDirectory(
                CombineProviderDirectory());
            if (listing.Code == StorageResultCode.NotFound)
            {
                return;
            }
            if (!listing.Success)
            {
                throw new IOException(listing.Error);
            }
            EnsureReservedNamesAvailable(
                listing.Documents,
                m_Record.TemporaryDisplayName,
                m_Record.BackupDisplayName,
                m_Record.InvalidDisplayName);
            m_Record.TargetDocumentId = ResolveFileOverwriteTarget(
                listing.Documents, TargetDocumentId, m_Record.TargetDisplayName).Value;
        }

        internal static void EnsureReservedNamesAvailable(
            IReadOnlyList<StorageDocument> documents,
            params string[] reservedNames)
        {
            StorageDocument collision = documents.FirstOrDefault(document =>
                reservedNames.Any(name => string.Equals(
                    document.DisplayName, name, StringComparison.OrdinalIgnoreCase)));
            if (collision != null)
            {
                throw new IOException(
                    $"A document already occupies the reserved transaction filename " +
                    $"'{collision.DisplayName}'. Rename it before saving this file.");
            }
        }

        internal static StorageDocumentId ResolveFileOverwriteTarget(
            IReadOnlyList<StorageDocument> documents, StorageDocumentId targetDocumentId, string targetName)
        {
            if (targetDocumentId.IsValid)
            {
                StorageDocument target = documents.FirstOrDefault(document =>
                    document.DocumentId.Equals(targetDocumentId));
                if (target == null)
                {
                    throw new IOException(
                        "The SAF document selected for overwrite no longer exists.");
                }
                if (target.IsDirectory)
                {
                    throw new IOException("A SAF directory cannot be overwritten by a file.");
                }
                if (!string.Equals(
                        target.DisplayName,
                        targetName,
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        "The SAF document selected for overwrite was renamed externally.");
                }
                if (documents.Any(document =>
                        !document.DocumentId.Equals(targetDocumentId) &&
                        string.Equals(
                            document.DisplayName,
                            targetName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    throw new IOException(
                        "Multiple SAF documents share the overwrite destination name.");
                }
                return target.DocumentId;
            }

            List<StorageDocument> matches = documents.Where(document =>
                string.Equals(
                    document.DisplayName,
                    targetName,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Any(document => document.IsDirectory))
            {
                throw new IOException("A SAF directory already occupies the requested filename.");
            }
            if (matches.Count > 1)
            {
                throw new IOException(
                    "Multiple SAF documents share the requested destination name.");
            }
            if (matches.Count == 1)
            {
                return matches[0].DocumentId;
            }
            return default;
        }

        private Stream OpenTemporaryRead()
        {
            if (!AndroidSafStorage.TryOpenSeekableReadStream(
                    TemporaryDocumentId, out Stream stream, out string error))
            {
                throw new IOException(error);
            }
            return stream;
        }

        private bool ValidatePayload(Stream stream)
        {
            if (m_Record.Kind == "tilt-replacement")
            {
                return TiltFile.IsArchiveValid(
                    stream,
                    m_Record.TemporaryDisplayName,
                    testData: false);
            }
            if (!stream.CanRead)
            {
                return false;
            }
            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.End);
                stream.Seek(0, SeekOrigin.Begin);
            }
            return true;
        }

        private string CombineProviderDirectory()
        {
            string root = SafUserStorageBackend.GetAreaPath(m_Area);
            return string.IsNullOrEmpty(m_RelativeDirectory)
                ? root
                : $"{root}/{m_RelativeDirectory}";
        }

        /// The state is a log label rather than stored state: nothing reads it back, because
        /// recovery works from the sidecars in shared storage rather than from anything this
        /// process remembers.
        private void Transition(SafTransactionState state)
        {
            Debug.Log($"SAF_TRANSACTION {m_Record.TransactionId} {state}");
        }

        private StorageMutationResult CommitFailed(string error)
        {
            Fail(
                m_NamespaceMutationStarted
                    ? SafTransactionState.RollbackRequired
                    : m_Record.TemporaryWriteCompleted
                        ? SafTransactionState.TemporaryComplete
                        : SafTransactionState.WritingTemporary,
                error);
            m_Finished = m_NamespaceMutationStarted;
            if (m_Finished)
            {
                ReleaseLock();
            }
            return new StorageMutationResult(
                StorageResultCode.Failed, TargetDocumentId, error);
        }

        private void Fail(SafTransactionState state, string error)
        {
            Debug.LogWarning(
                $"SAF_TRANSACTION {m_Record.TransactionId} {state}: {error ?? ""}");
        }

        private void DeleteMarkerBestEffort()
        {
            try
            {
                SafTransactionMarker.Delete(m_Record);
            }
            catch (Exception e) when (
                e is IOException || e is UnauthorizedAccessException)
            {
                // A stale marker is safe: startup recovery validates the canonical document,
                // finds no sidecars, and retries this deletion.
                Debug.LogWarning(
                    $"SAF_TRANSACTION Could not remove marker for " +
                    $"{m_Record.TransactionId}: {e.Message}");
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
                // Durable, not merely flushed to the OS. The rename sequence below is about to
                // treat this payload as the good copy, and process death leaves the page cache
                // intact but a power loss does not: without the fsync a torn middle can survive
                // behind an intact zip central directory, which is the corruption recovery would
                // otherwise have to decompress the whole archive to detect. Measured at ~740ms
                // per GiB on a Nothing Phone (3a), so roughly 150ms for a 200MB sketch.
                if (m_Stream is ISyncableStream syncable)
                {
                    syncable.FlushToDisk();
                }
                else
                {
                    m_Stream.Flush();
                }
                m_Stream.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Disposing a writable SafDocumentStream flushes it to disk before closing its
                // channel, so the caller has already made the temporary document durable.
            }
            finally
            {
                m_Stream = null;
            }
        }

        private void ReleaseLock()
        {
            m_DestinationLock?.Dispose();
            m_DestinationLock = null;
        }

        private static void SplitRelativePath(
            string relativePath, out string directory, out string fileName)
        {
            string normalized = (relativePath ?? "").Replace('\\', '/');
            if (Path.IsPathRooted(normalized) ||
                normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.EndsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Storage destination must be a relative file path.",
                    nameof(relativePath));
            }
            string[] segments = normalized.Split('/');
            if (segments.Length == 0)
            {
                throw new ArgumentException("Storage destination is empty.", nameof(relativePath));
            }
            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArgumentException("Storage destination is invalid.", nameof(relativePath));
                }
            }
            fileName = segments[segments.Length - 1];
            directory = segments.Length == 1
                ? ""
                : string.Join("/", segments, 0, segments.Length - 1);
        }
    }
}
