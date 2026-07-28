// Copyright 2020 The Tilt Brush Authors
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
using UnityEngine;
using NUnit.Framework;

namespace TiltBrush
{

    internal class TestFile
    {
        private sealed class MemoryReadStreamSource : IReopenableReadStream
        {
            private readonly byte[] m_Data;

            public MemoryReadStreamSource(byte[] data)
            {
                m_Data = data;
            }

            public Stream Open()
            {
                return new MemoryStream(m_Data, writable: false);
            }
        }

        private sealed class FakeSafBackend : IUserStorageBackend
        {
            private sealed class Entry
            {
                public StorageDocumentId Id;
                public string Name;
                public byte[] Data;
            }

            private readonly Dictionary<StorageDocumentId, Entry> m_Entries =
                new Dictionary<StorageDocumentId, Entry>();
            public int CommitCount { get; private set; }
            public int FailCommitNumber { get; set; }
            public string RootAfterFirstCommit { get; set; }
            public List<string> CommittedNames { get; } = new List<string>();

            private sealed class WriteTransaction : IStorageWriteTransaction
            {
                private readonly FakeSafBackend m_Backend;
                private readonly string m_Name;
                private readonly MemoryStream m_Stream = new MemoryStream();
                private bool m_Finished;

                public StorageDocumentId TargetDocumentId { get; private set; }
                public StorageDocumentId TemporaryDocumentId { get; } =
                    new StorageDocumentId(Guid.NewGuid().ToString("N"));

                public WriteTransaction(FakeSafBackend backend, string relativePath)
                {
                    m_Backend = backend;
                    m_Name = Path.GetFileName(relativePath);
                }

                public Stream OpenWrite()
                {
                    return m_Stream;
                }

                public StorageMutationResult Commit()
                {
                    int commitNumber = m_Backend.CommitCount + 1;
                    if (m_Backend.FailCommitNumber == commitNumber)
                    {
                        m_Finished = true;
                        return new StorageMutationResult(
                            StorageResultCode.Failed,
                            TargetDocumentId,
                            "Injected publication failure.");
                    }
                    TargetDocumentId = m_Backend.AddOrReplace(m_Name, m_Stream.ToArray());
                    m_Backend.CommitCount = commitNumber;
                    m_Backend.CommittedNames.Add(m_Name);
                    if (m_Backend.RootAfterFirstCommit != null &&
                        commitNumber == 1)
                    {
                        m_Backend.RootIdentity = m_Backend.RootAfterFirstCommit;
                    }
                    m_Finished = true;
                    return new StorageMutationResult(
                        StorageResultCode.Success, TargetDocumentId);
                }

                public void Rollback()
                {
                    m_Finished = true;
                }

                public void Dispose()
                {
                    if (!m_Finished)
                    {
                        Rollback();
                    }
                    m_Stream.Dispose();
                }
            }

            public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
            public bool IsReady => true;
            public string RootIdentity { get; set; } = $"fake-root-{Guid.NewGuid():N}";

            public StorageDocumentId Add(string name, byte[] data)
            {
                var entry = new Entry
                {
                    Id = new StorageDocumentId(Guid.NewGuid().ToString("N")),
                    Name = name,
                    Data = data,
                };
                m_Entries.Add(entry.Id, entry);
                return entry.Id;
            }

            private StorageDocumentId AddOrReplace(string name, byte[] data)
            {
                foreach (Entry entry in m_Entries.Values)
                {
                    if (entry.Name == name)
                    {
                        entry.Data = data;
                        return entry.Id;
                    }
                }
                return Add(name, data);
            }

            public bool Contains(string name)
            {
                foreach (Entry entry in m_Entries.Values)
                {
                    if (entry.Name == name)
                    {
                        return true;
                    }
                }
                return false;
            }

            public StorageDirectoryResult List(
                StorageArea area, string relativeDirectory, CancellationToken cancellationToken)
            {
                var documents = new List<StorageDocument>();
                foreach (Entry entry in m_Entries.Values)
                {
                    documents.Add(new StorageDocument(
                        entry.Id,
                        default,
                        entry.Name,
                        TiltFile.TILT_MIME_TYPE,
                        false,
                        entry.Data.Length,
                        DateTime.Now,
                        0,
                        entry.Name));
                }
                return StorageDirectoryResult.Succeeded(documents);
            }

