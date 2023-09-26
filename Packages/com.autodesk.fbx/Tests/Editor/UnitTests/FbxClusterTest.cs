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
    internal class FbxClusterTest : Base<FbxCluster>
    {

        [Test]
        public void TestBasics ()
        {
            using (var fbxCluster = FbxCluster.Create (Manager, "")) {

                // test set link mode
                fbxCluster.SetLinkMode (FbxCluster.ELinkMode.eAdditive);
                Assert.AreEqual (FbxCluster.ELinkMode.eAdditive, fbxCluster.GetLinkMode ());

                // test set link
                FbxNode node = FbxNode.Create(Manager, "node");
                fbxCluster.SetLink (node);
                Assert.AreEqual (node, fbxCluster.GetLink ());
                // test set null link
                Assert.That (() => { fbxCluster.SetLink(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test add control point index (make sure it doesn't crash)
                fbxCluster.AddControlPointIndex(0, 0);
                fbxCluster.AddControlPointIndex(-1, 0); // doesn't get added (probably because -1 is not a valid index)
                fbxCluster.AddControlPointIndex(0, -1.1);

                Assert.AreEqual (2, fbxCluster.GetControlPointIndicesCount ());

                fbxCluster.SetControlPointIWCount(-1); // test that setting invalid doesn't cause crash
                fbxCluster.SetControlPointIWCount (10);
                Assert.AreEqual (10, fbxCluster.GetControlPointIndicesCount ());

                Assert.AreEqual (0, fbxCluster.GetControlPointIndexAt (0));
                Assert.AreEqual (0, fbxCluster.GetControlPointWeightAt (0));
                Assert.AreEqual (0, fbxCluster.GetControlPointIndexAt (1));
                Assert.AreEqual (-1.1, fbxCluster.GetControlPointWeightAt (1));

                // test set transform matrix
                FbxAMatrix matrix = new FbxAMatrix();
                fbxCluster.SetTransformMatrix (matrix);
                FbxAMatrix returnMatrix = new FbxAMatrix();
                Assert.AreEqual (matrix, fbxCluster.GetTransformMatrix (returnMatrix));
                // test set null transform matrix
                Assert.That (() => { fbxCluster.SetTransformMatrix (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                // test get null transform matrix
                Assert.That (() => { fbxCluster.GetTransformMatrix (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test set transform link matrix
                matrix = new FbxAMatrix();
                fbxCluster.SetTransformLinkMatrix (matrix);
                FbxAMatrix returnMatrix2 = new FbxAMatrix();
                Assert.AreEqual (matrix, fbxCluster.GetTransformLinkMatrix (returnMatrix2));
                // test set null transform link matrix
                Assert.That (() => { fbxCluster.SetTransformLinkMatrix (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                // test get null transform link matrix
                Assert.That (() => { fbxCluster.GetTransformLinkMatrix (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }
    }
}
