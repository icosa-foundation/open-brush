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
    internal class FbxSkeletonTest : FbxNodeAttributeBase<FbxSkeleton>
    {
        [Test]
        public void TestBasics ()
        {
            var skeleton = CreateObject ("skeleton");
            base.TestBasics(skeleton, FbxNodeAttribute.EType.eSkeleton);

            Assert.IsFalse (skeleton.GetSkeletonTypeIsSet());
            skeleton.SetSkeletonType(FbxSkeleton.EType.eLimb);
            Assert.AreEqual (FbxSkeleton.EType.eLimb, skeleton.GetSkeletonType ());
            Assert.AreEqual (FbxSkeleton.EType.eRoot, skeleton.GetSkeletonTypeDefaultValue ());
            Assert.IsTrue (skeleton.IsSkeletonRoot());

            Assert.AreEqual (FbxSkeleton.sDefaultLimbLength, skeleton.GetLimbLengthDefaultValue());
            Assert.AreEqual (FbxSkeleton.sDefaultSize, skeleton.GetLimbNodeSizeDefaultValue());

            Assert.IsFalse (skeleton.GetLimbNodeColorIsSet());
            // Note: alpha does not seem to go through SetLimbNodeColor.
            Assert.IsTrue (skeleton.SetLimbNodeColor(new FbxColor(0.5, 0.8, 0.2)));
            Assert.AreEqual (new FbxColor(0.5, 0.8, 0.2), skeleton.GetLimbNodeColor());
            Assert.AreEqual (new FbxColor(0.8, 0.8, 0.8), skeleton.GetLimbNodeColorDefaultValue());
            skeleton.Reset();
            Assert.AreEqual (new FbxColor(0.8, 0.8, 0.8), skeleton.GetLimbNodeColor());

            Assert.AreEqual (skeleton.Size, skeleton.FindProperty(FbxSkeleton.sSize));
            Assert.AreEqual (skeleton.LimbLength, skeleton.FindProperty(FbxSkeleton.sLimbLength));
        }
    }
}
