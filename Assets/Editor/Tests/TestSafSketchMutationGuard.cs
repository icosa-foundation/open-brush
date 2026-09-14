using System;
using NUnit.Framework;

namespace TiltBrush
{
    public class TestSafSketchMutationGuard
    {
        private static StorageDocument MakeDocument()
        {
            return new StorageDocument(
                new StorageDocumentId("sketch-id"), default, "Sketch.tilt",
                TiltFile.TILT_MIME_TYPE, false, null, DateTime.UtcNow, 0, "Sketch.tilt");
        }

        [TestCase("root")]
        [TestCase("backend")]
        public void StaleSceneFileCannotRenameOrDelete(string change)
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var backend = new CatalogTestBackend { RootIdentity = "root-a" };
            try
            {
                UserStorage.SetBackendForTests(backend);
                var file = new SafSceneFileInfo(backend, MakeDocument());
                if (change == "root") { backend.RootIdentity = "root-b"; }
                else { UserStorage.SetBackendForTests(new CatalogTestBackend()); }

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
            var backend = new CatalogTestBackend { RootIdentity = "root-a" };
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
            }
            finally
            {
                UserStorage.SetBackendForTests(previous);
            }
        }
    }
}
