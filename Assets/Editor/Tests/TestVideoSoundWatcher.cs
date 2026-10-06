using System;
using System.IO;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestVideoSoundWatcher
    {
        [Test]
        public void ChangedDetectedPaths_DeduplicateAndIgnoreDeletedFiles()
        {
            StringComparer comparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            string root = Path.Combine(Path.GetTempPath(), "catalog-change-root");
            string existing = Path.Combine(root, "clip.mp4");
            string deleted = Path.Combine(root, "gone.mp4");
            string[] result = CatalogChangeSet.GetChangedDetectedPaths(
                new[] { existing, existing, deleted }, new[] { existing }, comparer);
            CollectionAssert.AreEqual(new[] { existing }, result);
        }

        [Test]
        public void ScanGuard_RejectsStaleGenerationBackendAndDirectory()
        {
            object backend = new object();
            StringComparer comparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            Assert.IsTrue(CatalogScanGuard.IsCurrent(
                2, 2, backend, backend, "A", "A", comparer));
            Assert.IsFalse(CatalogScanGuard.IsCurrent(
                1, 2, backend, backend, "A", "A", comparer));
            Assert.IsFalse(CatalogScanGuard.IsCurrent(
                2, 2, backend, new object(), "A", "A", comparer));
            Assert.IsFalse(CatalogScanGuard.IsCurrent(
                2, 2, backend, backend, "A", "B", comparer));
        }

        [TestCase("clip.mp4", true)]
        [TestCase("CLIP.MP4", true)]
        [TestCase("clip.txt", false)]
        [TestCase("Nested/clip.mp4", false)]
        public void VideoChangedPath_RequiresDirectSupportedChild(string relativePath, bool expected)
        {
            string directory = Path.Combine(Path.GetTempPath(), "video-watch-root");
            string path = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.AreEqual(expected, VideoCatalog.IsDirectChildSupportedPath(
                directory, path, new[] { ".mp4" }));
        }

        [TestCase("clip.wav", true)]
        [TestCase("CLIP.WAV", true)]
        [TestCase("clip.mp4", false)]
        [TestCase("Nested/clip.wav", false)]
        public void SoundChangedPath_RequiresDirectSupportedChild(string relativePath, bool expected)
        {
            string directory = Path.Combine(Path.GetTempPath(), "sound-watch-root");
            string path = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.AreEqual(expected, SoundClipCatalog.IsDirectChildSupportedPath(
                directory, path, new[] { ".wav" }));
        }
    }
}
