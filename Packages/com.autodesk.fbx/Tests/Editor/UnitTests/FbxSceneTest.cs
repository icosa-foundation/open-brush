// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxSceneTest : Base<FbxScene>
    {
        protected override void TestSceneContainer()
        {
            // GetScene returns the parent scene.
            using(var scene = FbxScene.Create(Manager, "thescene")) {
                Assert.AreEqual(null, scene.GetScene());
                var subscene = CreateObject(scene, "subscene");
                Assert.AreEqual(scene, subscene.GetScene());
                var subsubscene = CreateObject(subscene, "subscene");
                Assert.AreEqual(subscene, subsubscene.GetScene());
            }
        }

        [Test]
        public void TestBasics()
        {
            using (var scene = FbxScene.Create(Manager, "scene")) {
                // Just call every function. TODO: and test them at least minimally!
                scene.GetGlobalSettings();
                scene.GetRootNode();

                var docInfo = FbxDocumentInfo.Create(Manager, "info");
                scene.SetDocumentInfo(docInfo);
                Assert.AreEqual(docInfo, scene.GetDocumentInfo());

                docInfo = FbxDocumentInfo.Create(Manager, "info2");
                scene.SetSceneInfo(docInfo);
                Assert.AreEqual(docInfo, scene.GetSceneInfo());

                scene.Clear();

                FbxCollectionTest.GenericTests (scene, Manager);
            }
        }

        [Test]
        public override void TestDisposeDestroy ()
        {
           // The scene destroys recursively even if you ask it not to
           DoTestDisposeDestroy(canDestroyNonRecursive: false);
        }

        [Test]
        public void TestNodeCount ()
        {
            using (FbxScene newScene = FbxScene.Create (Manager, ""))
            {
                Assert.GreaterOrEqual (newScene.GetNodeCount (), 0);
            }
        }

        [Test]
        public void TestAddPose()
        {
            using (FbxScene newScene = FbxScene.Create (Manager, "")) {
                FbxPose fbxPose = FbxPose.Create (Manager, "pose");
                bool result = newScene.AddPose (fbxPose);
                Assert.IsTrue (result);
                Assert.AreEqual (fbxPose, newScene.GetPose (0));

                // test null
                Assert.That (() => { newScene.AddPose(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test invalid
                fbxPose.Destroy();
                Assert.That (() => { newScene.AddPose(fbxPose); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }

        [Test]
        public void TestSetCurrentAnimStack()
        {
            using (FbxScene newScene = FbxScene.Create (Manager, "")) {
                FbxAnimStack animStack = FbxAnimStack.Create (Manager, "");
                newScene.SetCurrentAnimationStack (animStack);
                Assert.AreEqual (animStack, newScene.GetCurrentAnimationStack ());

                // test null
                Assert.That (() => { newScene.SetCurrentAnimationStack(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test invalid
                animStack.Destroy();
                Assert.That (() => { newScene.SetCurrentAnimationStack(animStack); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }
    }
}
