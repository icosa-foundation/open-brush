// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxPoseTest : Base<FbxPose>
    {

        [Test]
        public void TestSetIsBindPose ()
        {
            var fbxPose = CreateObject ("pose");
            fbxPose.SetIsBindPose (false);
            Assert.IsFalse (fbxPose.IsBindPose ());
        }

        [Test]
        public void TestAdd()
        {
            using(var fbxPose = CreateObject ("pose")){
                using(var fbxNode = FbxNode.Create (Manager, "node"))
                using(var fbxMatrix = new FbxMatrix ()){

                    Assert.AreEqual (0, fbxPose.GetCount ());

                    // test basic use
                    int index = fbxPose.Add (fbxNode, fbxMatrix); // returns -1 if it fails
                    Assert.Greater(index, -1);
                    Assert.AreEqual (fbxPose.GetNode (index), fbxNode);
                    Assert.AreEqual (fbxPose.GetMatrix (index), fbxMatrix);

                    Assert.AreEqual (1, fbxPose.GetCount ());

                    // test adding null
                    Assert.That (() => {
                        fbxPose.Add (null, null);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());

                    fbxPose.Add (FbxNode.Create(Manager,"node1"), fbxMatrix);
                    Assert.AreEqual (2, fbxPose.GetCount ());
                }

                var node = FbxNode.Create (Manager, "node1");
                using (var fbxMatrix = new FbxMatrix ()) {
                    // test adding invalid node
                    node.Destroy ();
                    Assert.That (() => {
                        fbxPose.Add (node, fbxMatrix);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());
                }

                using (var fbxNode = FbxNode.Create (Manager, "node2")){
                    var fbxMatrix = new FbxMatrix ();
                    // test adding invalid matrix
                    fbxMatrix.Dispose ();
                    Assert.That (() => {
                        fbxPose.Add (fbxNode, fbxMatrix);
                    }, Throws.Exception.TypeOf<System.ArgumentNullException> ());
                }

                using (var fbxNode = FbxNode.Create (Manager, "node3"))
                using (var fbxMatrix = new FbxMatrix ()) {
                    // test with local matrix arg
                    int index = fbxPose.Add (fbxNode, fbxMatrix, true); // false is default
                    Assert.Greater(index, -1);
                    Assert.AreEqual (fbxPose.GetNode (index), fbxNode);
                    Assert.AreEqual (fbxPose.GetMatrix (index), fbxMatrix);
                }

                using (var fbxNode = FbxNode.Create (Manager, "node4"))
                using (var fbxMatrix = new FbxMatrix ()) {
                    // test with multiple bind pose arg
                    int index = fbxPose.Add (fbxNode, fbxMatrix, false, false); // true is default
                    Assert.Greater(index, -1);
                    Assert.AreEqual (fbxPose.GetNode (index), fbxNode);
                    Assert.AreEqual (fbxPose.GetMatrix (index), fbxMatrix);
                }
            }
        }
    }
}
