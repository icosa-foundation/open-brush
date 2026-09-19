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
using NUnit.Framework;

namespace TiltBrush
{
    /// Covers how recovery *discovers* interrupted saves, which is the half that changed when the
    /// transaction journal was removed. Recovery used to read records of what had been started;
    /// it now sweeps storage for the sidecars an interrupted save leaves behind.
    internal class TestSafRecoverySweep
    {
        /// Area-scoped, unlike the shared fake in TestFile, because the sweep visits every area
        /// in turn and a backend that ignores the area would report each document many times.
        private sealed class AreaScopedBackend : IUserStorageBackend
        {
            private sealed class Entry
            {
                public StorageDocumentId Id;
                public StorageArea Area;
                public string Name;
                public byte[] Data;
            }

            private readonly List<Entry> m_Entries = new List<Entry>();

            public bool UserRootWasRecursive { get; private set; }

            public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
            public bool IsReady => true;
            public string RootIdentity { get; set; } = "sweep-root";

            public StorageDocumentId Add(StorageArea area, string name, byte[] data)
            {
                var entry = new Entry
                {
                    Id = new StorageDocumentId(Guid.NewGuid().ToString("N")),
                    Area = area,
                    Name = name,
                    Data = data,
                };
                m_Entries.Add(entry);
                return entry.Id;
            }

            public bool Contains(StorageArea area, string name) =>
                m_Entries.Any(e => e.Area == area && e.Name == name);

            public int Count => m_Entries.Count;

            private StorageDocument ToDocument(Entry entry) => new StorageDocument(
                entry.Id, default, entry.Name, TiltFile.TILT_MIME_TYPE, false,
                entry.Data.Length, DateTime.UtcNow, 0, entry.Name);

            public StorageDirectoryResult List(
                StorageArea area, string relativeDirectory, CancellationToken cancellationToken)
            {
                // Everything in these fixtures sits at the area root.
                if (!string.IsNullOrEmpty(relativeDirectory))
                {
                    return StorageDirectoryResult.Succeeded(Array.Empty<StorageDocument>());
                }
                return StorageDirectoryResult.Succeeded(
                    m_Entries.Where(e => e.Area == area).Select(ToDocument).ToList());
            }

            public StorageTreeResult EnumerateTree(
                StorageArea area, string relativeDirectory, StorageTreeQuery query,
                CancellationToken cancellationToken)
            {
                if (area == StorageArea.UserRoot)
                {
                    UserRootWasRecursive = query.Recursive;
                }
                return StorageTreeEnumerator.Enumerate(
                    this, area, relativeDirectory, query, cancellationToken);
            }

            public Stream OpenRead(
                StorageArea area, string relativePath, bool requireSeekable,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public bool Exists(StorageArea area, string relativePath) =>
                m_Entries.Any(e => e.Area == area && e.Name == relativePath);

            public Stream OpenRead(
                StorageDocumentId documentId, bool requireSeekable,
                CancellationToken cancellationToken)
            {
                Entry entry = m_Entries.Single(e => e.Id.Equals(documentId));
                return new MemoryStream(entry.Data, writable: false);
            }

            public IStorageWriteTransaction BeginWrite(
                StorageArea area, string relativePath, string mimeType,
                CancellationToken cancellationToken,
                StorageDocumentId targetDocumentId = default) =>
                throw new NotSupportedException();

            public StorageMutationResult Rename(
                StorageDocumentId documentId, string newDisplayName,
                CancellationToken cancellationToken)
            {
                Entry entry = m_Entries.Single(e => e.Id.Equals(documentId));
                if (m_Entries.Any(e => e.Area == entry.Area && e.Name == newDisplayName))
                {
                    return new StorageMutationResult(
                        StorageResultCode.Failed, documentId, "Name already exists.");
                }
                entry.Name = newDisplayName;
                return new StorageMutationResult(StorageResultCode.Success, documentId);
            }

            public StorageMutationResult Delete(
                StorageDocumentId documentId, CancellationToken cancellationToken)
            {
                m_Entries.RemoveAll(e => e.Id.Equals(documentId));
                return new StorageMutationResult(StorageResultCode.Success, documentId);
            }
        }

