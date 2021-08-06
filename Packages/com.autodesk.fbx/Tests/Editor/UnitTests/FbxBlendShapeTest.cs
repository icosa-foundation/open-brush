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
    internal class FbxBlendShapeTest : FbxDeformerTestBase<FbxBlendShape>
    {

        [Test]
        public void TestBasics ()
        {
            using (var fbxBlendShape = CreateObject ()) {
                // test FbxDeformer functions
                TestBasics(fbxBlendShape, FbxDeformer.EDeformerType.eBlendShape);

                int origCount = fbxBlendShape.GetBlendShapeChannelCount ();

                // test AddBlendShapeChannel()
                var fbxBlendShapeChannel = FbxBlendShapeChannel.Create (Manager, "blendShapeChannel");
                fbxBlendShape.AddBlendShapeChannel (fbxBlendShapeChannel);

                Assert.AreEqual (origCount+1, fbxBlendShape.GetBlendShapeChannelCount ());
                Assert.AreEqual (fbxBlendShapeChannel, fbxBlendShape.GetBlendShapeChannel (origCount));

                // test RemoveBlendShapeChannel()
                Assert.AreEqual(fbxBlendShapeChannel, fbxBlendShape.RemoveBlendShapeChannel(fbxBlendShapeChannel));
                // test already removed
                Assert.AreEqual(null, fbxBlendShape.RemoveBlendShapeChannel(fbxBlendShapeChannel));

                // test null
                Assert.That (() => { fbxBlendShape.AddBlendShapeChannel (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That (() => { fbxBlendShape.RemoveBlendShapeChannel (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test destroyed
                fbxBlendShapeChannel.Destroy();
                Assert.That (() => { fbxBlendShape.AddBlendShapeChannel (fbxBlendShapeChannel); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That (() => { fbxBlendShape.RemoveBlendShapeChannel (fbxBlendShapeChannel); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test SetGeometry()
                FbxGeometry fbxGeom = FbxGeometry.Create(Manager, "geometry");
                Assert.IsTrue(fbxBlendShape.SetGeometry (fbxGeom));
                Assert.AreEqual (fbxGeom, fbxBlendShape.GetGeometry ());

                // test null
                Assert.That (() => { fbxBlendShape.SetGeometry (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test destroyed
                fbxGeom = FbxGeometry.Create(Manager, "geometry2");
                fbxGeom.Destroy();
                Assert.That (() => { fbxBlendShape.SetGeometry (fbxGeom); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }
    }
}