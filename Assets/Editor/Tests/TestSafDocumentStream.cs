using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace TiltBrush
{
    /// The buffering in SafDocumentStream is the part that cannot be checked on a device without
    /// a full build, and it is where an off-by-one costs a corrupted sketch rather than an error.
    public class TestSafDocumentStream
    {
        /// Backed by a plain growable buffer rather than a MemoryStream, because a positioned
        /// channel wants positioned access: reading through MemoryStream meant a ToArray() copy
        /// of the whole document on every single read, which turned the randomised test below
        /// into gigabytes of pointless copying.
        private sealed class FakeChannel : ISafDocumentChannel
        {
            private byte[] m_Data = new byte[0];
            private int m_Length;

            public int Reads;
            public int Writes;
            public int Flushes;
            public int DiskFlushes;
            public bool Closed;
            public bool FailReads;
            public bool FailWrites;
            private string m_Error;

            public byte[] Contents
            {
                get
                {
                    var copy = new byte[m_Length];
                    Buffer.BlockCopy(m_Data, 0, copy, 0, m_Length);
                    return copy;
                }
            }

            public long Length => m_Length;

            public void Seed(byte[] bytes)
            {
                Grow(bytes.Length);
                Buffer.BlockCopy(bytes, 0, m_Data, 0, bytes.Length);
                m_Length = bytes.Length;
            }

            private void Grow(int required)
            {
                if (m_Data.Length >= required) { return; }
                int capacity = Math.Max(required, Math.Max(1024, m_Data.Length * 2));
                Array.Resize(ref m_Data, capacity);
            }

            public byte[] Read(long position, int count)
            {
                ++Reads;
                if (FailReads)
                {
                    m_Error = "read failed";
                    return null;
                }
                if (position >= m_Length)
                {
                    return Array.Empty<byte>();
                }
                int take = (int)Math.Min(count, m_Length - position);
                var result = new byte[take];
                Buffer.BlockCopy(m_Data, (int)position, result, 0, take);
                return result;
            }

            public int Write(long position, byte[] data, int count)
            {
                ++Writes;
                Assert.LessOrEqual(count, data.Length);
                if (FailWrites)
                {
                    m_Error = "write failed";
                    return -1;
                }
                Grow((int)position + count);
                // A write past the end leaves a zero-filled gap, as a real file does.
                if (position > m_Length)
                {
                    Array.Clear(m_Data, m_Length, (int)position - m_Length);
                }
                Buffer.BlockCopy(data, 0, m_Data, (int)position, count);
                m_Length = Math.Max(m_Length, (int)position + count);
                return count;
            }

            public bool Truncate(long length)
            {
                Grow((int)length);
                if (length > m_Length)
                {
                    Array.Clear(m_Data, m_Length, (int)length - m_Length);
                }
                m_Length = (int)length;
                return true;
            }

            public bool Flush(bool toDisk)
            {
                ++Flushes;
                if (toDisk) { ++DiskFlushes; }
                return true;
            }

            public string DescribeLastError() => m_Error;

            public void Close() { Closed = true; }
        }

        /// Assert.AreEqual on a large byte[] runs NUnit's constraint engine over every element
        /// and is slow enough at these sizes to look like a hang. Compare directly, and only
        /// involve NUnit when there is something to report.
        private static void AssertBytesEqual(byte[] expected, byte[] actual, string message)
        {
            if (expected.Length != actual.Length)
            {
                Assert.Fail(
                    $"{message}: expected {expected.Length} bytes, got {actual.Length}.");
            }
            for (int i = 0; i < expected.Length; ++i)
            {
                if (expected[i] != actual[i])
                {
                    Assert.Fail(
                        $"{message}: byte {i} was {actual[i]}, expected {expected[i]}.");
                }
            }
        }

        private static byte[] Pattern(int length)
        {
            var bytes = new byte[length];
            for (int i = 0; i < length; ++i)
            {
                bytes[i] = (byte)(i * 31 + (i >> 8));
            }
            return bytes;
        }

        [Test]
        public void SmallWritesCoalesceIntoOneCrossing()
        {
            var channel = new FakeChannel();
            byte[] expected = Pattern(100_000);
            using (var stream = new SafDocumentStream(channel, 0, canWrite: true))
            {
                for (int offset = 0; offset < expected.Length; offset += 100)
                {
                    stream.Write(expected, offset, 100);
                }
                Assert.AreEqual(0, channel.Writes, "Small writes reached the provider unbuffered.");
                Assert.AreEqual(expected.Length, stream.Length);
                stream.Flush();
                Assert.AreEqual(1, channel.Writes);
            }
            AssertBytesEqual(expected, channel.Contents, "Coalesced writes");
        }

        [Test]
        public void WritesLargerThanTheBufferGoStraightThrough()
        {
            var channel = new FakeChannel();
            byte[] expected = Pattern(700_000);
            using (var stream = new SafDocumentStream(channel, 0, canWrite: true))
            {
                stream.Write(expected, 0, expected.Length);
                Assert.AreEqual(1, channel.Writes);
            }
            AssertBytesEqual(expected, channel.Contents, "Write-through");
        }

        [Test]
        public void ManySmallReadsShareOneReadAhead()
        {
            var channel = new FakeChannel();
            byte[] expected = Pattern(200_000);
            channel.Seed(expected);
            using var stream = new SafDocumentStream(channel, expected.Length, canWrite: false);
            for (int i = 0; i < 1000; ++i)
            {
                Assert.AreEqual(expected[i], stream.ReadByte(), $"Byte {i} differed.");
            }
            Assert.AreEqual(1, channel.Reads);
        }

        [Test]
        public void ReadFillsTheRequestAcrossBlocks()
        {
            var channel = new FakeChannel();
            byte[] expected = Pattern(200_000);
            channel.Seed(expected);
            using var stream = new SafDocumentStream(channel, expected.Length, canWrite: false);
            var actual = new byte[expected.Length];
            Assert.AreEqual(expected.Length, stream.Read(actual, 0, actual.Length));
            AssertBytesEqual(expected, actual, "Filled read");
            Assert.AreEqual(expected.Length, stream.Position);
        }

        [Test]
        public void ReadingPastTheEndStopsShortWithoutFailing()
        {
            var channel = new FakeChannel();
            byte[] expected = Pattern(100);
            channel.Seed(expected);
            using var stream = new SafDocumentStream(channel, expected.Length, canWrite: false);
            var actual = new byte[500];
            Assert.AreEqual(100, stream.Read(actual, 0, actual.Length));
            Assert.AreEqual(0, stream.Read(actual, 0, actual.Length));
            Assert.AreEqual(-1, stream.ReadByte());
        }

        [Test]
        public void SeekingBackwardsServesTheBufferAndThenRefillsIt()
        {
            var channel = new FakeChannel();
            byte[] expected = Pattern(200_000);
            channel.Seed(expected);
            using var stream = new SafDocumentStream(channel, expected.Length, canWrite: false);
            var one = new byte[1];

            stream.Seek(10, SeekOrigin.Begin);
            stream.Read(one, 0, 1);
            Assert.AreEqual(expected[10], one[0]);
            Assert.AreEqual(1, channel.Reads);

            // Still inside the block that was read ahead.
            stream.Seek(-1, SeekOrigin.Current);
            stream.Read(one, 0, 1);
            Assert.AreEqual(expected[10], one[0]);
            Assert.AreEqual(1, channel.Reads);

            // Outside it, so the block has to be replaced.
            stream.Seek(-4, SeekOrigin.End);
            stream.Read(one, 0, 1);
            Assert.AreEqual(expected[expected.Length - 4], one[0]);
            Assert.AreEqual(2, channel.Reads);
        }

        [Test]
        public void SeekingAwayFromPendingWritesLandsThemWhereTheyWereWritten()
        {
            var channel = new FakeChannel();
            using (var stream = new SafDocumentStream(channel, 0, canWrite: true))
            {
                stream.Write(Encoding.ASCII.GetBytes("AAAA"), 0, 4);
                stream.Seek(0, SeekOrigin.Begin);
                stream.Write(Encoding.ASCII.GetBytes("B"), 0, 1);
            }
            Assert.AreEqual("BAAA", Encoding.ASCII.GetString(channel.Contents));
        }

        [Test]
        public void ReadingAfterWritingSeesTheWrittenBytes()
        {
            var channel = new FakeChannel();
            using var stream = new SafDocumentStream(channel, 0, canWrite: true);
            stream.Write(Encoding.ASCII.GetBytes("hello"), 0, 5);
            stream.Seek(0, SeekOrigin.Begin);
            var actual = new byte[5];
            Assert.AreEqual(5, stream.Read(actual, 0, 5));
            Assert.AreEqual("hello", Encoding.ASCII.GetString(actual));
        }

        [Test]
        public void DisposeLandsPendingWritesAndClosesTheChannel()
        {
            var channel = new FakeChannel();
            var stream = new SafDocumentStream(channel, 0, canWrite: true);
            stream.Write(Encoding.ASCII.GetBytes("pending"), 0, 7);
            Assert.AreEqual(0, channel.Writes);
            stream.Dispose();
            Assert.AreEqual("pending", Encoding.ASCII.GetString(channel.Contents));
            Assert.IsTrue(channel.Closed);
        }

        [Test]
        public void FlushToDiskIsAnFsyncAndAnOrdinaryFlushIsNot()
        {
            var channel = new FakeChannel();
            using var stream = new SafDocumentStream(channel, 0, canWrite: true);
            stream.Write(Encoding.ASCII.GetBytes("x"), 0, 1);
            stream.Flush();
            Assert.AreEqual(0, channel.DiskFlushes);
            stream.FlushToDisk();
            Assert.AreEqual(1, channel.DiskFlushes);
            Assert.AreEqual(2, channel.Flushes);
        }

        [Test]
        public void SetLengthTruncatesAndClampsThePosition()
        {
            var channel = new FakeChannel();
            channel.Seed(Pattern(1000));
            using var stream = new SafDocumentStream(channel, 1000, canWrite: true);
            stream.Seek(0, SeekOrigin.End);
            stream.SetLength(10);
            Assert.AreEqual(10, stream.Length);
            Assert.AreEqual(10, stream.Position);
            Assert.AreEqual(10, channel.Length);
        }

        [Test]
        public void ProviderFailuresSurfaceAsIoExceptionsCarryingTheReason()
        {
            var writeChannel = new FakeChannel { FailWrites = true };
            using (var stream = new SafDocumentStream(writeChannel, 0, canWrite: true))
            {
                stream.Write(Encoding.ASCII.GetBytes("x"), 0, 1);
                var thrown = Assert.Throws<IOException>(() => stream.Flush());
                StringAssert.Contains("write failed", thrown.Message);
                // Cleared, so disposing inside a finally does not throw over the first failure.
                Assert.DoesNotThrow(() => stream.Flush());
            }

            var readChannel = new FakeChannel { FailReads = true };
            readChannel.Seed(Pattern(10));
            using (var stream = new SafDocumentStream(readChannel, 10, canWrite: false))
            {
                var thrown = Assert.Throws<IOException>(() => stream.Read(new byte[4], 0, 4));
                StringAssert.Contains("read failed", thrown.Message);
            }
        }

        /// The cases above are the ones worth naming. This is the one that finds what they miss:
        /// the same random sequence of seeks, reads and writes against both this stream and a
        /// MemoryStream, which is the behaviour it is supposed to be indistinguishable from.
        ///
        /// Sizes are deliberately small. The buffers are 64 KiB and 256 KiB, so operations of a
        /// few hundred KiB already cross every boundary that matters - partial buffer hits,
        /// buffer misses, writes larger than the write-behind - and running at megabyte scale
        /// only bought comparison time.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RandomOperationsMatchAMemoryStream(int seed)
        {
            var random = new System.Random(seed);
            var channel = new FakeChannel();
            byte[] seeded = Pattern(300_000);
            channel.Seed(seeded);
            var reference = new MemoryStream();
            reference.Write(seeded, 0, seeded.Length);
            reference.Position = 0;

            using var stream = new SafDocumentStream(channel, seeded.Length, canWrite: true);
            for (int step = 0; step < 300; ++step)
            {
                switch (random.Next(5))
                {
                    case 0:
                        {
                            long target = random.Next((int)reference.Length + 1000);
                            Assert.AreEqual(
                                reference.Seek(target, SeekOrigin.Begin),
                                stream.Seek(target, SeekOrigin.Begin),
                                $"Step {step} seek.");
                            break;
                        }
                    case 1:
                    case 2:
                        {
                            int count = random.Next(1, 150_000);
                            var expected = new byte[count];
                            var actual = new byte[count];
                            int expectedRead = reference.Read(expected, 0, count);
                            int actualRead = stream.Read(actual, 0, count);
                            Assert.AreEqual(expectedRead, actualRead, $"Step {step} read length.");
                            AssertBytesEqual(expected, actual, $"Step {step} read contents");
                            break;
                        }
                    case 3:
                        {
                            byte[] payload = Pattern(random.Next(1, 300_000));
                            for (int i = 0; i < payload.Length; ++i)
                            {
                                payload[i] ^= (byte)step;
                            }
                            reference.Write(payload, 0, payload.Length);
                            stream.Write(payload, 0, payload.Length);
                            break;
                        }
                    case 4:
                        {
                            stream.Flush();
                            Assert.AreEqual(
                                reference.Length, stream.Length, $"Step {step} length.");
                            Assert.AreEqual(
                                reference.Position, stream.Position, $"Step {step} position.");
                            AssertBytesEqual(
                                reference.ToArray(), channel.Contents, $"Step {step} contents");
                            break;
                        }
                }
            }
            stream.Flush();
            AssertBytesEqual(reference.ToArray(), channel.Contents, "Final contents");
        }

        [Test]
        public void AReadOnlyStreamRefusesWrites()
        {
            var channel = new FakeChannel();
            using var stream = new SafDocumentStream(channel, 0, canWrite: false);
            Assert.IsFalse(stream.CanWrite);
            Assert.Throws<NotSupportedException>(() => stream.WriteByte(0));
            Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
        }

        [Test]
        public void UsingAClosedStreamIsAnObjectDisposedException()
        {
            var channel = new FakeChannel();
            var stream = new SafDocumentStream(channel, 0, canWrite: true);
            stream.Dispose();
            Assert.Throws<ObjectDisposedException>(() => stream.ReadByte());
            Assert.Throws<ObjectDisposedException>(() => stream.Seek(0, SeekOrigin.Begin));
            Assert.DoesNotThrow(() => stream.Dispose());
        }
    }
}
