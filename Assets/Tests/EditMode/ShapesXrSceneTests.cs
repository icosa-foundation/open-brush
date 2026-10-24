// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class ShapesXrSceneTests
    {
        // Independently encoded synthetic edit log; no vendor or user content.
        private const string kSpace = "kpIFkgMW3AAWkgCTxBAKCgoKCgoKCgoKCgoKCgoKwwCSBpPEEAoKCgoKCgoKCgoKCgoKCgqmc3Atc2NvkcQQCwsLCwsLCwsLCwsLCwsLC5IAk8QQCwsLCwsLCwsLCwsLCwsLC8MBkgaTxBALCwsLCwsLCwsLCwsLCwsLpHNjLW6RxBAMDAwMDAwMDAwMDAwMDAwMkgaTxBALCwsLCwsLCwsLCwsLCwsLpXNjLW5tp0luaXRpYWySAJPEEAwMDAwMDAwMDAwMDAwMDAzDApIGk8QQDAwMDAwMDAwMDAwMDAwMDKRzdC16xBALCwsLCwsLCwsLCwsLCwsLkgCTxBABAQEBAQEBAQEBAQEBAQEBwwOSBpPEEAEBAQEBAQEBAQEBAQEBAQGicGmSxBACAgICAgICAgICAgICAgICwpIGk8QQAQEBAQEBAQEBAQEBAQEBAaF0k5PLP/AAAAAAAADLAAAAAAAAAADLAAAAAAAAAACUywAAAAAAAAAAywAAAAAAAAAAywAAAAAAAAAAyz/wAAAAAAAAk8s/8AAAAAAAAMs/8AAAAAAAAMs/8AAAAAAAAJIAk8QQAgICAgICAgICAgICAgICAsMDkgaTxBACAgICAgICAgICAgICAgICoXSTk8tAJAAAAAAAAMsAAAAAAAAAAMsAAAAAAAAAAJTLAAAAAAAAAADLAAAAAAAAAADLAAAAAAAAAADLP/AAAAAAAACTy0AAAAAAAAAAyz/wAAAAAAAAyz/wAAAAAAAAkgCTxBADAwMDAwMDAwMDAwMDAwMDwwOSBpPEEAMDAwMDAwMDAwMDAwMDAwOhdJOTywAAAAAAAAAAywAAAAAAAAAAywAAAAAAAAAAlMsAAAAAAAAAAMsAAAAAAAAAAMsAAAAAAAAAAMs/8AAAAAAAAJPLP/AAAAAAAADLP/AAAAAAAADLP/AAAAAAAACSAJPEEAQEBAQEBAQEBAQEBAQEBATDBJIGk8QQBAQEBAQEBAQEBAQEBAQEBKdzbnAtZW50xBABAQEBAQEBAQEBAQEBAQEBkgaTxBAEBAQEBAQEBAQEBAQEBAQEpnNucC1zdMQQDAwMDAwMDAwMDAwMDAwMDJIGk8QQBAQEBAQEBAQEBAQEBAQEBKdvYmotaXNhw5IAk8QQBQUFBQUFBQUFBQUFBQUFBcMEkgaTxBAFBQUFBQUFBQUFBQUFBQUFp3NucC1lbnTEEAICAgICAgICAgICAgICAgKSBpPEEAUFBQUFBQUFBQUFBQUFBQWmc25wLXN0xBAMDAwMDAwMDAwMDAwMDAwMkgaTxBAFBQUFBQUFBQUFBQUFBQUFp29iai1pc2HC";

        [Test]
        public void SelectsInitialStateAndPreservesParentPlacementAndVisibility()
        {
            var scene = new ShapesXrScene(new ShapesXrDocument(Convert.FromBase64String(kSpace)));
            Assert.AreEqual("Initial", scene.Name);
            Assert.AreEqual(2, scene.Objects.Values.Count(value => value.HasSnapshot));
            var child = scene.Objects[Id(1)];
            Assert.AreEqual(Id(2), child.Parent.Entity.Id);
            Assert.AreEqual(12, child.Placement().Position[0], 1e-5);
            Assert.IsFalse(child.Visible);
            Assert.IsFalse(scene.Objects[Id(3)].Visible);
        }

        [Test]
        public void RejectsCyclesRegardlessOfEntityOrder()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            document.Entities[Id(2)].Properties["pi"] = new JArray(new JValue(Id(1).ToByteArray()), false);
            Assert.Throws<InvalidDataException>(() => new ShapesXrScene(document));
        }

        [Test]
        public void RejectsAmbiguousSnapshotsAndMissingParents()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            document.Entities[Id(5)].Properties["snp-ent"] = new JValue(Id(1).ToByteArray());
            Assert.Throws<InvalidDataException>(() => new ShapesXrScene(document));
            document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            document.Entities[Id(1)].Properties["pi"] = new JArray(new JValue(Id(99).ToByteArray()), false);
            Assert.Throws<InvalidDataException>(() => new ShapesXrScene(document));
        }

        [Test]
        public void TextKeepsFixedAndAutoFitAnchorsAndIntrinsicHierarchyScale()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            var properties = document.Entities[Id(1)].Properties;
            properties["obj-tx"] = "A <b>label</b>";
            properties["obj-s"] = new JArray(2, 4, 0);
            properties["obj-ts"] = 5f;
            properties["obj-ty"] = 1;
            properties["obj-ta"] = 2;
            properties["obj-tva"] = 512;
            var state = new ShapesXrScene(document).Objects[Id(1)];
            var text = ShapesXrTextAdapter.Read(state, 10);
            Assert.AreEqual("A <b>label</b>", text.Text);
            Assert.IsFalse(text.AutoSize);
            Assert.AreEqual(514, text.Alignment);
            Assert.AreEqual(12, text.Position[0], 1e-5);
            Assert.AreEqual(20, text.GlyphTransform[0], 1e-5);
            Assert.AreEqual(-.5f, text.GlyphTransform[14], 1e-5);
            state.Properties["obj-ts"] = 0;
            text = ShapesXrTextAdapter.Read(state, 10);
            Assert.IsTrue(text.AutoSize);
            Assert.AreEqual(11, text.Position[0], 1e-5);
            Assert.AreEqual(2, text.Position[1], 1e-5);
            Assert.AreEqual(.05f, text.Position[2], 1e-5);
        }

        [Test]
        public void RejectsMalformedTextDimensionsAndFontSize()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            var state = new ShapesXrScene(document).Objects[Id(1)];
            state.Properties["obj-tx"] = "label";
            state.Properties["obj-s"] = new JArray(0, 1, 0);
            state.Properties["obj-ts"] = 5;
            Assert.Throws<InvalidDataException>(() => ShapesXrTextAdapter.Read(state, 10));
            state.Properties["obj-s"] = new JArray(1, 1, 0);
            state.Properties["obj-ts"] = -1;
            Assert.Throws<InvalidDataException>(() => ShapesXrTextAdapter.Read(state, 10));
        }

        private static Guid Id(byte value) => new Guid(Enumerable.Repeat(value, 16).ToArray());
    }
}
