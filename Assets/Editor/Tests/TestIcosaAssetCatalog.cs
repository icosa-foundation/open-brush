// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace TiltBrush
{
    internal class TestIcosaAssetCatalog
    {
        private static readonly FieldInfo sm_AppInstanceField =
            typeof(App).GetField("m_Instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo sm_ValidModelCache =
            typeof(IcosaAssetCatalog).GetMethod("ValidModelCache", BindingFlags.Static | BindingFlags.NonPublic);

        private const string kAssetCacheVersion = "2.28.10";
        private readonly List<string> m_AssetIdsToCleanup = new List<string>();
        private readonly List<string> m_CachePathsToCleanup = new List<string>();
        private GameObject m_AppObject;
        private GameObject m_CatalogObject;

        [TearDown]
        public void TearDown()
        {
            if (m_CatalogObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_CatalogObject);
                m_CatalogObject = null;
            }
            if (m_AppObject != null)
            {
                UnityEngine.Object.DestroyImmediate(m_AppObject);
                m_AppObject = null;
            }

            sm_AppInstanceField.SetValue(null, null);

            string cacheDir = Path.Combine(Application.persistentDataPath, "assetCache");
            foreach (string assetId in m_AssetIdsToCleanup)
            {
                string assetPath = Path.Combine(cacheDir, kAssetCacheVersion, assetId);
                if (Directory.Exists(assetPath))
                {
                    Directory.Delete(assetPath, true);
                }
            }
            foreach (string cachePath in m_CachePathsToCleanup)
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                }
            }
            m_AssetIdsToCleanup.Clear();
            m_CachePathsToCleanup.Clear();
        }

        [Test]
        public void ValidModelCache_AcceptsSupportedRootFormats()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"IcosaValidModelCache-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                string[] extensions = { ".vox", ".ply", ".obj", ".gltf", ".gltf2", ".glb" };
                foreach (string extension in extensions)
                {
                    Directory.CreateDirectory(tempDir);
                    string modelFile = Path.Combine(tempDir, $"model{extension}");
                    File.WriteAllText(modelFile, "test");

                    string result = (string)sm_ValidModelCache.Invoke(null, new object[] { tempDir });
                    Assert.AreEqual($"model{extension}", result, $"Expected cache with {extension} file to be valid");

                    File.Delete(modelFile);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void Init_RetainsCachedAssetsForSupportedFormats()
        {
            m_AppObject = new GameObject("TestApp");
            var app = m_AppObject.AddComponent<App>();
            sm_AppInstanceField.SetValue(null, app);

            m_CatalogObject = new GameObject("IcosaAssetCatalog");
            var catalog = m_CatalogObject.AddComponent<IcosaAssetCatalog>();

            string[] extensions = { ".vox", ".ply", ".obj", ".gltf", ".gltf2", ".glb" };
            foreach (string extension in extensions)
            {
                string assetId = $"unit-test-{extension.TrimStart('.')}-{Guid.NewGuid():N}";
                m_AssetIdsToCleanup.Add(assetId);

                string assetDir = Path.Combine(
                    Application.persistentDataPath, "assetCache", kAssetCacheVersion, assetId);
                Directory.CreateDirectory(assetDir);
                File.WriteAllText(Path.Combine(assetDir, $"model{extension}"), "test");
            }

            catalog.Init();

            foreach (string assetId in m_AssetIdsToCleanup)
            {
                string assetDir = Path.Combine(
                    Application.persistentDataPath, "assetCache", kAssetCacheVersion, assetId);
                Assert.IsTrue(Directory.Exists(assetDir), $"Expected startup to retain valid cache {assetId}");
                Assert.NotNull(catalog.GetModel(assetId), $"Expected startup to register model for {assetId}");
            }
        }

        [Test]
        public void Init_RetainsCachedAssetsWithNestedRootPath()
        {
            m_AppObject = new GameObject("TestApp");
            var app = m_AppObject.AddComponent<App>();
            sm_AppInstanceField.SetValue(null, app);

            m_CatalogObject = new GameObject("IcosaAssetCatalog");
            var catalog = m_CatalogObject.AddComponent<IcosaAssetCatalog>();

            string assetId = $"unit-test-nested-{Guid.NewGuid():N}";
            m_AssetIdsToCleanup.Add(assetId);

            string assetDir = Path.Combine(
                Application.persistentDataPath, "assetCache", kAssetCacheVersion, assetId);
            string nestedDir = Path.Combine(assetDir, "source", "subdir");
            Directory.CreateDirectory(nestedDir);
            File.WriteAllText(Path.Combine(nestedDir, "model.gltf2"), "test");

            catalog.Init();

            Assert.IsTrue(Directory.Exists(assetDir), $"Expected startup to retain valid cache {assetId}");
            Assert.NotNull(catalog.GetModel(assetId), $"Expected startup to register model for {assetId}");
        }

        [Test]
        public void Init_RemovesCacheDirectoriesOutsideCurrentVersion()
        {
            m_AppObject = new GameObject("TestApp");
            var app = m_AppObject.AddComponent<App>();
            sm_AppInstanceField.SetValue(null, app);

            m_CatalogObject = new GameObject("IcosaAssetCatalog");
            var catalog = m_CatalogObject.AddComponent<IcosaAssetCatalog>();

            string cacheDir = Path.Combine(Application.persistentDataPath, "assetCache");
            string oldAssetId = $"unit-test-old-{Guid.NewGuid():N}";
            string newAssetId = $"unit-test-new-{Guid.NewGuid():N}";

            string oldAssetDir = Path.Combine(cacheDir, oldAssetId);
            Directory.CreateDirectory(oldAssetDir);
            File.WriteAllText(Path.Combine(oldAssetDir, "old.gltf2"), "test");
            m_CachePathsToCleanup.Add(oldAssetDir);

            string versionedAssetDir = Path.Combine(cacheDir, kAssetCacheVersion, newAssetId);
            Directory.CreateDirectory(versionedAssetDir);
            File.WriteAllText(Path.Combine(versionedAssetDir, "new.gltf2"), "test");
            m_AssetIdsToCleanup.Add(newAssetId);

            string previousVersionDir = Path.Combine(cacheDir, "2.28.9");
            Directory.CreateDirectory(previousVersionDir);
            m_CachePathsToCleanup.Add(previousVersionDir);

            catalog.Init();

            Assert.IsFalse(Directory.Exists(oldAssetDir), "Expected old unversioned cache folder to be deleted");
            Assert.IsTrue(Directory.Exists(versionedAssetDir), "Expected current versioned cache folder to be retained");
            Assert.IsFalse(Directory.Exists(previousVersionDir), "Expected old version cache folder to be deleted");
        }

        [Test]
        public void CanAutoDownloadForPreview_AllowsNonArchiveSelectedFormat()
        {
            var catalog = new GameObject("IcosaAssetCatalog").AddComponent<IcosaAssetCatalog>();
            m_CatalogObject = catalog.gameObject;

            const string assetId = "unit-test-nonarchive";
            catalog.SetJsonForAsset(assetId, AssetJson(
                FormatJson("GLTF2", "https://assets.example.com/model.gltf",
                    "https://assets.example.com/model.bin")));

            Assert.IsTrue(catalog.CanAutoDownloadForPreview(assetId));
        }

        [Test]
        public void CanAutoDownloadForPreview_BlocksArchiveSelectedFormat()
        {
            var catalog = new GameObject("IcosaAssetCatalog").AddComponent<IcosaAssetCatalog>();
            m_CatalogObject = catalog.gameObject;

            const string rootArchiveAssetId = "unit-test-root-archive";
            catalog.SetJsonForAsset(rootArchiveAssetId, AssetJson(
                FormatJson("GLTF2", "https://web.archive.org/model.gltf",
                    "https://assets.example.com/model.bin")));
            Assert.IsFalse(catalog.CanAutoDownloadForPreview(rootArchiveAssetId));

            const string resourceArchiveAssetId = "unit-test-resource-archive";
            catalog.SetJsonForAsset(resourceArchiveAssetId, AssetJson(
                FormatJson("GLTF2", "https://assets.example.com/model.gltf",
                    "https://archive.org/model.bin")));
            Assert.IsFalse(catalog.CanAutoDownloadForPreview(resourceArchiveAssetId));
        }

        [Test]
        public void CanAutoDownloadForPreview_OnlyChecksSelectedFormat()
        {
            var catalog = new GameObject("IcosaAssetCatalog").AddComponent<IcosaAssetCatalog>();
            m_CatalogObject = catalog.gameObject;

            const string assetId = "unit-test-selected-format";
            catalog.SetJsonForAsset(assetId, AssetJson(
                FormatJson("VOX", "https://web.archive.org/model.vox"),
                FormatJson("GLTF2", "https://assets.example.com/model.gltf", true,
                    "https://assets.example.com/model.bin")));

            Assert.IsTrue(catalog.CanAutoDownloadForPreview(assetId));
        }

        private static JObject AssetJson(params JObject[] formats)
        {
            return new JObject
            {
                ["formats"] = new JArray(formats)
            };
        }

        private static JObject FormatJson(
            string formatType, string rootUrl, params string[] resourceUrls)
        {
            return FormatJson(formatType, rootUrl, false, resourceUrls);
        }

        private static JObject FormatJson(
            string formatType, string rootUrl, bool isPreferredForDownload,
            params string[] resourceUrls)
        {
            var resources = new JArray();
            for (int i = 0; i < resourceUrls.Length; i++)
            {
                resources.Add(new JObject
                {
                    ["relativePath"] = $"resource-{i}.bin",
                    ["url"] = resourceUrls[i]
                });
            }

            return new JObject
            {
                ["formatType"] = formatType,
                ["isPreferredForDownload"] = isPreferredForDownload,
                ["root"] = new JObject
                {
                    ["relativePath"] = "model.gltf",
                    ["url"] = rootUrl
                },
                ["resources"] = resources
            };
        }
    }
}
