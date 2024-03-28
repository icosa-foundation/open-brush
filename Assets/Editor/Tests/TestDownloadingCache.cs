using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TiltBrush
{
    [TestFixture]
    public class TestDownloadingCache
    {
        private DownloadingCache m_dlCache;
        private FileCache m_Cache;
        private string m_Path;

        private const string kLocalFile = "file://TestData/main_1.png";
        private const string kRemoteFile = "http://openbrush.app/assets/icon.png";

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
            m_dlCache = new DownloadingCache(m_Cache);
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
        public async void LocalFileLoads()
        {
            var bytes = await m_dlCache.Read("test", "logo1", kLocalFile);
            Assert.That(bytes != null);
            Assert.That(bytes.Length == 32983);
        }

        [Test]
        public async void RemoteFileLoads()
        {
            var bytes = await m_dlCache.Read("test", "logo1", kRemoteFile);
            Assert.That(bytes != null);
            Assert.That(bytes.Length == 32983);
        }

        [Test]
        public async void RemoteFileIsStoredInCache()
        {
            var bytes = await m_dlCache.Read("test", "logo1", kRemoteFile);
            Assert.That(m_Cache.CacheSize == 32983);
        }

        [Test]
        public async void RemoteFileCanBeLoadedFromCache()
        {
            var bytes = await m_dlCache.Read("test", "logo1", kRemoteFile);
            var bytes2 = m_Cache.Read("test", "logo1");
            Assert.That(Enumerable.SequenceEqual(bytes, bytes2));
            var bytes3 = await m_dlCache.Read("test", "logo1", kRemoteFile);
            Assert.That(Enumerable.SequenceEqual(bytes, bytes3));
        }

    }
}
