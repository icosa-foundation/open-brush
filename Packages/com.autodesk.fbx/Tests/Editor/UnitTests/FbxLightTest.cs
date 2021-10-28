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
    internal class FbxLightTest : FbxNodeAttributeBase<FbxLight>
    {
        [Test]
        public void TestBasics()
        {
            using (var fbxLight = CreateObject ("light")) {
                base.TestBasics(fbxLight, FbxNodeAttribute.EType.eLight);

                var shadowTexture = FbxTexture.Create (Manager, "tex");
                fbxLight.SetShadowTexture (shadowTexture);
                Assert.AreEqual (shadowTexture, fbxLight.GetShadowTexture ());

                // test setting null shadow texture
                Assert.That (() => { fbxLight.SetShadowTexture(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test setting invalid texture
                shadowTexture.Destroy();
                Assert.That (() => { fbxLight.SetShadowTexture(shadowTexture); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            }
        }

        [Test]
        public void TestProperties ()
        {
            using (var fbxLight = CreateObject ("light")) {
                // Get the color. Both the one defined in FbxLight, and the one
                // defined in its base class -- they're different functions!
                TestGetter (fbxLight.Color);
                TestGetter (((FbxNodeAttribute)fbxLight).Color);

                // Make sure they return the same property handle under the hood.
                // If in a future version that changes, we should rename both
                // of the properties to avoid bug reports.
                Assert.AreEqual(fbxLight.Color, ((FbxNodeAttribute)fbxLight).Color);

                // Get everything else, which behaves normally.
                TestGetter (fbxLight.DrawFrontFacingVolumetricLight);
                TestGetter (fbxLight.DrawGroundProjection);
                TestGetter (fbxLight.DrawVolumetricLight);
                TestGetter (fbxLight.FileName);
                TestGetter (fbxLight.InnerAngle);
                TestGetter (fbxLight.Intensity);
                TestGetter (fbxLight.LightType);
                TestGetter (fbxLight.OuterAngle);
                TestGetter (fbxLight.AreaLightShape);
                TestGetter (fbxLight.BottomBarnDoor);
                TestGetter (fbxLight.CastLight);
                TestGetter (fbxLight.CastShadows);
                TestGetter (fbxLight.DecayStart);
                TestGetter (fbxLight.DecayType);
                TestGetter (fbxLight.EnableBarnDoor);
                TestGetter (fbxLight.EnableFarAttenuation);
                TestGetter (fbxLight.EnableNearAttenuation);
                TestGetter (fbxLight.FarAttenuationEnd);
                TestGetter (fbxLight.FarAttenuationStart);
                TestGetter (fbxLight.Fog);
                TestGetter (fbxLight.LeftBarnDoor);
                TestGetter (fbxLight.NearAttenuationEnd);
                TestGetter (fbxLight.NearAttenuationStart);
                TestGetter (fbxLight.RightBarnDoor);
                TestGetter (fbxLight.ShadowColor);
                TestGetter (fbxLight.TopBarnDoor);
            }
        }
    }
}
