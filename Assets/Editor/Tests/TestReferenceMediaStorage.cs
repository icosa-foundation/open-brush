using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestReferenceMediaStorage
    {
        [TestCase("stream.txt", true)]
        [TestCase("stream.TXT", true)]
        [TestCase("clip.mp4", false)]
        public void VideoExtensionMatching_RecognizesNetworkPointers(string path, bool expected)
        {
            var video = new ReferenceVideo(path, "fixture", null, path);
            Assert.AreEqual(expected, video.NetworkVideo);
        }

        [Test]
        public void VideoRestore_ResolvesSiblingFoldersWithoutChangingThePanelListing()
        {
            string root = Path.Combine(Path.GetTempPath(), $"open-brush-video-restore-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(root, "A"));
            Directory.CreateDirectory(Path.Combine(root, "B"));
            try
            {
                File.WriteAllText(Path.Combine(root, "A", "clip.mp4"), "first fixture");
                File.WriteAllText(Path.Combine(root, "B", "clip.MP4"), "second fixture");
                var backend = new LocalUserStorageBackend(_ => root);
                string[] extensions = { ".mp4" };
                var first = VideoCatalog.ResolveVideoByPersistentPath(backend, root, "A\\clip.mp4", extensions);
                var second = VideoCatalog.ResolveVideoByPersistentPath(backend, root, "./B/clip.MP4", extensions);
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                Assert.AreEqual("A/clip.mp4", first.PersistentPath);
                Assert.AreEqual("B/clip.MP4", second.PersistentPath);
                Assert.AreNotEqual(first.AbsolutePath, second.AbsolutePath);
                Assert.IsFalse(first.IsInitialized);
                Assert.IsFalse(second.IsInitialized);
                Assert.IsEmpty(VideoCatalog.ListSafFiles(backend, StorageArea.MediaLibraryVideos, ""));
                foreach (string path in new[] { "missing.mp4", "../outside.mp4", "/absolute.mp4",
                             "C:/absolute.mp4", "A/clip.bin", "", null })
                {
                    Assert.IsNull(VideoCatalog.ResolveVideoByPersistentPath(backend, root, path, extensions), path);
                }
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void ModelCatalog_SupportedExtensionsMatchAllStorageBackends()
        {
            var extensions = ModelCatalog.GetSupportedExtensions();
            CollectionAssert.IsSubsetOf(
                new[] { ".gltf2", ".gltf", ".glb", ".ply", ".spz", ".sog", ".svg", ".obj", ".vox" },
                extensions);
            Assert.IsTrue(extensions.Contains(".SPZ"));
            Assert.IsTrue(extensions.Contains(".SOG"));
#if USD_SUPPORTED
            CollectionAssert.IsSubsetOf(new[] { ".usda", ".usdc", ".usd" }, extensions);
#else
            Assert.IsFalse(extensions.Contains(".usd"));
#endif
#if FBX_SUPPORTED
            Assert.IsTrue(extensions.Contains(".fbx"));
#else
            Assert.IsFalse(extensions.Contains(".fbx"));
#endif
        }

        [Test]
        public void VideoQuery_ListsOnlySelectedDirectoryAndPreservesLogicalPaths()
        {
            string root = Path.Combine(Path.GetTempPath(), $"open-brush-video-query-{Guid.NewGuid():N}");
            string nested = Path.Combine(root, "Nested");
            string deeper = Path.Combine(nested, "Deeper");
            Directory.CreateDirectory(deeper);
            try
            {
                File.WriteAllText(Path.Combine(root, "root.mp4"), "root video");
                File.WriteAllText(Path.Combine(nested, "clip.mp4"), "child video");
                File.WriteAllText(Path.Combine(deeper, "hidden.mp4"), "nested video");
                var backend = new LocalUserStorageBackend(_ => root);
                var parent = VideoCatalog.ListSafFiles(backend, StorageArea.MediaLibraryVideos, "");
                Assert.AreEqual("root.mp4", parent.Single().RelativeDisplayPath);
                var child = VideoCatalog.ListSafFiles(backend, StorageArea.MediaLibraryVideos, "Nested");
                Assert.AreEqual("Nested/clip.mp4", child.Single().RelativeDisplayPath);
                Assert.IsFalse(child.Single().IsDirectory);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        private static StorageDocument OverwriteDocument(string id, string name, bool directory)
        {
            return new StorageDocument(new StorageDocumentId(id), default, name,
                "application/octet-stream", directory, null, null, 0, name);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SafOverwrite_RejectsDirectoryByNameOrExplicitIdentity(bool explicitIdentity)
        {
            StorageDocument directory = OverwriteDocument("directory", "target.bin", true);
            Assert.Throws<IOException>(() => SafFileWriteTransaction.ResolveFileOverwriteTarget(
                new[] { directory }, explicitIdentity ? directory.DocumentId : default, "target.bin"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SafOverwrite_AllowsExistingFile(bool explicitIdentity)
        {
            StorageDocument file = OverwriteDocument("file", "target.bin", false);
            Assert.AreEqual(file.DocumentId, SafFileWriteTransaction.ResolveFileOverwriteTarget(
                new[] { file }, explicitIdentity ? file.DocumentId : default, "target.bin"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SafOverwrite_RejectsFileAndDirectoryNameCollision(bool explicitIdentity)
        {
            StorageDocument file = OverwriteDocument("file", "target.bin", false);
            StorageDocument directory = OverwriteDocument("directory", "TARGET.BIN", true);
            Assert.Throws<IOException>(() => SafFileWriteTransaction.ResolveFileOverwriteTarget(
                new[] { file, directory }, explicitIdentity ? file.DocumentId : default, "target.bin"));
        }

        [Test]
        public void SafOverwrite_AllowsNewFileAlongsideUnrelatedDirectory()
        {
            StorageDocument directory = OverwriteDocument("directory", "other", true);
            Assert.IsFalse(SafFileWriteTransaction.ResolveFileOverwriteTarget(
                new[] { directory }, default, "target.bin").IsValid);
        }

        [Test]
        public void GltfBundle_IncludesBuffersAndTexturesOnceAndSkipsEmbeddedData()
        {
            var gltf = Newtonsoft.Json.Linq.JObject.Parse(@"{
                'buffers': [{'uri':'geometry.bin'}],
                'images': [{'uri':'textures/My Texture.png'}, {'uri':'geometry.bin'},
                           {'uri':'data:image/png;base64,AA=='}, {'bufferView':0}]
            }");
            CollectionAssert.AreEquivalent(new[] { "geometry.bin", "textures/My Texture.png" },
                ApiMethods.GetGltfExternalFiles(gltf));
            Assert.IsEmpty(ApiMethods.GetGltfExternalFiles(new Newtonsoft.Json.Linq.JObject()));
        }

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
            var first = new ReferenceImage("cache-a/image.png", "id-a", null, 1, "./Nested/image.png");
            var reopened = new ReferenceImage("cache-b/image.png", "id-b", null, 1, "./Nested/image.png");
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
