// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.IO.Compression;
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
    }
}
