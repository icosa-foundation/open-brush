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
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace TiltBrush
{
    [Serializable]
    public sealed class SafStagedPath
    {
        public string SourcePath;
        public string DestinationRelativePath;

        public SafStagedPath(string sourcePath, string destinationRelativePath)
        {
            SourcePath = sourcePath;
            DestinationRelativePath = destinationRelativePath;
        }
    }

    [Serializable]
    internal sealed class SafPublicationItem
    {
        public string SourcePath;
        public string DestinationRelativePath;
        public bool IsDirectory;
    }

    [Serializable]
    internal sealed class SafPublicationRecord
    {
        public int Version = 1;
        public string TransactionId;
        public string RootId;
        public string Area;
        public string DestinationRelativePath;
        public string StagedPath;
        public bool IsDirectory;
        public bool TransactionOwnsPayload;
        public List<SafPublicationItem> Items = new List<SafPublicationItem>();
        public List<string> CompletedFiles = new List<string>();
        public string State = "Publishing";
        public string CreatedUtc;
        public int AttemptCount;
        public string LastError = "";
    }

    public sealed class SafPublicationResult
    {
        public StorageResultCode Code { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        public SafPublicationResult(StorageResultCode code, string error = null)
        {
            Code = code;
            Error = error;
        }
    }

    /// Publishes path-based generator output without treating the staged tree as canonical.
    public static class SafStagedOutputPublisher
    {
        private const int kVersion = 1;

        public static SafPublicationResult Publish(
            IUserStorageBackend backend,
            StorageArea area,
            string destinationRelativePath,
            string stagedPath,
            bool transactionOwnsPayload,
            CancellationToken cancellationToken)
        {
            if (backend == null ||
                backend.Kind != StorageBackendKind.StorageAccessFramework ||
                !backend.IsReady)
            {
                return new SafPublicationResult(
                    StorageResultCode.NotReady,
                    "Open Brush shared folder is unavailable.");
            }
            return PublishBundle(
                backend,
                area,
                new[] { new SafStagedPath(stagedPath, destinationRelativePath) },
                transactionOwnsPayload,
                cancellationToken);
        }

        public static SafPublicationResult PublishBundle(
            IUserStorageBackend backend,
            StorageArea area,
            IEnumerable<SafStagedPath> stagedPaths,
            bool transactionOwnsPayload,
            CancellationToken cancellationToken)
        {
            if (backend == null ||
                backend.Kind != StorageBackendKind.StorageAccessFramework ||
                !backend.IsReady)
            {
                return new SafPublicationResult(
                    StorageResultCode.NotReady,
                    "Open Brush shared folder is unavailable.");
            }
            var items = new List<SafPublicationItem>();
            foreach (SafStagedPath stagedPath in stagedPaths)
            {
                bool isFile = File.Exists(stagedPath.SourcePath);
                bool isDirectory = Directory.Exists(stagedPath.SourcePath);
                if (!isFile && !isDirectory)
                {
                    return new SafPublicationResult(
                        StorageResultCode.NotFound,
                        $"Staged output does not exist: {stagedPath.SourcePath}");
                }
                if (transactionOwnsPayload && !IsOwnedStagingPath(stagedPath.SourcePath))
                {
                    return new SafPublicationResult(
                        StorageResultCode.InvalidPath,
                        $"Transaction-owned payload is outside SAF staging: " +
                        $"{stagedPath.SourcePath}");
                }
                items.Add(new SafPublicationItem
                {
                    SourcePath = Path.GetFullPath(stagedPath.SourcePath),
                    DestinationRelativePath = NormalizeRelativePath(
                        stagedPath.DestinationRelativePath),
                    IsDirectory = isDirectory,
                });
            }
            if (items.Count == 0)
            {
                return new SafPublicationResult(
                    StorageResultCode.InvalidPath, "Publication bundle is empty.");
            }
            string rootId = backend.RootIdentity;
            var record = new SafPublicationRecord
            {
                TransactionId = Guid.NewGuid().ToString("N"),
                RootId = rootId,
                Area = area.ToString(),
                DestinationRelativePath = items[0].DestinationRelativePath,
                StagedPath = items[0].SourcePath,
                IsDirectory = items[0].IsDirectory,
                TransactionOwnsPayload = transactionOwnsPayload,
                Items = items,
                CreatedUtc = DateTime.UtcNow.ToString("o"),
            };
            Persist(record);
            return Resume(backend, record, cancellationToken);
        }

        public static SafRecoveryReport RecoverAll(
            IUserStorageBackend backend, CancellationToken cancellationToken)
        {
            var report = new SafRecoveryReport();
            if (backend == null ||
                backend.Kind != StorageBackendKind.StorageAccessFramework ||
                !backend.IsReady)
            {
                report.Errors.Add("SAF backend is unavailable for publication recovery.");
                return report;
            }

            string rootId = backend.RootIdentity;
            string directory = GetPublicationDirectory(rootId);
            if (!Directory.Exists(directory))
            {
                return report;
            }
            foreach (string path in Directory.EnumerateFiles(directory, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                SafPublicationRecord record;
                try
                {
                    record = JsonConvert.DeserializeObject<SafPublicationRecord>(
                        File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    report.Pending++;
                    report.Errors.Add($"Failed to read SAF publication {path}: {e.Message}");
                    continue;
                }
                if (record == null ||
                    record.Version != kVersion ||
                    record.RootId != rootId ||
                    string.IsNullOrEmpty(record.TransactionId))
                {
                    report.Pending++;
                    report.Errors.Add($"Unsupported or malformed SAF publication: {path}");
                    continue;
                }

                SafPublicationResult result = Resume(backend, record, cancellationToken);
                if (result.Success)
                {
                    report.Recovered++;
                }
                else
                {
                    report.Pending++;
                    report.Errors.Add(
                        $"SAF_OUTPUT Publication pending for {record.TransactionId}: " +
                        $"{result.Error}");
                }
            }
            return report;
        }

        private static SafPublicationResult Resume(
            IUserStorageBackend backend,
            SafPublicationRecord record,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using IDisposable publicationLock = SafDestinationLocks.Acquire(
                    $"{record.RootId}\n{record.Area}\n__publication_subtree__"
                        .ToLowerInvariant(),
                    cancellationToken);
                if (!string.Equals(
                        record.RootId,
                        backend.RootIdentity,
                        StringComparison.Ordinal))
                {
                    return Fail(
                        record,
                        StorageResultCode.NotReady,
                        "The staged output belongs to a different Open Brush folder.");
                }
                EnsureItems(record);
                foreach (SafPublicationItem item in record.Items)
                {
                    if (item.IsDirectory && !Directory.Exists(item.SourcePath) ||
                        !item.IsDirectory && !File.Exists(item.SourcePath))
                    {
                        return Fail(record, StorageResultCode.NotFound,
                            $"Staged publication payload is missing: {item.SourcePath}");
                    }
                }

                var completed = new HashSet<string>(
                    record.CompletedFiles, StringComparer.Ordinal);
                for (int itemIndex = 0; itemIndex < record.Items.Count; ++itemIndex)
                {
                    if (!string.Equals(
                            record.RootId,
                            backend.RootIdentity,
                            StringComparison.Ordinal))
                    {
                        return Fail(
                            record,
                            StorageResultCode.NotReady,
                            "The selected Open Brush folder changed during publication.");
                    }
                    SafPublicationItem item = record.Items[itemIndex];
                    if (item.IsDirectory)
                    {
                        string providerDirectory = CombineProviderPath(
                            SafUserStorageBackend.GetAreaPath(ParseArea(record.Area)),
                            item.DestinationRelativePath);
                        if (!AndroidSafStorage.EnsureDirectory(providerDirectory))
                        {
                            return Fail(
                                record,
                                StorageResultCode.ProviderUnavailable,
                                $"Failed to create shared directory: " +
                                $"{item.DestinationRelativePath}");
                        }
                    }

                    foreach ((string sourcePath, string relativeFile) in EnumerateFiles(item))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!string.Equals(
                                record.RootId,
                                backend.RootIdentity,
                                StringComparison.Ordinal))
                        {
                            return Fail(
                                record,
                                StorageResultCode.NotReady,
                                "The selected Open Brush folder changed during publication.");
                        }
                        string completedKey = $"{itemIndex}:{relativeFile}";
                        string destination = item.IsDirectory
                            ? CombineProviderPath(item.DestinationRelativePath, relativeFile)
                            : item.DestinationRelativePath;
                        SafPublicationResult result = PublishFile(
                            backend,
                            ParseArea(record.Area),
                            destination,
                            sourcePath,
                            cancellationToken);
                        if (!result.Success)
                        {
                            return Fail(record, result.Code, result.Error);
                        }
                        if (completed.Add(completedKey))
                        {
                            record.CompletedFiles.Add(completedKey);
                        }
                        record.LastError = "";
                        Persist(record);
                    }
                }

                record.State = "Complete";
                Persist(record);
                DeleteJournal(record);
                if (record.TransactionOwnsPayload)
                {
                    foreach (SafPublicationItem item in record.Items)
                    {
                        DeleteOwnedPayload(item.SourcePath, item.IsDirectory);
                    }
                }
                return new SafPublicationResult(StorageResultCode.Success);
            }
            catch (OperationCanceledException e)
            {
                return Fail(record, StorageResultCode.Cancelled, e.Message);
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException ||
                e is ArgumentException)
            {
                return Fail(record, StorageResultCode.Failed, e.Message);
            }
        }

        private static SafPublicationResult PublishFile(
            IUserStorageBackend backend,
            StorageArea area,
            string destination,
            string sourcePath,
            CancellationToken cancellationToken)
        {
            using (IStorageWriteTransaction transaction = backend.BeginWrite(
                area, destination, GuessMimeType(sourcePath), cancellationToken))
            {
                using (Stream input = new FileStream(
                    sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (Stream output = transaction.OpenWrite())
                {
                    input.CopyTo(output);
                }
                StorageMutationResult commit = transaction.Commit();
                return new SafPublicationResult(commit.Code, commit.Error);
            }
        }

        private static IEnumerable<(string sourcePath, string relativeFile)> EnumerateFiles(
            SafPublicationItem item)
        {
            if (!item.IsDirectory)
            {
                yield return (item.SourcePath, "");
                yield break;
            }
            foreach (string path in Directory.EnumerateFiles(
                item.SourcePath, "*", SearchOption.AllDirectories).OrderBy(path => path))
            {
                yield return (
                    path,
                    Path.GetRelativePath(item.SourcePath, path).Replace('\\', '/'));
            }
        }

        private static void EnsureItems(SafPublicationRecord record)
        {
            if (record.Items != null && record.Items.Count > 0)
            {
                return;
            }
            record.Items = new List<SafPublicationItem>
            {
                new SafPublicationItem
                {
                    SourcePath = record.StagedPath,
                    DestinationRelativePath = record.DestinationRelativePath,
                    IsDirectory = record.IsDirectory,
                },
            };
        }

        private static SafPublicationResult Fail(
            SafPublicationRecord record, StorageResultCode code, string error)
        {
            record.AttemptCount++;
            record.LastError = error ?? "";
            record.State = "Pending";
            string resultError = error;
            try
            {
                Persist(record);
            }
            catch (Exception e) when (
                e is IOException ||
                e is UnauthorizedAccessException)
            {
                resultError =
                    $"{error} Recovery journal update failed: {e.Message}".Trim();
            }
            return new SafPublicationResult(code, resultError);
        }

        private static void Persist(SafPublicationRecord record)
        {
            string path = GetJournalPath(record);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
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

        private static void DeleteJournal(SafPublicationRecord record)
        {
            string path = GetJournalPath(record);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string GetJournalPath(SafPublicationRecord record)
        {
            return Path.Combine(
                GetPublicationDirectory(record.RootId), $"{record.TransactionId}.json");
        }

        private static string GetPublicationDirectory(string rootId)
        {
            return Path.Combine(
                SafTransactionJournal.GetRecoveryRootDirectory(rootId), "publications");
        }

        private static StorageArea ParseArea(string value)
        {
            if (!Enum.TryParse(value, out StorageArea area))
            {
                throw new ArgumentException($"Unknown storage area: {value}");
            }
            return area;
        }

        private static string NormalizeRelativePath(string path)
        {
            string normalized = (path ?? "").Replace('\\', '/');
            if (Path.IsPathRooted(normalized) ||
                normalized.StartsWith("/", StringComparison.Ordinal) ||
                normalized.EndsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Publication destination must be a relative path.");
            }
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArgumentException("Publication destination is invalid.");
                }
            }
            return normalized;
        }

        private static string CombineProviderPath(string left, string right)
        {
            return string.IsNullOrEmpty(right)
                ? left
                : $"{left.TrimEnd('/')}/{right.TrimStart('/')}";
        }

        private static string GuessMimeType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".tilt": return TiltFile.TILT_MIME_TYPE;
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".mp4": return "video/mp4";
                case ".webm": return "video/webm";
                case ".json": return "application/json";
                case ".txt": return "text/plain";
                case ".glb": return "model/gltf-binary";
                case ".gltf": return "model/gltf+json";
                default: return "application/octet-stream";
            }
        }

        private static void DeleteOwnedPayload(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                Directory.Delete(path, true);
            }
            else
            {
                File.Delete(path);
            }
        }

        private static bool IsOwnedStagingPath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            string[] roots =
            {
                Path.GetFullPath(OpenBrushStorage.LocalStagingPath),
            };
            foreach (string root in roots)
            {
                string prefix = root.TrimEnd(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
