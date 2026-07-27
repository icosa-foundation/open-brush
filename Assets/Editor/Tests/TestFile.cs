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
    }

}
