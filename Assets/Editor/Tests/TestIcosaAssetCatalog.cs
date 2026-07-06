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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;

namespace TiltBrush
{
    internal class TestIcosaAssetCatalog
    {
        private static readonly FieldInfo sm_AppInstanceField =
            typeof(App).GetField("m_Instance", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo sm_ValidModelCache =
            typeof(IcosaAssetCatalog).GetMethod("ValidModelCache", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo sm_AppendQueryParam =
            typeof(VrAssetService).GetMethod("AppendQueryParam", BindingFlags.Static | BindingFlags.NonPublic);

        private readonly List<string> m_CachePathsToCleanup = new List<string>();
        private readonly List<string> m_FilePathsToCleanup = new List<string>();
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

            foreach (string cachePath in m_CachePathsToCleanup)
            {
                if (Directory.Exists(cachePath))
                {
                    Directory.Delete(cachePath, true);
                }
            }
            m_CachePathsToCleanup.Clear();

            foreach (string filePath in m_FilePathsToCleanup)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
                string tempDownloadPath = filePath + ".download";
                if (File.Exists(tempDownloadPath))
                {
                    File.Delete(tempDownloadPath);
                }
            }
            m_FilePathsToCleanup.Clear();
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
            var assetIds = new List<string>();
            foreach (string extension in extensions)
            {
                string assetId = $"unit-test-{extension.TrimStart('.')}-{Guid.NewGuid():N}";
                assetIds.Add(assetId);

                string assetDir = catalog.GetCacheDirectoryForAsset(assetId);
                m_CachePathsToCleanup.Add(assetDir);
                Directory.CreateDirectory(assetDir);
                File.WriteAllText(Path.Combine(assetDir, $"model{extension}"), "test");
            }

            catalog.Init();

            foreach (string assetId in assetIds)
            {
                string assetDir = catalog.GetCacheDirectoryForAsset(assetId);
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

            string assetDir = catalog.GetCacheDirectoryForAsset(assetId);
            m_CachePathsToCleanup.Add(assetDir);
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

            string versionedAssetDir = catalog.GetCacheDirectoryForAsset(newAssetId);
            Directory.CreateDirectory(versionedAssetDir);
            File.WriteAllText(Path.Combine(versionedAssetDir, "new.gltf2"), "test");
            m_CachePathsToCleanup.Add(versionedAssetDir);

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

        [Test]
        public void TryGetDownloadFormat_ReturnsFalseForNullDesiredTypes()
        {
            Assert.IsFalse(IcosaAssetCatalog.TryGetDownloadFormat(
                AssetJson(FormatJson("GLTF2", "https://assets.example.com/model.gltf")),
                null, out JToken format, out VrAssetFormat selectedType, out string formatType));

            Assert.IsNull(format);
            Assert.AreEqual(VrAssetFormat.Unknown, selectedType);
            Assert.IsNull(formatType);
        }

        [Test]
        public void TryGetDownloadFormat_ClearsOutValuesWhenSelectedFormatTypeIsInvalid()
        {
            Assert.IsFalse(IcosaAssetCatalog.TryGetDownloadFormat(
                AssetJson(FormatJson("NOT_A_FORMAT", "https://assets.example.com/model.gltf")),
                new[] { VrAssetFormat.GLTF2 }, out JToken format, out VrAssetFormat selectedType,
                out string formatType));

            Assert.IsNull(format);
            Assert.AreEqual(VrAssetFormat.Unknown, selectedType);
            Assert.IsNull(formatType);
        }

        [Test]
        public void IcosaSceneFileInfo_ToleratesMissingFields()
        {
            Assert.DoesNotThrow(() => new IcosaSceneFileInfo(new JObject()));

            var info = new IcosaSceneFileInfo(new JObject());
            Assert.IsFalse(info.Valid);
            Assert.IsFalse(info.Exists);
            Assert.AreEqual("Untitled", info.HumanName);
            Assert.AreEqual(1, info.TriangleCount);
        }

        [Test]
        public void IcosaSceneFileInfo_InvalidWithoutTiltDownloadUrl()
        {
            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "missing-tilt-url",
                "Missing Tilt Url",
                FormatJson("GLTF2", "https://assets.example.com/model.gltf")));

            Assert.IsFalse(info.Valid);
            Assert.IsNull(info.TiltFileUrl);
        }

        [Test]
        public void IcosaSceneFileInfo_DefaultsMalformedTriangleCount()
        {
            JObject gltf = FormatJson("GLTF2", "https://assets.example.com/model.gltf");
            gltf["formatComplexity"] = new JObject
            {
                ["triangleCount"] = "not-a-number"
            };

            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "bad-triangle-count",
                "Bad Triangle Count",
                FormatJson("TILT", "https://assets.example.com/sketch.tilt"),
                gltf));

