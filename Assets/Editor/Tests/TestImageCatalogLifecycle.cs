// Copyright 2026 The Open Brush Authors

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace TiltBrush
{
    public class ImageLifecycleTestCatalog : ReferenceImageCatalog
    {
        protected override void ProcessReferenceDirectory(bool userOverlay = true) { }
    }

    public class BackgroundLifecycleTestCatalog : BackgroundImageCatalog
    {
        protected override void ProcessReferenceDirectory(bool userOverlay = true) { }
    }

    public class TestImageCatalogLifecycle
    {
        [TestCase(false)]
        [TestCase(true)]
        public void NavigationDisposesPreviousWatcherAndClearsIconRequests(bool background)
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var owner = new GameObject("ImageCatalogLifecycleTest");
            owner.SetActive(false);
            string root = Path.Combine(Path.GetTempPath(), $"image-watcher-{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            using (var native = new FileSystemWatcher(root))
            {
                try
                {
                    ReferenceImageCatalog catalog = background
                        ? (ReferenceImageCatalog)owner.AddComponent<BackgroundLifecycleTestCatalog>()
                        : owner.AddComponent<ImageLifecycleTestCatalog>();
                    UserStorage.SetBackendForTests(new LocalUserStorageBackend(_ => root));
                    var watcher = (FileWatcher)FormatterServices.GetUninitializedObject(typeof(FileWatcher));
                    typeof(FileWatcher).GetField("m_InternalFileWatcher", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(watcher, native);
                    var requested = new Stack<int>();
                    requested.Push(7);
                    Set(catalog, "m_RequestedLoads", requested);
                    Set(catalog, "m_FileWatcher", watcher);
                    var callback = (FileSystemEventHandler)Delegate.CreateDelegate(typeof(FileSystemEventHandler),
                        catalog, typeof(ReferenceImageCatalog).GetMethod("OnChanged", BindingFlags.Instance | BindingFlags.NonPublic));
                    watcher.FileChanged += callback;
                    watcher.FileCreated += callback;
                    watcher.FileDeleted += callback;
                    native.EnableRaisingEvents = true;

                    catalog.ChangeDirectory(Path.Combine(root, "not-created"));

                    Assert.Throws<ObjectDisposedException>(() => native.EnableRaisingEvents = true);
                    Assert.IsEmpty(requested);
                    Assert.IsTrue((bool)Get(catalog, "m_ResetImageEnumeration"));
                    Set(catalog, "m_DirNeedsProcessing", false);
                    watcher.NotifyChanged(Path.Combine(root, "old.png"));
                    callback(watcher, new FileSystemEventArgs(WatcherChangeTypes.Changed, root, "old.png"));
                    Assert.IsFalse((bool)Get(catalog, "m_DirNeedsProcessing"));
                }
                finally
                {
                    UserStorage.SetBackendForTests(previous);
                    UnityEngine.Object.DestroyImmediate(owner);
                }
            }
            Directory.Delete(root);
        }

        private static object Get(ReferenceImageCatalog catalog, string name) =>
            typeof(ReferenceImageCatalog).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(catalog);

        private static void Set(ReferenceImageCatalog catalog, string name, object value) =>
            typeof(ReferenceImageCatalog).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(catalog, value);
    }
}
