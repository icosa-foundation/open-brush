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
    internal class FbxCameraTest : FbxNodeAttributeBase<FbxCamera>
    {
        [Test]
        public void TestBasics()
        {
            using (var fbxCamera = CreateObject ("camera")) {

                base.TestBasics(fbxCamera, FbxNodeAttribute.EType.eCamera);

                // test SetAspect
                fbxCamera.SetAspect (FbxCamera.EAspectRatioMode.eFixedResolution, 100, 200);
                Assert.AreEqual (FbxCamera.EAspectRatioMode.eFixedResolution, fbxCamera.GetAspectRatioMode ());

                // test SetAspect with invalid width/height (make sure it doesn't crash)
                fbxCamera.SetAspect (FbxCamera.EAspectRatioMode.eFixedResolution, -100, 200);
                fbxCamera.SetAspect (FbxCamera.EAspectRatioMode.eFixedResolution, 100, -200);

                // Test SetApertureWidth
                fbxCamera.SetApertureWidth(100.0);
                Assert.AreEqual (100, (int)fbxCamera.GetApertureWidth ());
                // test with negative width
                fbxCamera.SetApertureWidth(-100.0);

                // Test SetApertureHeight
                fbxCamera.SetApertureHeight(100.0);
                Assert.AreEqual (100, (int)fbxCamera.GetApertureHeight ());
                // test with negative height
                fbxCamera.SetApertureHeight(-100.0);

                // Test SetApertureMode
                fbxCamera.SetApertureMode(FbxCamera.EApertureMode.eFocalLength);
                Assert.AreEqual (FbxCamera.EApertureMode.eFocalLength, fbxCamera.GetApertureMode ());

                // Test SetNearPlane
                fbxCamera.SetNearPlane(10.0);
                Assert.AreEqual (10, (int)fbxCamera.GetNearPlane ());
                // test with negative value
                fbxCamera.SetNearPlane(-10.0);

                // Test SetFarPlane
                fbxCamera.SetFarPlane(10.0);
                Assert.AreEqual (10, (int)fbxCamera.GetFarPlane ());
                // test with negative value
                fbxCamera.SetFarPlane(-10.0);

                // Test ComputeFocalLength
                double result = fbxCamera.ComputeFocalLength(90);
                Assert.GreaterOrEqual (result, 0);
                // test with negative value
                result = fbxCamera.ComputeFocalLength(-90);
                Assert.LessOrEqual (result, 0);
            }
        }

        [Test]
        public void TestProperties(){
            using (var fbxCamera = CreateObject ("camera")) {
                // test getting the properties
                TestGetter (fbxCamera.ProjectionType);
                TestGetter (fbxCamera.FilmAspectRatio);
                TestGetter (fbxCamera.FocalLength);
                TestGetter (fbxCamera.AspectHeight);
                TestGetter (fbxCamera.AspectWidth);
                TestGetter (fbxCamera.NearPlane);
                TestGetter (fbxCamera.FieldOfView);
                TestGetter (fbxCamera.GateFit);
                TestGetter (fbxCamera.FilmOffsetX);
                TestGetter (fbxCamera.FilmOffsetY);
            }
        }
    }
}