        /// Matches the fixture the other storage tests use: a real archive written through
        /// TiltFile's own writer, so IsArchiveValid accepts it.
        private static byte[] ValidTilt()
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new TiltFile.ArchiveWriter(
                    output, ownsOutputStream: false))
                {
                    using (Stream entry = writer.GetWriteStream(TiltFile.FN_SKETCH))
                    {
                        entry.WriteByte(1);
                    }
                    using (Stream entry = writer.GetWriteStream(TiltFile.FN_METADATA))
                    {
                        entry.WriteByte((byte)'{');
                        entry.WriteByte((byte)'}');
                    }
                }
                return output.ToArray();
            }
        }

        [Test]
        public void Sweep_RestoresTheBackupWhenTheCanonicalIsMissing()
        {
            var backend = new AreaScopedBackend();
            backend.Add(StorageArea.Sketches, "Sketch.tilt.ob-bak", ValidTilt());

            SafRecoveryReport report =
                SafTransactionRecovery.RecoverAll(backend, CancellationToken.None);

            Assert.AreEqual(1, report.Recovered, string.Join("; ", report.Errors));
            Assert.IsTrue(backend.Contains(StorageArea.Sketches, "Sketch.tilt"));
            Assert.IsFalse(backend.Contains(StorageArea.Sketches, "Sketch.tilt.ob-bak"));
        }

        [Test]
        public void Sweep_PromotesACompletedTemporaryWhenNothingElseSurvives()
        {
            var backend = new AreaScopedBackend();
            backend.Add(StorageArea.Sketches, "Sketch.tilt.ob-tmp", ValidTilt());

            SafRecoveryReport report =
                SafTransactionRecovery.RecoverAll(backend, CancellationToken.None);

            Assert.AreEqual(1, report.Recovered, string.Join("; ", report.Errors));
            Assert.IsTrue(backend.Contains(StorageArea.Sketches, "Sketch.tilt"));
        }

        [Test]
        public void Sweep_KeepsAGoodCanonicalAndClearsItsLeftovers()
        {
            var backend = new AreaScopedBackend();
            StorageDocumentId canonical =
                backend.Add(StorageArea.Sketches, "Sketch.tilt", ValidTilt());
            backend.Add(StorageArea.Sketches, "Sketch.tilt.ob-bak", ValidTilt());

            SafRecoveryReport report =
                SafTransactionRecovery.RecoverAll(backend, CancellationToken.None);

            Assert.AreEqual(1, report.Recovered, string.Join("; ", report.Errors));
            Assert.IsTrue(backend.Contains(StorageArea.Sketches, "Sketch.tilt"));
            Assert.IsFalse(backend.Contains(StorageArea.Sketches, "Sketch.tilt.ob-bak"));
            Assert.AreEqual(1, backend.Count);
        }

        [Test]
        public void Sweep_FindsNothingWhenNoSaveWasInterrupted()
        {
            var backend = new AreaScopedBackend();
            backend.Add(StorageArea.Sketches, "Sketch.tilt", ValidTilt());

            SafRecoveryReport report =
                SafTransactionRecovery.RecoverAll(backend, CancellationToken.None);

            Assert.AreEqual(0, report.Recovered);
            Assert.AreEqual(0, report.Pending);
            Assert.IsTrue(backend.Contains(StorageArea.Sketches, "Sketch.tilt"));
        }

        [Test]
        public void Sweep_DoesNotRecursivelyRescanMappedAreasThroughUserRoot()
        {
            var backend = new AreaScopedBackend();

            SafTransactionRecovery.RecoverAll(backend, CancellationToken.None);

            Assert.IsFalse(backend.UserRootWasRecursive);
        }

        [Test]
        public void Sweep_LeavesSidecarsInOtherAreasToTheirOwnArea()
        {
            var backend = new AreaScopedBackend();
            backend.Add(StorageArea.MediaLibraryVideos, "Clip.mp4.ob-bak", new byte[] { 1, 2, 3 });

            SafRecoveryReport report =
                SafTransactionRecovery.RecoverAll(backend, CancellationToken.None);

            // A non-tilt payload is restored on presence alone, in the area it was found in.
            Assert.AreEqual(1, report.Recovered, string.Join("; ", report.Errors));
            Assert.IsTrue(backend.Contains(StorageArea.MediaLibraryVideos, "Clip.mp4"));
        }
    }
}
