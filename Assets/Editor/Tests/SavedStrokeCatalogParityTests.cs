using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace TiltBrush
{
    internal class SavedStrokeCatalogParityTests
    {
        [Test]
        public void SafDirectorySketchReadsContainerChildrenRatherThanDirectoryStream()
        {
            string root = Path.Combine(Path.GetTempPath(), $"saved-stroke-container-{System.Guid.NewGuid():N}");
            string container = Path.Combine(root, "Nested", "Container.tilt");
            Directory.CreateDirectory(container);
            try
            {
                foreach (string name in new[] { TiltFile.FN_METADATA, TiltFile.FN_SKETCH, TiltFile.FN_THUMBNAIL })
                {
                    File.WriteAllText(Path.Combine(container, name), name);
                }
                var backend = new LocalUserStorageBackend(_ => root);
                StorageDocument document = System.Linq.Enumerable.Single(backend.List(
                    StorageArea.SavedStrokes, "Nested", System.Threading.CancellationToken.None).Documents);
                var info = new SafSceneFileInfo(backend, document, StorageArea.SavedStrokes);
                Assert.IsTrue(info.IsHeaderValid());
                using (var reader = new StreamReader(info.GetReadStream(TiltFile.FN_SKETCH)))
                {
                    Assert.AreEqual(TiltFile.FN_SKETCH, reader.ReadToEnd());
                }
                Assert.IsNull(info.GetReadStream("../outside"));
                File.Delete(Path.Combine(container, TiltFile.FN_THUMBNAIL));
                Assert.IsFalse(info.IsHeaderValid());
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestCase("Sketches", "Sketches/Child", true)]
        [TestCase("Sketches", "SketchesSibling", false)]
        [TestCase("Sketches", "SketchesOther/Child", false)]
        public void SavedStrokeFolderContainmentHonorsDirectoryBoundary(
            string root, string path, bool expected)
        {
            Assert.AreEqual(expected,
                SavedStrokesCatalog.IsPathWithinDirectory(root, path));
        }

        [Test]
        public void SavedStrokeListingUsesDirectChildrenOnly()
        {
            string root = Path.Combine(Path.GetTempPath(), "SavedStrokes");
            Assert.IsTrue(SavedStrokesCatalog.IsDirectChildPath(
                root, Path.Combine(root, "A.tilt")));
            Assert.IsFalse(SavedStrokesCatalog.IsDirectChildPath(
                root, Path.Combine(root, "Nested", "A.tilt")));
        }

        [TestCase("Folder", true)]
        [TestCase("Container.tilt", false)]
        [TestCase("Container.TILT", false)]
        public void TiltContainersAreNotNavigableFolders(string name, bool expected)
        {
            Assert.AreEqual(expected,
                SavedStrokesCatalog.IsNavigableDirectory(name));
        }

        [Test]
        public void SavedStrokeTreeDiscoveryStopsAtTiltContainers()
        {
            string root = Path.Combine(Path.GetTempPath(),
                $"ob-savedstroke-test-{System.Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Nested"));
                Directory.CreateDirectory(Path.Combine(root, "Container.tilt", "Inner"));
                File.WriteAllText(Path.Combine(root, "root.tilt"), "placeholder");
                File.WriteAllText(Path.Combine(root, "Nested", "child.TILT"), "placeholder");
                File.WriteAllText(Path.Combine(root, "Container.tilt", "Inner", "false.tilt"), "placeholder");

                var backend = new LocalUserStorageBackend(_ => root);
                StorageTreeResult result = backend.EnumerateTree(
                    StorageArea.SavedStrokes, "",
                    new StorageTreeQuery(
                        recursive: true,
                        includeDirectories: true,
                        includeExtensions: new[] { ".tilt" },
                        recurseIntoDirectory: name => !name.EndsWith(
                            ".tilt", System.StringComparison.OrdinalIgnoreCase)),
                    CancellationToken.None);

                Assert.IsTrue(result.Success);
                CollectionAssert.AreEquivalent(
                    new[] { "root.tilt", "Nested/child.TILT", "Container.tilt" },
                    result.Entries.Where(entry => entry.DisplayName.EndsWith(
                        ".tilt", System.StringComparison.OrdinalIgnoreCase))
                        .Select(entry => entry.RelativeDisplayPath));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }
    }
}
