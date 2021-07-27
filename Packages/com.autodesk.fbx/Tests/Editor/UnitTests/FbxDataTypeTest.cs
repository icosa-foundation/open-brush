// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxDataTypeTest
    {
        [Test]
        public void TestEquality()
        {
            // Left here in case we add equality operations back in.
            // For now, equality is just reference equality.
            EqualityTester<FbxDataType>.TestEquality(Globals.FbxBoolDT, Globals.FbxFloatDT, Globals.FbxBoolDT);
        }

        [Test]
        public void BasicTests ()
        {
            // Try all the constructors; make sure they don't crash
            new FbxDataType();
            var v = Globals.FbxBoolDT;
            var v2 = new FbxDataType(v);

            // Call the basic functions, make sure they're reasonable.
            Assert.IsTrue(v.Valid());
            Assert.AreEqual(EFbxType.eFbxBool, v.ToEnum());
            Assert.AreEqual("Bool", v.GetName());
            Assert.AreEqual("bool", v.GetNameForIO());
            Assert.IsTrue(v.Is(v2));

            using(new FbxDataType(EFbxType.eFbxFloat));
            using(new FbxDataType("name", EFbxType.eFbxFloat));
            using(new FbxDataType("name", v));

            // make sure disposing doesn't crash in either case (disposing a handle to a
            // global, or disposing a handle to a copy)
            v.Dispose();
            v2.Dispose();
        }

        public static void TestGetter<U>(U item) { /* we tested the getter by passing the argument! */ }

        [Test]
        public void TestGet()
        {
            /* Get all the constants. */
            TestGetter(Globals.FbxUndefinedDT);
            TestGetter(Globals.FbxBoolDT);
            TestGetter(Globals.FbxCharDT);
            TestGetter(Globals.FbxUCharDT);
            TestGetter(Globals.FbxShortDT);
            TestGetter(Globals.FbxUShortDT);
            TestGetter(Globals.FbxIntDT);
            TestGetter(Globals.FbxUIntDT);
            TestGetter(Globals.FbxLongLongDT);
            TestGetter(Globals.FbxULongLongDT);
            TestGetter(Globals.FbxFloatDT);
            TestGetter(Globals.FbxHalfFloatDT);
            TestGetter(Globals.FbxDoubleDT);
            TestGetter(Globals.FbxDouble2DT);
            TestGetter(Globals.FbxDouble3DT);
            TestGetter(Globals.FbxDouble4DT);
            TestGetter(Globals.FbxDouble4x4DT);
            TestGetter(Globals.FbxEnumDT);
            TestGetter(Globals.FbxStringDT);
            TestGetter(Globals.FbxTimeDT);
            TestGetter(Globals.FbxReferenceDT);
            TestGetter(Globals.FbxBlobDT);
            TestGetter(Globals.FbxDistanceDT);
            TestGetter(Globals.FbxDateTimeDT);
            TestGetter(Globals.FbxColor3DT);
            TestGetter(Globals.FbxColor4DT);
            TestGetter(Globals.FbxCompoundDT);
            TestGetter(Globals.FbxReferenceObjectDT);
            TestGetter(Globals.FbxReferencePropertyDT);
            TestGetter(Globals.FbxVisibilityDT);
            TestGetter(Globals.FbxVisibilityInheritanceDT);
            TestGetter(Globals.FbxUrlDT);
            TestGetter(Globals.FbxXRefUrlDT);
            TestGetter(Globals.FbxTranslationDT);
            TestGetter(Globals.FbxRotationDT);
            TestGetter(Globals.FbxScalingDT);
            TestGetter(Globals.FbxQuaternionDT);
            TestGetter(Globals.FbxLocalTranslationDT);
            TestGetter(Globals.FbxLocalRotationDT);
            TestGetter(Globals.FbxLocalScalingDT);
            TestGetter(Globals.FbxLocalQuaternionDT);
            TestGetter(Globals.FbxTransformMatrixDT);
            TestGetter(Globals.FbxTranslationMatrixDT);
            TestGetter(Globals.FbxRotationMatrixDT);
            TestGetter(Globals.FbxScalingMatrixDT);
            TestGetter(Globals.FbxMaterialEmissiveDT);
            TestGetter(Globals.FbxMaterialEmissiveFactorDT);
            TestGetter(Globals.FbxMaterialAmbientDT);
            TestGetter(Globals.FbxMaterialAmbientFactorDT);
            TestGetter(Globals.FbxMaterialDiffuseDT);
            TestGetter(Globals.FbxMaterialDiffuseFactorDT);
            TestGetter(Globals.FbxMaterialBumpDT);
            TestGetter(Globals.FbxMaterialNormalMapDT);
            TestGetter(Globals.FbxMaterialTransparentColorDT);
            TestGetter(Globals.FbxMaterialTransparencyFactorDT);
            TestGetter(Globals.FbxMaterialSpecularDT);
            TestGetter(Globals.FbxMaterialSpecularFactorDT);
            TestGetter(Globals.FbxMaterialShininessDT);
            TestGetter(Globals.FbxMaterialReflectionDT);
            TestGetter(Globals.FbxMaterialReflectionFactorDT);
            TestGetter(Globals.FbxMaterialDisplacementDT);
            TestGetter(Globals.FbxMaterialVectorDisplacementDT);
            TestGetter(Globals.FbxMaterialCommonFactorDT);
            TestGetter(Globals.FbxMaterialCommonTextureDT);
            TestGetter(Globals.FbxLayerElementUndefinedDT);
            TestGetter(Globals.FbxLayerElementNormalDT);
            TestGetter(Globals.FbxLayerElementBinormalDT);
            TestGetter(Globals.FbxLayerElementTangentDT);
            TestGetter(Globals.FbxLayerElementMaterialDT);
            TestGetter(Globals.FbxLayerElementTextureDT);
            TestGetter(Globals.FbxLayerElementPolygonGroupDT);
            TestGetter(Globals.FbxLayerElementUVDT);
            TestGetter(Globals.FbxLayerElementVertexColorDT);
            TestGetter(Globals.FbxLayerElementSmoothingDT);
            TestGetter(Globals.FbxLayerElementCreaseDT);
            TestGetter(Globals.FbxLayerElementHoleDT);
            TestGetter(Globals.FbxLayerElementUserDataDT);
            TestGetter(Globals.FbxLayerElementVisibilityDT);
            TestGetter(Globals.FbxAliasDT);
            TestGetter(Globals.FbxPresetsDT);
            TestGetter(Globals.FbxStatisticsDT);
            TestGetter(Globals.FbxTextLineDT);
            TestGetter(Globals.FbxUnitsDT);
            TestGetter(Globals.FbxWarningDT);
            TestGetter(Globals.FbxWebDT);
            TestGetter(Globals.FbxActionDT);
            TestGetter(Globals.FbxCameraIndexDT);
            TestGetter(Globals.FbxCharPtrDT);
            TestGetter(Globals.FbxConeAngleDT);
            TestGetter(Globals.FbxEventDT);
            TestGetter(Globals.FbxFieldOfViewDT);
            TestGetter(Globals.FbxFieldOfViewXDT);
            TestGetter(Globals.FbxFieldOfViewYDT);
            TestGetter(Globals.FbxFogDT);
            TestGetter(Globals.FbxHSBDT);
            TestGetter(Globals.FbxIKReachTranslationDT);
            TestGetter(Globals.FbxIKReachRotationDT);
            TestGetter(Globals.FbxIntensityDT);
            TestGetter(Globals.FbxLookAtDT);
            TestGetter(Globals.FbxOcclusionDT);
            TestGetter(Globals.FbxOpticalCenterXDT);
            TestGetter(Globals.FbxOpticalCenterYDT);
            TestGetter(Globals.FbxOrientationDT);
            TestGetter(Globals.FbxRealDT);
            TestGetter(Globals.FbxRollDT);
            TestGetter(Globals.FbxScalingUVDT);
            TestGetter(Globals.FbxShapeDT);
            TestGetter(Globals.FbxStringListDT);
            TestGetter(Globals.FbxTextureRotationDT);
            TestGetter(Globals.FbxTimeCodeDT);
            TestGetter(Globals.FbxTimeWarpDT);
            TestGetter(Globals.FbxTranslationUVDT);
            TestGetter(Globals.FbxWeightDT);
        }
    }
}
