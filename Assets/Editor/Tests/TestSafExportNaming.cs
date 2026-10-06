using System;
using System.IO;
using NUnit.Framework;

namespace TiltBrush
{
    public class TestSafExportNaming
    {
        [TestCase(1)]
        [TestCase(2)]
        public void LegacyGlbStreamMatchesFileOutputAndCopiesSidecars(int version)
        {
            string root = Path.Combine(Path.GetTempPath(), $"glb-stream-{Guid.NewGuid():N}");
            string local = Path.Combine(root, "local");
            Directory.CreateDirectory(local);
            try
            {
                string source = Path.Combine(root, "texture.png");
                File.WriteAllBytes(source, new byte[] { 1, 2, 3, 4 });
                string localGlb = Path.Combine(local, "model.glb");
                using (var globals = new GlTF_Globals(Path.Combine(root, "scratch-local"), version))
                {
                    globals.binary = true;
                    globals.extras["texture"] = ExportFileReference.CreateLocal(source, "texture.png");
                    globals.OpenFiles(localGlb);
                    globals.Write();
                    globals.CloseFiles();
                }
                var backend = new LocalUserStorageBackend(_ => Path.Combine(root, "shared"));
                OpenBrushStorage.WriteSharedFile(backend, StorageArea.Exports, "model.glb", output =>
                {
                    using var globals = new GlTF_Globals(Path.Combine(root, "scratch-shared"), version);
                    globals.binary = true;
                    globals.extras["texture"] = ExportFileReference.CreateLocal(source, "texture.png");
                    globals.OpenFiles("model.glb", output, (file, destination) =>
                        OpenBrushStorage.WriteSharedFile(backend, StorageArea.Exports, destination, file.CopyTo));
                    globals.Write();
                    globals.CloseFiles();
                    Assert.IsTrue(output.CanWrite, "The transaction owns the output stream");
                });
                CollectionAssert.AreEqual(File.ReadAllBytes(localGlb),
                    File.ReadAllBytes(Path.Combine(root, "shared", "model.glb")));
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(local, "texture.png")),
                    File.ReadAllBytes(Path.Combine(root, "shared", "texture.png")));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void DirectExportsKeepFormatsTogetherAndChooseNewFolder()
        {
            string root = Path.Combine(Path.GetTempPath(), $"direct-export-{Guid.NewGuid():N}");
            var backend = new LocalUserStorageBackend(_ => root);
            Directory.CreateDirectory(root);
            try
            {
                string first = OpenBrushStorage.GetUniqueOutputDirectoryName(backend, StorageArea.Exports, "Sketch");
                foreach (string format in new[] { "glb", "json", "latk", "stl", "wrl" })
                {
                    OpenBrushStorage.WriteSharedFile(backend, StorageArea.Exports,
                        $"{first}/{format}/Sketch.{format}", output => output.WriteByte(42));
                }
                string second = OpenBrushStorage.GetUniqueOutputDirectoryName(backend, StorageArea.Exports, "Sketch");
                Assert.AreEqual("Sketch", first);
                Assert.AreEqual("Sketch 0", second);
                Assert.AreEqual(1, Directory.GetDirectories(root).Length);
                Assert.AreEqual(5, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
            }
            finally { Directory.Delete(root, true); }
        }

        [TestCase("Exports", StorageArea.Exports)]
        [TestCase("Snapshots", StorageArea.Snapshots)]
        [TestCase("Videos", StorageArea.Videos)]
        [TestCase("VRVideos", StorageArea.VrVideos)]
        [TestCase("SplatPoses", StorageArea.SplatPoses)]
        public void GeneratedPathsResolveToSharedDestinations(string folder, StorageArea expectedArea)
        {
            string path = Path.Combine(OpenBrushStorage.LocalStagingPath, folder, "Example", "output.bin");
            var (area, relativePath) = OpenBrushStorage.GetGeneratedDestination(path);
            Assert.AreEqual(expectedArea, area);
            Assert.AreEqual("Example/output.bin", relativePath);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingSharedFilesAndDirectoriesReserveNames(bool isDirectory)
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Succeeded(new[]
                {
                    new StorageDocument(new StorageDocumentId("existing"), default,
                        "Sketch", "", isDirectory, null, null, 0, "Sketch"),
                    new StorageDocument(new StorageDocumentId("case-collision"), default,
                        "SKETCH 0", "", isDirectory, null, null, 0, "SKETCH 0")
                })
            };
            Assert.AreEqual("Sketch 1", OpenBrushStorage.GetUniqueOutputDirectoryName(
                backend, StorageArea.Exports, "Sketch"));
        }

        [Test]
        public void MissingExportAreaUsesOriginalName()
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Failed(StorageResultCode.NotFound, "missing")
            };
            Assert.AreEqual("Sketch", OpenBrushStorage.GetUniqueOutputDirectoryName(
                backend, StorageArea.Exports, "Sketch"));
        }

        [Test]
        public void ListingFailureCannotChooseAnUncheckedName()
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Failed(StorageResultCode.ProviderUnavailable, "denied")
            };
            Assert.Throws<IOException>(() => OpenBrushStorage.GetUniqueOutputDirectoryName(
                backend, StorageArea.Exports, "Sketch"));
        }



    }
}
