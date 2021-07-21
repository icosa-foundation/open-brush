// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;
using System.Collections.Generic;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxBindingTableTest : Base<FbxBindingTable>
    {
        [Test]
        public void TestBasics() {
            var table = FbxBindingTable.Create(Manager, "table");

            // Call the getters, make sure they get.
            GetSetProperty(table.DescAbsoluteURL, "file:///dev/null");
            GetSetProperty(table.DescRelativeURL, "shader.glsl");
            GetSetProperty(table.DescTAG, "user");

            // Test dispose.
            var entry = table.AddNewEntry();
            DisposeTester.TestDispose(entry);

            // Test the views.
            entry = table.AddNewEntry();

            var propertyView = new FbxPropertyEntryView(entry, false);
            Assert.IsFalse(propertyView.IsValid());
            DisposeTester.TestDispose(propertyView);

            propertyView = new FbxPropertyEntryView(entry, true, true);
            Assert.IsTrue(propertyView.IsValid());
            Assert.AreEqual("FbxPropertyEntry", propertyView.EntryType());
            propertyView.SetProperty("property");
            Assert.AreEqual("property", propertyView.GetProperty());

            var semanticView = new FbxSemanticEntryView(entry, false);
            Assert.IsFalse(semanticView.IsValid());
            DisposeTester.TestDispose(semanticView);

            semanticView = new FbxSemanticEntryView(entry, false, true);
            Assert.IsTrue(semanticView.IsValid());
            Assert.AreEqual("FbxSemanticEntry", semanticView.EntryType());
            semanticView.SetSemantic("semantic");
            Assert.AreEqual("semantic", semanticView.GetSemantic());
            Assert.AreEqual(0, semanticView.GetIndex());
            Assert.AreEqual("semantic", semanticView.GetSemantic(false));
        }

        void GetSetProperty(FbxPropertyString prop, string value) {
            prop.Set(value);
            Assert.AreEqual(value, prop.Get());
        }
    }
}
