// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.  
//
// Licensed under the ##LICENSENAME##. 
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Autodesk.Fbx;
using System.Linq;

namespace Autodesk.Fbx.UnitTests
{
	internal class FbxAnimCurveFilterUnrollTest : TestBase<FbxAnimCurveFilterUnroll>
    {
        public static IEnumerable KeyTimeValues {
            get {
                yield return new float[4] {0f, 33f, 149f, 7f};
                yield return new float[4] {30f, 59f, -43f, 170f};
                yield return new float[4] {60f, -40f, -31f, 175f};
                yield return new float[4] {90f, -54f, 141f, 6f};
                yield return new float[4] {120f, -7f, 146f, 3f};
            }
        }

        [Test]
        public void TestBasics() {

            // create a curve we can unroll.
            var fbxScene = FbxScene.Create(Manager, "scene");
            var fbxNode = FbxNode.Create(fbxScene, "node");

            var fbxAnimNode = FbxAnimCurveNode.CreateTypedCurveNode(fbxNode.LclRotation, fbxScene);
            FbxAnimCurve[] fbxAnimCurves = {
                fbxAnimNode.CreateCurve(fbxAnimNode.GetName(), Globals.FBXSDK_CURVENODE_COMPONENT_X),
                fbxAnimNode.CreateCurve(fbxAnimNode.GetName(), Globals.FBXSDK_CURVENODE_COMPONENT_Y),
                fbxAnimNode.CreateCurve(fbxAnimNode.GetName(), Globals.FBXSDK_CURVENODE_COMPONENT_Z)
            };

            FbxAnimCurveFilterUnroll filter = new FbxAnimCurveFilterUnroll();

            Assert.That(filter.NeedApply(fbxAnimNode), Is.False,  "expected not to need to unroll curves");
            Assert.That(filter.Apply(fbxAnimNode), Is.False, "expected to have nothing to do");

            // ensure coverage for function that takes an FbxStatus
            Assert.That (filter.NeedApply (fbxAnimNode, new FbxStatus ()), Is.False);
            Assert.That (filter.Apply (fbxAnimNode, new FbxStatus()), Is.False);

            // configure the unroll condition
            foreach (float[] keydata in KeyTimeValues)
            {
                double seconds = keydata[0];

                foreach (var fbxAnimCurve in fbxAnimCurves)
                    fbxAnimCurve.KeyModifyBegin();
                
                using (var fbxTime = FbxTime.FromSecondDouble(seconds))
                {
                    for (int ci = 0; ci < fbxAnimCurves.Length; ci++)
                    {
                        int ki = fbxAnimCurves[ci].KeyAdd(fbxTime);
                        fbxAnimCurves[ci].KeySet(ki, fbxTime, keydata[ci+1]);
                    }
                }

                foreach (var fbxAnimCurve in fbxAnimCurves)
                    fbxAnimCurve.KeyModifyEnd();

            }

            Assert.That(filter.NeedApply(fbxAnimNode), Is.True,  "expected to need to unroll curves");
            Assert.That(filter.Apply(fbxAnimNode), Is.True, "expected to have unroll");

            IEnumerator origKeydata = KeyTimeValues.GetEnumerator();

            for (int ki=0; ki < fbxAnimCurves[0].KeyGetCount(); ki++)
            {
                List<float> result = new List<float>(){(float)fbxAnimCurves[0].KeyGetTime(ki).GetSecondDouble()};

                result = result.Concat((from ac in fbxAnimCurves select ac.KeyGetValue(ki))).ToList();

                origKeydata.MoveNext(); 
                if (ki == 0 || ki == 3 || ki == 4)
                    Assert.That( result, Is.EqualTo(origKeydata.Current));
                else
                    Assert.That( result, Is.Not.EqualTo(origKeydata.Current));
            }

            filter.Reset();
            filter.Dispose ();
        }

        protected FbxManager Manager {
            get;
            private set;
        }

        [SetUp]
        public virtual void Init ()
        {
            Manager = FbxManager.Create ();
        }

        [TearDown]
        public virtual void Term ()
        {
            try {
                Manager.Destroy ();
            }
            catch (System.ArgumentNullException) {
            }
        }
    }
}