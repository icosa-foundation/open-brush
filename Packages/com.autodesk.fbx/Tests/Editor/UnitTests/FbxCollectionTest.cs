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
    internal class FbxCollectionTest : Base<FbxCollection>
    {

        public static void GenericTests<T>(T fbxCollection, FbxManager manager) where T : FbxCollection
        {
            // TODO: FbxScene has a member count of 3 instead of one (even after clearing), is this normal?
            int initialMemberCount = fbxCollection.GetMemberCount ();

            // test AddMember
            FbxObject obj = FbxObject.Create (manager, "");
            bool result = fbxCollection.AddMember (obj);
            Assert.IsTrue (result);
            Assert.AreEqual(initialMemberCount+1, fbxCollection.GetMemberCount());

            // test Clear
            fbxCollection.Clear ();
            Assert.AreEqual (initialMemberCount, fbxCollection.GetMemberCount());

            // test GetAnimLayerMember()
            fbxCollection.AddMember(FbxAnimLayer.Create(manager, "animLayer"));
            var animLayer = fbxCollection.GetAnimLayerMember ();
            Assert.IsInstanceOf<FbxAnimLayer> (animLayer);

            var animLayer2 = fbxCollection.GetAnimLayerMember (0);

            Assert.AreEqual (animLayer, animLayer2);

            // check invalid
            Assert.IsNull(fbxCollection.GetAnimLayerMember (1));
            Assert.IsNull(fbxCollection.GetAnimLayerMember (-1));
        }

        [Test]
        public void TestBasics ()
        {
            GenericTests (CreateObject ("fbx collection"), Manager);
        }
    }
}
