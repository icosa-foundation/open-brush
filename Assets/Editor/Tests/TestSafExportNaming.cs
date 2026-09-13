using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            public Stream OpenRead(StorageDocumentId id, bool seekable, CancellationToken token) => m_Local.OpenRead(id, seekable, token);
            public IStorageWriteTransaction BeginWrite(StorageArea area, string path, string mime, CancellationToken token, StorageDocumentId targetDocumentId = default)
            {
                if (FailWrites) { throw new IOException("Injected export failure"); }
                return m_Local.BeginWrite(area, path, mime, token, targetDocumentId);
            }
            public StorageMutationResult Rename(StorageDocumentId id, string name, CancellationToken token) => m_Local.Rename(id, name, token);
            public StorageMutationResult Delete(StorageDocumentId id, CancellationToken token) => m_Local.Delete(id, token);
            public string Materialize(StorageDocumentId id, MaterializationScope scope, CancellationToken token) => m_Local.Materialize(id, scope, token);
            public string GetMaterializationPath(StorageDocumentId id) => m_Local.GetMaterializationPath(id);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedExportsPreserveCompletedAndPendingDestinations(bool failFirst)
        {
            string fixture = Path.Combine(OpenBrushStorage.LocalStagingPath, $"export-test-{Guid.NewGuid():N}");
            string shared = Path.Combine(fixture, "shared");
            var backend = new ExportBackend(shared);
            string recovery = SafTransactionJournal.GetRecoveryRootDirectory(backend.RootIdentity);
            Directory.CreateDirectory(shared);
            try
            {
                string first = Path.Combine(fixture, "first", "Sketch");
                string second = Path.Combine(fixture, "second", "Sketch");
                Directory.CreateDirectory(first);
                Directory.CreateDirectory(second);
                File.WriteAllText(Path.Combine(first, "model.obj"), "first");
                File.WriteAllText(Path.Combine(second, "model.obj"), "second");
                string readme1 = Path.Combine(fixture, "first", "README.txt");
                string readme2 = Path.Combine(fixture, "second", "README.txt");
                File.WriteAllText(readme1, "readme");
                File.WriteAllText(readme2, "readme");
                backend.FailWrites = failFirst;
                var firstResult = SafStagedOutputPublisher.PublishExport(backend, first, readme1, CancellationToken.None);
                Assert.AreEqual(!failFirst, firstResult.Success, firstResult.Error);
                Assert.AreEqual(failFirst, Directory.Exists(first));
                backend.FailWrites = false;
                var secondResult = SafStagedOutputPublisher.PublishExport(backend, second, readme2, CancellationToken.None);
                Assert.IsTrue(secondResult.Success, secondResult.Error);
                Assert.AreEqual("second", File.ReadAllText(Path.Combine(shared, "Sketch 0", "model.obj")));
                if (failFirst)
                {
                    var report = SafStagedOutputPublisher.RecoverAll(backend, CancellationToken.None);
                    Assert.AreEqual(1, report.Recovered);
                    Assert.AreEqual(0, report.Pending);
                }
                Assert.AreEqual("first", File.ReadAllText(Path.Combine(shared, "Sketch", "model.obj")));
                Assert.AreEqual("second", File.ReadAllText(Path.Combine(shared, "Sketch 0", "model.obj")));
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
            Assert.AreEqual("Sketch 1", SafStagedOutputPublisher.SelectExportDirectoryName(
                backend, "Sketch", Array.Empty<string>(), CancellationToken.None));
        }

        [Test]
        public void PendingPublicationsReserveNamesEvenWithoutProviderEntries()
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Succeeded(Array.Empty<StorageDocument>())
            };
            Assert.AreEqual("Sketch 1", SafStagedOutputPublisher.SelectExportDirectoryName(
                backend, "Sketch", new[] { "Sketch", "Sketch 0" }, CancellationToken.None));
            Assert.AreEqual("Sketch", SafStagedOutputPublisher.SelectExportDirectoryName(
                backend, "Sketch", Array.Empty<string>(), CancellationToken.None));
        }

        [Test]
        public void MissingExportAreaUsesOriginalName()
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Failed(StorageResultCode.NotFound, "missing")
            };
            Assert.AreEqual("Sketch", SafStagedOutputPublisher.SelectExportDirectoryName(
                backend, "Sketch", Array.Empty<string>(), CancellationToken.None));
        }

        [Test]
        public void ListingFailureCannotChooseAnUncheckedName()
        {
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Failed(StorageResultCode.ProviderUnavailable, "denied")
            };
            Assert.Throws<IOException>(() => SafStagedOutputPublisher.SelectExportDirectoryName(
                backend, "Sketch", Array.Empty<string>(), CancellationToken.None));
        }

        [Test]
        public void RootReplacementCannotChooseAName()
        {
            var backend = new CatalogTestBackend();
            backend.Listing = () =>
            {
                backend.RootIdentity = "changed";
                return StorageDirectoryResult.Succeeded(Array.Empty<StorageDocument>());
            };
            Assert.Throws<IOException>(() => SafStagedOutputPublisher.SelectExportDirectoryName(
                backend, "Sketch", Array.Empty<string>(), CancellationToken.None));
        }
    }
}
