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
    public sealed class RuntimeContentSeed
    {
        public StorageArea Area { get; }
        public string RelativePath { get; }
        public string MimeType { get; }
        public byte[] Data { get; }

        public RuntimeContentSeed(
            StorageArea area, string relativePath, string mimeType, byte[] data)
        {
            LocalUserRuntimeContent.EnsureRuntimeArea(area);
            Area = area;
            RelativePath = relativePath ?? throw new ArgumentNullException(nameof(relativePath));
            MimeType = mimeType;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }

    public sealed class RuntimeContentSeedResult
    {
        public StorageResultCode Code { get; }
        public int SeededCount { get; }
        public string Error { get; }
        public bool Success => Code == StorageResultCode.Success;

        public RuntimeContentSeedResult(
            StorageResultCode code, int seededCount, string error = null)
        {
            Code = code;
            SeededCount = seededCount;
            Error = error;
        }
    }

    public static class RuntimeContentSeeder
    {
        public static RuntimeContentSeedResult SeedMissing(
            IUserStorageBackend backend,
            IEnumerable<RuntimeContentSeed> seeds,
            CancellationToken cancellationToken)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }
            string rootIdentity = backend.RootIdentity;
            int seededCount = 0;
            foreach (IGrouping<(StorageArea Area, string Directory), RuntimeContentSeed> group in
                seeds.GroupBy(seed => (
                    seed.Area,
                    GetLogicalDirectory(NormalizeRelativePath(seed.RelativePath)))))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!RootMatches(backend, rootIdentity))
                {
                    return new RuntimeContentSeedResult(
                        StorageResultCode.Cancelled,
                        seededCount,
                        "The selected storage root changed while seeding runtime content.");
                }
                StorageDirectoryResult listing = backend.List(
                    group.Key.Area, group.Key.Directory, cancellationToken);
                if (!listing.Success && listing.Code != StorageResultCode.NotFound)
                {
                    return new RuntimeContentSeedResult(
                        listing.Code, seededCount, listing.Error);
                }
                var names = new HashSet<string>(
                    listing.Documents.Select(document => document.DisplayName),
                    StringComparer.OrdinalIgnoreCase);
                foreach (RuntimeContentSeed seed in group)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string relativePath = NormalizeRelativePath(seed.RelativePath);
                    string displayName = Path.GetFileName(relativePath);
                    if (names.Contains(displayName))
                    {
                        continue;
                    }
                    try
                    {
                        using (IStorageWriteTransaction transaction = backend.BeginWrite(
                            seed.Area,
                            relativePath,
                            seed.MimeType ?? StorageMimeTypes.ForPath(relativePath),
                            cancellationToken))
                        {
                            if (backend.Kind == StorageBackendKind.StorageAccessFramework &&
                                transaction.TargetDocumentId.IsValid)
                            {
                                names.Add(displayName);
                                continue;
                            }
                            using (Stream output = transaction.OpenWrite())
                            {
                                output.Write(seed.Data, 0, seed.Data.Length);
                            }
                            StorageMutationResult commit = transaction.Commit();
                            if (!commit.Success)
                            {
                                return new RuntimeContentSeedResult(
                                    commit.Code, seededCount, commit.Error);
                            }
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                        return new RuntimeContentSeedResult(
                            StorageResultCode.Cancelled, seededCount, e.Message);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        return new RuntimeContentSeedResult(
                            StorageResultCode.PermissionDenied, seededCount, e.Message);
                    }
                    catch (Exception e) when (
                        e is IOException ||
                        e is ArgumentException ||
                        e is InvalidOperationException)
                    {
                        return new RuntimeContentSeedResult(
                            StorageResultCode.Failed, seededCount, e.Message);
                    }
                    names.Add(displayName);
                    ++seededCount;
                }
            }
            if (!RootMatches(backend, rootIdentity))
            {
                return new RuntimeContentSeedResult(
                    StorageResultCode.Cancelled,
                    seededCount,
                    "The selected storage root changed while seeding runtime content.");
            }
            return new RuntimeContentSeedResult(StorageResultCode.Success, seededCount);
        }

        private static bool RootMatches(
            IUserStorageBackend backend, string rootIdentity)
        {
            return backend.IsReady &&
                string.Equals(
                    backend.RootIdentity, rootIdentity, StringComparison.Ordinal);
        }

        private static string NormalizeRelativePath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) ||
                Path.IsPathRooted(relativePath))
            {
                throw new ArgumentException("Runtime seed path must be relative.");
            }
            string normalized = relativePath.Replace('\\', '/').Trim('/');
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArgumentException(
                        "Runtime seed path escapes its storage area.");
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
}
