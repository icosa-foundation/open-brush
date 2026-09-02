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
using System.Linq;
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
            public string RootAfterFirstRead { get; set; }
            public StorageResultCode? ListFailureCode { get; set; }
            public byte[] CreateBeforeNextWriteData { get; set; }
            public int ReadCount { get; private set; }
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
                    TargetDocumentId = backend.Find(m_Name)?.Id ?? default;
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

            private Entry Find(string name)
            {
                return m_Entries.Values.FirstOrDefault(
                    entry => string.Equals(
                        entry.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            public StorageDocumentId Replace(string name, byte[] data)
            {
                return AddOrReplace(name, data);
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
                if (ListFailureCode.HasValue)
                {
                    return StorageDirectoryResult.Failed(
                        ListFailureCode.Value, "Injected directory query failure.");
                }
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
                byte[] data = m_Entries[documentId].Data;
                ++ReadCount;
                if (ReadCount == 1 && RootAfterFirstRead != null)
                {
                    RootIdentity = RootAfterFirstRead;
                }
                return new MemoryStream(data, writable: false);
            }

            public IStorageWriteTransaction BeginWrite(
                StorageArea area,
                string relativePath,
                string mimeType,
                CancellationToken cancellationToken,
                StorageDocumentId targetDocumentId = default)
            {
                if (CreateBeforeNextWriteData != null)
                {
                    AddOrReplace(
                        Path.GetFileName(relativePath), CreateBeforeNextWriteData);
                    CreateBeforeNextWriteData = null;
                }
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
        public void SafStagedOutputPublisher_RejectsRootedDestination()
        {
            string stagedFile = Path.GetTempFileName();
            try
            {
                var backend = new FakeSafBackend();
                Assert.Throws<ArgumentException>(() =>
                    SafStagedOutputPublisher.Publish(
                        backend,
                        StorageArea.Exports,
                        Path.GetFullPath("outside.txt"),
                        stagedFile,
                        transactionOwnsPayload: false,
                        CancellationToken.None));
            }
            finally
            {
                File.Delete(stagedFile);
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

        [Test]
        public void StorageTreeEnumerator_RecursesAndFiltersFiles()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-storage-tree-test-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "nested", "deeper"));
                File.WriteAllText(Path.Combine(root, "top.lua"), "top");
                File.WriteAllText(Path.Combine(root, "ignored.txt"), "ignored");
                File.WriteAllText(Path.Combine(root, "nested", "child.LUA"), "child");
                File.WriteAllText(Path.Combine(root, "nested", "deeper", "last.lua"), "last");
                var backend = new LocalUserStorageBackend(_ => root);

                StorageTreeResult result = backend.EnumerateTree(
                    StorageArea.Plugins,
                    "",
                    new StorageTreeQuery(
                        recursive: true,
                        includeDirectories: false,
                        includeExtensions: new[] { ".lua" }),
                    CancellationToken.None);

                Assert.IsTrue(result.Success, result.Error);
                CollectionAssert.AreEqual(
                    new[] { "nested/child.LUA", "nested/deeper/last.lua", "top.lua" },
                    result.Entries.Select(entry => entry.RelativeDisplayPath).ToArray());
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void StorageTreeEnumerator_MissingAreaIsSuccessfulEmptyTree()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-storage-tree-test-{Guid.NewGuid():N}");
            var backend = new LocalUserStorageBackend(_ => root);

            StorageTreeResult result = backend.EnumerateTree(
                StorageArea.Scripts,
                "",
                new StorageTreeQuery(),
                CancellationToken.None);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsEmpty(result.Entries);
        }

        [Test]
        public void StorageTreeEnumerator_FailsInsteadOfTruncatingAtDepthLimit()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-storage-tree-test-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "nested"));
                File.WriteAllText(Path.Combine(root, "nested", "child.lua"), "child");
                var backend = new LocalUserStorageBackend(_ => root);

                StorageTreeResult result = backend.EnumerateTree(
                    StorageArea.Plugins,
                    "",
                    new StorageTreeQuery(recursive: true, maximumDepth: 0),
                    CancellationToken.None);

                Assert.IsFalse(result.Success);
                StringAssert.Contains("depth limit", result.Error);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SafRuntimeContent_ProjectsCanonicalFilesAndRefreshesChanges()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-runtime-content-test-{Guid.NewGuid():N}");
            var backend = new FakeSafBackend();
            backend.Add("first.lua", System.Text.Encoding.UTF8.GetBytes("one"));
            backend.Add("remove.lua", System.Text.Encoding.UTF8.GetBytes("remove"));
            var content = new SafUserRuntimeContent(
                backend, root, area => Path.Combine(root, "legacy", area.ToString()));
            try
            {
                RuntimeProjectionResult initial = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsTrue(initial.Success, initial.Error);
                Assert.AreEqual(
                    "one", File.ReadAllText(Path.Combine(initial.RuntimePath, "first.lua")));
                Assert.IsTrue(File.Exists(Path.Combine(initial.RuntimePath, "remove.lua")));

                backend.Replace(
                    "first.lua", System.Text.Encoding.UTF8.GetBytes("two"));
                StorageDocument remove = backend.List(
                    StorageArea.Plugins, "", CancellationToken.None).Documents
                    .Single(document => document.DisplayName == "remove.lua");
                backend.Delete(remove.DocumentId, CancellationToken.None);
                backend.Add("added.lua", System.Text.Encoding.UTF8.GetBytes("added"));

                RuntimeProjectionResult refreshed = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsTrue(refreshed.Success, refreshed.Error);
                Assert.AreNotEqual(initial.RuntimePath, refreshed.RuntimePath);
                Assert.AreEqual(
                    "two", File.ReadAllText(Path.Combine(refreshed.RuntimePath, "first.lua")));
                Assert.AreEqual(
                    "added", File.ReadAllText(Path.Combine(refreshed.RuntimePath, "added.lua")));
                Assert.IsFalse(File.Exists(Path.Combine(refreshed.RuntimePath, "remove.lua")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SafRuntimeContent_QueryFailureRetainsCurrentGeneration()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-runtime-content-test-{Guid.NewGuid():N}");
            var backend = new FakeSafBackend();
            backend.Add("plugin.lua", System.Text.Encoding.UTF8.GetBytes("retained"));
            var content = new SafUserRuntimeContent(
                backend, root, area => Path.Combine(root, "legacy", area.ToString()));
            try
            {
                RuntimeProjectionResult initial = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();
                Assert.IsTrue(initial.Success, initial.Error);
                backend.ListFailureCode = StorageResultCode.ProviderUnavailable;

                RuntimeProjectionResult failed = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsFalse(failed.Success);
                Assert.AreEqual(initial.RuntimePath, failed.RuntimePath);
                Assert.AreEqual(
                    "retained", File.ReadAllText(Path.Combine(failed.RuntimePath, "plugin.lua")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SafRuntimeContent_RootChangeCannotCommitGeneration()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-runtime-content-test-{Guid.NewGuid():N}");
            var backend = new FakeSafBackend
            {
                RootAfterFirstRead = $"replacement-root-{Guid.NewGuid():N}",
            };
            backend.Add("plugin.lua", System.Text.Encoding.UTF8.GetBytes("content"));
            var content = new SafUserRuntimeContent(
                backend, root, area => Path.Combine(root, "legacy", area.ToString()));
            try
            {
                RuntimeProjectionResult result = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsFalse(result.Success);
                Assert.AreEqual(StorageResultCode.Cancelled, result.Code);
                Assert.IsFalse(File.Exists(Path.Combine(
                    content.GetRuntimePath(StorageArea.Plugins), "plugin.lua")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SafRuntimeContent_MigratesAndCleansLegacyPrivateFile()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-runtime-content-test-{Guid.NewGuid():N}");
            string legacyRoot = Path.Combine(root, "legacy", "Plugins");
            Directory.CreateDirectory(legacyRoot);
            string legacyFile = Path.Combine(legacyRoot, "plugin.lua");
            File.WriteAllText(legacyFile, "legacy");
            var backend = new FakeSafBackend();
            var content = new SafUserRuntimeContent(
                backend, root, _ => legacyRoot);
            try
            {
                RuntimeProjectionResult result = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(
                    "legacy", File.ReadAllText(Path.Combine(result.RuntimePath, "plugin.lua")));
                Assert.IsFalse(File.Exists(legacyFile));
                Assert.IsTrue(backend.Contains("plugin.lua"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void SafRuntimeContent_PreservesDifferingMigrationConflict()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-runtime-content-test-{Guid.NewGuid():N}");
            string legacyRoot = Path.Combine(root, "legacy", "Plugins");
            Directory.CreateDirectory(legacyRoot);
            File.WriteAllText(Path.Combine(legacyRoot, "plugin.lua"), "local");
            var backend = new FakeSafBackend();
            backend.Add("plugin.lua", System.Text.Encoding.UTF8.GetBytes("shared"));
            var content = new SafUserRuntimeContent(
                backend, root, _ => legacyRoot);
            try
            {
                RuntimeProjectionResult result = content.EnsureCurrentAsync(
                    StorageArea.Plugins, CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(result.Success, result.Error);
                Assert.AreEqual(
                    "shared", File.ReadAllText(Path.Combine(result.RuntimePath, "plugin.lua")));
                string[] recovered = Directory.GetFiles(
                    result.RuntimePath, "plugin.local-recovered-*.lua");
                Assert.AreEqual(1, recovered.Length);
                Assert.AreEqual("local", File.ReadAllText(recovered[0]));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void RuntimeContentSeeder_WritesOnlyMissingFiles()
        {
            var backend = new FakeSafBackend();
            backend.Add("existing.lua", System.Text.Encoding.UTF8.GetBytes("user"));
            var seeds = new[]
            {
                new RuntimeContentSeed(
                    StorageArea.Plugins,
                    "existing.lua",
                    "text/x-lua",
                    System.Text.Encoding.UTF8.GetBytes("default")),
                new RuntimeContentSeed(
                    StorageArea.Plugins,
                    "missing.lua",
                    "text/x-lua",
                    System.Text.Encoding.UTF8.GetBytes("seed")),
            };

            RuntimeContentSeedResult first = RuntimeContentSeeder.SeedMissing(
                backend, seeds, CancellationToken.None);
            RuntimeContentSeedResult second = RuntimeContentSeeder.SeedMissing(
                backend, seeds, CancellationToken.None);

            Assert.IsTrue(first.Success, first.Error);
            Assert.AreEqual(1, first.SeededCount);
            Assert.IsTrue(second.Success, second.Error);
            Assert.AreEqual(0, second.SeededCount);
            Assert.AreEqual(1, backend.CommitCount);
            StorageDocument existing = backend.List(
                StorageArea.Plugins, "", CancellationToken.None).Documents
                .Single(document => document.DisplayName == "existing.lua");
            using (var reader = new StreamReader(backend.OpenRead(
                existing.DocumentId, false, CancellationToken.None)))
            {
                Assert.AreEqual("user", reader.ReadToEnd());
            }
        }

        [Test]
        public void RuntimeContentSeeder_DoesNotOverwriteFileCreatedAfterListing()
        {
            var backend = new FakeSafBackend
            {
                CreateBeforeNextWriteData =
                    System.Text.Encoding.UTF8.GetBytes("user"),
            };
            var seed = new RuntimeContentSeed(
                StorageArea.Plugins,
                "appeared.lua",
                "text/x-lua",
                System.Text.Encoding.UTF8.GetBytes("default"));

            RuntimeContentSeedResult result = RuntimeContentSeeder.SeedMissing(
                backend, new[] { seed }, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.SeededCount);
            Assert.AreEqual(0, backend.CommitCount);
            StorageDocument appeared = backend.List(
                StorageArea.Plugins, "", CancellationToken.None).Documents.Single();
            using (var reader = new StreamReader(backend.OpenRead(
                appeared.DocumentId, false, CancellationToken.None)))
            {
                Assert.AreEqual("user", reader.ReadToEnd());
            }
        }

        [Test]
        public void RuntimeContentPublication_DoesNotOverwriteFileCreatedAfterListing()
        {
            IUserStorageBackend previousBackend = UserStorage.Backend;
            var backend = new FakeSafBackend
            {
                CreateBeforeNextWriteData =
                    System.Text.Encoding.UTF8.GetBytes("user"),
            };
            UserStorage.SetBackendForTests(backend);
            try
            {
                RuntimeContentWriteResult result =
                    UserRuntimeContent.PublishIfMissingAsync(
                        StorageArea.Scripts,
                        "appeared.html",
                        "text/html",
                        System.Text.Encoding.UTF8.GetBytes("default"),
                        CancellationToken.None).GetAwaiter().GetResult();

                Assert.IsTrue(result.Success, result.Error);
                Assert.IsFalse(result.Created);
                Assert.AreEqual(0, backend.CommitCount);
                StorageDocument appeared = backend.List(
                    StorageArea.Scripts, "", CancellationToken.None).Documents.Single();
                using (var reader = new StreamReader(backend.OpenRead(
                    appeared.DocumentId, false, CancellationToken.None)))
                {
                    Assert.AreEqual("user", reader.ReadToEnd());
                }
            }
            finally
            {
                UserStorage.SetBackendForTests(previousBackend);
                UserRuntimeContent.SetForTests(new LocalUserRuntimeContent());
            }
        }

        [Test]
        public void DriveSyncLedger_RecognizesConfirmedStorageAndDriveVersions()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-drive-ledger-test-{Guid.NewGuid():N}");
            try
            {
                var ledger = new DriveSyncLedger(
                    "account", "drive-root", "storage-root", root);
                DateTime modified = DateTime.UtcNow;
                var document = new StorageDocument(
                    new StorageDocumentId("document-one"),
                    default,
                    "plugin.lua",
                    "text/x-lua",
                    false,
                    7,
                    modified,
                    0,
                    "plugin.lua");
                var driveFile = new Google.Apis.Drive.v3.Data.File
                {
                    Id = "drive-one",
                    Name = "plugin.lua",
                    Size = 7,
                    ModifiedTime = modified,
                    Md5Checksum = "local-md5",
                    Version = 3,
                };
                ledger.Confirm(
                    StorageArea.Plugins,
                    "plugin.lua",
                    document,
                    "local-sha",
                    "local-md5",
                    driveFile,
                    "Upload");

                DriveSyncLedger.Entry entry =
                    ledger.Get(StorageArea.Plugins, "plugin.lua");

                Assert.IsTrue(ledger.StorageMatches(
                    entry, document, () => "unexpected"));
                Assert.IsTrue(ledger.DriveMatches(entry, driveFile));
                driveFile.Version = 4;
                Assert.IsFalse(ledger.DriveMatches(entry, driveFile));
                var replacementDocument = new StorageDocument(
                    new StorageDocumentId("document-two"),
                    default,
                    "plugin.lua",
                    "text/x-lua",
                    false,
                    7,
                    modified.AddMinutes(1),
                    0,
                    "plugin.lua");
                Assert.IsTrue(ledger.StorageMatches(
                    entry, replacementDocument, () => "local-sha"));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        [Test]
        public void DriveSyncLedger_RetainsUnknownVersion()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"open-brush-drive-ledger-test-{Guid.NewGuid():N}");
            try
            {
                var ledger = new DriveSyncLedger(
                    "account", "drive-root", "storage-root", root);
                var document = new StorageDocument(
                    new StorageDocumentId("document"),
                    default,
                    "plugin.lua",
                    "text/x-lua",
                    false,
                    1,
                    DateTime.UtcNow,
                    0,
                    "plugin.lua");
                var driveFile = new Google.Apis.Drive.v3.Data.File
                {
                    Id = "drive",
                    Size = 1,
                    ModifiedTime = DateTime.UtcNow,
                    Version = 1,
                };
                ledger.Confirm(
                    StorageArea.Plugins,
                    "plugin.lua",
                    document,
                    "sha",
                    "md5",
                    driveFile,
                    "Upload");
                string ledgerPath = Directory.GetFiles(
                    root, "*.json", SearchOption.AllDirectories).Single();
                string unknown = File.ReadAllText(ledgerPath)
                    .Replace("\"Version\": 1", "\"Version\": 999");
                File.WriteAllText(ledgerPath, unknown);
                var reloaded = new DriveSyncLedger(
                    "account", "drive-root", "storage-root", root);

                Assert.Throws<IOException>(
                    () => reloaded.Get(StorageArea.Plugins, "plugin.lua"));
                StringAssert.Contains("\"Version\": 999", File.ReadAllText(ledgerPath));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
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