            Assert.IsTrue(info.Valid);
            Assert.AreEqual(1, info.TriangleCount);
        }

        [Test]
        public void IcosaSceneFileInfo_ValidWithTiltDownloadUrl()
        {
            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "valid-tilt",
                "Valid Tilt",
                FormatJson("TILT", "https://assets.example.com/sketch.tilt")));

            Assert.IsTrue(info.Valid);
            Assert.IsTrue(info.Exists);
            Assert.AreEqual("https://assets.example.com/sketch.tilt", info.TiltFileUrl);
        }

        [Test]
        public void IcosaSceneFileInfo_UsesExistingCachedTiltFile()
        {
            string source = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Support", "Sketches", "Intro",
                    "Intro_Sketch_Simple.tilt"));
            Assert.IsTrue(File.Exists(source), $"Missing test sketch: {source}");

            string target = Path.Combine(
                Path.GetTempPath(), $"IcosaSceneFileInfo-{Guid.NewGuid():N}.tilt");
            m_FilePathsToCleanup.Add(target);
            File.Copy(source, target);

            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "cached-tilt",
                "Cached Tilt",
                FormatJson("TILT", "https://assets.example.com/sketch.tilt")));
            info.TiltPath = target;

            Assert.IsFalse(info.Available);
            Assert.IsTrue(info.TryUseCachedTiltFile());
            Assert.IsTrue(info.Available);
        }

        [Test]
        public void AppendQueryParam_EscapesReservedCharacters()
        {
            object[] args = { "https://api.example.com/assets?", "name", "space & equals=question?" };

            sm_AppendQueryParam.Invoke(null, args);

            string uri = (string)args[0];
            Assert.AreEqual(
                "https://api.example.com/assets?name=space%20%26%20equals%3Dquestion%3F&",
                uri);
        }

        [Test]
        public void IcosaTiltDownloader_ReturnsMissingUrlWithoutRequest()
        {
            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "missing-download",
                "Missing Download",
                FormatJson("GLTF2", "https://assets.example.com/model.gltf")));
            IcosaTiltDownloadResult result = null;
            IEnumerator routine = IcosaTiltDownloader.DownloadTiltCoroutine(
                info, "unused.tilt", new byte[1024],
                isCanceled: null,
                onRequestChanged: null,
                onComplete: r => result = r);

            while (routine.MoveNext()) { }

            Assert.NotNull(result);
            Assert.AreEqual(IcosaTiltDownloadStatus.MissingUrl, result.Status);
        }

        [Test]
        public void IcosaTiltDownloader_ReturnsInvalidUrlWithoutRequest()
        {
            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "bad-download-url",
                "Bad Download Url",
                FormatJson("TILT", "not a url")));
            IcosaTiltDownloadResult result = null;
            IEnumerator routine = IcosaTiltDownloader.DownloadTiltCoroutine(
                info, "unused.tilt", new byte[1024],
                isCanceled: null,
                onRequestChanged: null,
                onComplete: r => result = r);

            while (routine.MoveNext()) { }

            Assert.NotNull(result);
            Assert.AreEqual(IcosaTiltDownloadStatus.InvalidUrl, result.Status);
        }

        [UnityTest]
        public IEnumerator IcosaTiltDownloader_DownloadsFileUrlToTargetPath()
        {
            string source = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "Support", "Sketches", "Intro",
                    "Intro_Sketch_Simple.tilt"));
            Assert.IsTrue(File.Exists(source), $"Missing test sketch: {source}");

            string target = Path.Combine(
                Path.GetTempPath(), $"IcosaTiltDownloader-{Guid.NewGuid():N}.tilt");
            m_FilePathsToCleanup.Add(target);

            var info = new IcosaSceneFileInfo(SketchAssetJson(
                "file-url-download",
                "File Url Download",
                FormatJson("TILT", new Uri(source).AbsoluteUri)));
            IcosaTiltDownloadResult result = null;

            yield return IcosaTiltDownloader.DownloadTiltCoroutine(
                info, target, new byte[1024 * 1024],
                isCanceled: null,
                onRequestChanged: null,
                onComplete: r => result = r);

            Assert.NotNull(result);
            Assert.AreEqual(IcosaTiltDownloadStatus.Success, result.Status);
            Assert.IsTrue(File.Exists(target));
            Assert.IsTrue(new TiltFile(target).IsHeaderValid());
            Assert.IsTrue(info.TiltDownloaded);
        }

        private static JObject AssetJson(params JObject[] formats)
        {
            return new JObject
            {
                ["formats"] = new JArray(formats)
            };
        }

        private static JObject SketchAssetJson(string assetId, string displayName, params JObject[] formats)
        {
            return new JObject
            {
                ["assetId"] = assetId,
                ["displayName"] = displayName,
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