            public Stream OpenRead(
                StorageDocumentId documentId,
                bool requireSeekable,
                CancellationToken cancellationToken)
            {
                return new MemoryStream(m_Entries[documentId].Data, writable: false);
            }

            public IStorageWriteTransaction BeginWrite(
                StorageArea area,
                string relativePath,
                string mimeType,
                CancellationToken cancellationToken,
                StorageDocumentId targetDocumentId = default)
            {
                return new WriteTransaction(this, relativePath);
            }

            public StorageMutationResult Rename(
                StorageDocumentId documentId,
                string newDisplayName,
                CancellationToken cancellationToken)
            {
                foreach (Entry candidate in m_Entries.Values)
                {
                    if (candidate.Name == newDisplayName)
                    {
                        return new StorageMutationResult(
                            StorageResultCode.Failed, documentId, "Name already exists.");
                    }
                }
                m_Entries[documentId].Name = newDisplayName;
                return new StorageMutationResult(StorageResultCode.Success, documentId);
            }

            public StorageMutationResult Delete(
                StorageDocumentId documentId, CancellationToken cancellationToken)
            {
                return m_Entries.Remove(documentId)
                    ? new StorageMutationResult(StorageResultCode.Success, documentId)
                    : new StorageMutationResult(StorageResultCode.NotFound, documentId);
            }

            public string Materialize(
                StorageDocumentId documentId,
                MaterializationScope scope,
                CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public string GetMaterializationPath(StorageDocumentId documentId)
            {
                throw new NotSupportedException();
            }
        }

        private Stream GetReadStream(string zipfile, string subfile, bool useSharpZipLib)
        {
            if (useSharpZipLib)
            {
                return new ZipSubfileReader_SharpZipLib(zipfile, subfile);
            }
            else
            {
                return new ZipSubfileReader_DotNetZip(zipfile, subfile);
            }
        }

        // Test SketchBinaryReader.Skip() on non-seekable stream
        [Test]
        public void TestSkip([Values(0u, 1u, 4u, 18u)] uint amount,
                             [Values(false, true)] bool useSharpZipLib)
        {
            string zipfile = Path.Combine(Application.dataPath, "Editor/Tests/TestData/data.zip");
            uint a, b;

            // Skip forward, then read
            using (Stream instream = GetReadStream(zipfile, "data.bin", useSharpZipLib))
            {
                SketchBinaryReader reader = new SketchBinaryReader(instream);
                reader.UInt32(); // start test at non-zero offset
                bool ok = reader.Skip(amount);
                Assert.IsTrue(ok);
                a = reader.UInt32();
            }

            // Read forward, then read
            using (Stream instream = GetReadStream(zipfile, "data.bin", useSharpZipLib))
            {
                SketchBinaryReader reader = new SketchBinaryReader(instream);
                byte[] buf = new byte[amount + 4];
                instream.Read(buf, 0, buf.Length);
                b = reader.UInt32();
            }

            Assert.AreEqual(a, b);
        }

        // return n bytes of random data
        private byte[] MakeData(int n)
        {
            var r = new System.Random();
            byte[] ret = new byte[n];
            r.NextBytes(ret);
            return ret;
        }

        private unsafe void WriteBuf(SketchBinaryWriter w, byte[] buf)
        {
            fixed (byte* pbuf = buf)
            {
                w.Write((IntPtr)pbuf, buf.Length);
            }
        }

