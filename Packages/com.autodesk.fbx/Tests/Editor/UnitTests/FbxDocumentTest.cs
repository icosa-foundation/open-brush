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
    internal class FbxDocumentTest : Base<FbxDocument>
    {
        private static Dictionary<string, string> m_dataValues = new Dictionary<string, string> ()
        {
            { "title",      "Document Title" },
            { "subject",    "Unit Tests for DocumentInfo class." },
            { "author",     "Unity Technologies" },
            { "revision",   "1.0" },
            { "keywords",   "do not crash" },
            { "comment",    "Testing the DocumentInfo object." },
        };

        protected Dictionary<string, string> dataValues { get { return m_dataValues; } }

        // Override this test and simply ignore it: documents don't fit into scenes.
        protected override void TestSceneContainer() { }

        [Test]
        public void TestDocumentInfo ()
        {
            using (var doc = CreateObject("RootDoc"))
            {
                // NOTE: we'll get a ArgumentNullException warning if we use the using
                // scope because doc.Clear() will destroy the FbxDocumentInfo.
                var docInfo = FbxDocumentInfo.Create (this.Manager, "myDocumentInfo");
                Assert.IsNotNull (docInfo);

                doc.SetDocumentInfo (FbxDocumentInfoTest.InitDocumentInfo (docInfo, this.dataValues));

                var docInfo2 = doc.GetDocumentInfo ();
                Assert.IsNotNull (docInfo2);

                FbxDocumentInfoTest.CheckDocumentInfo (docInfo2, this.dataValues);

                // TODO: test identity
                // Assert.AreEqual (docInfo2, docInfo);
                // Assert.AreSame (docInfo2, docInfo);

                Assert.That (() => { doc.SetDocumentInfo (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // CRASH ALERT!!! remove reference to document info before
                // going out of using docInfo scope.
                doc.Clear ();

                Assert.IsNull (doc.GetDocumentInfo ());

                FbxCollectionTest.GenericTests (doc, Manager);
            }
        }
    }
}
