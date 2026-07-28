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

    [Serializable]
    internal sealed class SafTransactionRecord
    {
        public int Version = 1;
        public string TransactionId;
        public string Kind = "tilt-replacement";
        public string RootId;
        public string Area;
        public string RelativePath;
        public string TargetDisplayName;
        public string TargetDocumentId;
        public string TemporaryDisplayName;
        public string TemporaryDocumentId;
        public string BackupDisplayName;
        public string BackupDocumentId;
        public string InvalidDisplayName;
        public string State;
        public string CreatedUtc;
        public int AttemptCount;
        public string LastError = "";
    }

    internal static class SafTransactionJournal
    {
        public const int Version = 1;

        public static string GetJournalDirectory(string rootId)
        {
            return Path.Combine(
                GetRecoveryRootDirectory(rootId),
                "journals");
        }

        public static string GetRecoveryRootDirectory(string rootId)
        {
            return Path.Combine(
                Application.persistentDataPath,
                "OpenBrushSafRecovery",
                GetRootNamespaceId(rootId));
        }

        public static string GetRootNamespaceId(string rootId)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] bytes = hash.ComputeHash(
                    Encoding.UTF8.GetBytes(rootId ?? ""));
                var result = new StringBuilder(bytes.Length * 2);
                foreach (byte value in bytes)
                {
                    result.Append(value.ToString("x2"));
                }
                return result.ToString();
            }
        }

        public static string GetJournalPath(SafTransactionRecord record)
        {
            return Path.Combine(
                GetJournalDirectory(record.RootId), $"{record.TransactionId}.json");
        }

        public static void Persist(SafTransactionRecord record)
        {
            string path = GetJournalPath(record);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporaryPath = $"{path}.tmp";
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
            string path = GetJournalPath(record);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static List<SafTransactionRecord> Load(string rootId, out List<string> errors)
        {
            errors = new List<string>();
            var records = new List<SafTransactionRecord>();
            string directory = GetJournalDirectory(rootId);
            if (!Directory.Exists(directory))
            {
                return records;
            }
            foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
            {
                try
                {
                    SafTransactionRecord record =
                        JsonConvert.DeserializeObject<SafTransactionRecord>(
                            File.ReadAllText(path));
                    if (record == null ||
                        record.Version != Version ||
                        string.IsNullOrEmpty(record.TransactionId) ||
                        record.RootId != rootId)
                    {
                        errors.Add($"Unsupported or malformed SAF journal: {path}");
                        continue;
                    }
                    records.Add(record);
                }
                catch (Exception e)
                {
                    errors.Add($"Failed to read SAF journal {path}: {e.Message}");
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
        private FileStream m_Stream;
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
                Kind = m_MimeType == TiltFile.TILT_MIME_TYPE
                    ? "tilt-replacement"
                    : "file-replacement",
                RootId = rootId,
                Area = area.ToString(),
                RelativePath = relativePath.Replace('\\', '/'),
                TargetDisplayName = targetName,
                TargetDocumentId = targetDocumentId.Value,
                TemporaryDisplayName = $".ob-{transactionId}.tmp",
                BackupDisplayName = $".ob-{transactionId}.bak",
                InvalidDisplayName = $".ob-{transactionId}.invalid",
                State = SafTransactionState.CreatingTemporary.ToString(),
                CreatedUtc = DateTime.UtcNow.ToString("o"),
            };
            string destinationKey = SafDestinationLocks.GetDestinationKey(
                rootId, area, m_Record.RelativePath);
            m_DestinationLock = SafDestinationLocks.Acquire(
                destinationKey, cancellationToken);
            try
            {
                EnsureSelectedRoot();
                FindExistingTarget(cancellationToken);
                SafTransactionJournal.Persist(m_Record);
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

            EnsureSelectedRoot();
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
            if (!IsSelectedRootCurrent())
            {
                CloseStream();
                StorageMutationResult cleanup =
                    AndroidSafStorage.DeleteDocument(temporaryId);
                if (cleanup.Success || cleanup.Code == StorageResultCode.NotFound)
                {
                    m_Record.TemporaryDocumentId = null;
                }
                Fail(
                    SafTransactionState.RollbackRequired,
                    cleanup.Success || cleanup.Code == StorageResultCode.NotFound
                        ? "The selected Open Brush folder changed while creating a temporary document."
                        : $"The selected Open Brush folder changed and temporary cleanup failed: " +
                          $"{cleanup.Error}");
                throw new IOException(m_Record.LastError);
            }
            m_Record.State = SafTransactionState.WritingTemporary.ToString();
            SafTransactionJournal.Persist(m_Record);
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
                SafTransactionJournal.Delete(m_Record);
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
                SafTransactionJournal.Delete(m_Record);
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
            EnsureSelectedRoot();
            StorageDirectoryResult listing = AndroidSafStorage.QueryDirectory(
                CombineProviderDirectory());
            EnsureSelectedRoot();
            if (listing.Code == StorageResultCode.NotFound)
            {
                return;
            }
            if (!listing.Success)
            {
                throw new IOException(listing.Error);
            }
            if (TargetDocumentId.IsValid)
            {
                StorageDocument target = listing.Documents.FirstOrDefault(document =>
                    document.DocumentId.Equals(TargetDocumentId));
                if (target == null)
                {
                    throw new IOException(
                        "The SAF document selected for overwrite no longer exists.");
                }
                if (!string.Equals(
                        target.DisplayName,
                        m_Record.TargetDisplayName,
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        "The SAF document selected for overwrite was renamed externally.");
                }
                if (listing.Documents.Any(document =>
                        !document.DocumentId.Equals(TargetDocumentId) &&
                        string.Equals(
                            document.DisplayName,
                            m_Record.TargetDisplayName,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    throw new IOException(
                        "Multiple SAF documents share the overwrite destination name.");
                }
                return;
            }

            List<StorageDocument> matches = listing.Documents.Where(document =>
                string.Equals(
                    document.DisplayName,
                    m_Record.TargetDisplayName,
                    StringComparison.OrdinalIgnoreCase)).ToList();
            if (matches.Count > 1)
            {
                throw new IOException(
                    "Multiple SAF documents share the requested destination name.");
            }
            if (matches.Count == 1)
            {
                m_Record.TargetDocumentId = matches[0].DocumentId.Value;
            }
        }

        private Stream OpenTemporaryRead()
        {
            if (!AndroidSafStorage.TryOpenSeekableReadStream(
                    TemporaryDocumentId, out FileStream stream, out string error))
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

        private void Transition(SafTransactionState state)
        {
            m_Record.State = state.ToString();
            m_Record.LastError = "";
            SafTransactionJournal.Persist(m_Record);
            Debug.Log($"SAF_TRANSACTION {m_Record.TransactionId} {state}");
        }

        private StorageMutationResult CommitFailed(string error)
        {
            Fail(
                m_NamespaceMutationStarted
                    ? SafTransactionState.RollbackRequired
                    : SafTransactionState.TemporaryComplete,
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
            m_Record.State = state.ToString();
            m_Record.AttemptCount++;
            m_Record.LastError = error ?? "";
            try
            {
                SafTransactionJournal.Persist(m_Record);
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                Debug.LogError(
                    $"SAF_TRANSACTION {m_Record.TransactionId} could not update its " +
                    $"recovery journal: {e.Message}");
            }
            Debug.LogWarning(
                $"SAF_TRANSACTION {m_Record.TransactionId} {state}: " +
                $"{m_Record.LastError}");
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
                // The archive writer's caller already closed and flushed the descriptor.
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

        private bool IsSelectedRootCurrent()
        {
            return string.Equals(
                m_Record.RootId,
                AndroidSafStorage.GetSelectedRootIdentity(),
                StringComparison.Ordinal);
        }

        private void EnsureSelectedRoot()
        {
            if (!IsSelectedRootCurrent())
            {
                throw new IOException(
                    "The selected Open Brush folder changed during the storage transaction.");
            }
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
