// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.IO;
using NUnit.Framework;
using TiltBrush.DocumentImport;

namespace TiltBrush.Tests
{
    public class ShapesXrDocumentTests
    {
        // Synthetic wire fixtures, independently encoded with Python msgpack; no vendor content.
        private const string kSnapshotSpace = "kpIFkgMJmZIAk8QQAQEBAQEBAQEBAQEBAQEBAcMDkgaTxBABAQEBAQEBAQEBAQEBAQEBpWxhYmVsp2luaXRpYWySBpPEEAEBAQEBAQEBAQEBAQEBAQGlbGFiZWyndXBkYXRlZJIAk8QQAgICAgICAgICAgICAgICAsMEkhGTxBACAgICAgICAgICAgICAgICp3NucC1lbnTEEAEBAQEBAQEBAQEBAQEBAQGSEZPEEAICAgICAgICAgICAgICAgKmc25wLXN0xBADAwMDAwMDAwMDAwMDAwMDkgiTxBACAgICAgICAgICAgICAgICqW9iai1wbXVnc5PLP9mZmZmZmZrLP9MzMzMzMzPLP6mZmZmZmZqSAZPEEAICAgICAgICAgICAgICAgKnb2JqLWlzYcKSY5KmZnV0dXJlgaZvcGFxdWXEA2FiYw==";
        private const string kLegacySpace = "kpIlApKSAJLEEAEBAQEBAQEBAQEBAQEBAQHDkgaTxBABAQEBAQEBAQEBAQEBAQEBpWxhYmVso29sZA==";

        [Test]
        public void ReplaysEditsAndResolvesExplicitSnapshotWithoutMutatingBase()
        {
            var doc = new ShapesXrDocument(Convert.FromBase64String(kSnapshotSpace));
            Assert.AreEqual(5, doc.Version);
            Assert.AreEqual(9, doc.EditCount);
            var objectId = new Guid("01010101-0101-0101-0101-010101010101");
            var stateId = new Guid("03030303-0303-0303-0303-030303030303");
            Assert.AreEqual("updated", (string)doc.Entities[objectId].Properties["label"]);
            var resolved = doc.ResolveProperties(objectId, stateId);
            Assert.AreEqual(.4, (double)resolved["obj-pmugs"][0], 1e-6);
            Assert.IsFalse((bool)resolved["obj-isa"]);
            Assert.IsNull(doc.Entities[objectId].Properties["obj-pmugs"]);
            Assert.AreEqual(1, doc.UnknownEdits.Count);
            Assert.AreEqual(99, (int)doc.UnknownEdits[0][0]);
        }

        [Test]
        public void RecognizesHeaderWithoutSchemaAndRetainsLegacyData()
        {
            var doc = new ShapesXrDocument(Convert.FromBase64String(kLegacySpace));
            Assert.AreEqual(0, doc.Version);
            Assert.AreEqual(1, doc.Entities.Count);
            var id = new Guid("01010101-0101-0101-0101-010101010101");
            Assert.AreEqual("old", (string)doc.Entities[id].Properties["label"]);
            Assert.AreEqual(-1, doc.Entities[id].Kind);
            Assert.Throws<NotSupportedException>(() => doc.ResolveProperties(id, Guid.Empty));
        }

        [Test]
        public void RejectsTruncatedAndTrailingWireData()
        {
            byte[] bytes = Convert.FromBase64String(kSnapshotSpace);
            var truncated = new byte[bytes.Length - 1];
            System.Array.Copy(bytes, truncated, truncated.Length);
            Assert.Throws<EndOfStreamException>(() => new ShapesXrDocument(truncated));
            var trailing = new byte[bytes.Length + 1];
            System.Array.Copy(bytes, trailing, bytes.Length);
            Assert.Throws<InvalidDataException>(() => new ShapesXrDocument(trailing));
        }

        [Test]
        public void RejectsUnknownSchemaRatherThanApplyingCurrentSemantics()
        {
            byte[] bytes = Convert.FromBase64String(kSnapshotSpace);
            bytes[2] = 127;
            Assert.Throws<NotSupportedException>(() => new ShapesXrDocument(bytes));
        }
    }
}
