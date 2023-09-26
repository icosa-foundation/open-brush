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
    internal class FbxMarkerTest : FbxNodeAttributeBase<FbxMarker>
    {
        [Test]
        public void TestBasics ()
        {
            var marker = CreateObject ("marker");
            base.TestBasics(marker, FbxNodeAttribute.EType.eMarker);

            /* Note: the type is undefined until you set it! */

            marker.SetMarkerType(FbxMarker.EType.eStandard);
            Assert.AreEqual (FbxMarker.EType.eStandard, marker.GetMarkerType ());

            TestGetter (marker.Size);
            TestGetter (marker.ShowLabel);
            TestGetter (marker.Look);
            TestGetter (marker.DrawLink);

            marker.SetMarkerType(FbxMarker.EType.eOptical);
            {
                marker.SetDefaultOcclusion(0.5);
                Assert.AreEqual(0.5, marker.GetDefaultOcclusion());
                TestGetter (marker.GetOcclusion());
            }

            marker.SetMarkerType(FbxMarker.EType.eEffectorIK);
            {
                marker.SetDefaultIKReachTranslation(0.5);
                Assert.AreEqual(0.5, marker.GetDefaultIKReachTranslation());

                marker.SetDefaultIKReachRotation(0.5);
                Assert.AreEqual(0.5, marker.GetDefaultIKReachRotation());

                marker.SetDefaultIKPull(0.5);
                Assert.AreEqual(0.5, marker.GetDefaultIKPull());

                marker.SetDefaultIKPullHips(0.5);
                Assert.AreEqual(0.5, marker.GetDefaultIKPullHips());

                TestGetter (marker.IKPivot);
                TestGetter (marker.GetIKPull());
                TestGetter (marker.GetIKPullHips());
                TestGetter (marker.GetIKReachRotation());
                TestGetter (marker.GetIKReachTranslation());
            }

            marker.Reset();
            Assert.AreEqual (FbxMarker.EType.eStandard, marker.GetMarkerType ());
        }
    }
}
