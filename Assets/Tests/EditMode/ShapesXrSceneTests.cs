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
        private const string kSpace = "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";

        [Test]
        public void StrokeReadsBaseGeometryWhileTheSelectedStateControlsPlacement()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            var properties = document.Entities[Id(1)].Properties;
            properties["a"] = new JValue(new Guid("3a0fc5ad-c934-4306-8063-60f5940cae06").ToByteArray());
            properties["p"] = new JArray(new JArray(0, 0, 0), new JArray(0, .1, 0));
            properties["r"] = new JArray(new JArray(0, 0, 0, 1), new JArray(0, 0, 0, 1));
            properties["w"] = new JArray(2, 2);
            document.Entities[Id(4)].Properties["p"] = new JArray();
            document.Entities[Id(4)].Properties["r"] = new JArray();
            document.Entities[Id(4)].Properties["w"] = new JArray();
            document.Entities[Id(4)].Properties["t"] = new JArray(new JArray(3, 0, 0), new JArray(0, 0, 0, 1), new JArray(1, 1, 1));
            var state = new ShapesXrScene(document).Objects[Id(1)];
            Assert.IsTrue(ShapesXrStrokeAdapter.IsStroke(state));
            var ribbon = ShapesXrStrokeAdapter.ReadRibbon(state, 10);
            Assert.AreEqual(159.6f, ribbon.FirstEdge[0][0], 1e-4);
            Assert.AreEqual(160.4f, ribbon.SecondEdge[0][0], 1e-4);
            Assert.AreEqual(1f, ribbon.FirstEdge[1][1], 1e-4);
        }

        [Test]
        public void StrokeClassificationUsesPresetIdentityAndRecognizesUnknownExplicitBrushData()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            document.Entities[Id(1)].Properties["a"] = new JValue(new Guid("3a0fc5ad-c934-4306-8063-60f5940cae06").ToByteArray());
            Assert.IsTrue(ShapesXrStrokeAdapter.IsStroke(new ShapesXrScene(document).Objects[Id(1)]));
            document.Entities[Id(1)].Properties["a"] = new JValue(Id(99).ToByteArray());
            Assert.IsFalse(ShapesXrStrokeAdapter.IsStroke(new ShapesXrScene(document).Objects[Id(1)]));
            document.Entities[Id(1)].Properties["p"] = new JArray();
            document.Entities[Id(1)].Properties["r"] = new JArray();
            document.Entities[Id(1)].Properties["w"] = new JArray();
            Assert.IsTrue(ShapesXrStrokeAdapter.IsStroke(new ShapesXrScene(document).Objects[Id(1)]));
        }

        [Test]
        public void NativeGroupPresetRemainsInTheHierarchyWithoutBecomingRenderableContent()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            document.Entities[Id(2)].Properties["a"] = new JValue(new Guid("03ddfa60-93c1-4fb1-acef-d632575a106c").ToByteArray());
            var scene = new ShapesXrScene(document);
            Assert.IsTrue(scene.Objects[Id(2)].IsGroup);
            Assert.IsFalse(scene.Objects[Id(1)].IsGroup);
            Assert.AreEqual(scene.Objects[Id(2)], scene.Objects[Id(1)].Parent);
            Assert.AreEqual(12, scene.Objects[Id(1)].Placement().Position[0], 1e-5);
            document.Entities[Id(2)].Properties["a"] = new JValue(Id(99).ToByteArray());
            Assert.IsFalse(new ShapesXrScene(document).Objects[Id(2)].IsGroup);
        }

        [Test]
        public void SelectsInitialStateAndPreservesParentPlacementAndVisibility()
        {
            var scene = new ShapesXrScene(new ShapesXrDocument(Convert.FromBase64String(kSpace)));
            Assert.AreEqual("Initial", scene.Name);
            Assert.AreEqual(3, scene.Objects.Values.Count(value => value.HasSnapshot));
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

        [Test]
        public void ImagesFitAspectIndependentlyOfAuthoredHierarchyScale()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            var state = new ShapesXrScene(document).Objects[Id(1)];
            state.Properties["s"] = new JArray(2, 1, 0);
            var image = ShapesXrImageAdapter.Read(state, 10);
            Assert.AreEqual(5, image.Height, 1e-5);
            Assert.AreEqual(4, image.GeometryTransform[0], 1e-5);
            Assert.AreEqual(1, image.GeometryTransform[5], 1e-5);
            state.Properties["s"] = new JArray(.5f, 1, 0);
            image = ShapesXrImageAdapter.Read(state, 10);
            Assert.AreEqual(10, image.Height, 1e-5);
            Assert.AreEqual(1, image.GeometryTransform[0], 1e-5);
        }

        [Test]
        public void IncludesBackgroundObjectsUsingTheirOwnState()
        {
            var document = new ShapesXrDocument(Convert.FromBase64String(kSpace));
            document.Entities[Id(1)].Properties["pi"] = new JArray(new JValue(Id(15).ToByteArray()), false);
            var scene = new ShapesXrScene(document);
            var background = scene.Objects[Id(15)];
            Assert.IsTrue(background.HasSnapshot);
            Assert.IsTrue(background.Visible);
            Assert.AreEqual(Id(14), background.StateId);
            Assert.AreEqual(100, background.Placement().Position[0], 1e-5);
            Assert.AreEqual(101, scene.Objects[Id(1)].Placement().Position[0], 1e-5);
        }

        [Test]
        public void ResolvesVersionThreeInitialContentWithoutChangingOriginalProperties()
        {
            byte[] bytes = Convert.FromBase64String(kSpace);
            bytes[2] = 3;
            var document = new ShapesXrDocument(bytes);
            foreach (byte id in new byte[] { 1, 2 }) { document.Entities[Id(id)].Properties["z"] = new JValue(Id(11).ToByteArray()); }
            var properties = document.Entities[Id(1)].Properties;
            properties["tx"] = "Legacy label";
            properties["s"] = new JArray(2, 4, 0);
            properties["ts"] = 5;
            properties["l"] = true;
            var scene = new ShapesXrScene(document);
            var child = scene.Objects[Id(1)];
            Assert.AreEqual(scene.SceneId, scene.StateId);
            Assert.IsTrue(child.HasSnapshot);
            Assert.IsTrue(child.Locked);
            Assert.IsTrue(child.Visible);
            Assert.IsFalse(scene.Objects[Id(3)].HasSnapshot);
            Assert.AreEqual("Legacy label", ShapesXrTextAdapter.Read(child, 10).Text);
            Assert.AreEqual("Legacy label", (string)child.Properties["tx"]);
            Assert.IsNull(properties["obj-tx"]);
            Assert.AreEqual(12, child.Placement().Position[0], 1e-5);
        }

        private static Guid Id(byte value) => new Guid(Enumerable.Repeat(value, 16).ToArray());
    }
}
