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
    internal class CameraExportTest : AnimationClipsExportTest
    {
        [SetUp]
        public override void Init ()
        {
            fileNamePrefix = "_safe_to_delete__camera_export_test";
            base.Init ();
        }

        protected override FbxScene CreateScene (FbxManager manager)
        {
            FbxScene scene = base.CreateScene(manager);
            FbxNode cameraNode = scene.GetRootNode ().GetChild (0);
            FbxCamera camera = FbxCamera.Create (scene, "camera");

            camera.ProjectionType.Set (FbxCamera.EProjectionType.ePerspective);
            camera.SetAspect (FbxCamera.EAspectRatioMode.eFixedRatio, 300, 400);
            camera.FilmAspectRatio.Set (240);
            camera.SetApertureWidth (4);
            camera.SetApertureHeight (2);
            camera.SetApertureMode (FbxCamera.EApertureMode.eFocalLength);
            camera.FocalLength.Set (32);
            camera.SetNearPlane (1);
            camera.SetFarPlane (100);

            // create custom property (background color)
            var bgColorProperty = FbxProperty.Create (cameraNode, Globals.FbxColor4DT, "backgroundColor");
            Assert.IsTrue (bgColorProperty.IsValid ());

            bgColorProperty.Set (new FbxColor(0.5, 0.4, 0.1, 1));

            // Must be marked user-defined or it won't be shown in most DCCs
            bgColorProperty.ModifyFlag (FbxPropertyFlags.EFlags.eUserDefined, true);
            bgColorProperty.ModifyFlag (FbxPropertyFlags.EFlags.eAnimatable, true);

            Assert.IsTrue (bgColorProperty.GetFlag (FbxPropertyFlags.EFlags.eUserDefined));
            Assert.IsTrue (bgColorProperty.GetFlag (FbxPropertyFlags.EFlags.eAnimatable));

            // create custom property (clear flags)
            var clearFlagsProperty = FbxProperty.Create (cameraNode, Globals.FbxIntDT, "clearFlags");
            Assert.IsTrue (clearFlagsProperty.IsValid ());

            clearFlagsProperty.Set (4);

            // Must be marked user-defined or it won't be shown in most DCCs
            clearFlagsProperty.ModifyFlag (FbxPropertyFlags.EFlags.eUserDefined, true);
            clearFlagsProperty.ModifyFlag (FbxPropertyFlags.EFlags.eAnimatable, true);

            Assert.IsTrue (clearFlagsProperty.GetFlag (FbxPropertyFlags.EFlags.eUserDefined));
            Assert.IsTrue (clearFlagsProperty.GetFlag (FbxPropertyFlags.EFlags.eAnimatable));

            // Add camera properties to animation clip
            FbxAnimStack animStack = scene.GetCurrentAnimationStack ();
            FbxAnimLayer animLayer = animStack.GetAnimLayerMember ();

            // TODO: (UNI-19438) Figure out why trying to do GetCurve for NearPlane always returns null
            CreateAnimCurves (cameraNode, animLayer, new List<PropertyComponentPair> () {
                new PropertyComponentPair("backgroundColor", new string[] {
                    Globals.FBXSDK_CURVENODE_COLOR_RED, 
                    Globals.FBXSDK_CURVENODE_COLOR_GREEN, 
                    Globals.FBXSDK_CURVENODE_COLOR_BLUE, "W"
                }),
                new PropertyComponentPair("FocalLength", new string[]{null}),
                new PropertyComponentPair("clearFlags", new string[]{null})
            }, (index) => { return index; }, (index) => { return index/5.0f; }, camera);

            cameraNode.SetNodeAttribute (camera);

            // set the default camera
            scene.GetGlobalSettings ().SetDefaultCamera (cameraNode.GetName());

            return scene;
        }

        protected override void CheckScene (FbxScene scene)
        {
            base.CheckScene (scene);

            FbxScene origScene = CreateScene (FbxManager);

            FbxNode origCameraNode = origScene.GetRootNode ().GetChild (0);
            FbxNode importCameraNode = scene.GetRootNode ().GetChild (0);

            Assert.IsNotNull (origCameraNode);
            Assert.IsNotNull (importCameraNode);

            Assert.AreEqual (origScene.GetGlobalSettings ().GetDefaultCamera (), scene.GetGlobalSettings ().GetDefaultCamera ());

            FbxCamera origCamera = origCameraNode.GetCamera ();
            FbxCamera importCamera = importCameraNode.GetCamera ();

            Assert.IsNotNull (origCamera);
            Assert.IsNotNull (importCamera);

            CheckCameraSettings (origCamera, importCamera, origCameraNode, importCameraNode);

            // check anim
            FbxAnimStack origAnimStack = origScene.GetCurrentAnimationStack();
            FbxAnimLayer origAnimLayer = origAnimStack.GetAnimLayerMember ();
            Assert.IsNotNull (origAnimStack);
            Assert.IsNotNull (origAnimLayer);

            FbxAnimStack importAnimStack = scene.GetCurrentAnimationStack();
            FbxAnimLayer importAnimLayer = importAnimStack.GetAnimLayerMember ();
            Assert.IsNotNull (importAnimStack);
            Assert.IsNotNull (importAnimLayer);

            CheckAnimCurve (origCameraNode, importCameraNode, origAnimLayer, importAnimLayer, new List<PropertyComponentPair>(){
                new PropertyComponentPair("backgroundColor", new string[] {
                    Globals.FBXSDK_CURVENODE_COLOR_RED, 
                    Globals.FBXSDK_CURVENODE_COLOR_GREEN, 
                    Globals.FBXSDK_CURVENODE_COLOR_BLUE, "W"
                }),
                new PropertyComponentPair("FocalLength", new string[]{null}),
                new PropertyComponentPair("clearFlags", new string[]{null})
            }, origCamera, importCamera);
        }

        protected void CheckCameraSettings(FbxCamera origCamera, FbxCamera importCamera, FbxNode origCameraNode, FbxNode importCameraNode)
        {
            Assert.AreEqual (origCamera.ProjectionType.Get (), importCamera.ProjectionType.Get ());
            Assert.AreEqual (origCamera.AspectWidth.Get (), importCamera.AspectWidth.Get ());
            Assert.AreEqual (origCamera.AspectHeight.Get (), importCamera.AspectHeight.Get ());
            Assert.AreEqual (origCamera.GetAspectRatioMode (), importCamera.GetAspectRatioMode ());
            Assert.AreEqual (origCamera.FilmAspectRatio.Get (), importCamera.FilmAspectRatio.Get ());
            Assert.AreEqual (origCamera.GetApertureWidth (), importCamera.GetApertureWidth ());
            Assert.AreEqual (origCamera.GetApertureHeight (), importCamera.GetApertureHeight ());
            Assert.AreEqual (origCamera.GetApertureMode (), origCamera.GetApertureMode ());
            Assert.AreEqual (origCamera.FocalLength.Get (), importCamera.FocalLength.Get ());
            Assert.AreEqual (origCamera.GetNearPlane (), importCamera.GetNearPlane ());
            Assert.AreEqual (origCamera.GetFarPlane (), importCamera.GetFarPlane ());

            foreach (var customProp in new string[]{ "backgroundColor", "clearFlags" }) {
                FbxProperty property = origCameraNode.FindProperty (customProp);
                Assert.IsNotNull (property);
                Assert.IsTrue (property.IsValid ());

                FbxProperty importBgColorProp = importCameraNode.FindProperty (customProp);
                Assert.IsNotNull (importBgColorProp);
                Assert.IsTrue (importBgColorProp.IsValid ());

                if (property.GetPropertyDataType ().Equals(Globals.FbxColor4DT)) {
                    Assert.AreEqual(property.GetFbxColor(), property.GetFbxColor());
                }
                else if (property.GetPropertyDataType().Equals(Globals.FbxIntDT)){
                    Assert.AreEqual(property.GetInt(), property.GetInt());
                }

                Assert.AreEqual (property.GetFlag (FbxPropertyFlags.EFlags.eUserDefined),
                    importBgColorProp.GetFlag (FbxPropertyFlags.EFlags.eUserDefined));
                Assert.AreEqual (property.GetFlag (FbxPropertyFlags.EFlags.eAnimatable),
                    importBgColorProp.GetFlag (FbxPropertyFlags.EFlags.eAnimatable));
            }
        }
    }
}