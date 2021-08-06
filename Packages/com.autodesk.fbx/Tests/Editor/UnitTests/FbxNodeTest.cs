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

    internal class FbxNodeTest : Base<FbxNode>
    {
        [Test]
        public void TestBasics ()
        {
            bool ok;
            FbxNode found;

            // Call every function once in a non-corner-case way
            var root = CreateObject("root");

            Assert.AreEqual(0, root.GetChildCount()); // non-recursive
            Assert.AreEqual(0, root.GetChildCount(true)); // recursive

            var t = root.LclTranslation;
            Assert.AreEqual(new FbxDouble3(0,0,0), t.Get());
            var s = root.LclScaling;
            Assert.AreEqual(new FbxDouble3(1,1,1), s.Get());
            var r = root.LclRotation;
            Assert.AreEqual(new FbxDouble3(0,0,0), r.Get());
            var vi = root.VisibilityInheritance;
            Assert.AreEqual (true, vi.Get ());

            var child = CreateObject("child");
            ok = root.AddChild(child);
            Assert.IsTrue(ok);
            Assert.AreEqual(0, child.GetChildCount()); // non-recursive
            Assert.AreEqual(0, child.GetChildCount(true)); // recursive
            Assert.AreEqual(1, root.GetChildCount()); // non-recursive
            Assert.AreEqual(1, root.GetChildCount(true)); // recursive
            found = child.GetParent();
            Assert.AreEqual(root, found);
            found = root.GetChild(0);
            Assert.AreEqual(child, found);

            var grandchild = CreateObject("grandchild");
            ok = child.AddChild(grandchild);
            Assert.IsTrue(ok);
            Assert.AreEqual(0, grandchild.GetChildCount()); // non-recursive
            Assert.AreEqual(0, grandchild.GetChildCount(true)); // recursive
            Assert.AreEqual(1, child.GetChildCount()); // non-recursive
            Assert.AreEqual(1, child.GetChildCount(true)); // recursive
            Assert.AreEqual(1, root.GetChildCount()); // non-recursive
            Assert.AreEqual(2, root.GetChildCount(true)); // recursive
            found = root.GetChild(0);
            Assert.AreEqual(child, found);
            found = child.GetChild(0);
            Assert.AreEqual(grandchild, found);

            // Create a node from the grandchild. That's a child.
            var greatgrandchild = FbxNode.Create(grandchild, "greatgrandchild");
            Assert.AreEqual(1, grandchild.GetChildCount());

            found = root.FindChild("child"); // recursive
            Assert.AreEqual(child, found);
            found = root.FindChild("grandchild"); // recursive
            Assert.AreEqual(grandchild, found);
            found = root.FindChild("grandchild", pRecursive: false);
            Assert.IsNull(found);
            greatgrandchild.SetName("greatest");
            found = root.FindChild("greatgrandchild", pRecursive: true, pInitial: false);
            Assert.AreEqual(null, found);
            found = root.FindChild("greatgrandchild", pRecursive: true, pInitial: true);
            Assert.AreEqual(greatgrandchild, found);

            // Destroying the grandchild recursively nukes the great-grandchild and unparents from child.
            grandchild.Destroy(pRecursive: true);
            Assert.That(() => { greatgrandchild.GetName(); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
            Assert.AreEqual(0, child.GetChildCount());

            // Destroying the child non-recursively (after adding a new
            // grandchild) doesn't destroy the grandchild.
            grandchild = CreateObject("grandchild2");
            child.AddChild(grandchild);
            child.Destroy();
            Assert.AreEqual("grandchild2", grandchild.GetName()); // actually compare by name => check it doesn't throw

            // That unparents the grandchild.
            Assert.IsNull(grandchild.GetParent());

            // Recursively destroying the root does not destroy the grandchild.
            root.Destroy(pRecursive: true);
            Assert.AreEqual("grandchild2", grandchild.GetName()); // actually compare by name => check it doesn't throw

            // Test we can remove a child.
            var fooNode = FbxNode.Create(grandchild, "foo");
            grandchild.RemoveChild(fooNode);
            Assert.IsNull(fooNode.GetParent());
            Assert.AreEqual(0, grandchild.GetChildCount());

            // Add a material.
            var mat = FbxSurfaceMaterial.Create(Manager, "mat");
            Assert.AreEqual(0, fooNode.AddMaterial(mat));
            Assert.That(() => { fooNode.AddMaterial (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            int matIndex = fooNode.GetMaterialIndex ("mat");
            Assert.GreaterOrEqual (matIndex, 0);
            Assert.AreEqual (fooNode.GetMaterial (matIndex), mat);

            // test that invalid material index doesnt crash
            fooNode.GetMaterial(int.MinValue);
            fooNode.GetMaterial (int.MaxValue);

            Assert.Less (fooNode.GetMaterialIndex ("not a mat"), 0);
            // TODO: Find a way to do a null arg check without breaking Create function
            //       (as they both us pName as a param)
            //Assert.That(() => { fooNode.GetMaterialIndex (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

            // Test whether it's a skeleton, camera, etc. It isn't.
            Assert.IsNull(fooNode.GetCamera());
            Assert.IsNull(fooNode.GetGeometry());
            Assert.IsNull(fooNode.GetMesh());
            Assert.IsNull(fooNode.GetNodeAttribute());
            Assert.IsNull(fooNode.GetSkeleton());
            Assert.IsNull (fooNode.GetLight ());

            // Test that we can get at the limits by reference.
            Assert.IsNotNull(fooNode.GetTranslationLimits());
            Assert.IsNotNull(fooNode.GetRotationLimits());
            Assert.IsNotNull(fooNode.GetScalingLimits());

            var limits = fooNode.GetTranslationLimits();
            Assert.IsFalse(limits.GetActive());
            limits.SetActive(true);
            Assert.IsTrue(fooNode.GetTranslationLimits().GetActive());
        }

        [Test]
        public void TestSetNodeAttribute()
        {
            using (FbxNode node = CreateObject ("root")) {
                var nodeAttribute = FbxNodeAttribute.Create (Manager, "node attribute");

                // from Fbx Sdk 2017 docs:
                //    Returns pointer to previous node attribute. NULL if the node didn't have a node
                //    attribute, or if the new node attribute is equal to the one currently set.
                FbxNodeAttribute prevNodeAttribute = node.SetNodeAttribute (nodeAttribute);

                Assert.IsNull (prevNodeAttribute);
                Assert.AreEqual (nodeAttribute, node.GetNodeAttribute ());

                prevNodeAttribute = node.SetNodeAttribute (nodeAttribute);

                Assert.IsNull(prevNodeAttribute);
                Assert.AreEqual (nodeAttribute, node.GetNodeAttribute ());

                prevNodeAttribute = node.SetNodeAttribute(FbxNodeAttribute.Create(Manager, "node attribute 2"));

                Assert.AreEqual (prevNodeAttribute, nodeAttribute);
            }
        }

        [Test]
        public void TestSetNullNodeAttribute()
        {
            using (FbxNode node = CreateObject ("root")) {
                // passing a null NodeAttribute throws a ArgumentNullException
                Assert.That (() => { node.SetNodeAttribute (null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.IsNull (node.GetNodeAttribute ());
            }
        }

        [Test]
        public void TestSetShadingModeToWireFrame()
        {
            using (FbxNode node = CreateObject ("root")) {
                node.SetShadingMode (FbxNode.EShadingMode.eWireFrame);

                Assert.AreEqual (FbxNode.EShadingMode.eWireFrame, node.GetShadingMode ());
            }
        }

        [Test]
        public void TestSetVisibility()
        {
            using(FbxNode node = CreateObject("root")){
                node.SetVisibility (false);
                Assert.AreEqual (node.GetVisibility (), false);
            }
        }

        [Test]
        public void TestEvaluateGlobalTransform()
        {
            // make sure it doesn't crash
            using (FbxNode node = CreateObject ("root")) {
                node.EvaluateGlobalTransform ();
                node.EvaluateGlobalTransform (new FbxTime());
                node.EvaluateGlobalTransform (new FbxTime(), FbxNode.EPivotSet.eDestinationPivot); // eSourcePivot is default
                node.EvaluateGlobalTransform (new FbxTime(), FbxNode.EPivotSet.eSourcePivot, true); // false is default
                node.EvaluateGlobalTransform (new FbxTime(), FbxNode.EPivotSet.eSourcePivot, false, true); // false is default
            }
        }

        [Test]
        public void TestEvaluateLocalTransform()
        {
            // make sure it doesn't crash
            using (FbxNode node = CreateObject ("root")) {
                node.EvaluateLocalTransform ();
                node.EvaluateLocalTransform (new FbxTime());
                node.EvaluateLocalTransform (new FbxTime(), FbxNode.EPivotSet.eDestinationPivot); // eSourcePivot is default
                node.EvaluateLocalTransform (new FbxTime(), FbxNode.EPivotSet.eSourcePivot, true); // false is default
                node.EvaluateLocalTransform (new FbxTime(), FbxNode.EPivotSet.eSourcePivot, false, true); // false is default
            }
        }

        [Test]
        public void TestGetMesh(){
            // make sure it doesn't crash
            using (FbxNode node = CreateObject ("root")) {
                FbxMesh mesh = FbxMesh.Create (Manager, "mesh");
                node.SetNodeAttribute (mesh);
                Assert.AreEqual (mesh, node.GetMesh ());
            }
        }

        [Test]
        public void TestGetLight(){
            // make sure it doesn't crash
            using (FbxNode node = CreateObject ("root")) {
                FbxLight light = FbxLight.Create (Manager, "light");
                node.SetNodeAttribute (light);
                Assert.AreEqual (light, node.GetLight ());
            }
        }

        [Test]
        public void TestSetRotationScalePivotOffset(){
            using (FbxNode node = CreateObject ("root")) {
                FbxVector4 rot = new FbxVector4 (1, 2, 3);
                node.SetPreRotation (FbxNode.EPivotSet.eSourcePivot, rot);
                Assert.AreEqual(rot, node.GetPreRotation(FbxNode.EPivotSet.eSourcePivot));
                Assert.AreNotEqual (rot, node.GetPreRotation (FbxNode.EPivotSet.eDestinationPivot));

                node.SetPostRotation (FbxNode.EPivotSet.eSourcePivot, rot);
                Assert.AreEqual (rot, node.GetPostRotation (FbxNode.EPivotSet.eSourcePivot));

                rot.X = 5;
                node.SetPostRotation (FbxNode.EPivotSet.eDestinationPivot, rot);
                Assert.AreEqual (rot, node.GetPostRotation (FbxNode.EPivotSet.eDestinationPivot));

                node.SetRotationPivot (FbxNode.EPivotSet.eSourcePivot, rot);
                Assert.AreEqual (rot, node.GetRotationPivot (FbxNode.EPivotSet.eSourcePivot));

                node.SetRotationOffset (FbxNode.EPivotSet.eSourcePivot, rot);
                Assert.AreEqual (rot, node.GetRotationOffset (FbxNode.EPivotSet.eSourcePivot));

                node.SetScalingPivot (FbxNode.EPivotSet.eSourcePivot, rot);
                Assert.AreEqual (rot, node.GetScalingPivot (FbxNode.EPivotSet.eSourcePivot));

                node.SetScalingOffset (FbxNode.EPivotSet.eSourcePivot, rot);
                Assert.AreEqual (rot, node.GetScalingOffset (FbxNode.EPivotSet.eSourcePivot));
            }
        }

        [Test]
        public void TestSetPivotState(){
            using (FbxNode node = CreateObject ("root")) {
                // make sure it doesn't crash
                node.SetPivotState (FbxNode.EPivotSet.eSourcePivot, FbxNode.EPivotState.ePivotActive);
            }
        }

        [Test]
        public void TestSetRotationActive(){
            using (FbxNode node = CreateObject ("root")) {
                node.SetRotationActive (true);
                Assert.AreEqual(true, node.GetRotationActive());
            }
        }

        [Test]
        public void TestSetRotationOrder(){
            using (FbxNode node = CreateObject ("root")) {
                // test that it works
                node.SetRotationOrder (FbxNode.EPivotSet.eSourcePivot, FbxEuler.EOrder.eOrderXZY);
                int output = 0;
                node.GetRotationOrder (FbxNode.EPivotSet.eSourcePivot, out output);
                Assert.AreEqual (FbxEuler.EOrder.eOrderXZY, (FbxEuler.EOrder)output);

                // same with destination pivot
                node.SetRotationOrder (FbxNode.EPivotSet.eDestinationPivot, FbxEuler.EOrder.eOrderZXY);
                output = 0;
                node.GetRotationOrder (FbxNode.EPivotSet.eDestinationPivot, out output);
                Assert.AreEqual (FbxEuler.EOrder.eOrderZXY, (FbxEuler.EOrder)output);
            }
        }

        [Test]
        public void TestTransformInheritType(){
            using (FbxNode node = CreateObject ("root")) {
                node.SetTransformationInheritType (FbxTransform.EInheritType.eInheritRrs);
                Assert.AreEqual (FbxTransform.EInheritType.eInheritRrs, node.InheritType.Get());
            }
        }
    }
}
