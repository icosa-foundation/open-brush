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
    internal class FbxSkinTest : FbxDeformerTestBase<FbxSkin>
    {
        [Test]
        public void TestDeformerBasics()
        {
            // test FbxDeformer functions
            TestBasics(CreateObject(), FbxDeformer.EDeformerType.eSkin);
        }

        [Test]
        public void TestAddCluster ()
        {
            var fbxSkin = CreateObject ("skin");
            var fbxCluster = FbxCluster.Create (Manager, "cluster");

            bool result = fbxSkin.AddCluster (fbxCluster);
            Assert.IsTrue (result);
            Assert.AreEqual (fbxSkin.GetCluster (0), fbxCluster);

            // test adding null cluster
            Assert.That (() => { fbxSkin.AddCluster(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // add invalid cluster
            var fbxCluster2 = FbxCluster.Create(Manager, "cluster2");
            fbxCluster2.Dispose();
            Assert.That (() => { fbxSkin.AddCluster(fbxCluster2); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
        }
    }
}
