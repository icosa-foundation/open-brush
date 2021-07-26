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
    internal class FbxMatrixTest : FbxDouble4x4TestBase<FbxMatrix>
    {

        public static bool AssertIsIdentity(FbxMatrix mx,
                double tolerance = 1e-10, bool nothrow = false)
        {
            using (var id = new FbxMatrix()) {
                return AssertSimilar(id, mx, tolerance, nothrow);
            }
        }

        public static bool AssertSimilar(FbxMatrix expected, FbxMatrix actual,
                double tolerance = 1e-10, bool nothrow = false)
        {
            for(int y = 0; y < 4; ++y) {
                for(int x = 0; x < 4; ++x) {
                    if (System.Math.Abs(expected.Get(x, y) - actual.Get(x, y)) >= tolerance) {
                        if (!nothrow) {
                            Assert.AreEqual(expected, actual, string.Format("Index {0} {1}", x, y));
                        }
                        return false;
                    }
                }
            }
            return true;
        }

        [Test]
        public void TestEquality()
        {
            var zero = new FbxVector4();
            var one = new FbxVector4(1,1,1);
            var mx1 = new FbxMatrix(zero, zero, one);
            var mx2 = new FbxMatrix(one, zero, one);
            // Check that equality is value equality, not reference equality.
            var mx1copy = new FbxMatrix(zero, zero, one);
            EqualityTester<FbxMatrix>.TestEquality(mx1, mx2, mx1copy);

            // Check that we can compare with an affine matrix.
            mx1 = new FbxMatrix(new FbxVector4(1, 2, 3), new FbxVector4(0, -90, 0), one);
            var affine = new FbxAMatrix(new FbxVector4(1, 2, 3), new FbxVector4(0, -90, 0), one);
            Assert.IsTrue(mx1 == affine);
        }

        [Test]
        public void BasicTests ()
        {
            base.TestElementAccessAndDispose(new FbxMatrix());

            FbxMatrix mx;

            // make sure the constructors compile and don't crash
            mx = new FbxMatrix();
            mx = new FbxMatrix(new FbxMatrix());
            mx = new FbxMatrix(new FbxAMatrix());
            mx = new FbxMatrix(new FbxVector4(), new FbxVector4(), new FbxVector4(1,1,1));
            mx = new FbxMatrix(new FbxVector4(), new FbxQuaternion(), new FbxVector4(1,1,1));
            mx = new FbxMatrix(0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15);

            /* Check the values we typed in match up. */
            for(int y = 0; y < 4; ++y) {
                for(int x = 0; x < 4; ++x) {
                    Assert.AreEqual(x + 4 * y, mx.Get(y, x));
                }
            }
            Assert.AreEqual(new FbxVector4(4, 5, 6, 7), mx.GetRow(1));
            Assert.AreEqual(new FbxVector4(1, 5, 9, 13), mx.GetColumn(1));

            /* Check that set and get work (silly transpose operation). */
            FbxMatrix mx2 = new FbxMatrix();
            for(int y = 0; y < 4; ++y) {
                for(int x = 0; x < 4; ++x) {
                    mx2.Set(y, x, y + 4 * x);
                    Assert.AreEqual(mx.Get(x, y), mx2.Get(y, x));
                }
            }

            /* normal transpose operation */
            Assert.AreEqual(mx, mx2.Transpose());

            // Test SetIdentity
            Assert.IsFalse(AssertIsIdentity(mx, nothrow: true));
            AssertIsIdentity(mx, 15); // squint very, very, very hard
            mx.SetIdentity();
            AssertIsIdentity(mx);

            // Test getting the elements from a matrix built by TRS
            var translate = new FbxVector4(1, 2, 3);
            var rotate = new FbxVector4(0, 90, 0);
            var scale = new FbxVector4(1, 2, .5);
            mx = new FbxMatrix(translate, rotate, scale);
            FbxVector4 t,r,s, shear;
            double sign;
            mx.GetElements(out t, out r, out shear, out s, out sign);
            Assert.AreEqual(1, sign);
            FbxVector4Test.AssertSimilarXYZ(translate, t);
            FbxVector4Test.AssertSimilarEuler(rotate, r);
            FbxVector4Test.AssertSimilarXYZ(new FbxVector4(), shear);
            FbxVector4Test.AssertSimilarXYZ(scale, s);

            FbxQuaternion q = new FbxQuaternion();
            mx.GetElements(out r, q, out shear, out s, out sign);
            Assert.AreEqual(1, sign);
            FbxVector4Test.AssertSimilarXYZ(translate, t);
            FbxQuaternionTest.AssertSimilar(rotate, q);
            FbxVector4Test.AssertSimilarXYZ(new FbxVector4(), shear);
            FbxVector4Test.AssertSimilarXYZ(scale, s);

            // Try SetTRS and SetTQS with the same arguments.
            using (var X = new FbxMatrix()) {
                X.SetTRS(translate, rotate, scale);
                X.GetElements(out r, q, out shear, out s, out sign);
                Assert.AreEqual(1, sign);
                FbxVector4Test.AssertSimilarXYZ(translate, t);
                FbxQuaternionTest.AssertSimilar(rotate, q);
                FbxVector4Test.AssertSimilarXYZ(new FbxVector4(), shear);
                FbxVector4Test.AssertSimilarXYZ(scale, s);
            }

            using (var X = new FbxMatrix()) {
                FbxQuaternion qRotate = new FbxQuaternion();
                qRotate.ComposeSphericalXYZ(rotate);
                X.SetTQS(translate, q, scale);
                X.GetElements(out r, q, out shear, out s, out sign);
                Assert.AreEqual(1, sign);
                FbxVector4Test.AssertSimilarXYZ(translate, t);
                FbxQuaternionTest.AssertSimilar(rotate, q);
                FbxVector4Test.AssertSimilarXYZ(new FbxVector4(), shear);
                FbxVector4Test.AssertSimilarXYZ(scale, s);

                // While we're at it, transform a vertex.
                // Verify also that w turns out normalized.
                var v = new FbxVector4(1, 2, 3, 4);
                var v2 = X.MultNormalize(v);
                FbxVector4Test.AssertSimilarXYZW(new FbxVector4(2.5,6,2,1), v2);

                // While we're at it, test that we can invert the matrix.
                // This matrix is invertible (since it's an affine transformation),
                // and the inversion turns out to be exact.
                AssertIsIdentity(X.Inverse() * X);
                using (var inv = new FbxMatrix(
                            0, 0, 2, 0,
                            0, 0.5, 0, 0,
                            -1, 0, 0, 0,
                            3, -1, -2, 1)) {
                    Assert.AreEqual(inv, X.Inverse());
                }
            }

            // Test set column + set row
            mx = new FbxMatrix();
            mx.SetColumn (1, new FbxVector4 (1, 2, 3, 4));
            mx.SetRow (2, new FbxVector4 (5, 6, 7, 8));
            //check that the column is what we expect
            Assert.AreEqual (1, mx.Get (0, 1));
            Assert.AreEqual (2, mx.Get (1, 1));
            Assert.AreEqual (6, mx.Get (2, 1)); // this value got changed by SetRow
            Assert.AreEqual (4, mx.Get (3, 1));
            // check that the row is what we expect
            Assert.AreEqual (new FbxDouble4 (5, 6, 7, 8), mx [2]);

            // Test operators on two matrices.
            using (var a = new FbxMatrix(
                        0,1,2,3,
                        4,5,6,7,
                        8,9,10,11,
                        12,13,14,15)) {
                using (var b = new FbxMatrix(
                            15,14,13,12,
                            11,10,9,8,
                            7,6,5,4,
                            3,2,1,0)) {
                    using (var sum = new FbxMatrix(
                                15,15,15,15,
                                15,15,15,15,
                                15,15,15,15,
                                15,15,15,15)) {
                        Assert.AreEqual(sum, a + b);
                    }
                    using (var diff = new FbxMatrix(
                                -15,-13,-11,-9,
                                -7,-5,-3,-1,
                                1,3,5,7,
                                9,11,13,15)) {
                        Assert.AreEqual(diff, a - b);
                    }
                    using (var prod = new FbxMatrix(
                                304,358,412,466,
                                208,246,284,322,
                                112,134,156,178,
                                16,22,28,34)) {
                        Assert.AreEqual(prod, a * b);
                    }
                    using (var neg = new FbxMatrix(
                        0,-1,-2,-3,
                        -4,-5,-6,-7,
                        -8,-9,-10,-11,
                        -12,-13,-14,-15)) {
                        Assert.AreEqual(neg, -a);
                    }
                }
            }

            var eyePosition = new FbxVector4(1, 2, 3);
            var eyeDirection = new FbxVector4(-1, -1, -1);
            var eyeUp = new FbxVector4(0, 1, 0);

            using (mx = new FbxMatrix()) {
                mx.SetLookToRH(eyePosition, eyeDirection, eyeUp);
                AssertSimilar(new FbxMatrix(
                            0.707 , -0.408, 0.577, 0,
                            0     ,  0.816, 0.577, 0,
                            -0.707, -0.408, 0.577, 0,
                            1.414 ,  0    ,-3.464, 1), mx, 1e-2);

                mx.SetLookToLH(eyePosition, eyeDirection, eyeUp);
                AssertSimilar(new FbxMatrix(
                            -0.707, -0.408,-0.577, 0,
                            0     ,  0.816,-0.577, 0,
                            0.707 , -0.408,-0.577, 0,
                            -1.414,  0    , 3.464, 1), mx, 1e-2);

                mx.SetLookAtRH(eyePosition, eyeDirection, eyeUp);
                AssertSimilar(new FbxMatrix(
                            0.894 , -0.249, 0.371, 0,
                            0     ,  0.834, 0.557, 0,
                            -0.447, -0.498, 0.742, 0,
                            0.447 ,  0.083,-3.713, 1), mx, 1e-2);

                mx.SetLookAtLH(eyePosition, eyeDirection, eyeUp);
                AssertSimilar(new FbxMatrix(
                            -0.894, -0.249,-0.371, 0,
                            0     ,  0.834,-0.557, 0,
                            0.447 , -0.498,-0.742, 0,
                            -0.447,  0.083, 3.713, 1), mx, 1e-2);
            }
        }
    }
}
