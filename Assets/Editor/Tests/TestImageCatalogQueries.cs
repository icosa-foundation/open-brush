// Copyright 2026 The Open Brush Authors

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    public class ImageQueryTestCatalog : ReferenceImageCatalog
    {
        public string TestRoot;
        public override string HomeDirectory => TestRoot;
        protected override void ProcessReferenceDirectory(bool userOverlay = true) { }
        public void Initialize(string root)
        {
            TestRoot = root;
            m_RequestedLoads = new Stack<int>();
            ChangeDirectory(root);
        }
    }

    internal class CatalogTestBackend : IUserStorageBackend
    {
        public StorageBackendKind Kind => StorageBackendKind.StorageAccessFramework;
        public bool IsReady => true;
        public string RootIdentity { get; set; } = "catalog-query-root";
        public Func<StorageDirectoryResult> Listing;
        public int RenameCalls;
        public int DeleteCalls;
        public StorageDirectoryResult List(StorageArea area, string path, CancellationToken cancellationToken) => Listing();
        public StorageTreeResult EnumerateTree(StorageArea area, string path, StorageTreeQuery query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Stream OpenRead(StorageArea area, string relativePath, bool requireSeekable, CancellationToken cancellationToken) => throw new NotSupportedException();
        public bool Exists(StorageArea area, string relativePath) => false;
        public Stream OpenRead(StorageDocumentId documentId, bool requireSeekable, CancellationToken cancellationToken) => throw new NotSupportedException();
        public IStorageWriteTransaction BeginWrite(StorageArea area, string relativePath, string mimeType, CancellationToken cancellationToken, StorageDocumentId targetDocumentId = default) => throw new NotSupportedException();
        public StorageMutationResult Rename(StorageDocumentId documentId, string newDisplayName, CancellationToken cancellationToken)
        {
            ++RenameCalls;
            return new StorageMutationResult(StorageResultCode.Success, documentId);
        }
        public StorageMutationResult Delete(StorageDocumentId documentId, CancellationToken cancellationToken)
        {
            ++DeleteCalls;
            return new StorageMutationResult(StorageResultCode.Success, documentId);
        }
        public string Materialize(StorageDocumentId documentId, MaterializationScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public string GetMaterializationPath(StorageDocumentId documentId) => throw new NotSupportedException();
    }

    public class TestImageCatalogQueries
    {
        [TestCase("current")]
        [TestCase("directory")]
        [TestCase("away-and-back")]
        [TestCase("root")]
        [TestCase("backend")]
        [TestCase("dispose")]
        public async Task DelayedQueryOnlyPublishesToItsOriginalDirectoryAndSource(string change)
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var owner = new GameObject("ImageQueryTest");
            owner.SetActive(false);
            var release = new TaskCompletionSource<StorageDirectoryResult>();
            var backend = new CatalogTestBackend { Listing = () => release.Task.GetAwaiter().GetResult() };
            IEnumerator<object> scan = null;
            try
            {
                UserStorage.SetBackendForTests(backend);
                var catalog = owner.AddComponent<ImageQueryTestCatalog>();
                string root = Path.Combine(Path.GetTempPath(), $"image-query-{Guid.NewGuid():N}");
                catalog.Initialize(root);
                // The query is tested without starting Unity's icon cache coroutine.
                typeof(ReferenceImageCatalog).GetField("m_RunningImageCacheCoroutine", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(catalog, true);
                int publications = 0;
                catalog.CatalogChanged += () => ++publications;
                scan = (IEnumerator<object>)typeof(ReferenceImageCatalog)
                    .GetMethod("QuerySafReferenceDirectory", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(catalog, new object[] { root, false });
                Assert.IsTrue(scan.MoveNext());
                if (change == "directory" || change == "away-and-back") catalog.ChangeDirectory(Path.Combine(root, "B"));
                if (change == "away-and-back") catalog.ChangeDirectory(root);
                if (change == "root") backend.RootIdentity = "replacement-root";
                if (change == "backend") UserStorage.SetBackendForTests(new CatalogTestBackend());
                if (change == "dispose") scan.Dispose();
                release.SetResult(StorageDirectoryResult.Succeeded(Array.Empty<StorageDocument>()));
                if (change != "dispose")
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                    while (scan.MoveNext())
                    {
                        Assert.Less(DateTime.UtcNow, deadline, "Delayed image query did not finish.");
                        await Task.Delay(10);
                    }
                }
                Assert.AreEqual(change == "current" ? 1 : 0, publications);
                Assert.IsFalse((bool)typeof(ReferenceImageCatalog)
                    .GetField("m_SafQueryInProgress", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(catalog));
            }
            finally
            {
                release.TrySetResult(StorageDirectoryResult.Succeeded(Array.Empty<StorageDocument>()));
                scan?.Dispose();
                UserStorage.SetBackendForTests(previous);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
