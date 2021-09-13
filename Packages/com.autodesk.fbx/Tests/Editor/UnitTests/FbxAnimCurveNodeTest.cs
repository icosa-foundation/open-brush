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
    internal class FbxAnimCurveNodeTest : Base<FbxAnimCurveNode>
    {

        [Test]
        public void TestBasics()
        {
            var scene = FbxScene.Create(Manager, "scene");
            var node = FbxNode.Create(scene, "node");

            /* Test all we can test with a non-composite curve node, namely one that points to
               a lcl translation. */
            var animNode = FbxAnimCurveNode.CreateTypedCurveNode(node.LclTranslation, scene);
            Assert.IsFalse(animNode.IsComposite());
            Assert.AreEqual(3, animNode.GetChannelsCount());
            Assert.AreEqual(0, animNode.GetChannelIndex(Globals.FBXSDK_CURVENODE_COMPONENT_X));
            Assert.AreEqual(Globals.FBXSDK_CURVENODE_COMPONENT_Y, animNode.GetChannelName(1));

            var xcurve = animNode.CreateCurve(animNode.GetName(), Globals.FBXSDK_CURVENODE_COMPONENT_X);
            Assert.IsNotNull(xcurve);
            var xcurve2 = animNode.CreateCurve(animNode.GetName());
            Assert.IsNotNull(xcurve2);
            var ycurve = animNode.CreateCurve(animNode.GetName(), 1);
            Assert.IsNotNull(ycurve);

            animNode.SetChannelValue(Globals.FBXSDK_CURVENODE_COMPONENT_Z, 6);
            Assert.AreEqual(6, animNode.GetChannelValue(Globals.FBXSDK_CURVENODE_COMPONENT_Z, 0));
            Assert.AreEqual(6, animNode.GetChannelValue(2, 0));
            animNode.SetChannelValue(2, 0);

            Assert.AreEqual(2, animNode.GetCurveCount(0));
            Assert.AreEqual(1, animNode.GetCurveCount(1, animNode.GetName()));

            Assert.AreEqual(xcurve, animNode.GetCurve(0));
            Assert.AreEqual(xcurve2, animNode.GetCurve(0,1));
            Assert.AreEqual(xcurve2, animNode.GetCurve(0, 1, animNode.GetName()));
            Assert.IsNull(animNode.GetCurve(1,1));

            var key = xcurve.KeyAdd(FbxTime.FromSecondDouble(0));
            xcurve.KeySet(key, FbxTime.FromSecondDouble(0), 5);
            key = xcurve.KeyAdd(FbxTime.FromSecondDouble(1));
            xcurve.KeySet(key, FbxTime.FromSecondDouble(1), -5);

            Assert.IsTrue(animNode.IsAnimated());
            /* TODO: build a composite anim node and test this for real. */
            Assert.IsTrue(animNode.IsAnimated(true));

            var timespan = new FbxTimeSpan();
            Assert.IsTrue(animNode.GetAnimationInterval(timespan));
            Assert.AreEqual(FbxTime.FromSecondDouble(0), timespan.GetStart());
            Assert.AreEqual(FbxTime.FromSecondDouble(1), timespan.GetStop());

            /* Get a property that isn't a Double3; add a channel for it. */
            var boolNode = FbxAnimCurveNode.CreateTypedCurveNode(node.VisibilityInheritance, scene);
            Assert.IsFalse(boolNode.IsComposite());
            Assert.IsFalse(boolNode.IsAnimated());
            Assert.IsTrue(boolNode.AddChannel("vis", 1));
        }
    }
}
