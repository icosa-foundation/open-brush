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
    internal class AnimationClipsExportTest : RoundTripTestBase
    {
        protected int m_keyCount = 5;

        protected virtual string[] PropertyNames
        {
            get
            {
                return new string[] {
                    "Lcl Translation",
                    "Lcl Rotation",
                    "Lcl Scaling"
                };
            }
        }

        protected virtual string[] Components
        {
            get
            {
                return new string[] {
                    Globals.FBXSDK_CURVENODE_COMPONENT_X,
                    Globals.FBXSDK_CURVENODE_COMPONENT_Y,
                    Globals.FBXSDK_CURVENODE_COMPONENT_Z
                };
            }
        }

        protected List<PropertyComponentPair> m_propComponentList;
        protected virtual List<PropertyComponentPair> PropertyComponentList
        {
            get
            {
                if (m_propComponentList == null)
                {
                    m_propComponentList = new List<PropertyComponentPair>();
                    foreach (var prop in PropertyNames)
                    {
                        m_propComponentList.Add(new PropertyComponentPair(prop, Components));
                    }
                }
                return m_propComponentList;
            }
        }

        protected struct PropertyComponentPair
        {
            public string propertyName;
            public string[] componentList;

            public PropertyComponentPair(string propName, string[] components)
            {
                propertyName = propName;
                componentList = components;
            }
        }

        [SetUp]
        public override void Init ()
        {
            fileNamePrefix = "_safe_to_delete__animation_clips_export_test";
            base.Init ();
        }

        protected override FbxScene CreateScene (FbxManager manager)
        {
            // Create a scene with a single node that has an animation clip
            // attached to it
            FbxScene scene = FbxScene.Create (manager, "myScene");

            FbxNode animNode = FbxNode.Create (scene, "animNode");

            // setup anim stack
            FbxAnimStack fbxAnimStack = CreateAnimStack(scene);

            // add an animation layer
            FbxAnimLayer fbxAnimLayer = FbxAnimLayer.Create (scene, "animBaseLayer");
            fbxAnimStack.AddMember (fbxAnimLayer);

            // set up the translation
            CreateAnimCurves (
                animNode, fbxAnimLayer, PropertyComponentList, (index) => { return index*2.0; }, (index) => { return index*3.0f - 1; }
                );

            // TODO: avoid needing to this by creating typemaps for
            //       FbxObject::GetSrcObjectCount and FbxCast.
            //       Not trivial to do as both fbxobject.i and fbxemitter.i
            //       have to be moved up before the ignore all statement
            //       to allow use of templates.
            scene.SetCurrentAnimationStack (fbxAnimStack);
            scene.GetRootNode().AddChild (animNode);
            return scene;
        }

        protected FbxAnimStack CreateAnimStack(FbxScene scene)
        {
            FbxAnimStack fbxAnimStack = FbxAnimStack.Create(scene, "animClip");
            fbxAnimStack.Description.Set("Animation Take");

            FbxTime.EMode timeMode = FbxTime.EMode.eFrames30;
            scene.GetGlobalSettings().SetTimeMode(timeMode);

            // set time correctly
            var fbxStartTime = FbxTime.FromSecondDouble(0);
            var fbxStopTime = FbxTime.FromSecondDouble(25);

            fbxAnimStack.SetLocalTimeSpan(new FbxTimeSpan(fbxStartTime, fbxStopTime));
            return fbxAnimStack;
        }

        protected void CreateAnimCurves(
            FbxObject animObject, FbxAnimLayer animLayer,
            List<PropertyComponentPair> properties,
            System.Func<int,double> calcTime, // lambda function for calculating time based on index
            System.Func<int,float> calcValue, // lambda function for calculating value based on index
            FbxNodeAttribute animNodeAttr=null)
        {
            foreach(var pair in properties){
                FbxProperty fbxProperty = animObject.FindProperty (pair.propertyName, false);
                if (animNodeAttr != null && (fbxProperty == null || !fbxProperty.IsValid ())) {
                    // backup method for finding the property if we can't find it on the node itself
                    fbxProperty = animNodeAttr.FindProperty (pair.propertyName, false);
                }

                Assert.IsNotNull (fbxProperty);
                Assert.IsTrue (fbxProperty.IsValid ());
                Assert.That(fbxProperty.GetFlag(FbxPropertyFlags.EFlags.eAnimatable), Is.True);

                foreach (var component in pair.componentList) {
                    // Create the AnimCurve on the channel
                    FbxAnimCurve fbxAnimCurve = fbxProperty.GetCurve (animLayer, component, true);

                    Assert.IsNotNull (fbxAnimCurve);

                    fbxAnimCurve.KeyModifyBegin ();
                    for (int keyIndex = 0; keyIndex < m_keyCount; ++keyIndex) {
                        FbxTime fbxTime = FbxTime.FromSecondDouble(calcTime(keyIndex));
                        fbxAnimCurve.KeyAdd (fbxTime);
                        fbxAnimCurve.KeySet (keyIndex, fbxTime, calcValue(keyIndex));
                    }
                    fbxAnimCurve.KeyModifyEnd ();
                }
            }
        }

        protected override void CheckScene (FbxScene scene)
        {
            FbxScene origScene = CreateScene (FbxManager);

            FbxNode origAnimNode = origScene.GetRootNode ().GetChild (0);
            FbxNode importAnimNode = scene.GetRootNode ().GetChild (0);

            Assert.AreEqual (origScene.GetRootNode ().GetChildCount (), scene.GetRootNode ().GetChildCount ());
            Assert.IsNotNull (origAnimNode);
            Assert.IsNotNull (importAnimNode);
            Assert.AreEqual (origAnimNode.GetName (), importAnimNode.GetName ());

            FbxAnimStack origStack = origScene.GetCurrentAnimationStack ();
            FbxAnimStack importStack = scene.GetCurrentAnimationStack ();

            CheckAnimStack(origStack, importStack);

            FbxAnimLayer origLayer = origStack.GetAnimLayerMember ();
            FbxAnimLayer importLayer = importStack.GetAnimLayerMember ();

            Assert.IsNotNull (origLayer);
            Assert.IsNotNull (importLayer);

            Assert.AreEqual(FbxTime.EMode.eFrames30, scene.GetGlobalSettings().GetTimeMode());

            CheckAnimCurve (origAnimNode, importAnimNode, origLayer, importLayer, PropertyComponentList);
        }

        protected void CheckAnimStack(FbxAnimStack origStack, FbxAnimStack importStack)
        {
            Assert.IsNotNull(origStack);
            Assert.IsNotNull(importStack);
            Assert.AreEqual(origStack.GetName(), importStack.GetName());
            Assert.AreEqual(origStack.Description.Get(), importStack.Description.Get());
            Assert.AreEqual(origStack.GetMemberCount(), importStack.GetMemberCount());

            Assert.AreEqual(origStack.GetLocalTimeSpan(), importStack.GetLocalTimeSpan());
        }

        protected void CheckAnimCurve(
            FbxObject origAnimObject, FbxObject importAnimObject,
            FbxAnimLayer origLayer, FbxAnimLayer importLayer,
            List<PropertyComponentPair> propCompPairs,
            FbxNodeAttribute origNodeAttr=null, FbxNodeAttribute importNodeAttr=null)
        {
            foreach (var pair in propCompPairs) {
                FbxProperty origProperty = origAnimObject.FindProperty (pair.propertyName, false);
                if (origNodeAttr != null && (origProperty == null || !origProperty.IsValid ())) {
                    origProperty = origNodeAttr.FindProperty (pair.propertyName, false);
                }
                FbxProperty importProperty = importAnimObject.FindProperty (pair.propertyName, false);
                if (importNodeAttr != null && (importProperty == null || !importProperty.IsValid ())) {
                    importProperty = importNodeAttr.FindProperty (pair.propertyName, false);
                }

                Assert.IsNotNull (origProperty);
                Assert.IsNotNull (importProperty);
                Assert.IsTrue (origProperty.IsValid ());
                Assert.IsTrue (importProperty.IsValid ());

                foreach (var component in pair.componentList) {

                    FbxAnimCurve origAnimCurve = origProperty.GetCurve (origLayer, component, false);
                    FbxAnimCurve importAnimCurve = importProperty.GetCurve (importLayer, component, false);

                    Assert.IsNotNull (origAnimCurve);
                    Assert.IsNotNull (importAnimCurve);

                    Assert.AreEqual (origAnimCurve.KeyGetCount (), importAnimCurve.KeyGetCount ());

                    for (int i = 0; i < origAnimCurve.KeyGetCount (); i++) {
                        Assert.AreEqual (origAnimCurve.KeyGetTime (i), importAnimCurve.KeyGetTime (i));
                        Assert.AreEqual (origAnimCurve.KeyGetValue (i), importAnimCurve.KeyGetValue (i));
                    }
                }
            }
        }
    }
}