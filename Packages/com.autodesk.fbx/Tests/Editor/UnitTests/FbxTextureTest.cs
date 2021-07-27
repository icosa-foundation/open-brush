// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;
using System.Collections.Generic;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxTextureTest : Base<FbxTexture>
    {
        public static void CommonTextureTests<T>(T tex) where T : FbxTexture
        {
            // get all the properties
            TestGetter(tex.Alpha);
            TestGetter(tex.WrapModeU);
            TestGetter(tex.WrapModeV);
            TestGetter(tex.UVSwap);
            TestGetter(tex.PremultiplyAlpha);
            TestGetter(tex.Translation);
            TestGetter(tex.Rotation);
            TestGetter(tex.Scaling);
            TestGetter(tex.RotationPivot);
            TestGetter(tex.ScalingPivot);
            TestGetter(tex.CurrentTextureBlendMode);
            TestGetter(tex.UVSet);

            // call all the functions
            tex.SetSwapUV(true);
            Assert.IsTrue(tex.GetSwapUV());

            tex.SetPremultiplyAlpha(true);
            Assert.IsTrue(tex.GetPremultiplyAlpha());

            tex.SetAlphaSource(FbxTexture.EAlphaSource.eRGBIntensity);
            Assert.AreEqual(FbxTexture.EAlphaSource.eRGBIntensity, tex.GetAlphaSource());

            tex.SetCropping(1, 2, 3, 4);
            Assert.AreEqual(1, tex.GetCroppingLeft());
            Assert.AreEqual(2, tex.GetCroppingTop());
            Assert.AreEqual(3, tex.GetCroppingRight());
            Assert.AreEqual(4, tex.GetCroppingBottom());

            tex.SetMappingType(FbxTexture.EMappingType.eSpherical);
            Assert.AreEqual(FbxTexture.EMappingType.eSpherical, tex.GetMappingType());

            tex.SetPlanarMappingNormal(FbxTexture.EPlanarMappingNormal.ePlanarNormalY);
            Assert.AreEqual(FbxTexture.EPlanarMappingNormal.ePlanarNormalY, tex.GetPlanarMappingNormal());

            tex.SetTextureUse(FbxTexture.ETextureUse.eShadowMap);
            Assert.AreEqual(FbxTexture.ETextureUse.eShadowMap, tex.GetTextureUse());

            tex.SetWrapMode(FbxTexture.EWrapMode.eRepeat, FbxTexture.EWrapMode.eClamp);
            Assert.AreEqual(FbxTexture.EWrapMode.eRepeat, tex.GetWrapModeU());
            Assert.AreEqual(FbxTexture.EWrapMode.eClamp, tex.GetWrapModeV());

            tex.SetBlendMode(FbxTexture.EBlendMode.eAdditive);
            Assert.AreEqual(FbxTexture.EBlendMode.eAdditive, tex.GetBlendMode());

            tex.SetDefaultAlpha(0.5);
            Assert.AreEqual(0.5, tex.GetDefaultAlpha());

            tex.SetTranslation(1, 2);
            Assert.AreEqual(1, tex.GetTranslationU());
            Assert.AreEqual(2, tex.GetTranslationV());

            tex.SetRotation(20, 30, 40);
            Assert.AreEqual(20, tex.GetRotationU());
            Assert.AreEqual(30, tex.GetRotationV());
            Assert.AreEqual(40, tex.GetRotationW());

            tex.SetRotation(20, 30);
            Assert.AreEqual(20, tex.GetRotationU());
            Assert.AreEqual(30, tex.GetRotationV());
            Assert.AreEqual(0, tex.GetRotationW());

            tex.SetScale(2, 3);
            Assert.AreEqual(2, tex.GetScaleU());
            Assert.AreEqual(3, tex.GetScaleV());

            tex.Reset();
        }

        [Test]
        public void TestBasics() {
            var tex = FbxTexture.Create(Manager, "tex");
            CommonTextureTests(tex);
            TestGetter(FbxTexture.sVectorSpace);
            TestGetter(FbxTexture.sVectorSpaceWorld);
            TestGetter(FbxTexture.sVectorSpaceObject);
            TestGetter(FbxTexture.sVectorSpaceTangent);
            TestGetter(FbxTexture.sVectorEncoding);
            TestGetter(FbxTexture.sVectorEncodingFP);
            TestGetter(FbxTexture.sVectorEncodingSE);
        }
    }

    internal class FbxFileTextureTest : Base<FbxFileTexture>
    {
        [Test]
        public void TestBasics() {
            var tex = FbxFileTexture.Create(Manager, "tex");
            FbxTextureTest.CommonTextureTests(tex);

            TestGetter(tex.UseMaterial);
            TestGetter(tex.UseMipMap);

            tex.SetFileName("/a/b/c/d.png");
            Assert.AreEqual("/a/b/c/d.png", tex.GetFileName());

            tex.SetRelativeFileName("d.png");
            Assert.AreEqual("d.png", tex.GetRelativeFileName());

            tex.SetMaterialUse(FbxFileTexture.EMaterialUse.eDefaultMaterial);
            Assert.AreEqual(FbxFileTexture.EMaterialUse.eDefaultMaterial, tex.GetMaterialUse());
        }
    }
}
