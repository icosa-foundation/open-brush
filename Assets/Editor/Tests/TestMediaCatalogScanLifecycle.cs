// Copyright 2026 The Open Brush Authors

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiltBrush
{
    public class TestMediaCatalogScanLifecycle
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MissingLocalFolderReleasesScanningFlag(bool sound)
        {
            IUserStorageBackend previous = UserStorage.Backend;
            var owner = new GameObject("MediaScanLifecycleTest");
            owner.SetActive(false);
            try
            {
                string directory = Path.Combine(Path.GetTempPath(), $"missing-media-{Guid.NewGuid():N}");
                UserStorage.SetBackendForTests(new LocalUserStorageBackend(_ => directory));
                MonoBehaviour catalog = sound ? (MonoBehaviour)owner.AddComponent<SoundClipCatalog>() : owner.AddComponent<VideoCatalog>();
                Type type = catalog.GetType();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                type.GetField("m_ScanningDirectory", flags).SetValue(catalog, true);
                type.GetField("m_ScanGeneration", flags).SetValue(catalog, 1);
                object[] arguments;
                if (sound)
                {
                    var clips = new List<SoundClip>();
                    type.GetField("m_SoundClips", flags).SetValue(catalog, clips);
                    arguments = new object[] { directory, clips, new HashSet<string>(), 1 };
                }
                else
                {
                    type.GetField("m_Videos", flags).SetValue(catalog, new List<ReferenceVideo>());
                    type.GetField("m_ChangedFiles", flags).SetValue(catalog, new HashSet<string>());
                    arguments = new object[] { directory, 1 };
                }
                LogAssert.Expect(LogType.Warning, new Regex("CATALOG_SCAN Could not scan .* folder"));
                using (var scan = (IEnumerator<object>)type.GetMethod("ScanReferenceDirectory", flags).Invoke(catalog, arguments))
                {
                    Assert.IsFalse(scan.MoveNext());
                }
                Assert.IsFalse((bool)type.GetField("m_ScanningDirectory", flags).GetValue(catalog));
            }
            finally
            {
                UserStorage.SetBackendForTests(previous);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
