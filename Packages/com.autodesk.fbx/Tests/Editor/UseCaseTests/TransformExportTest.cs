// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UseCaseTests
{
    internal class TransformExportTest : RoundTripTestBase
    {
        [SetUp]
        public override void Init ()
        {
            fileNamePrefix = "_safe_to_delete__transform_export_test_";
            base.Init ();
        }

        // Export GameObject as standard marker
        protected FbxNull ExportNull (FbxScene fbxScene)
        {
            // create the marker structure.
            FbxNull fbxNull = FbxNull.Create (fbxScene, "Null");

            fbxNull.Look.Set (FbxNull.ELook.eCross);
            fbxNull.Size.Set (1.0f);

            return fbxNull;
        }

        protected override FbxScene CreateScene (FbxManager manager)
        {
            FbxScene scene = FbxScene.Create (manager, "myScene");

            // Create the following node hierarchy with transforms:
            //          Root
            //        (t: 0,10,4)
            //        (r: 0,0,0)
            //        (s: 1,1,1)
            //         /    \
            //   child0      child1
            // (t: 1,1,1)    (t: 0,0,0)
            // (r: 0,0,90)   (r: 180,5,0)
            // (s: 2,2,2)    (s: 3,2,1)
            //                  |
            //                child2
            //               (t: 5,6,20)
            //               (r: 0,10,0)
            //               (s: 1,0.5,1)

            FbxNode root = FbxNode.Create (scene, "Root");
            root.SetNodeAttribute (ExportNull (scene));
            root.SetShadingMode (FbxNode.EShadingMode.eWireFrame);

            // Set the transform values
            root.LclTranslation.Set(new FbxDouble3(0,10,4));
            root.LclRotation.Set(new FbxDouble3(0,0,0));
            root.LclScaling.Set(new FbxDouble3(1,1,1));

            // Set the pre/post rotation, pivots and offsets
            // NOTE: For some reason when using PreRotation.Set() instead of SetPreRotation(),
            //       the PreRotation does not get imported properly. Same is true for the other properties.
            //       Also only works if EPivot set is SourcePivot.
            //       TODO: figure out why the other ways don't work.
            root.SetPreRotation(FbxNode.EPivotSet.eSourcePivot, new FbxVector4(30, 10, 45));
            root.SetPostRotation (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (9, 10, 5));
            root.SetRotationPivot (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (5, 6, 7));
            root.SetScalingPivot (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (1, 2, 1));
            root.SetRotationOffset (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (0.6, 8, 0.3));
            root.SetScalingOffset (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (10, 4, 3));

            FbxNode[] children = new FbxNode[3];
            FbxDouble3[][] transforms = {
                new FbxDouble3[]{new FbxDouble3(1,1,1), new FbxDouble3(0,0,90), new FbxDouble3(2,2,2)},
                new FbxDouble3[]{new FbxDouble3(0,0,0), new FbxDouble3(180,5,0), new FbxDouble3(3,2,1)},
                new FbxDouble3[]{new FbxDouble3(5,6,20), new FbxDouble3(0,10,0), new FbxDouble3(1,0.5,1)}
            };

            for (int i = 0; i < children.Length; i++) {
                children [i] = FbxNode.Create (scene, "Child" + i);

                // set the fbxNode's node attribute
                children[i].SetNodeAttribute (ExportNull (scene));
                children[i].SetShadingMode (FbxNode.EShadingMode.eWireFrame);

                // set the transform
                children [i].LclTranslation.Set (transforms [i] [0]);
                children [i].LclRotation.Set (transforms [i] [1]);
                children [i].LclScaling.Set (transforms [i] [2]);

                // set some values to check against later (doesn't really matter what the values are)
                children [i].SetPreRotation(FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (i, i*2, i%3));
                children [i].SetPostRotation (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (i-1, i+5, i));
                children [i].SetRotationPivot (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (i/2, i, i+3));
                children [i].SetScalingPivot (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (i*5, i-1, i/4));
                children [i].SetRotationOffset (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (0.6*i, 8, i/2.0f));
                children [i].SetScalingOffset (FbxNode.EPivotSet.eSourcePivot, new FbxVector4 (i, i, i));
            }

            // Create the hierarchy
            scene.GetRootNode ().AddChild (root);
            root.AddChild (children [0]);
            root.AddChild (children [1]);
            children [1].AddChild (children [2]);

            return scene;
        }

        protected override void CheckScene (FbxScene scene)
        {
            FbxScene origScene = CreateScene (FbxManager);

            // Compare the hierarchy and transforms of the two scenes
            FbxNode origRoot = origScene.GetRootNode();
            FbxNode importRoot = scene.GetRootNode ();

            CheckSceneHelper (origRoot, importRoot);
        }

        // compare the hierarchy and transform of two nodes
        private void CheckSceneHelper(FbxNode node1, FbxNode node2)
        {
            if (node1 == null && node2 == null) {
                return;
            }

            Assert.IsNotNull (node1);
            Assert.IsNotNull (node2);

            Assert.AreEqual (node1.GetChildCount (), node2.GetChildCount ());

            // compare the transforms
            Assert.AreEqual (node1.LclTranslation.Get(), node2.LclTranslation.Get());
            Assert.AreEqual (node1.LclRotation.Get(), node2.LclRotation.Get());
            Assert.AreEqual (node1.LclScaling.Get(), node2.LclScaling.Get());

            Assert.AreEqual (node1.GetPreRotation (FbxNode.EPivotSet.eSourcePivot),
                node2.GetPreRotation (FbxNode.EPivotSet.eSourcePivot));
            
            Assert.AreEqual (node1.GetPostRotation(FbxNode.EPivotSet.eSourcePivot), 
                node2.GetPostRotation(FbxNode.EPivotSet.eSourcePivot));
            
            Assert.AreEqual (node1.GetRotationPivot(FbxNode.EPivotSet.eSourcePivot),
                node2.GetRotationPivot(FbxNode.EPivotSet.eSourcePivot));
            
            Assert.AreEqual (node1.GetScalingPivot(FbxNode.EPivotSet.eSourcePivot),
                node2.GetScalingPivot(FbxNode.EPivotSet.eSourcePivot));
            
            Assert.AreEqual (node1.GetRotationOffset(FbxNode.EPivotSet.eSourcePivot),
                node2.GetRotationOffset(FbxNode.EPivotSet.eSourcePivot));
            
            Assert.AreEqual (node1.GetScalingOffset(FbxNode.EPivotSet.eSourcePivot),
                node2.GetScalingOffset(FbxNode.EPivotSet.eSourcePivot));

            Assert.AreEqual (node1.GetName (), node2.GetName ());

            for (int i = 0; i < node1.GetChildCount (); i++) {
                // recurse through the hierarchy
                CheckSceneHelper (node1.GetChild (i), node2.GetChild (i));
            }
        }
    }
}