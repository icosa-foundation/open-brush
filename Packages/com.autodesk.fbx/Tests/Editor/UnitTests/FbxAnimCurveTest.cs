// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections.Generic;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxAnimCurveTest : Base<FbxAnimCurve>
    {
        Dictionary<FbxManager, FbxScene> m_scenes = new Dictionary<FbxManager, FbxScene>();

        public override FbxAnimCurve CreateObject(FbxManager mgr, string name = "") {
            if (mgr == null) { throw new System.ArgumentNullException(); }

            /* Creating in a manager doesn't work for AnimCurves, but for the benefit of
               testing, just fudge it by creating a scene for the manager. */
            FbxScene scene;
            if (!m_scenes.TryGetValue(mgr, out scene)) {
                scene = FbxScene.Create(mgr, "__testscene");
                m_scenes.Add(mgr, scene);
            }
            return FbxAnimCurve.Create(scene, name);
        }

        public override FbxAnimCurve CreateObject(FbxObject container, string name = "") {
            if (container == null) { throw new System.ArgumentNullException(); }

            if (container is FbxScene) {
                /* Probably should have cast to a scene already... but ok. */
                return FbxAnimCurve.Create((FbxScene)container, name);
            } else {
                /* This create call doesn't do what you want. Use the manager's scene instead. */
                return CreateObject(container.GetFbxManager(), name);
            }
        }

        protected override void TestSceneContainer()
        {
            // The base test tries to make FbxAnimCurve with an FbxAnimCurve as
            // parent; that doesn't work for some reason. So simplify it.
            using(var scene = FbxScene.Create(Manager, "thescene")) {
                var obj = CreateObject(scene, "scene_object");
                Assert.AreEqual(scene, obj.GetScene());
            }
            {
                // The base test assumes that if there's no scene, the object
                // won't be in a scene. But FbxAnimCurve synthesizes a test
                // scene.
                var obj = CreateObject(Manager, "not_scene_object");
                Assert.AreNotEqual(null, obj.GetScene());
            }
        }


        [Test]
        public void TestBasics ()
        {
            var scene = FbxScene.Create(Manager, "scene");
            using (FbxAnimCurve curve = FbxAnimCurve.Create(scene, "curve")) {
                // test KeyModifyBegin (make sure it doesn't crash)
                curve.KeyModifyBegin ();

                // test KeyAdd
                int last = 0;
                int index = curve.KeyAdd (FbxTime.FromFrame (5), ref last);
                Assert.GreaterOrEqual (index, 0);

                // test KeyAdd null FbxTime
                Assert.That (() => { curve.KeyAdd(null); }, Throws.Exception.TypeOf<System.ArgumentNullException>());
                Assert.That (() => { curve.KeyAdd(null, ref last); }, Throws.Exception.TypeOf<System.ArgumentNullException>());

                // test KeySet
                FbxTime keyTime = FbxTime.FromSecondDouble(3);
                curve.KeySet(index, keyTime, 5);

                // test KeyGetValue, KeyGetTime, KeyGetCount
                Assert.AreEqual (5, curve.KeyGetValue (index));
                Assert.AreEqual (keyTime, curve.KeyGetTime (index));
                Assert.AreEqual (1, curve.KeyGetCount ());
                
                // test don't crash
                FbxAnimCurveKey key = curve.KeyGet(index);
                Assert.That(key, Is.Not.Null);

                // make sure none of the variations crash
                curve.KeySet (index, new FbxTime (), 5, FbxAnimCurveDef.EInterpolationType.eInterpolationConstant
                );
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto
                );
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto, 4
                );
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto,
                    0, 3);
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto,
                    0, 0, FbxAnimCurveDef.EWeightedMode.eWeightedAll);
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto,
                    0, 0, FbxAnimCurveDef.EWeightedMode.eWeightedAll, 0);
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto,
                    0, 0, FbxAnimCurveDef.EWeightedMode.eWeightedAll, 0, 0);
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto,
                    0, 0, FbxAnimCurveDef.EWeightedMode.eWeightedAll, 0, 0, 0);
                curve.KeySet (index, new FbxTime (), 0,
                    FbxAnimCurveDef.EInterpolationType.eInterpolationCubic,
                    FbxAnimCurveDef.ETangentMode.eTangentAuto,
                    0, 0, FbxAnimCurveDef.EWeightedMode.eWeightedAll, 0, 0, 0, 0);

                // more setter test
                curve.KeySetTangentMode (index, FbxAnimCurveDef.ETangentMode.eTangentUser);
                Assert.That(curve.KeyGetTangentMode (index), Is.EqualTo (FbxAnimCurveDef.ETangentMode.eTangentUser));
                curve.KeySetTangentMode (index, FbxAnimCurveDef.ETangentMode.eTangentGenericBreak);
                Assert.That(curve.KeyGetTangentMode (index), Is.EqualTo (FbxAnimCurveDef.ETangentMode.eTangentGenericBreak));

                // test settings key parameters
                key.SetTangentMode (FbxAnimCurveDef.ETangentMode.eTangentUser);
                // Set break is only meaningful if tangent is eTangentAuto or eTangentUser
                key.SetBreak (true);
                Assert.True (key.GetBreak ());
                key.SetDataFloat (FbxAnimCurveDef.EDataIndex.eRightSlope, 1.0f);
                Assert.That (key.GetDataFloat (FbxAnimCurveDef.EDataIndex.eRightSlope), Is.EqualTo (1.0f).Within (float.Epsilon));
                key.SetBreak (false);
                Assert.False (key.GetBreak ());
                //
                key.SetTangentWeightMode (FbxAnimCurveDef.EWeightedMode.eWeightedAll);
                key.SetTangentWeightMode (FbxAnimCurveDef.EWeightedMode.eWeightedAll, FbxAnimCurveDef.EWeightedMode.eWeightedAll);
                Assert.That(key.GetTangentWeightMode (), Is.EqualTo(FbxAnimCurveDef.EWeightedMode.eWeightedAll));
                //
                key.SetBreak (true);
                key.SetTangentWeightAndAdjustTangent (FbxAnimCurveDef.EDataIndex.eRightSlope, 1.0);
                key.SetTangentWeightAndAdjustTangent (FbxAnimCurveDef.EDataIndex.eNextLeftSlope, 1.0);
                key.SetTangentWeightAndAdjustTangent (FbxAnimCurveDef.EDataIndex.eWeights, 1.0);
                key.SetTangentWeightAndAdjustTangent (FbxAnimCurveDef.EDataIndex.eRightWeight, 1.0);
                key.SetTangentWeightAndAdjustTangent (FbxAnimCurveDef.EDataIndex.eNextLeftWeight, 1.0);
                key.SetBreak (false);
                //
                key.SetTangentVelocityMode (FbxAnimCurveDef.EVelocityMode.eVelocityAll);
                key.SetTangentVelocityMode (FbxAnimCurveDef.EVelocityMode.eVelocityAll, FbxAnimCurveDef.EVelocityMode.eVelocityAll);
                Assert.That (key.GetTangentVelocityMode (), Is.EqualTo (FbxAnimCurveDef.EVelocityMode.eVelocityAll));
                //
                key.SetTangentVisibility(FbxAnimCurveDef.ETangentVisibility.eTangentShowLeft);
                Assert.That (key.GetTangentVisibility (), Is.EqualTo (FbxAnimCurveDef.ETangentVisibility.eTangentShowLeft));

                // test KeyModifyEnd (make sure it doesn't crash)
                curve.KeyModifyEnd ();
            }

            // Also test that the AnimCurveBase can't be created.
            Assert.That(() => FbxAnimCurveBase.Create(Manager, ""), Throws.Exception.TypeOf<System.NotImplementedException>());
            Assert.That(() => FbxAnimCurveBase.Create(FbxObject.Create(Manager, ""), ""), Throws.Exception.TypeOf<System.NotImplementedException>());
        }

        [Test]
        public override void TestDisposeDestroy()
        {
            // Because we can't build a recursive structure of anim curves,
            // this test is much simpler than the usual FbxObject test.
            var curve = CreateObject("a");
            DisposeTester.TestDispose(curve);
            using (CreateObject("b"));

            curve = CreateObject("c");
            curve.Destroy();
            Assert.That(() => curve.GetName(), Throws.Exception.TypeOf<System.ArgumentNullException>());

            // we can't destroy recursively, but we still get the flag
            curve = CreateObject("d");
            curve.Destroy(false);
        }

        [Test]
        public void TestKeyAddBeforeKeyModifyBegin()
        {
            using (FbxAnimCurve curve = CreateObject ("curve")) {
                curve.KeyAdd (new FbxTime ());
                curve.KeyModifyBegin ();
            }
        }

        [Test]
        public void TestKeyModifyEndBeforeKeyModifyBegin()
        {
            using (FbxAnimCurve curve = CreateObject ("curve")) {
                curve.KeyModifyEnd ();
                curve.KeyModifyBegin ();
            }
        }
    }

    internal class FbxAnimCurveDefTest /* testing a static class, so we can't derive from TestBase */
    {
        [Test]
        public void TestBasics()
        {
            Assert.AreEqual(0.0, FbxAnimCurveDef.sDEFAULT_VELOCITY);
            Assert.AreNotEqual(0.0, FbxAnimCurveDef.sDEFAULT_WEIGHT);
            Assert.That(FbxAnimCurveDef.sMIN_WEIGHT, Is.GreaterThan(0.0f));
            Assert.That(FbxAnimCurveDef.sMAX_WEIGHT, Is.LessThan(1.0f));
        }
    }
}
