// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using System.Collections.Generic;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UseCaseTests
{
    internal class LightExportTest : AnimationClipsExportTest
    {
        [SetUp]
        public override void Init ()
        {
            fileNamePrefix = "_safe_to_delete__light_export_test";
            base.Init ();
        }

        protected override FbxScene CreateScene (FbxManager manager)
        {
            FbxScene scene = base.CreateScene (manager);
            FbxNode lightNode = scene.GetRootNode ().GetChild (0);

            FbxLight light = FbxLight.Create (scene, "light");
            light.LightType.Set(FbxLight.EType.eSpot);

            light.InnerAngle.Set(20);
            light.OuterAngle.Set(95);

            // Export bounceIntensity as custom property
            ExportFloatProperty (lightNode, 3, "bounceIntensity");

            light.Color.Set (new FbxDouble3(0.3, 0.1, 0.75));

            // Export colorTemperature as custom property
            ExportFloatProperty (lightNode, 6, "colorTemperature");

            light.FileName.Set ("/path/to/texture.png");
            light.DrawGroundProjection.Set (true);
            light.DrawVolumetricLight.Set (true);
            light.DrawFrontFacingVolumetricLight.Set (false);

            ExportFloatProperty (lightNode, 4.2f, "cookieSize");

            light.Intensity.Set (120);
            light.FarAttenuationStart.Set (0.01f /* none zero start */);
            light.FarAttenuationEnd.Set(9.8f);
            light.CastShadows.Set (true);

            FbxAnimStack animStack = scene.GetCurrentAnimationStack ();
            FbxAnimLayer animLayer = animStack.GetAnimLayerMember ();

            // TODO: (UNI-19438) figure out why trying to add anim curves to FbxNodeAttribute.sColor,
            //          Intensity and InnerAngle fails
            // add animation
            CreateAnimCurves (lightNode, animLayer, new List<PropertyComponentPair> () {
                new PropertyComponentPair ("colorTemperature", new string[]{null}),
                new PropertyComponentPair ("cookieSize", new string[]{null})
            }, (index) => { return (index + 1)/2.0; }, (index) => { return index%2; });

            // set ambient lighting
            scene.GetGlobalSettings ().SetAmbientColor (new FbxColor (0.1, 0.2, 0.3));

            lightNode.SetNodeAttribute (light);
            scene.GetRootNode ().AddChild (lightNode);
            return scene;
        }

        protected FbxProperty ExportFloatProperty (FbxObject fbxObject, float value, string name)
        {
            var fbxProperty = FbxProperty.Create (fbxObject, Globals.FbxDoubleDT, name);
            Assert.IsTrue (fbxProperty.IsValid ());

            fbxProperty.Set (value);

            // Must be marked user-defined or it won't be shown in most DCCs
            fbxProperty.ModifyFlag (FbxPropertyFlags.EFlags.eUserDefined, true);
            fbxProperty.ModifyFlag (FbxPropertyFlags.EFlags.eAnimatable, true);

            return fbxProperty;
        }

        protected override void CheckScene (FbxScene scene)
        {
            base.CheckScene (scene);
            FbxScene origScene = CreateScene (FbxManager);

            FbxNode origLightNode = origScene.GetRootNode ().GetChild (0);
            FbxNode importLightNode = scene.GetRootNode ().GetChild (0);
            Assert.IsNotNull (origLightNode);
            Assert.IsNotNull (importLightNode);

            FbxLight origLight = origLightNode.GetLight ();
            FbxLight importLight = importLightNode.GetLight ();
            Assert.IsNotNull (origLight);
            Assert.IsNotNull (importLight);

            Assert.AreEqual (origLight.GetName (), importLight.GetName ());

            // Check properties
            CheckProperties(
                origLightNode, importLightNode,
                origLight, importLight,
                new string[]{ "bounceIntensity", "colorTemperature", "cookieSize" }
            );

            // Check anim
            FbxAnimStack origAnimStack = origScene.GetCurrentAnimationStack();
            FbxAnimLayer origAnimLayer = origAnimStack.GetAnimLayerMember ();
            Assert.IsNotNull (origAnimStack);
            Assert.IsNotNull (origAnimLayer);

            FbxAnimStack importAnimStack = scene.GetCurrentAnimationStack();
            FbxAnimLayer importAnimLayer = importAnimStack.GetAnimLayerMember ();
            Assert.IsNotNull (importAnimStack);
            Assert.IsNotNull (importAnimLayer);

            // TODO: (UNI-19438) figure out why trying to add anim curves to FbxNodeAttribute.sColor,
            //                  Intensity and InnerAngle fails
            CheckAnimCurve (origLightNode, importLightNode, origAnimLayer, importAnimLayer, new List<PropertyComponentPair>(){
                new PropertyComponentPair ("colorTemperature", new string[]{null}),
                new PropertyComponentPair ("cookieSize", new string[]{null})
            }, origLight, importLight);
        }

        protected void CheckProperties(
            FbxNode origLightNode, FbxNode importLightNode,
            FbxLight origLight, FbxLight importLight, string[] customProperties)
        {
            Assert.AreEqual (origLight.LightType.Get (), importLight.LightType.Get ());
            Assert.AreEqual (origLight.InnerAngle.Get (), importLight.InnerAngle.Get ());
            Assert.AreEqual (origLight.OuterAngle.Get (), importLight.OuterAngle.Get ());
            Assert.AreEqual (origLight.Color.Get (), importLight.Color.Get ());
            Assert.AreEqual (origLight.FileName.Get (), importLight.FileName.Get ());
            Assert.AreEqual (origLight.DrawGroundProjection.Get (), importLight.DrawGroundProjection.Get ());
            Assert.AreEqual (origLight.DrawVolumetricLight.Get (), importLight.DrawVolumetricLight.Get ());
            Assert.That (origLight.DrawFrontFacingVolumetricLight.Get (), Is.EqualTo(importLight.DrawFrontFacingVolumetricLight.Get ()).Within(2).Ulps);
            Assert.AreEqual (origLight.Intensity.Get (), importLight.Intensity.Get ());
            Assert.That (origLight.FarAttenuationStart.Get (), Is.EqualTo(importLight.FarAttenuationStart.Get ()).Within(2).Ulps);
            Assert.That (origLight.FarAttenuationEnd.Get (), Is.EqualTo(importLight.FarAttenuationEnd.Get ()).Within(2).Ulps);
            Assert.AreEqual (origLight.CastShadows.Get (), importLight.CastShadows.Get ());

            foreach (var customProp in customProperties) {
                var origProperty = origLightNode.FindProperty (customProp);
                var importProperty = importLightNode.FindProperty (customProp);
                Assert.IsNotNull (origProperty);
                Assert.IsNotNull (importProperty);
                Assert.AreEqual (origProperty.GetFloat (), importProperty.GetFloat ());
            }
        }
    }
}