        // Test SketchBinaryWriter against BinaryWriter
        [Test]
        public unsafe void TestSketchBinaryWriter()
        {
            byte[] b0 = MakeData(0);
            byte[] b10 = MakeData(10);
            byte[] b5000 = MakeData(5000);

            using (var astr = new MemoryStream())
            using (var bstr = new MemoryStream())
            using (var aw = new BinaryWriter(astr, System.Text.Encoding.UTF8))
            {
                var bw = new SketchBinaryWriter(bstr);
                Quaternion q1 = new Quaternion(4.1f, 4.2f, 4.3f, 4.4f);
                Quaternion q2 = new Quaternion(2.5f, 3.5f, 4.5f, 5.3f);

                aw.Write(0x7123abcd);
                aw.Write(0xdb1f117eu);
                aw.Write(-0f);
                aw.Write(-1f);
                aw.Write(1e27f);
                aw.Write(q1.x);
                aw.Write(q1.y);
                aw.Write(q1.z);
                aw.Write(q1.w);
                aw.Write(q2.x);
                aw.Write(q2.y);
                aw.Write(q2.z);
                aw.Write(q2.w);
                aw.Flush();
                astr.Write(b0, 0, b0.Length);
                astr.Write(b10, 0, b10.Length);
                astr.Write(b5000, 0, b5000.Length);

                bw.Int32(0x7123abcd);
                bw.UInt32(0xdb1f117eu);
                bw.Vec3(new Vector3(-0f, -1f, 1e27f));
                bw.Quaternion(q1);
                Quaternion* pq2 = &q2;
                bw.Write((IntPtr)pq2, sizeof(Quaternion));
                WriteBuf(bw, b0);
                WriteBuf(bw, b10);
                WriteBuf(bw, b5000);

                Assert.AreEqual(astr.ToArray(), bstr.ToArray());
            }
        }

        [Test]
        public void TiltArchiveWriter_WritesReadableStreamArchive()
        {
            byte[] expected = { 1, 2, 3, 4, 5 };
            byte[] archive;
            using (var output = new MemoryStream())
            {
                using (var writer = new TiltFile.ArchiveWriter(
                    output, ownsOutputStream: false))
                {
                    using (Stream entry = writer.GetWriteStream(TiltFile.FN_SKETCH))
                    {
                        entry.Write(expected, 0, expected.Length);
                    }
                    writer.Complete();
                }

                Assert.IsTrue(output.CanWrite);
                archive = output.ToArray();
            }

            using (var archiveStream = new MemoryStream(archive, writable: false))
            {
                Assert.IsTrue(TiltFile.IsHeaderValid(archiveStream));
            }

            using (var reader = new ZipSubfileReader_SharpZipLib(
                new MemoryStream(archive, writable: false), TiltFile.FN_SKETCH))
            using (var copy = new MemoryStream())
            {
                reader.CopyTo(copy);
                Assert.AreEqual(expected, copy.ToArray());
            }
        }

        [Test]
        public void TiltArchiveValidation_RejectsTruncatedArchive()
        {
            byte[] archive = CreateMinimalTiltArchive();
            Array.Resize(ref archive, archive.Length - 8);
            using (var stream = new MemoryStream(archive, writable: false))
            {
                Assert.IsFalse(TiltFile.IsArchiveValid(stream, testData: true));
            }
        }

        [Test]
        public void TiltFile_ReadsEntriesFromReopenableStream()
        {
            byte[] expected = { 9, 8, 7 };
            byte[] archive;
            using (var output = new MemoryStream())
            {
                using (var writer = new TiltFile.ArchiveWriter(
                    output, ownsOutputStream: false))
                using (Stream entry = writer.GetWriteStream(TiltFile.FN_THUMBNAIL))
                {
                    entry.Write(expected, 0, expected.Length);
                }
                archive = output.ToArray();
            }

            var tiltFile = new TiltFile(new MemoryReadStreamSource(archive), "memory.tilt");
            Assert.IsTrue(tiltFile.IsHeaderValid());
            using (Stream reader = tiltFile.GetReadStream(TiltFile.FN_THUMBNAIL))
            using (var copy = new MemoryStream())
            {
                Assert.IsNotNull(reader);
                reader.CopyTo(copy);
                Assert.AreEqual(expected, copy.ToArray());
            }
        }

        [Test]
        public void LocalStorageBackend_ListsAndMutatesByDocumentIdentity()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-storage-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                var backend = new LocalUserStorageBackend(_ => root);
                using (IStorageWriteTransaction transaction = backend.BeginWrite(
                    StorageArea.Sketches,
                    "one.tilt",
                    TiltFile.TILT_MIME_TYPE,
                    CancellationToken.None))
                {
                    using (Stream stream = transaction.OpenWrite())
                    {
                        stream.WriteByte(42);
                    }
                    Assert.IsTrue(transaction.Commit().Success);
                }

