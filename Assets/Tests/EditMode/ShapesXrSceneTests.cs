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

        private static Guid Id(byte value) => new Guid(Enumerable.Repeat(value, 16).ToArray());
    }
}