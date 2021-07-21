// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.  
//
// Licensed under the ##LICENSENAME##. 
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using Autodesk.Fbx;
using System.IO;
using System.Collections.Generic;

namespace Autodesk.Fbx.UseCaseTests
{

    internal class EmptyExportTest : RoundTripTestBase
    {

        private static Dictionary<string, string> m_dataValues = new Dictionary<string, string> ()
        {
            { "title",      "Empty scene" },
            { "subject",    "Example of an empty scene with document information settings" },
            { "author",     "Unit Technologies" },
            { "revision",   "1.0" },
            { "keywords",   "example empty scene" },
            { "comment",    "basic scene settings. Note that the scene thumnail is not set." },
        };

        protected Dictionary<string, string> dataValues { get { return m_dataValues; } }

        [SetUp]
        public override void Init ()
        {
            fileNamePrefix = "_safe_to_delete__empty_export_test_";
            base.Init ();
        }

        protected override FbxScene CreateScene (FbxManager manager)
        {
            FbxScene scene = FbxScene.Create (manager, "myScene");

            // create scene info
            FbxDocumentInfo sceneInfo = FbxDocumentInfo.Create (manager, "mySceneInfo");

            sceneInfo.mTitle = dataValues ["title"];
            sceneInfo.mSubject = dataValues ["subject"];
            sceneInfo.mAuthor = dataValues ["author"];
            sceneInfo.mRevision = dataValues ["revision"];
            sceneInfo.mKeywords = dataValues ["keywords"];
            sceneInfo.mComment = dataValues ["comment"];

            scene.SetSceneInfo (sceneInfo);

            // TODO: port SetSceneThumbnail

            return scene;
        }

        protected override void CheckScene (FbxScene scene)
        {
            Dictionary<string, string> values = this.dataValues;

            FbxDocumentInfo sceneInfo = scene.GetSceneInfo ();

            Assert.AreEqual (sceneInfo.mTitle, values ["title"]);
            Assert.AreEqual (sceneInfo.mSubject, values ["subject"]);
            Assert.AreEqual (sceneInfo.mAuthor, values ["author"]);
            Assert.AreEqual (sceneInfo.mRevision, values ["revision"]);
            Assert.AreEqual (sceneInfo.mKeywords, values ["keywords"]);
            Assert.AreEqual (sceneInfo.mComment, values ["comment"]);
        }
    }
}
