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
    internal class FbxDocumentInfoTest : Base<FbxDocumentInfo>
    {
        private static Dictionary<string, string> m_dataValues = new Dictionary<string, string> ()
        {
            { "title",      ".YvH5peIJMdg" },
            { "subject",    "lmESAM8Fe3HV" },
            { "author",     "hLsYMCqUekvr" },
            { "revision",   "SknI2x=Ncp5P" },
            { "keywords",   "netJRGcb8alS" },
            { "comment",    ".0pzL-twb6mx" },
        };

        protected Dictionary<string, string> dataValues { get { return m_dataValues; } }

        public static FbxDocumentInfo InitDocumentInfo (FbxDocumentInfo docInfo, Dictionary<string, string> values)
        {
            docInfo.mTitle = values ["title"];
            docInfo.mSubject = values ["subject"];
            docInfo.mAuthor = values ["author"];
            docInfo.mRevision = values ["revision"];
            docInfo.mKeywords = values ["keywords"];
            docInfo.mComment = values ["comment"];

            return docInfo;
        }

        public static void CheckDocumentInfo (FbxDocumentInfo docInfo, Dictionary<string, string> values)
        {
        	Assert.AreEqual (docInfo.mTitle, values ["title"]);
        	Assert.AreEqual (docInfo.mSubject, values ["subject"]);
        	Assert.AreEqual (docInfo.mAuthor, values ["author"]);
        	Assert.AreEqual (docInfo.mRevision, values ["revision"]);
        	Assert.AreEqual (docInfo.mKeywords, values ["keywords"]);
        	Assert.AreEqual (docInfo.mComment, values ["comment"]);
        }

        [Test]
        public void TestDocumentInfo ()
        {
            using (FbxDocumentInfo docInfo = CreateObject()) {
                CheckDocumentInfo (InitDocumentInfo (docInfo, this.dataValues), this.dataValues);

                TestGetter(docInfo.LastSavedUrl);
                TestGetter(docInfo.Url);
                TestGetter(docInfo.Original);
                TestGetter(docInfo.Original_ApplicationVendor);
                TestGetter(docInfo.Original_ApplicationName);
                TestGetter(docInfo.Original_ApplicationVersion);
                TestGetter(docInfo.Original_FileName);
                TestGetter(docInfo.LastSaved);
                TestGetter(docInfo.LastSaved_ApplicationVendor);
                TestGetter(docInfo.LastSaved_ApplicationName);
                TestGetter(docInfo.LastSaved_ApplicationVersion);
                TestGetter(docInfo.EmbeddedUrl);

                docInfo.Clear();
                Assert.AreEqual(docInfo.mTitle, "");
            }
        }

        [Test]
        [Ignore("FbxScene.GetDocumentInfo can return an invalid object and crash.")]
        public void TestCrashOnGetDocumentInfo()
        {
            using (var doc = FbxDocument.Create(Manager, "")) {
                using (var docInfo = CreateObject()) {
                    doc.SetDocumentInfo(docInfo);
                    docInfo.Destroy();

                    // Crash! Normally FBX disconnects when you destroy an
                    // object, but not so for the link between a document and
                    // its document info.
                    doc.GetDocumentInfo().Url.Get();
                }
            }
        }

        [Test]
        [Ignore("FbxScene.GetSceneInfo can return an invalid object and crash.")]
        public void TestCrashOnGetSceneInfo()
        {
            using (var scene = FbxScene.Create(Manager, "")) {
                using (var docInfo = CreateObject()) {
                    scene.SetSceneInfo(docInfo);
                    docInfo.Destroy();

                    // Crash! Normally FBX disconnects when you destroy an
                    // object, but not so for the link between the scene and
                    // its scene info.
                    scene.GetSceneInfo().Url.Get();
                }
            }
        }
    }
}
