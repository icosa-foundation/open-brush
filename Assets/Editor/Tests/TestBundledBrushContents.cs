// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestBundledBrushContents
    {
        [Test]
        public void EnumeratesImmediateDirectoriesWithoutDirectoryRecords()
        {
            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    zip.CreateEntry("Brushes/Alpha/Brush.cfg");
                    zip.CreateEntry("Brushes/alpha/icon.png");
                    zip.CreateEntry("Brushes\\Beta\\nested\\Brush.cfg");
                    zip.CreateEntry("BrushesOther/NotABrush/Brush.cfg");
                    zip.CreateEntry("Brushes/file.txt");
                }
                stream.Position = 0;
                CollectionAssert.AreEquivalent(new[] { "Alpha", "Beta" },
                    BundledSketchAssets.GetContentsAt(stream, "\\brushes\\"));
            }
        }

        [Test]
        public void SceneReaderFindsNestedConfigAndReadsItsRelativeImagesWithoutFullPath()
        {
            var scene = new StreamOnlyScene();
            var open = UserVariantBrush.CreateSceneBrushReader(scene, "Brushes\\Variant",
                out string config);
            Assert.NotNull(open);
            Assert.AreEqual("configuration", config);
            using (var reader = new StreamReader(open("Textures\\icon.png")))
                Assert.AreEqual("image bytes", reader.ReadToEnd());
            Assert.IsNull(open("missing.png"));
            Assert.AreEqual(1, scene.ConfigReadCount);
        }

        private sealed class StreamOnlyScene : SceneFileInfo
        {
            public int ConfigReadCount;
            public FileInfoType InfoType => FileInfoType.Disk;
            public string FullPath => throw new InvalidOperationException("Not a disk sketch");
            public string HumanName => "Bundled variant";
            public bool Valid => true;
            public bool Available => true;
            public bool Exists => true;
            public bool ReadOnly => true;
            public string AssetId => null;
            public string SourceId => null;
            public int? TriangleCount => null;
            public void Delete() => throw new NotSupportedException();
            public string Rename(string name) => throw new NotSupportedException();
            public bool IsHeaderValid() => true;

            public Stream GetReadStream(string name)
            {
                string text;
                switch (name)
                {
                    case "Brushes/Variant/nested/Brush.cfg":
                        ConfigReadCount++;
                        text = "configuration";
                        break;
                    case "Brushes/Variant/nested/Textures/icon.png":
                        text = "image bytes";
                        break;
                    default:
                        return null;
                }
                return new MemoryStream(Encoding.UTF8.GetBytes(text), false);
            }

            public IEnumerable<string> GetContentsAt(string path) =>
                path == "Brushes/Variant" ? new[] { "nested" } : Enumerable.Empty<string>();
        }
    }
}
