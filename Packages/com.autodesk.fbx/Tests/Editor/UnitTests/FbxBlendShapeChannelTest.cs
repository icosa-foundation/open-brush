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
    internal class FbxBlendShapeChannelTest : Base<FbxBlendShapeChannel>
    {
        [Test]
        public void TestBasics ()
        {
            using (var blendShapeChannel = CreateObject ()) {
                int origCount = blendShapeChannel.GetTargetShapeCount ();

                FbxShape shape = FbxShape.Create (Manager, "shape");
                Assert.IsTrue(blendShapeChannel.AddTargetShape (shape));

                Assert.AreEqual (origCount + 1, blendShapeChannel.GetTargetShapeCount ());
                Assert.AreEqual (shape, blendShapeChannel.GetTargetShape (origCount));
                Assert.AreEqual (origCount, blendShapeChannel.GetTargetShapeIndex (shape));

                // test RemoveTargetShape
                Assert.AreEqual (shape, blendShapeChannel.RemoveTargetShape (shape));
                Assert.IsNull (blendShapeChannel.GetTargetShape (origCount));

                // test AddTargetShape with double doesn't crash
                blendShapeChannel.AddTargetShape (shape, 45);

                // test null
                Assert.That (() => { blendShapeChannel.AddTargetShape (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That (() => { blendShapeChannel.RemoveTargetShape (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test destroyed
                shape.Destroy();
                Assert.That (() => { blendShapeChannel.AddTargetShape (shape); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That (() => { blendShapeChannel.RemoveTargetShape (shape); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test GetDeformPercent
                TestGetter (blendShapeChannel.DeformPercent);

                // test SetBlendShapeDeformer()
                FbxBlendShape blendShape = FbxBlendShape.Create(Manager, "blendShape");
                Assert.IsTrue(blendShapeChannel.SetBlendShapeDeformer (blendShape));
                Assert.AreEqual (blendShape, blendShapeChannel.GetBlendShapeDeformer ());

                // test null
                Assert.That (() => { blendShapeChannel.SetBlendShapeDeformer(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test destroyed
                blendShape = FbxBlendShape.Create(Manager, "blendShape2");
                blendShape.Destroy ();
                Assert.That (() => { blendShapeChannel.SetBlendShapeDeformer (blendShape); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }
    }
}