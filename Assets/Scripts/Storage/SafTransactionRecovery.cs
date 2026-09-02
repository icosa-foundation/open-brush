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
    public sealed class SafRecoveryReport
    {
        public int Recovered { get; internal set; }
        public int Pending { get; internal set; }
        public bool AutosaveRecovered { get; internal set; }
        public List<string> Errors { get; } = new List<string>();
    }

    public static class SafTransactionRecovery
    {
        public static SafRecoveryReport RecoverAll(
            IUserStorageBackend backend,
            CancellationToken cancellationToken,
            string rootIdOverride = null)
        {
            var report = new SafRecoveryReport();
            if (backend == null ||
                backend.Kind != StorageBackendKind.StorageAccessFramework ||
                !backend.IsReady)
            {
                report.Errors.Add("SAF backend is unavailable for transaction recovery.");
                return report;
            }

            string rootId = rootIdOverride ?? backend.RootIdentity;
            if (!string.Equals(rootId, backend.RootIdentity, StringComparison.Ordinal))
            {
                report.Errors.Add(
                    "SAF recovery records cannot be applied to a different selected root.");
                return report;
            }
            List<SafTransactionRecord> records =
                SafTransactionJournal.Load(rootId, out List<string> journalErrors);
            report.Errors.AddRange(journalErrors);
            foreach (SafTransactionRecord record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Enum.TryParse(record.Area, out StorageArea area))
                {
                    MarkPending(record, $"Unknown storage area '{record.Area}'.", report);
                    continue;
                }
                using (SafDestinationLocks.Acquire(
                    SafDestinationLocks.GetDestinationKey(
                        rootId, area, record.RelativePath),
                    cancellationToken))
                {
                    if (RecoverRecord(backend, area, record, report, cancellationToken))
                    {
                        report.Recovered++;
                    }
                    else
                    {
                        report.Pending++;
                    }
                }
            }
            return report;
        }

        private static bool RecoverRecord(
            IUserStorageBackend backend,
            StorageArea area,
            SafTransactionRecord record,
            SafRecoveryReport report,
            CancellationToken cancellationToken)
        {
            SplitRelativePath(record.RelativePath, out string directory, out _);
            StorageDirectoryResult listing = backend.List(
                area, directory, cancellationToken);
            if (!listing.Success)
            {
                MarkPending(record, listing.Error, report);
                return false;
            }

            string invalidName = string.IsNullOrEmpty(record.InvalidDisplayName)
                ? $".ob-{record.TransactionId}.invalid"
                : record.InvalidDisplayName;
            StorageDocument canonical = Find(listing, record.TargetDisplayName);
            StorageDocument temporary = Find(listing, record.TemporaryDisplayName);
            StorageDocument backup = Find(listing, record.BackupDisplayName);
            StorageDocument invalid = Find(listing, invalidName);

            if (IsValidDocument(backend, canonical, record.Kind, cancellationToken))
            {
                return CompleteWithCanonical(
                    backend, record, canonical, temporary, backup, invalid, report,
                    cancellationToken);
            }

            if (IsValidDocument(backend, backup, record.Kind, cancellationToken))
            {
                return RestoreDocument(
                    backend, record, canonical, backup, temporary, invalid, invalidName,
                    report, cancellationToken);
            }

            if (IsValidDocument(backend, temporary, record.Kind, cancellationToken))
            {
                return RestoreDocument(
                    backend, record, canonical, temporary, backup, invalid, invalidName,
                    report, cancellationToken);
            }

            MarkPending(
                record,
                "No validated canonical, backup, or temporary document was found.",
                report);
            return false;
        }

        private static bool CompleteWithCanonical(
            IUserStorageBackend backend,
            SafTransactionRecord record,
            StorageDocument canonical,
            StorageDocument temporary,
            StorageDocument backup,
            StorageDocument invalid,
            SafRecoveryReport report,
            CancellationToken cancellationToken)
        {
            if (!DeleteIfPresent(backend, temporary, cancellationToken, out string error) ||
                !DeleteIfPresent(backend, backup, cancellationToken, out error) ||
                !DeleteIfPresent(backend, invalid, cancellationToken, out error))
            {
                record.State = SafTransactionState.BackupCleanupPending.ToString();
                MarkPending(record, error, report);
                return false;
            }

            record.TargetDocumentId = canonical.DocumentId.Value;
            record.TemporaryDocumentId = null;
            record.BackupDocumentId = null;
            record.State = SafTransactionState.Complete.ToString();
            SafTransactionJournal.Persist(record);
            SafTransactionJournal.Delete(record);
            return true;
        }

        private static bool RestoreDocument(
            IUserStorageBackend backend,
            SafTransactionRecord record,
            StorageDocument canonical,
            StorageDocument source,
            StorageDocument otherReserved,
            StorageDocument existingInvalid,
            string invalidName,
            SafRecoveryReport report,
            CancellationToken cancellationToken)
        {
            StorageDocument invalid = existingInvalid;
            if (canonical != null)
            {
                if (invalid != null)
                {
                    StorageMutationResult removeOldQuarantine = Delete(
                        backend, invalid.DocumentId, cancellationToken);
                    if (!removeOldQuarantine.Success &&
                        removeOldQuarantine.Code != StorageResultCode.NotFound)
                    {
                        MarkPending(record, removeOldQuarantine.Error, report);
                        return false;
                    }
                    invalid = null;
                }
                StorageMutationResult quarantine = Rename(
                    backend, canonical.DocumentId, invalidName, cancellationToken);
                if (!quarantine.Success)
                {
                    MarkPending(record, quarantine.Error, report);
                    return false;
                }
                invalid = new StorageDocument(
                    quarantine.DocumentId,
                    canonical.ParentDocumentId,
                    invalidName,
                    canonical.MimeType,
                    false,
                    canonical.Size,
                    canonical.LastModified,
                    canonical.ProviderFlags,
                    invalidName);
            }

            StorageMutationResult restore = Rename(
                backend, source.DocumentId, record.TargetDisplayName, cancellationToken);
            if (!restore.Success)
            {
                MarkPending(record, restore.Error, report);
                return false;
            }
            var restored = new StorageDocument(
                restore.DocumentId,
                source.ParentDocumentId,
                record.TargetDisplayName,
                source.MimeType,
                false,
                source.Size,
                DateTime.Now,
                source.ProviderFlags,
                record.RelativePath);
            if (!IsValidDocument(backend, restored, record.Kind, cancellationToken))
            {
                MarkPending(record, "Restored SAF document failed validation.", report);
                return false;
            }

            return CompleteWithCanonical(
                backend, record, restored, otherReserved, null, invalid, report,
                cancellationToken);
        }

        private static bool IsValidDocument(
            IUserStorageBackend backend,
            StorageDocument document,
            string transactionKind,
            CancellationToken cancellationToken)
        {
            if (document == null || document.IsDirectory)
            {
                return false;
            }
            try
            {
                using (Stream stream = backend.OpenRead(
                    document.DocumentId, requireSeekable: true, cancellationToken))
                {
                    if (transactionKind == "tilt-replacement" ||
                        transactionKind == "sketch-replacement")
                    {
                        return TiltFile.IsArchiveValid(
                            stream,
                            document.DisplayName,
                            testData: true);
                    }
                    return stream.CanRead;
                }
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException ||
                e is OperationCanceledException)
            {
                return false;
            }
        }

        private static bool DeleteIfPresent(
            IUserStorageBackend backend,
            StorageDocument document,
            CancellationToken cancellationToken,
            out string error)
        {
            error = null;
            if (document == null)
            {
                return true;
            }
            StorageMutationResult result = Delete(
                backend, document.DocumentId, cancellationToken);
            error = result.Error;
            return result.Success || result.Code == StorageResultCode.NotFound;
        }

        private static StorageDocument Find(
            StorageDirectoryResult listing, string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                return null;
            }
            return listing.Documents.FirstOrDefault(document =>
                string.Equals(
                    document.DisplayName, displayName, StringComparison.Ordinal));
        }

        private static StorageMutationResult Rename(
            IUserStorageBackend backend,
            StorageDocumentId documentId,
            string newDisplayName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return backend is SafUserStorageBackend safBackend
                ? safBackend.RenameWithoutLock(documentId, newDisplayName)
                : backend.Rename(documentId, newDisplayName, cancellationToken);
        }

        private static StorageMutationResult Delete(
            IUserStorageBackend backend,
            StorageDocumentId documentId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return backend is SafUserStorageBackend safBackend
                ? safBackend.DeleteWithoutLock(documentId)
                : backend.Delete(documentId, cancellationToken);
        }

        private static void MarkPending(
            SafTransactionRecord record, string error, SafRecoveryReport report)
        {
            record.AttemptCount++;
            record.LastError = error ?? "";
            try
            {
                SafTransactionJournal.Persist(record);
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                record.LastError =
                    $"{record.LastError} Recovery journal update failed: {e.Message}".Trim();
            }
            string message =
                $"SAF_RECOVERY Pending {record.TransactionId}: {record.LastError}";
            report.Errors.Add(message);
            Debug.LogWarning(message);
        }

        private static void SplitRelativePath(
            string relativePath, out string directory, out string fileName)
        {
            string normalized = (relativePath ?? "").Replace('\\', '/').Trim('/');
            int separator = normalized.LastIndexOf('/');
            directory = separator < 0 ? "" : normalized.Substring(0, separator);
            fileName = separator < 0 ? normalized : normalized.Substring(separator + 1);
        }
    }
}
