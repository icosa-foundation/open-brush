using System;
using NUnit.Framework;

namespace TiltBrush
{
    public class TestSafSketchMutationGuard
    {
        private static StorageDocument MakeDocument(long providerFlags = (1L << 2) | (1L << 6))
        {
            return new StorageDocument(
                new StorageDocumentId("sketch-id"), default, "Sketch.tilt",
                TiltFile.TILT_MIME_TYPE, false, null, DateTime.UtcNow,
                providerFlags, "Sketch.tilt");
        }

        [TestCase("backend")]
        [TestCase("root")]
        public void StaleSceneFileCannotRenameOrDelete(string change)
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var backend = new CatalogTestBackend { RootIdentity = "root-a" };
            try
            {
                UserStorage.SetBackendForTests(backend);
                var file = new SafSceneFileInfo(backend, MakeDocument());
                if (change == "backend")
                {
                    UserStorage.SetBackendForTests(new CatalogTestBackend());
                }
                else
                {
                    backend.RootIdentity = "root-b";
                }

                Assert.AreEqual(StorageResultCode.Cancelled,
                    file.DeleteFromCurrentRoot().Code);
                Assert.AreEqual(StorageResultCode.Cancelled,
                    file.RenameInCurrentRoot("Renamed.tilt").Code);
                file.Delete();
                Assert.AreEqual("sketch-id", file.Rename("Renamed"));
                Assert.AreEqual(0, backend.DeleteCalls);
                Assert.AreEqual(0, backend.RenameCalls);
                Assert.IsFalse(file.Available);
            }
            finally
            {
                UserStorage.SetBackendForTests(previous);
            }
        }

        [Test]
        public void CurrentSceneFileCanRenameAndDelete()
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var backend = new CatalogTestBackend
            {
                RootIdentity = "root-a",
                RenameResultDocumentId = new StorageDocumentId("renamed-id"),
            };
            try
            {
                UserStorage.SetBackendForTests(backend);
                var file = new SafSceneFileInfo(backend, MakeDocument());
                Assert.AreEqual(StorageResultCode.Success,
                    file.DeleteFromCurrentRoot().Code);
                Assert.AreEqual(StorageResultCode.Success,
                    file.RenameInCurrentRoot("Renamed.tilt").Code);
                Assert.AreEqual(1, backend.DeleteCalls);
                Assert.AreEqual(1, backend.RenameCalls);
                Assert.IsTrue(file.Available);
                Assert.AreEqual("renamed-id", file.StorageId);
                Assert.AreEqual("Renamed", file.HumanName);
                Assert.AreEqual("Renamed.tilt", file.Document.RelativeDisplayPath);
            }
            finally
            {
                UserStorage.SetBackendForTests(previous);
            }
        }

        [Test]
        public void RenameOnlySceneFileCannotBeOverwrittenButCanStillBeRenamed()
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var backend = new CatalogTestBackend { RootIdentity = "root-a" };
            try
            {
                UserStorage.SetBackendForTests(backend);
                var file = new SafSceneFileInfo(backend, MakeDocument(1L << 6));

                Assert.IsTrue(file.ReadOnly);
                Assert.AreEqual(StorageResultCode.Success,
                    file.RenameInCurrentRoot("Renamed.tilt").Code);
                Assert.AreEqual(1, backend.RenameCalls);
            }
            finally
            {
                UserStorage.SetBackendForTests(previous);
            }
        }

        [Test]
        public void CommittedSceneUsesFreshProviderCapabilities()
        {
            var committed = new StorageDocument(
                new StorageDocumentId("committed-id"),
                new StorageDocumentId("parent-id"),
                "Saved.tilt",
                TiltFile.TILT_MIME_TYPE,
                false,
                123,
                DateTime.UtcNow,
                (1L << 2) | (1L << 6),
                "Saved.tilt");
            var backend = new CatalogTestBackend
            {
                Listing = () => StorageDirectoryResult.Succeeded(
                    new[] { committed }),
            };

            StorageDocument resolved = SaveLoadScript.ResolveCommittedSafDocument(
                backend,
                StorageArea.Sketches,
                committed.DocumentId,
                committed.DisplayName,
                previousDocument: null);

            Assert.AreSame(committed, resolved);
            Assert.IsTrue(resolved.SupportsReplacement);
        }
    }
}
