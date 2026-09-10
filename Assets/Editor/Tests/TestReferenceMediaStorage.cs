using NUnit.Framework;

namespace TiltBrush
{
    internal class TestReferenceMediaStorage
    {
        [Test]
        public void SafImages_KeepLibraryPathAcrossCacheLocations()
        {
            var first = new ReferenceImage("cache-a/image.png", "id-a", null, null, 1, "./Nested/image.png");
            var reopened = new ReferenceImage("cache-b/image.png", "id-b", null, null, 1, "./Nested/image.png");
            Assert.AreEqual("./Nested/image.png", first.RelativePath);
            Assert.AreEqual(first.RelativePath, reopened.RelativePath);
            Assert.AreNotEqual(first.FileFullPath, reopened.FileFullPath);
        }

        [Test]
        public void SafVideos_PreserveSubfoldersAndSeparatePlaybackPath()
        {
            var first = new ReferenceVideo("cache-a/clip.mp4", "id-a", null, "First/clip.mp4");
            var second = new ReferenceVideo("cache-b/clip.mp4", "id-b", null, "Second/clip.mp4");
            var reopened = new ReferenceVideo("cache-c/clip.mp4", "id-c", null, "First/clip.mp4");
            Assert.AreEqual(first.PersistentPath, reopened.PersistentPath);
            Assert.AreNotEqual(first.PersistentPath, second.PersistentPath);
            Assert.AreEqual("cache-c/clip.mp4", reopened.AbsolutePath);
            Assert.AreEqual("clip.mp4", reopened.HumanName);
        }
    }
}
