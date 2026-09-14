// Copyright 2026 The Open Brush Authors

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    public class ImageRestorationTestCatalog : ReferenceImageCatalog
    {
        public string TestRoot;
        public override string HomeDirectory => TestRoot;
        public void SetImages(params ReferenceImage[] images)
        {
            m_Images = new List<ReferenceImage>(images);
        }
    }

    public class TestReferenceImageRestoration
    {
        [TestCase("B/reference.png")]
        [TestCase("B/missing.png")]
        public void LocalSavedReferenceDoesNotEnterActiveFolder(string savedPath)
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var owner = new GameObject("ImageRestorationTest");
            owner.SetActive(false);
            try
            {
                var catalog = owner.AddComponent<ImageRestorationTestCatalog>();
                catalog.TestRoot = Path.Combine(Path.GetTempPath(), $"image-restore-{Guid.NewGuid():N}");
                UserStorage.SetBackendForTests(new LocalUserStorageBackend(_ => catalog.TestRoot));
                var visible = new ReferenceImage(Path.Combine(catalog.TestRoot, "A", "visible.png"));
                catalog.SetImages(visible);

                ReferenceImage restored = catalog.RelativePathToImage(savedPath);

                Assert.AreEqual(Path.GetFullPath(Path.Combine(catalog.TestRoot, savedPath)), restored.FileFullPath);
                Assert.AreEqual(1, catalog.ItemCount);
                Assert.AreSame(visible, catalog.IndexToImage(0));
                Assert.AreSame(visible, catalog.RelativePathToImage("A/visible.png"));
                Assert.IsNull(catalog.RelativePathToImage("../outside.png"));
                Assert.AreEqual(1, catalog.ItemCount);
            }
            finally
            {
                UserStorage.SetBackendForTests(previous);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
