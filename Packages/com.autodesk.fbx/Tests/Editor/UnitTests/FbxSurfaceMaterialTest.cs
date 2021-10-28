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
    internal class FbxSurfaceMaterialTest : Base<FbxSurfaceMaterial>
    {
        public static void TestSurface<T>(T material) where T:FbxSurfaceMaterial
        {
            material.ShadingModel.Get();
            material.MultiLayer.Get();
        }

        [Test]
        public void TestBasics()
        {
            using (var surface = CreateObject()) { TestSurface(surface); }

            // Use all the getters
            TestGetter(FbxSurfaceMaterial.sShadingModel);
            TestGetter(FbxSurfaceMaterial.sMultiLayer);
            TestGetter(FbxSurfaceMaterial.sMultiLayerDefault);
            TestGetter(FbxSurfaceMaterial.sEmissive);
            TestGetter(FbxSurfaceMaterial.sEmissiveFactor);
            TestGetter(FbxSurfaceMaterial.sAmbient);
            TestGetter(FbxSurfaceMaterial.sAmbientFactor);
            TestGetter(FbxSurfaceMaterial.sDiffuse);
            TestGetter(FbxSurfaceMaterial.sDiffuseFactor);
            TestGetter(FbxSurfaceMaterial.sSpecular);
            TestGetter(FbxSurfaceMaterial.sSpecularFactor);
            TestGetter(FbxSurfaceMaterial.sShininess);
            TestGetter(FbxSurfaceMaterial.sBump);
            TestGetter(FbxSurfaceMaterial.sNormalMap);
            TestGetter(FbxSurfaceMaterial.sBumpFactor);
            TestGetter(FbxSurfaceMaterial.sTransparentColor);
            TestGetter(FbxSurfaceMaterial.sTransparencyFactor);
            TestGetter(FbxSurfaceMaterial.sReflection);
            TestGetter(FbxSurfaceMaterial.sReflectionFactor);
            TestGetter(FbxSurfaceMaterial.sDisplacementColor);
            TestGetter(FbxSurfaceMaterial.sDisplacementFactor);
            TestGetter(FbxSurfaceMaterial.sVectorDisplacementColor);
            TestGetter(FbxSurfaceMaterial.sVectorDisplacementFactor);
            TestGetter(FbxSurfaceMaterial.sShadingModelDefault);
        }
    }

    internal class FbxSurfaceLambertTest : Base<FbxSurfaceLambert>
    {
        public static void TestLambert<T>(T lambert) where T:FbxSurfaceLambert
        {
            FbxSurfaceMaterialTest.TestSurface(lambert);
            TestGetter(lambert.Emissive);
            TestGetter(lambert.EmissiveFactor);
            TestGetter(lambert.Ambient);
            TestGetter(lambert.AmbientFactor);
            TestGetter(lambert.Diffuse);
            TestGetter(lambert.DiffuseFactor);
            TestGetter(lambert.NormalMap);
            TestGetter(lambert.Bump);
            TestGetter(lambert.BumpFactor);
            TestGetter(lambert.TransparentColor);
            TestGetter(lambert.TransparencyFactor);
            TestGetter(lambert.DisplacementColor);
            TestGetter(lambert.DisplacementFactor);
            TestGetter(lambert.VectorDisplacementColor);
            TestGetter(lambert.VectorDisplacementFactor);
        }

        [Test]
        public void TestBasics()
        {
            using (var lambert = CreateObject()) { TestLambert(lambert); }
        }
    }

    internal class FbxSurfacePhongTest : Base<FbxSurfacePhong>
    {
        [Test]
        public void TestBasics()
        {
            using (var phong = CreateObject()) {
                FbxSurfaceLambertTest.TestLambert(phong);
                TestGetter(phong.Specular);
                TestGetter(phong.SpecularFactor);
                TestGetter(phong.Shininess);
                TestGetter(phong.Reflection);
                TestGetter(phong.ReflectionFactor);
            }
        }
    }
}
