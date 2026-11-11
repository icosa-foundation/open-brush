// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class ShapesXrMaterialTests
    {
        [Test]
        public void ObjectVisibilityForcesBlendingAndCombinesWithMaterialOpacity()
        {
            var properties = JObject.Parse(@"{'m-c':[128,64,32,128],'m-o':0.8,'m-tam':0,'m-bm':1,'m-st':1}");
            var material = ShapesXrMaterialAdapter.Read(properties, .5f, ShapesXrMaterialUsage.Image);
            Assert.AreEqual("additive", material.Blend);
            Assert.AreEqual(128f / 255, material.Color[0], 1e-6);
            Assert.AreEqual(128f / 255 * .8f * .5f, material.Color[3], 1e-6);
            Assert.IsFalse(material.Lit);
            Assert.AreEqual(0, material.UnmappedProperties.Length);
            Assert.AreEqual(0, (int)properties["m-tam"]);
        }

        [Test]
        public void TextureAlphaModeControlsWhetherBlendModeHasAnEffect()
        {
            var properties = JObject.Parse(@"{'m-tam':0,'m-bm':2,'m-c':[255,255,255,128]}");
            var opaque = ShapesXrMaterialAdapter.Read(properties, 1, ShapesXrMaterialUsage.Image);
            Assert.AreEqual("opaque", opaque.Blend);
            Assert.AreEqual(1, opaque.Color[3]);
            properties["m-tam"] = 1;
            Assert.AreEqual("cutout", ShapesXrMaterialAdapter.Read(properties, 1, ShapesXrMaterialUsage.Image).Blend);
            properties["m-tam"] = 2;
            Assert.AreEqual("multiply", ShapesXrMaterialAdapter.Read(properties, 1, ShapesXrMaterialUsage.Image).Blend);
            properties["m-bm"] = 0;
            Assert.AreEqual("alphaBlend", ShapesXrMaterialAdapter.Read(properties, 1, ShapesXrMaterialUsage.Image).Blend);
        }

        [Test]
        public void ImageDefaultsAndTextOpacityFollowTheirDifferentSourceContracts()
        {
            var image = ShapesXrMaterialAdapter.Read(new JObject(), 1, ShapesXrMaterialUsage.Image);
            CollectionAssert.AreEqual(new[] { 1f, 1f, 1f, 1f }, image.Color);
            Assert.AreEqual("alphaBlend", image.Blend);
            Assert.IsFalse(image.Lit);
            var text = ShapesXrMaterialAdapter.Read(JObject.Parse(@"{'m-c':[255,0,0,0],'m-o':0.5}"), .5f, ShapesXrMaterialUsage.Text);
            Assert.AreEqual(.25f, text.Color[3]);
            Assert.AreEqual(155f / 255, ShapesXrMaterialAdapter.Read(new JObject(), 1, ShapesXrMaterialUsage.Text).Color[0], 1e-6);
        }

        [Test]
        public void InvalidAppearanceValuesFailAndUnmappedMaterialFieldsRemainVisible()
        {
            Assert.Throws<InvalidDataException>(() => ShapesXrMaterialAdapter.Read(JObject.Parse(@"{'m-c':[1,2,3,0.5]}"), 1, ShapesXrMaterialUsage.Image));
            Assert.Throws<InvalidDataException>(() => ShapesXrMaterialAdapter.Read(JObject.Parse(@"{'m-o':2}"), 1, ShapesXrMaterialUsage.Image));
            Assert.Throws<InvalidDataException>(() => ShapesXrMaterialAdapter.Read(new JObject(), float.NaN, ShapesXrMaterialUsage.Image));
            var material = ShapesXrMaterialAdapter.Read(JObject.Parse(@"{'m-pt':true,'m-e':[255,255,255,255]}"), 1, ShapesXrMaterialUsage.Image);
            Assert.IsTrue(material.Passthrough);
            CollectionAssert.AreEqual(new[] { "m-e" }, material.UnmappedProperties);
        }
    }
}
