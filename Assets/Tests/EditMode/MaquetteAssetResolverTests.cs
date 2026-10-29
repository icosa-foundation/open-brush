// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class MaquetteAssetResolverTests
    {
        private string m_Root;
        [SetUp]
        public void SetUp()
        {
            m_Root = Path.Combine(Path.GetTempPath(), $"OpenBrush-AssetTest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(m_Root, "project/Objects/models"));
            Directory.CreateDirectory(Path.Combine(m_Root, "project/Objects/textures"));
            Directory.CreateDirectory(Path.Combine(m_Root, "user/Objects/models"));
            Directory.CreateDirectory(Path.Combine(m_Root, "content/Library"));
            File.WriteAllText(Path.Combine(m_Root, "project/Objects/models/model.gltf"), "bundled");
            File.WriteAllText(Path.Combine(m_Root, "project/Objects/textures/color.png"), "texture");
            File.WriteAllText(Path.Combine(m_Root, "user/Objects/models/model.gltf"), "user");
            File.WriteAllText(Path.Combine(m_Root, "content/Library/builtin.fbx"), "installed");
        }
        [TearDown]
        public void TearDown() { Directory.Delete(m_Root, true); }

        [Test]
        public void BundledAssetsTakePrecedenceAndDependenciesCanUseParentDirectories()
        {
            using (var source = new NativeProjectSource(Path.Combine(m_Root, "project")))
            {
                var resolver = new MaquetteAssetResolver(source, Path.Combine(m_Root, "user"), Path.Combine(m_Root, "content"));
                var model = resolver.Resolve(@"<MAQUETTEOBJECT>\models\model.gltf");
                Assert.AreEqual("bundled", model.ReadText());
                Assert.AreEqual("texture", model.ResolveRelative("../textures/color.png").ReadText());
                Assert.AreEqual("installed", resolver.Resolve("<MAQUETTECONTENT>/Library/builtin.fbx").ReadText());
                Assert.IsNull(resolver.Resolve("https://example.invalid/model.glb"));
                Assert.Throws<InvalidDataException>(() => model.ResolveRelative("../../../outside"));
            }
        }

        [Test]
        public void ArchivesResolveTheSameAssetAndLegacyMachinePathsUseBundledFolders()
        {
            string archive = Path.Combine(m_Root, "project.mqp");
            ZipFile.CreateFromDirectory(Path.Combine(m_Root, "project"), archive);
            using (var source = new NativeProjectSource(archive))
            {
                var resolver = new MaquetteAssetResolver(source);
                var asset = resolver.Resolve(@"Z:\OldMachine\Project\Objects\models\model.gltf");
                Assert.AreEqual("bundled", asset.ReadText());
                Assert.IsNull(asset.LocalPath);
                Assert.AreEqual("texture", asset.ResolveRelative("../textures/color.png").ReadText());
            }
        }

        [Test]
        public void MissingBundledAssetsCanResolveFromTheUsersLibrary()
        {
            File.Delete(Path.Combine(m_Root, "project/Objects/models/model.gltf"));
            using (var source = new NativeProjectSource(Path.Combine(m_Root, "project")))
            {
                var resolver = new MaquetteAssetResolver(source, Path.Combine(m_Root, "user"));
                Assert.AreEqual("user", resolver.Resolve("<MAQUETTEOBJECT>/models/model.gltf").ReadText());
                Assert.IsNull(resolver.Resolve("<MAQUETTEOBJECT>/missing.glb"));
            }
        }

        [Test]
        public void LegacyShapesResolveThroughTheInstalledMapWithoutChangingSourceData()
        {
            string folder = Path.Combine(m_Root, "content/MaquetteLibrary/Shapes");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "PrimitivePathMap.json"), new JObject
            {
                ["maps"] = new JArray(new JObject
                {
                    ["primitiveType"] = "Cube", ["primitiveFilePath"] = "<MAQUETTECONTENT>/Library/builtin.fbx"
                })
            }.ToString());
            using (var source = new NativeProjectSource(Path.Combine(m_Root, "project")))
            {
                var resolver = new MaquetteAssetResolver(source, content: Path.Combine(m_Root, "content"));
                var original = new JObject { ["primitiveType"] = "Cube", ["primitivePath"] = "" };
                var legacy = resolver.Primitive(original, 4);
                Assert.AreEqual("ModelFile", legacy.Type);
                Assert.AreEqual("installed", resolver.Resolve(legacy.Path).ReadText());
                Assert.AreEqual("Maquette", legacy.LoadOption);
                Assert.IsTrue(legacy.UsesLegacyMeshOrigin);
                Assert.IsTrue(legacy.IgnoresImageTint);
                Assert.AreEqual("Cube", (string)original["primitiveType"]);
                var v5 = resolver.Primitive(new JObject
                {
                    ["primitiveType"] = "UnityResource", ["primitivePath"] = "Primitives/Maq_Primitive_Cube"
                }, 5);
                Assert.AreEqual(legacy.Path, v5.Path);
                Assert.IsNull(resolver.Primitive(new JObject { ["primitiveType"] = "UnknownShape" }, 4).Path);
            }
        }

        [Test]
        public void AssetVersionsPreserveModernReferencesAndImageCompatibilityFlags()
        {
            var data = new JObject
            {
                ["primitiveType"] = "ModelFile", ["primitivePath"] = "Objects/a.obj", ["assetLoadOption"] = "Original"
            };
            var modern = MaquettePrimitiveData.Read(data, 21, _ => throw new Exception("Unexpected library lookup"));
            Assert.AreEqual("Objects/a.obj", modern.Path);
            Assert.AreEqual("Original", modern.LoadOption);
            Assert.IsFalse(modern.UsesLegacyMeshOrigin);
            Assert.IsFalse(modern.IgnoresImageTint);
            Assert.IsFalse(modern.UsesDefaultImageMaterial);
            Assert.AreEqual("Original", MaquettePrimitiveData.Read(new JObject { ["primitiveType"] = "GooglePoly" }, 6, null).LoadOption);
            Assert.AreEqual("ImageFile", MaquettePrimitiveData.Read(new JObject { ["primitiveType"] = "IMAGE" }, 4, null).Type);
            Assert.AreEqual("ModelFile", MaquettePrimitiveData.Read(new JObject { ["primitiveType"] = "OBJECT" }, 4, null).Type);
        }

        [Test]
        public void NativeWorkspaceKeepsArchiveSidecarsAtTheirRelativePathsAndCleansOnlyItsOwnDirectory()
        {
            string archive = Path.Combine(m_Root, "project.mqp");
            ZipFile.CreateFromDirectory(Path.Combine(m_Root, "project"), archive);
            string cache = Path.Combine(m_Root, "cache");
            string extractedModel;
            using (var source = new NativeProjectSource(archive))
            using (var workspace = new NativeAssetWorkspace(source, cache))
            {
                var resolver = new MaquetteAssetResolver(source);
                extractedModel = workspace.LocalPath(resolver.Resolve("<MAQUETTEOBJECT>/models/model.gltf"));
                Assert.AreEqual("bundled", File.ReadAllText(extractedModel));
                Assert.AreEqual("texture", File.ReadAllText(Path.Combine(Path.GetDirectoryName(extractedModel), "../textures/color.png")));
                Assert.AreEqual(extractedModel, workspace.LocalPath(resolver.Resolve("<MAQUETTEOBJECT>/models/model.gltf")));
            }
            Assert.IsFalse(File.Exists(extractedModel));
            Assert.IsTrue(File.Exists(archive));
            Assert.IsTrue(File.Exists(Path.Combine(m_Root, "project/Objects/models/model.gltf")));
            Assert.IsEmpty(Directory.GetFileSystemEntries(cache));
        }

        [Test]
        public void NativeWorkspaceUsesExistingProjectAndLibraryFilesWithoutCopyingOrDeletingThem()
        {
            string cache = Path.Combine(m_Root, "cache");
            string project = Path.Combine(m_Root, "project");
            using (var source = new NativeProjectSource(project))
            using (var workspace = new NativeAssetWorkspace(source, cache))
            {
                var resolver = new MaquetteAssetResolver(source, content: Path.Combine(m_Root, "content"));
                Assert.AreEqual(Path.Combine(project, "Objects/models/model.gltf").Replace('\\', '/'),
                    workspace.LocalPath(resolver.Resolve("<MAQUETTEOBJECT>/models/model.gltf")).Replace('\\', '/'));
                Assert.AreEqual("installed", File.ReadAllText(workspace.LocalPath(resolver.Resolve("<MAQUETTECONTENT>/Library/builtin.fbx"))));
            }
            Assert.IsFalse(Directory.Exists(cache));
            Assert.IsTrue(File.Exists(Path.Combine(m_Root, "content/Library/builtin.fbx")));
        }
    }
}