                StorageDirectoryResult listing = backend.List(
                    StorageArea.Sketches, "", CancellationToken.None);
                Assert.IsTrue(listing.Success);
                Assert.AreEqual(1, listing.Documents.Count);
                StorageDocument document = listing.Documents[0];
                Assert.AreEqual("one.tilt", document.DisplayName);
                using (Stream stream = backend.OpenRead(
                    document.DocumentId, requireSeekable: true, CancellationToken.None))
                {
                    Assert.AreEqual(42, stream.ReadByte());
                }

                StorageMutationResult renamed = backend.Rename(
                    document.DocumentId, "two.tilt", CancellationToken.None);
                Assert.IsTrue(renamed.Success);
                Assert.IsTrue(backend.Delete(renamed.DocumentId, CancellationToken.None).Success);
                Assert.AreEqual(0, backend.List(
                    StorageArea.Sketches, "", CancellationToken.None).Documents.Count);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void LocalStorageBackend_RejectsPathsOutsideLogicalArea()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-storage-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            try
            {
                var backend = new LocalUserStorageBackend(_ => root);
                Assert.Throws<ArgumentException>(() => backend.BeginWrite(
                    StorageArea.Sketches,
                    Path.Combine("..", "outside.tilt"),
                    TiltFile.TILT_MIME_TYPE,
                    CancellationToken.None));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void StorageDocument_ReportsSafMutationCapabilities()
        {
            const long supportsWrite = 1L << 1;
            const long supportsDelete = 1L << 2;
            const long supportsRename = 1L << 6;
            const long supportsRemove = 1L << 10;
            var document = new StorageDocument(
                new StorageDocumentId("opaque"),
                new StorageDocumentId("parent"),
                "test.tilt",
                TiltFile.TILT_MIME_TYPE,
                false,
                1,
                DateTime.Now,
                supportsWrite | supportsDelete | supportsRename | supportsRemove,
                "test.tilt");

            Assert.IsTrue(document.SupportsWrite);
            Assert.IsTrue(document.SupportsDelete);
            Assert.IsTrue(document.SupportsRename);
            Assert.IsTrue(document.SupportsRemove);
        }

        [Test]
        public void SafTransactionJournal_IsVersionedAndAtomicallyUpdated()
        {
            string rootId = $"test-root-{Guid.NewGuid():N}";
            var record = new SafTransactionRecord
            {
                TransactionId = Guid.NewGuid().ToString("N"),
                RootId = rootId,
                Area = StorageArea.Sketches.ToString(),
                RelativePath = "Journal Test.tilt",
                TargetDisplayName = "Journal Test.tilt",
                State = SafTransactionState.CreatingTemporary.ToString(),
                CreatedUtc = DateTime.UtcNow.ToString("o"),
            };
            string journalDirectory = SafTransactionJournal.GetJournalDirectory(rootId);
            string recoveryRoot = Directory.GetParent(journalDirectory).FullName;
            try
            {
                SafTransactionJournal.Persist(record);
                record.State = SafTransactionState.TemporaryComplete.ToString();
                SafTransactionJournal.Persist(record);

                var loaded = SafTransactionJournal.Load(rootId, out var errors);
                Assert.AreEqual(0, errors.Count);
                Assert.AreEqual(1, loaded.Count);
                Assert.AreEqual(
                    SafTransactionState.TemporaryComplete.ToString(), loaded[0].State);

                record.Version = SafTransactionJournal.Version + 1;
                SafTransactionJournal.Persist(record);
                loaded = SafTransactionJournal.Load(rootId, out errors);
                Assert.AreEqual(0, loaded.Count);
                Assert.AreEqual(1, errors.Count);
                Assert.IsTrue(File.Exists(SafTransactionJournal.GetJournalPath(record)));
            }
            finally
            {
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        [Test]
        public void SafTransactionRecovery_RestoresValidatedBackup()
        {
            string rootId = $"test-root-{Guid.NewGuid():N}";
            string transactionId = Guid.NewGuid().ToString("N");
            string backupName = $".ob-{transactionId}.bak";
            var backend = new FakeSafBackend { RootIdentity = rootId };
            backend.Add("Recovery Test.tilt", new byte[] { 1, 2, 3 });
            backend.Add(backupName, CreateMinimalTiltArchive());
            var record = new SafTransactionRecord
            {
                TransactionId = transactionId,
                RootId = rootId,
                Area = StorageArea.Sketches.ToString(),
                RelativePath = "Recovery Test.tilt",
                TargetDisplayName = "Recovery Test.tilt",
                BackupDisplayName = backupName,
                InvalidDisplayName = $".ob-{transactionId}.invalid",
                State = SafTransactionState.RollbackRequired.ToString(),
                CreatedUtc = DateTime.UtcNow.ToString("o"),
            };
            string journalDirectory = SafTransactionJournal.GetJournalDirectory(rootId);
            string recoveryRoot = Directory.GetParent(journalDirectory).FullName;
            try
            {
                SafTransactionJournal.Persist(record);
                SafRecoveryReport report = SafTransactionRecovery.RecoverAll(
                    backend, CancellationToken.None, rootId);
                Assert.AreEqual(1, report.Recovered);
                Assert.AreEqual(0, report.Pending);
                Assert.IsTrue(backend.Contains("Recovery Test.tilt"));
                Assert.IsFalse(backend.Contains(backupName));
                Assert.IsFalse(File.Exists(SafTransactionJournal.GetJournalPath(record)));
            }
            finally
            {
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        [Test]
        public void SafStagedOutputPublisher_CommitsWholeDirectory()
        {
            string stagingRoot = Path.Combine(
                Path.GetTempPath(), $"open-brush-publication-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(stagingRoot, "nested"));
            File.WriteAllText(Path.Combine(stagingRoot, "one.txt"), "one");
            File.WriteAllBytes(Path.Combine(stagingRoot, "nested", "two.bin"), new byte[] { 2 });
            var backend = new FakeSafBackend();
            string recoveryRoot =
                SafTransactionJournal.GetRecoveryRootDirectory(backend.RootIdentity);
            try
            {
                SafPublicationResult result = SafStagedOutputPublisher.Publish(
                    backend,
                    StorageArea.Exports,
                    "Test Export",
                    stagingRoot,
                    transactionOwnsPayload: false,
                    CancellationToken.None);
                Assert.IsTrue(result.Success, result.Error);
                Assert.IsTrue(backend.Contains("one.txt"));
                Assert.IsTrue(backend.Contains("two.bin"));
                string publicationDirectory = Path.Combine(recoveryRoot, "publications");
                Assert.IsFalse(Directory.Exists(publicationDirectory) &&
                    Directory.GetFiles(publicationDirectory, "*.json").Length > 0);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        [Test]
        public void SafStagedOutputPublisher_RemovesCommittedOwnedPayload()
        {
            string stagingRoot = Path.Combine(
                OpenBrushStorage.LocalStagingPath,
                $"publication-test-{Guid.NewGuid():N}");
            string stagedFile = Path.Combine(stagingRoot, "snapshot.png");
            Directory.CreateDirectory(stagingRoot);
            File.WriteAllBytes(stagedFile, new byte[] { 1, 2, 3 });
            var backend = new FakeSafBackend();
            string recoveryRoot =
                SafTransactionJournal.GetRecoveryRootDirectory(backend.RootIdentity);
            try
            {
                SafPublicationResult result = SafStagedOutputPublisher.Publish(
                    backend,
                    StorageArea.Snapshots,
                    "snapshot.png",
                    stagedFile,
                    transactionOwnsPayload: true,
                    CancellationToken.None);
                Assert.IsTrue(result.Success, result.Error);
                Assert.IsFalse(File.Exists(stagedFile));
                Assert.IsTrue(backend.Contains("snapshot.png"));
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        [Test]
        public void SafStagedOutputPublisher_DoesNotCrossSelectedRoots()
        {
            string stagingRoot = Path.Combine(
                Path.GetTempPath(), $"open-brush-publication-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingRoot);
            string first = Path.Combine(stagingRoot, "first.txt");
            string second = Path.Combine(stagingRoot, "second.txt");
            File.WriteAllText(first, "first");
            File.WriteAllText(second, "second");
            var backend = new FakeSafBackend();
            string originalRoot = backend.RootIdentity;
            backend.RootAfterFirstCommit = $"different-root-{Guid.NewGuid():N}";
            string recoveryRoot =
                SafTransactionJournal.GetRecoveryRootDirectory(originalRoot);
            try
            {
                SafPublicationResult result = SafStagedOutputPublisher.PublishBundle(
                    backend,
                    StorageArea.Exports,
                    new[]
                    {
                        new SafStagedPath(first, "first.txt"),
                        new SafStagedPath(second, "second.txt"),
                    },
                    transactionOwnsPayload: false,
                    CancellationToken.None);

                Assert.IsFalse(result.Success);
                Assert.IsTrue(backend.Contains("first.txt"));
                Assert.IsFalse(backend.Contains("second.txt"));
                Assert.IsTrue(File.Exists(first));
                Assert.IsTrue(File.Exists(second));
                string publicationDirectory = Path.Combine(
                    recoveryRoot, "publications");
                Assert.AreEqual(
                    1,
                    Directory.GetFiles(publicationDirectory, "*.json").Length);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        [Test]
        public void SafStagedOutputPublisher_RetainsOwnedPayloadAfterFailure()
        {
            string stagingRoot = Path.Combine(
                OpenBrushStorage.LocalStagingPath,
                $"publication-test-{Guid.NewGuid():N}");
            string stagedFile = Path.Combine(stagingRoot, "snapshot.png");
            Directory.CreateDirectory(stagingRoot);
            File.WriteAllBytes(stagedFile, new byte[] { 1, 2, 3 });
            var backend = new FakeSafBackend { FailCommitNumber = 1 };
            string recoveryRoot =
                SafTransactionJournal.GetRecoveryRootDirectory(backend.RootIdentity);
            try
            {
                SafPublicationResult result = SafStagedOutputPublisher.Publish(
                    backend,
                    StorageArea.Snapshots,
                    "snapshot.png",
                    stagedFile,
                    transactionOwnsPayload: true,
                    CancellationToken.None);

                Assert.IsFalse(result.Success);
                Assert.IsTrue(File.Exists(stagedFile));
                Assert.AreEqual(
                    1,
                    Directory.GetFiles(
                        Path.Combine(recoveryRoot, "publications"), "*.json").Length);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        [Test]
        public void SafStagedOutputPublisher_CommitsFrameMetadataLast()
        {
            string stagingRoot = Path.Combine(
                Path.GetTempPath(), $"open-brush-publication-test-{Guid.NewGuid():N}");
            string frames = Path.Combine(stagingRoot, "frames");
            Directory.CreateDirectory(frames);
            File.WriteAllText(Path.Combine(frames, "0001.png"), "one");
            File.WriteAllText(Path.Combine(frames, "0002.png"), "two");
            string metadata = Path.Combine(stagingRoot, "sequence.txt");
            File.WriteAllText(metadata, "complete");
            var backend = new FakeSafBackend();
            string recoveryRoot =
                SafTransactionJournal.GetRecoveryRootDirectory(backend.RootIdentity);
            try
            {
                SafPublicationResult result = SafStagedOutputPublisher.PublishBundle(
                    backend,
                    StorageArea.Videos,
                    new[]
                    {
                        new SafStagedPath(frames, "frames"),
                        new SafStagedPath(metadata, "sequence.txt"),
                    },
                    transactionOwnsPayload: false,
                    CancellationToken.None);

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(
                    "sequence.txt",
                    backend.CommittedNames[backend.CommittedNames.Count - 1]);
            }
            finally
            {
                if (Directory.Exists(stagingRoot))
                {
                    Directory.Delete(stagingRoot, true);
                }
                if (Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                }
            }
        }

        private static byte[] CreateMinimalTiltArchive()
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
    }

}
