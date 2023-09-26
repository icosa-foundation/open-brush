using NUnit.Framework;
using System.Collections.Generic;

namespace Autodesk.Fbx.UseCaseTests
{
    internal class AnimatedConstraintExportTest : AnimationClipsExportTest
    {
        protected override string[] PropertyNames
        {
            get
            {
                return new string[] {
                    "Weight"
                };
            }
        }

        protected override string[] Components
        {
            get
            {
                return new string[] { null };
            }
        }

        protected const string ConstraintName = "posConstraint";

        [SetUp]
        public override void Init()
        {
            fileNamePrefix = "_safe_to_delete__animated_constraint_export_test";
            base.Init();
        }

        protected override FbxScene CreateScene(FbxManager manager)
        {
            // Create a scene with a single node that has an animation clip
            // attached to it
            FbxScene scene = FbxScene.Create(manager, "myScene");

            FbxNode sourceNode = FbxNode.Create(scene, "source");
            FbxNode constrainedNode = FbxNode.Create(scene, "constrained");

            scene.GetRootNode().AddChild(sourceNode);
            scene.GetRootNode().AddChild(constrainedNode);

            FbxConstraint posConstraint = CreatePositionConstraint(scene, sourceNode, constrainedNode);

            Assert.That(posConstraint, Is.Not.Null);

            bool result = posConstraint.ConnectDstObject(scene);
            Assert.That(result, Is.True);

            // animate weight + active
            // setup anim stack
            FbxAnimStack fbxAnimStack = CreateAnimStack(scene);

            // add an animation layer
            FbxAnimLayer fbxAnimLayer = FbxAnimLayer.Create(scene, "animBaseLayer");
            fbxAnimStack.AddMember(fbxAnimLayer);

            // set up the translation
            CreateAnimCurves(
                posConstraint, fbxAnimLayer, PropertyComponentList, (index) => { return index * 2.0; }, (index) => { return index * 3 - 2; }
                );

            // TODO: avoid needing to do this by creating typemaps for
            //       FbxObject::GetSrcObjectCount and FbxCast.
            //       Not trivial to do as both fbxobject.i and fbxemitter.i
            //       have to be moved up before the ignore all statement
            //       to allow use of templates.
            scene.SetCurrentAnimationStack(fbxAnimStack);
            return scene;
        }

        protected FbxConstraint CreatePositionConstraint(FbxScene scene, FbxNode sourceNode, FbxNode constrainedNode)
        {
            FbxConstraintPosition constraint = FbxConstraintPosition.Create(scene, ConstraintName);

            constraint.SetConstrainedObject(constrainedNode);
            constraint.AddConstraintSource(sourceNode);

            constraint.AffectX.Set(true);
            constraint.AffectY.Set(true);
            constraint.AffectZ.Set(true);

            constraint.Translation.Set(new FbxDouble3(1, 2, 3));

            return constraint;
        }

        protected override void CheckScene(FbxScene scene)
        {
            FbxScene origScene = CreateScene(FbxManager);
            Assert.That(origScene.GetRootNode().GetChildCount(), Is.EqualTo(scene.GetRootNode().GetChildCount()));

            // check nodes match
            FbxNode origSourceNode = origScene.GetRootNode().GetChild(0);
            FbxNode origConstrainedNode = origScene.GetRootNode().GetChild(1);

            FbxNode importSourceNode = scene.GetRootNode().GetChild(0);
            FbxNode importConstrainedNode = scene.GetRootNode().GetChild(1);

            Assert.That(origSourceNode, Is.Not.Null);
            Assert.That(importSourceNode, Is.Not.Null);
            Assert.That(origConstrainedNode, Is.Not.Null);
            Assert.That(importConstrainedNode, Is.Not.Null);
            Assert.That(importSourceNode.GetName(), Is.EqualTo(origSourceNode.GetName()));
            Assert.That(importConstrainedNode.GetName(), Is.EqualTo(origConstrainedNode.GetName()));

            // check constraints match
            // TODO: find a way to cast to FbxConstraint
            Assert.That(scene.GetSrcObjectCount(), Is.EqualTo(origScene.GetSrcObjectCount()));
            FbxObject origPosConstraint = origScene.FindSrcObject(ConstraintName);
            FbxObject importPosConstraint = scene.FindSrcObject(ConstraintName);

            Assert.That(origPosConstraint, Is.Not.Null);
            Assert.That(importPosConstraint, Is.Not.Null);
            Assert.That(importPosConstraint.GetName(), Is.EqualTo(origPosConstraint.GetName()));

            // check animation matches
            FbxAnimStack origStack = origScene.GetCurrentAnimationStack();
            FbxAnimStack importStack = scene.GetCurrentAnimationStack();

            CheckAnimStack(origStack, importStack);

            FbxAnimLayer origLayer = origStack.GetAnimLayerMember();
            FbxAnimLayer importLayer = importStack.GetAnimLayerMember();

            Assert.That(origLayer, Is.Not.Null);
            Assert.That(importLayer, Is.Not.Null);

            Assert.That(scene.GetGlobalSettings().GetTimeMode(), Is.EqualTo(FbxTime.EMode.eFrames30));

            CheckAnimCurve(origPosConstraint, importPosConstraint, origLayer, importLayer, PropertyComponentList);
        }
    }
}
