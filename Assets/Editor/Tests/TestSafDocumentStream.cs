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
        private sealed class FakeChannel : ISafDocumentChannel
        {
            private readonly MemoryStream m_Data = new MemoryStream();

            public int Reads;
            public int Writes;
            public int Flushes;
            public int DiskFlushes;
            public bool Closed;
            public bool FailReads;
            public bool FailWrites;
            private string m_Error;

            public byte[] Contents => m_Data.ToArray();
            public long Length => m_Data.Length;

            public void Seed(byte[] bytes)
            {
                m_Data.Position = 0;
                m_Data.Write(bytes, 0, bytes.Length);
            }

            public byte[] Read(long position, int count)
            {
                ++Reads;
                if (FailReads)
                {
                    m_Error = "read failed";
                    return null;
                }
                if (position >= m_Data.Length)
                {
                    return Array.Empty<byte>();
                }
                int take = (int)Math.Min(count, m_Data.Length - position);
                var result = new byte[take];
                Buffer.BlockCopy(m_Data.ToArray(), (int)position, result, 0, take);
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
                m_Data.Position = position;
                m_Data.Write(data, 0, count);
                return count;
            }

            public bool Truncate(long length)
            {
                m_Data.SetLength(length);
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
            Assert.AreEqual(expected, channel.Contents);
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
            Assert.AreEqual(expected, channel.Contents);
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
            Assert.AreEqual(expected, actual);
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
            for (int step = 0; step < 400; ++step)
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
                            int count = random.Next(1, 200_000);
                            var expected = new byte[count];
                            var actual = new byte[count];
                            int expectedRead = reference.Read(expected, 0, count);
                            int actualRead = stream.Read(actual, 0, count);
                            Assert.AreEqual(expectedRead, actualRead, $"Step {step} read length.");
                            Assert.AreEqual(expected, actual, $"Step {step} read contents.");
                            break;
                        }
                    case 3:
                        {
                            byte[] payload = Pattern(random.Next(1, 400_000));
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
                            Assert.AreEqual(
                                reference.ToArray(), channel.Contents, $"Step {step} contents.");
                            break;
                        }
                }
            }
            stream.Flush();
            Assert.AreEqual(reference.ToArray(), channel.Contents);
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
