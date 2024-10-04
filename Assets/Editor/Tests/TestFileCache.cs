using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TiltBrush
{
    [TestFixture]
    internal class TestFileCache
    {
        private FileCache m_Cache;
        private string m_Path;
        [SetUp]
        public void Setup()
        {
            m_Path = Path.Combine(Path.GetTempPath(), "FileCacheTest");
            if (File.Exists(m_Path))
            {
                File.Delete(m_Path);
            }
            if (Directory.Exists(m_Path))
            {
                Directory.Delete(m_Path, recursive: true);
            }
            m_Cache = new FileCache(m_Path, 1);
        }

        [TearDown]
        public void Teardown()
        {
            if (Directory.Exists(m_Path))
            {
                Directory.Delete(m_Path, recursive: true);
            }
        }

        [Test]
        public void IsDirectoryCreated()
        {
            Assert.IsTrue(Directory.Exists(m_Path));
        }

        [Test]
        public void IsCacheSizeUpdated()
        {
            Assert.That(m_Cache.CacheSize == 0);
            byte[] bytes = new byte[1000];
            m_Cache.Write("test", "onethousand", bytes);
            Assert.That(m_Cache.CacheSize == 1000);
        }

        [Test]
        public void IsCacheLimitRespected()
        {
            byte[] bytes = new byte[100000];
            for (int i = 0; i < 11; i++)
            {
                m_Cache.Write($"test_{i}", "100kbytes", bytes);
            }
            Assert.That(m_Cache.CacheSize == 1000000);
            var rootDir = new DirectoryInfo(m_Path);
            Assert.That(rootDir.EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(x => x.Length) == 1000000);
        }

        [Test]
        public void IsLastCreatedExpunged()
        {
            byte[] bytes = new byte[100000];
            for (int i = 0; i < 11; i++)
            {
                m_Cache.Write($"test_{i}", "100kbytes", bytes);
            }
            Assert.IsFalse(m_Cache.FilesetExists("test_0"));
        }

        [Test]
        public void CanWriteMultipleFilesSetAndFiles()
        {
            byte[] bytes = new byte[1000];
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; ++j)
                {
                    m_Cache.Write($"test_{i}", $"100kbytes_{j}", bytes);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                Assert.That(m_Cache.FilesetExists($"test_{i}"));
                for (int j = 0; j < 5; ++j)
                {
                    Assert.That(m_Cache.FileExists($"test_{i}", $"100kbytes_{j}"));
                }
            }
        }

        [Test]
        public void ThingsThatDontExistDontExist()
        {
            byte[] bytes = new byte[1000];
            m_Cache.Write("Real", "onethousand", bytes);

            Assert.IsFalse(m_Cache.FilesetExists("Imaginary"));
            Assert.IsFalse(m_Cache.FileExists("Imaginary", "onethousand"));
            Assert.IsFalse(m_Cache.FileExists("Real", "twothousand"));
        }

        [Test]
        public void FilesCanBeDeleted()
        {
            byte[] bytes = new byte[1000];
            m_Cache.Write("Real", "onethousand", bytes);
            Assert.That(m_Cache.FileExists("Real", "onethousand"));
            m_Cache.DeleteFile("Real", "onethousand");
            Assert.That(!m_Cache.FileExists("Real", "onethousand"));
        }

        [Test]
        public void FileHasTheRightContents()
        {
            byte[] bytes = Enumerable.Range(0, 127).Select(x => (byte)x).ToArray();
            m_Cache.Write("test", "data", bytes);
            byte[] read = m_Cache.Read("test", "data");
            Assert.That(bytes.SequenceEqual(read));
        }

        [Test]
        public void TestStreamRead()
        {
            byte[] bytes = Enumerable.Range(0, 127).Select(x => (byte)x).ToArray();
            m_Cache.Write("test", "data", bytes);
            using (var stream = m_Cache.ReadStream("test", "data"))
            {
                for (int i = 0; i < 127; ++i)
                {
                    Assert.That(stream.ReadByte() == i);
                }
            }
        }
    }
}
