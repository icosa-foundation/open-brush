using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace TiltBrush
{
    internal class SavedStrokeCatalogParityTests
    {
        [Test]
        public void LocalSavedStrokeRefreshTracksNestedFolderMovesAndDeletion()
        {
            string root = Path.Combine(Path.GetTempPath(), $"saved-stroke-refresh-{System.Guid.NewGuid():N}");
            string container = Path.Combine(root, "A", "Container.tilt");
            Directory.CreateDirectory(container);
            try
            {
                foreach (string name in new[] { TiltFile.FN_METADATA, TiltFile.FN_SKETCH, TiltFile.FN_THUMBNAIL })
                {
                    File.WriteAllText(Path.Combine(container, name), name);
                }
                var set = (FileSketchSet)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(FileSketchSet));
                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                void Set(string name, object value) => typeof(FileSketchSet).GetField(name, flags).SetValue(set, value);
                var sketches = typeof(FileSketchSet).GetField("m_Sketches", flags);
                sketches.SetValue(set, System.Activator.CreateInstance(sketches.FieldType));
                Set("m_Type", SketchSetType.SavedStrokes);
                Set("m_SketchesPath", root);
                Set("m_RequestedLoads", new System.Collections.Generic.Stack<int>());
                Set("Update__working", new System.Collections.Generic.Stack<int>());
                Set("m_ToAdd", new System.Collections.Queue());
                Set("m_ToDelete", new System.Collections.Queue());
                int updates = 0;
                Set("OnChanged", (System.Action)(() => ++updates));
                set.RequestRefresh();
                set.Update();
                Assert.AreEqual(1, set.NumSketches);
                Assert.AreEqual(container, set.GetSketchSceneFileInfo(0).FullPath);
                Directory.Move(Path.Combine(root, "A"), Path.Combine(root, "B"));
                set.RequestRefresh();
                set.Update();
                Assert.AreEqual(1, set.NumSketches);
                Assert.AreEqual(Path.Combine(root, "B", "Container.tilt"), set.GetSketchSceneFileInfo(0).FullPath);
                Directory.Delete(Path.Combine(root, "B"), true);
                set.RequestRefresh();
                set.Update();
                Assert.AreEqual(0, set.NumSketches);
                Assert.AreEqual(3, updates);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

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

        [TestCase("Sketch.tilt", false, true)]
        [TestCase("Sketch.TILT", true, true)]
        [TestCase("Folder", true, false)]
        [TestCase("Notes.txt", false, false)]
        public void SafSketchbookIncludesTiltFilesAndDirectories(
            string name, bool isDirectory, bool expected)
        {
            var document = new StorageDocument(
                new StorageDocumentId(name), default, name, "application/octet-stream",
                isDirectory, null, null, 0, name);

            Assert.AreEqual(expected, SafSketchSet.IsTopLevelSketchDocument(document));
        }

        [Test]
        public void SafDirectorySketchCannotBeOverwrittenAsAnArchive()
        {
            const long supportsRename = 1L << 6;
            var backend = new LocalUserStorageBackend(_ => Path.GetTempPath());
            var directory = new StorageDocument(
                new StorageDocumentId("directory"), default, "Directory.tilt",
                "vnd.android.document/directory", true, null, null,
                supportsRename, "Directory.tilt");
            var archive = new StorageDocument(
                new StorageDocumentId("archive"), default, "Archive.tilt",
                TiltFile.TILT_MIME_TYPE, false, 0, null,
                supportsRename, "Archive.tilt");

            Assert.IsTrue(new SafSceneFileInfo(backend, directory).ReadOnly);
            Assert.IsFalse(new SafSceneFileInfo(backend, archive).ReadOnly);
        }
    }
}
