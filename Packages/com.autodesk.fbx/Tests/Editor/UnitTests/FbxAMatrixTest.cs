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
    internal class FbxAMatrixTest : FbxDouble4x4TestBase<FbxAMatrix>
    {
        [Test]
        public void TestEquality()
        {
            var zero = new FbxVector4();
            var one = new FbxVector4(1,1,1);
            var mx1 = new FbxAMatrix(zero, zero, one);
            var mx2 = new FbxAMatrix(one, zero, one);
            var mx1copy = new FbxAMatrix(zero, zero, one);
            EqualityTester<FbxAMatrix>.TestEquality(mx1, mx2, mx1copy);
        }

        // Helper for the scaling operators.
        //
        // If scale is a power of two, tolerance can be zero.
        //
        // Scaling an FbxAMatrix scales the 3x3 matrix for scale and rotation,
        // and zeroes out the translation.
        static void AssertScaled(FbxAMatrix expected, FbxAMatrix scaled,
                double scale, double tolerance = 0)
        {
            for(int y = 0; y < 3; ++y) {
                for (int x = 0; x < 3; ++x) {
                    Assert.AreEqual(scale * expected.Get(x, y), scaled.Get(x, y),
                            tolerance, string.Format("Index ({0} {1})", x, y));
                }
            }
            Assert.AreEqual(new FbxVector4(0,0,0,1), scaled.GetRow(3));
            Assert.AreEqual(new FbxVector4(0,0,0,1), scaled.GetColumn(3));
        }

        [Test]
        public void BasicTests ()
        {
            base.TestElementAccessAndDispose(new FbxAMatrix());

            // make sure the constructors compile and don't crash
            new FbxAMatrix();
            new FbxAMatrix(new FbxAMatrix());
            var mx = new FbxAMatrix(new FbxVector4(), new FbxVector4(), new FbxVector4(1,1,1));

            // check that the matrix is the id matrix
            Assert.IsTrue(mx.IsIdentity());
            for(int y = 0; y < 4; ++y) {
                for(int x = 0; x < 4; ++x) {
                    Assert.AreEqual(x == y ? 1 : 0, mx.Get(y, x));
                }
            }

            // Test that all the operations work.
            // In particular, test that they don't return the default element
            // when they aren't supposed to.

            var translate = new FbxVector4(5, 3, 1);
            var euler = new FbxVector4(-135, -90, 0);
            var scale = new FbxVector4(1, 2, .5);
            var quat = new FbxQuaternion();
            quat.ComposeSphericalXYZ(euler);

            mx = new FbxAMatrix(translate, euler, scale);
            Assert.IsFalse(mx.IsIdentity());
            Assert.IsTrue(mx.IsIdentity(10)); // squint very, very, very hard

            FbxVector4Test.AssertSimilarXYZ(translate, mx.GetT());
            FbxVector4Test.AssertSimilarEuler(euler, mx.GetR());
            FbxQuaternionTest.AssertSimilar(quat, mx.GetQ());
            FbxVector4Test.AssertSimilarXYZ(scale, mx.GetS());
            FbxVector4Test.AssertSimilarXYZ(new FbxVector4(0.354, 0.354, 0), mx.GetRow(2), 1e-2);
            FbxVector4Test.AssertSimilarXYZ(new FbxVector4(1, 0, 0), mx.GetColumn(2));

            mx.SetT(translate * 2);
            FbxVector4Test.AssertSimilarXYZ(2 * translate, mx.GetT());

            mx.SetR(euler * 2);
            FbxVector4Test.AssertSimilarEuler(2 * euler, mx.GetR());

            mx.SetQ(quat * 2);
            FbxQuaternionTest.AssertSimilar(2 * quat, mx.GetQ());

            mx.SetS(scale * 2);
            FbxVector4Test.AssertSimilarXYZ(2 * scale, mx.GetS());

            mx.SetTRS(translate, euler, scale);
            FbxVector4Test.AssertSimilarXYZ(translate, mx.GetT());

            mx.SetTQS(2 * translate, 2 * quat, 2 * scale);
            FbxVector4Test.AssertSimilarXYZ(2 * translate, mx.GetT());

            // Test Inverse.
            var mxInv = mx.Inverse();
            Assert.AreNotEqual(mx.GetT(), mxInv.GetT());
            Assert.IsTrue((mx * mxInv).IsIdentity());

            // Test multiplying by a translation. Really we just want to make sure we got a result
            // different than doing nothing.
            FbxVector4Test.AssertSimilarXYZ(new FbxVector4(17.778175, 2.464466, 4), mx.MultT(new FbxVector4(1,2,3)), 1e-5);

            // Test multiplying by a rotation.
            FbxVector4Test.AssertSimilarEuler(new FbxVector4(-180, 0, 45), mx.MultR(new FbxVector4(0, -90, 0)));
            quat.ComposeSphericalXYZ(new FbxVector4(0, -90, 0));
            quat = mx.MultQ(quat);
            var quatExpected = new FbxQuaternion();
            quatExpected.ComposeSphericalXYZ(new FbxVector4(-180, 0, 45));
            FbxQuaternionTest.AssertSimilar(quatExpected, quat);

            // Test multiplying a scale.
            FbxVector4Test.AssertSimilarXYZ(new FbxVector4(4, 6, .5), mx.MultS(new FbxVector4(2, 1.5, .5)));

            // Test scaling. Multiply/divide by powers of two so there's no roundoff.
            // The scale/rotate is scaled, the translation is cleared to (0,0,0,1).
            AssertScaled(mx, mx * 2, 2);
            AssertScaled(mx, 2 * mx, 2);
            AssertScaled(mx, mx / 2, 0.5);

            // Test negating. This is different from scaling by -1.
            using (var mxNegated = -mx) {
                for(int y = 0; y < 4; ++y) {
                    for(int x = 0; x < 4; ++x) {
                        Assert.AreEqual(-mx.Get(x, y), mxNegated.Get(x, y),
                                string.Format("Index {0} {1}", x, y));
                    }
                }
            }

            // Test transpose.
            using (var mxTranspose = mx.Transpose()) {
                for(int y = 0; y < 4; ++y) {
                    for(int x = 0; x < 4; ++x) {
                        Assert.AreEqual(mx.Get(y, x), mxTranspose.Get(x, y),
                                string.Format("Index {0} {1}", x, y));
                    }
                }
            }

            // Test setting to identity.
            mx.SetIdentity();
            Assert.IsTrue(mx.IsIdentity());

            // Slerp between two rotation matrices.
            var q1 = new FbxQuaternion(); q1.ComposeSphericalXYZ(new FbxVector4(0, -90, 0));
            var q2 = new FbxQuaternion(); q2.ComposeSphericalXYZ(new FbxVector4(0,  90, 0));

            var m1 = new FbxAMatrix(); m1.SetQ(q1);
            var m2 = new FbxAMatrix(); m2.SetQ(q2);


            var m12 = m1.Slerp(m2, 0.25);
            var q12 = new FbxQuaternion(); q12.ComposeSphericalXYZ(new FbxVector4(0, -45, 0));
            FbxQuaternionTest.AssertSimilar(q12, m12.GetQ());
        }
    }
}
