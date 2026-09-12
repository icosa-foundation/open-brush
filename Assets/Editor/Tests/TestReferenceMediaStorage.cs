using NUnit.Framework;

namespace TiltBrush
{
    internal class TestReferenceMediaStorage
    {
        [TestCase("map_Kd My Texture.png", "My Texture.png")]
        [TestCase("map_Kd -s 1 1 1 -o -1 0 My Texture.png", "My Texture.png")]
        [TestCase("bump -bm 0.5 Textures/My  Texture.png", "Textures/My  Texture.png")]
        [TestCase("map_Kd -mm 0 1 \"My Texture.png\"", "My Texture.png")]
        [TestCase("map_Kd\t-clamp on\tMy Texture.png", "My Texture.png")]
        [TestCase("Kd 1 1 1", null)]
        [TestCase("map_Kd -s invalid", null)]
        public void SafObjTextures_PreserveFilenameAfterOptions(string line, string expected)
        {
            Assert.AreEqual(expected, SafUserStorageBackend.GetMaterialTexturePath(line));
        }

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
