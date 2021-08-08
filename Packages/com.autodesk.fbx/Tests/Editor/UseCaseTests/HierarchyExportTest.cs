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
    internal class HierarchyExportTest : RoundTripTestBase
    {
        [SetUp]
        public override void Init ()
        {
            fileNamePrefix = "_safe_to_delete__hierarchy_export_test_";
            base.Init ();
        }

        protected override FbxScene CreateScene (FbxManager manager)
        {
            // create the following node hierarchy to test:
            //       Root
            //      /    \
            // Child0    Child1
            //              |
            //            Child2
            //         /    |     \
            //    Child3  Child4  Child5
            FbxScene scene = FbxScene.Create (manager, "myScene");

            FbxNode root = FbxNode.Create (scene, "Root");
            FbxNode[] children = new FbxNode[6];
            for (int i = 0; i < children.Length; i++) {
                children [i] = FbxNode.Create (scene, "Child" + i);
            }

            scene.GetRootNode ().AddChild (root);
            root.AddChild (children [0]);
            root.AddChild (children [1]);
            children [1].AddChild (children [2]);
            children [2].AddChild (children [3]);
            children [2].AddChild (children [4]);
            children [2].AddChild (children [5]);

            return scene;
        }

        protected override void CheckScene (FbxScene scene)
        {
            FbxScene origScene = CreateScene (FbxManager);

            // Compare the hierarchy of the two scenes
            FbxNode origRoot = origScene.GetRootNode();
            FbxNode importRoot = scene.GetRootNode ();

            CheckSceneHelper (origRoot, importRoot);
        }

        // compare the hierarchy of two nodes
        private void CheckSceneHelper(FbxNode node1, FbxNode node2)
        {
            if (node1 == null && node2 == null) {
                return;
            }

            Assert.IsNotNull (node1);
            Assert.IsNotNull (node2);

            Assert.AreEqual (node1.GetChildCount (), node2.GetChildCount ());
            Assert.AreEqual (node1.GetName (), node2.GetName ());

            for (int i = 0; i < node1.GetChildCount (); i++) {
                // recurse through the hierarchy
                CheckSceneHelper (node1.GetChild (i), node2.GetChild (i));
            }
        }
    }
}