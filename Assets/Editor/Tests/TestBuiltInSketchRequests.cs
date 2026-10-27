// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System.IO;
using NUnit.Framework;

namespace TiltBrush
{
    internal class TestBuiltInSketchRequests
    {
        private FileSketchSet CreateCatalog()
        {
            var entry = new BundledSketchEntry
            {
                FileName = "Example by Artist..tilt",
                Metadata = "{}",
                Thumbnail = new byte[] { 1 }
            };
            var set = new FileSketchSet(SketchSetType.User, new[] { new DiskSceneFileInfo(entry) });
            set.Init();
            return set;
        }

        [TestCase("Example by Artist..tilt")]
        [TestCase("Example%20by%20Artist..tilt")]
        [TestCase("example by artist..tilt")]
        public void ResolvesPackagedFileWithoutASeededCopy(string name)
        {
            var info = SketchCatalog.FindBundledSketch(CreateCatalog(), name);
            Assert.NotNull(info);
            Assert.IsTrue(info.ReadOnly);
            Assert.IsTrue(((DiskSceneFileInfo)info).Embedded);
            Assert.AreEqual("Example by Artist..tilt", Path.GetFileName(info.FullPath));
            using (var metadata = new StreamReader(info.GetReadStream(TiltFile.FN_METADATA)))
                Assert.AreEqual("{}", metadata.ReadToEnd());
        }

        [TestCase("missing.tilt")]
        [TestCase("Example.tilt")] // Display name loses the artist; it is not the built-in ID.
        [TestCase("../Example by Artist..tilt")]
        [TestCase("%2e%2e%2fExample%20by%20Artist..tilt")]
        [TestCase("folder\\Example by Artist..tilt")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownOrNonFilenameRequestsDoNotResolveToDisk(string name)
        {
            Assert.IsNull(SketchCatalog.FindBundledSketch(CreateCatalog(), name));
        }
    }
}
