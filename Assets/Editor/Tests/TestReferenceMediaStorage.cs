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
            var video = new ReferenceVideo(path, "fixture", path);
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

        private const long kSupportsDeleteAndRename = (1L << 2) | (1L << 6);

        private static StorageDocument OverwriteDocument(
            string id, string name, bool directory,
            long providerFlags = kSupportsDeleteAndRename)
        {
            return new StorageDocument(new StorageDocumentId(id), default, name,
                "application/octet-stream", directory, null, null, providerFlags, name);
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

        [TestCase(0)]
        [TestCase(1L << 6)]
        public void SafOverwrite_RejectsTargetsWithoutSafeBackupCleanup(long providerFlags)
        {
            StorageDocument file = OverwriteDocument(
                "file", "target.bin", false, providerFlags);
            Assert.Throws<IOException>(() => SafFileWriteTransaction.ResolveFileOverwriteTarget(
                new[] { file }, file.DocumentId, "target.bin"));
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

        [TestCase("target.bin.ob-tmp")]
        [TestCase("TARGET.BIN.OB-BAK")]
        [TestCase("target.bin.ob-invalid")]
        public void SafOverwrite_RejectsPreexistingReservedSidecars(string existingName)
        {
            StorageDocument existing = OverwriteDocument("user-file", existingName, false);
            Assert.Throws<IOException>(() => SafFileWriteTransaction.EnsureReservedNamesAvailable(
                new[] { existing },
                "target.bin.ob-tmp",
                "target.bin.ob-bak",
                "target.bin.ob-invalid"));
        }

        [Test]
        public void SafApiImports_RemoveOnlyOwnedStagingDirectoriesFromEarlierSessions()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"saf-api-import-cleanup-{Guid.NewGuid():N}");
            string owned = Path.Combine(root, "Images", $"import-{Guid.NewGuid():N}");
            string userDirectory = Path.Combine(root, "Images", "import-reference");
            Directory.CreateDirectory(owned);
            Directory.CreateDirectory(userDirectory);
            File.WriteAllText(Path.Combine(owned, "image.png"), "staged image");
            try
            {
                SafApiImportStaging.CleanupOrphans(root);

                Assert.IsFalse(Directory.Exists(owned));
                Assert.IsTrue(Directory.Exists(userDirectory));
            }
            finally
            {
                if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
            }
        }

        [Test]
        public void SafApiImports_PreserveCurrentSessionStagingDuringCleanup()
        {
            string root = Path.Combine(
                Path.GetTempPath(), $"saf-api-import-current-{Guid.NewGuid():N}");
            string current = SafApiImportStaging.CreateDirectory(
                Path.Combine(root, "Videos"));
            Directory.CreateDirectory(current);
            File.WriteAllText(Path.Combine(current, "video.mp4"), "staged video");
            try
            {
                SafApiImportStaging.CleanupOrphans(root);

                Assert.IsTrue(Directory.Exists(current));
            }
            finally
            {
                if (Directory.Exists(root)) { Directory.Delete(root, recursive: true); }
            }
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

        [TestCase("Models/Robot", "mesh.bin", "Models/Robot/mesh.bin", true)]
        [TestCase("Models/Robot", "../Textures/albedo.png", "Models/Textures/albedo.png", true)]
        [TestCase("Models/Robot", "take..final.bin", "Models/Robot/take..final.bin", true)]
        [TestCase("Models", "../../outside.bin", null, false)]
        [TestCase("", "../outside.bin", null, false)]
        [TestCase("Models", "/absolute.bin", null, false)]
        public void SafGltfDependencies_NormalizeWithinTheStorageArea(
            string directory, string reference, string expected, bool valid)
        {
            Assert.AreEqual(valid, SafGltfDataLoader.TryResolveAreaRelativePath(
                directory, reference, out string resolved));
            Assert.AreEqual(expected, resolved);
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
        public void SafImages_OpenProviderStreamWithoutMaterializingAFile()
        {
            byte[] bytes = { 1, 2, 3, 4 };
            var image = new ReferenceImage(
                "Nested/image.png", $"export-{Guid.NewGuid():N}",
                () => new MemoryStream(bytes, writable: false), bytes.Length,
                "./Nested/image.png");

            using (Stream source = image.OpenExportSource())
            {
                CollectionAssert.AreEqual(
                    bytes, ReferenceImage.ReadBytesWithLimit(source, bytes.Length));
                Assert.AreEqual("Nested/image.png", image.FileFullPath);
                Assert.AreEqual("./Nested/image.png", image.RelativePath);
            }
            Assert.IsFalse(File.Exists(image.FileFullPath));
        }

        [Test]
        public void SafSvgImages_ReadProviderTextWithoutMaterializingAFile()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0\"/></svg>";
            var image = new ReferenceImage(
                "shared.svg", "svg-id",
                () => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(svg)),
                svg.Length,
                "./shared.svg");

            Assert.AreEqual(svg, image.ReadSvgText(svg.Length));
            Assert.IsFalse(File.Exists(image.FilePath));
        }

        [Test]
        public void SafImages_EnforceSizeLimitWhenProviderOmitsLength()
        {
            byte[] bytes = { 1, 2, 3, 4 };
            var image = new ReferenceImage(
                "shared.hdr", "unknown-size-id",
                () => new MemoryStream(bytes, writable: false),
                null,
                "./shared.hdr");

            CollectionAssert.AreEqual(bytes, image.ReadEncodedBytes(bytes.Length));
            Assert.Throws<IOException>(() => image.ReadEncodedBytes(bytes.Length - 1));
        }

        [Test]
        public void SafSvgImages_EnforceSizeLimitWhenProviderOmitsLength()
        {
            const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"/>";
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(svg);
            var image = new ReferenceImage(
                "shared.svg", "unknown-svg-size-id",
                () => new MemoryStream(bytes, writable: false),
                null,
                "./shared.svg");

            Assert.AreEqual(svg, image.ReadSvgText(bytes.Length));
            Assert.Throws<IOException>(() => image.ReadSvgText(bytes.Length - 1));
        }

        [Test]
        public void SafVideos_PreserveSubfoldersAndSeparatePlaybackPath()
        {
            var first = new ReferenceVideo("cache-a/clip.mp4", "id-a", "First/clip.mp4");
            var second = new ReferenceVideo("cache-b/clip.mp4", "id-b", "Second/clip.mp4");
            var reopened = new ReferenceVideo("cache-c/clip.mp4", "id-c", "First/clip.mp4");
            Assert.AreEqual(first.PersistentPath, reopened.PersistentPath);
            Assert.AreNotEqual(first.PersistentPath, second.PersistentPath);
            Assert.AreEqual("cache-c/clip.mp4", reopened.AbsolutePath);
            Assert.AreEqual("clip.mp4", reopened.HumanName);
        }
    }
}
