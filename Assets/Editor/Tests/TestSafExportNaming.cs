using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;

namespace TiltBrush
{
    public class TestSafExportNaming
    {
        private sealed class ExportBackend : IUserStorageBackend
        {
            private readonly LocalUserStorageBackend m_Local;
            public ExportBackend(string root) { m_Local = new LocalUserStorageBackend(_ => root); }
            public bool FailWrites;
            public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
            public bool IsReady => true;
            public string RootIdentity { get; } = $"export-test-{Guid.NewGuid():N}";
            public StorageDirectoryResult List(StorageArea area, string path, CancellationToken token) => m_Local.List(area, path, token);
            public StorageTreeResult EnumerateTree(StorageArea area, string path, StorageTreeQuery query, CancellationToken token) => m_Local.EnumerateTree(area, path, query, token);
            public Stream OpenRead(StorageArea area, string relativePath, bool seekable, CancellationToken token) => m_Local.OpenRead(area, relativePath, seekable, token);
            public bool Exists(StorageArea area, string relativePath) => m_Local.Exists(area, relativePath);
            public Stream OpenRead(StorageDocumentId id, bool seekable, CancellationToken token) => m_Local.OpenRead(id, seekable, token);
            public IStorageWriteTransaction BeginWrite(StorageArea area, string path, string mime, CancellationToken token, StorageDocumentId targetDocumentId = default)
            {
                if (FailWrites) { throw new IOException("Injected export failure"); }
                return m_Local.BeginWrite(area, path, mime, token, targetDocumentId);
            }
            public StorageMutationResult Rename(StorageDocumentId id, string name, CancellationToken token) => m_Local.Rename(id, name, token);
            public StorageMutationResult Delete(StorageDocumentId id, CancellationToken token) => m_Local.Delete(id, token);
        }

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
        public void GaussianDirectoriesPreserveCompletedAndPendingDatasets(bool failFirst)
        {
            string fixture = Path.Combine(OpenBrushStorage.LocalStagingPath,
                $"gaussian-test-{Guid.NewGuid():N}");
            string shared = Path.Combine(fixture, "shared");
            var backend = new ExportBackend(shared);
            string recovery = SafPrivatePaths.GetRecoveryRootDirectory(backend.RootIdentity);
            Directory.CreateDirectory(shared);
            try
            {
                string first = Path.Combine(fixture, "first", "Sketch_00");
                string second = Path.Combine(fixture, "second", "Sketch_00");
                Directory.CreateDirectory(first);
                Directory.CreateDirectory(second);
                File.WriteAllText(Path.Combine(first, "cameras.txt"), "first");
                File.WriteAllText(Path.Combine(second, "cameras.txt"), "second");
                backend.FailWrites = failFirst;
                SafPublicationResult firstResult = SafStagedOutputPublisher.PublishUniqueDirectory(
                    backend, StorageArea.SplatPoses, first,
                    transactionOwnsPayload: false, CancellationToken.None);
                Assert.AreEqual(!failFirst, firstResult.Success, firstResult.Error);
                Assert.IsTrue(Directory.Exists(first));

                backend.FailWrites = false;
                SafPublicationResult secondResult = SafStagedOutputPublisher.PublishUniqueDirectory(
                    backend, StorageArea.SplatPoses, second,
                    transactionOwnsPayload: false, CancellationToken.None);
                Assert.IsTrue(secondResult.Success, secondResult.Error);
                Assert.IsTrue(Directory.Exists(second));
                Assert.AreEqual("second", File.ReadAllText(
                    Path.Combine(shared, "Sketch_00 0", "cameras.txt")));
                if (failFirst)
                {
                    SafRecoveryReport report = SafStagedOutputPublisher.RecoverAll(
                        backend, CancellationToken.None);
                    Assert.AreEqual(1, report.Recovered);
                    Assert.AreEqual(0, report.Pending);
                }
                Assert.AreEqual("first", File.ReadAllText(
                    Path.Combine(shared, "Sketch_00", "cameras.txt")));
                Assert.AreEqual("second", File.ReadAllText(
                    Path.Combine(shared, "Sketch_00 0", "cameras.txt")));
            }
            finally
            {
                if (Directory.Exists(fixture)) { Directory.Delete(fixture, true); }
                if (Directory.Exists(recovery)) { Directory.Delete(recovery, true); }
            }
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
        public void PendingPublicationsReserveNamesEvenWithoutProviderEntries()
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Succeeded(Array.Empty<StorageDocument>())
            };
            Assert.AreEqual("Sketch 1", SafStagedOutputPublisher.SelectUniqueDirectoryName(
                backend, StorageArea.Exports, "Sketch", new[] { "Sketch", "Sketch 0" }, CancellationToken.None));
            Assert.AreEqual("Sketch", OpenBrushStorage.GetUniqueOutputDirectoryName(
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



        [Test]
        public void CompletedPublicationRecoveryFinishesPartialOwnedPayloadCleanup()
        {
            string fixture = Path.Combine(OpenBrushStorage.LocalStagingPath,
                $"cleanup-test-{Guid.NewGuid():N}");
            var backend = new ExportBackend(Path.Combine(fixture, "shared"));
            string recovery = SafPrivatePaths.GetRecoveryRootDirectory(backend.RootIdentity);
            string journalDirectory = Path.Combine(recovery, "publications");
            string alreadyDeleted = Path.Combine(fixture, "already-deleted");
            string remaining = Path.Combine(fixture, "remaining");
            Directory.CreateDirectory(remaining);
            File.WriteAllText(Path.Combine(remaining, "data.bin"), "payload");
            Directory.CreateDirectory(journalDirectory);
            var record = new SafPublicationRecord
            {
                TransactionId = "partial-cleanup",
                RootId = backend.RootIdentity,
                Area = StorageArea.Exports.ToString(),
                TransactionOwnsPayload = true,
                State = "Complete",
                Items = new System.Collections.Generic.List<SafPublicationItem>
                {
                    new SafPublicationItem { SourcePath = alreadyDeleted, IsDirectory = true },
                    new SafPublicationItem { SourcePath = remaining, IsDirectory = true },
                },
            };
            string journal = Path.Combine(journalDirectory, $"{record.TransactionId}.json");
            File.WriteAllText(journal, JsonConvert.SerializeObject(record));
            try
            {
                SafRecoveryReport report = SafStagedOutputPublisher.RecoverAll(
                    backend, CancellationToken.None);
                Assert.AreEqual(1, report.Recovered);
                Assert.AreEqual(0, report.Pending);
                Assert.IsFalse(Directory.Exists(remaining));
                Assert.IsFalse(File.Exists(journal));
            }
            finally
            {
                if (Directory.Exists(fixture)) { Directory.Delete(fixture, true); }
                if (Directory.Exists(recovery)) { Directory.Delete(recovery, true); }
            }
        }
    }
}
