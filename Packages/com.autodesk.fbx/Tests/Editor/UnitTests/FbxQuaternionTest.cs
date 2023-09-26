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
    internal class FbxQuaternionTest
    {
        /// <summary>
        /// Check that two quaternions represent a similar rotation.
        ///
        /// Either they're equal (within tolerance) or they're exactly opposite.
        /// Note that a slerp will go opposite directions if they're opposite.
        ///
        /// If you want to use the boolean result, pass in 'nothrow' as true.
        /// Otherwise a failed comparision will throw an exception.
        /// </summary>
        public static bool AssertSimilar(FbxQuaternion expected, FbxQuaternion actual,
                double tolerance = 1e-10, bool nothrow = false)
        {
            // Are they bitwise equal?
            if (expected == actual) {
                return true;
            }

            // Compute the dot product. It'll be +1 or -1 if they're the same rotation.
            if (System.Math.Abs(expected.DotProduct(actual)) >= 1 - tolerance) {
                return true;
            }

            // Fail. Print it out nicely.
            if (!nothrow) { Assert.AreEqual(expected, actual); }
            return false;
        }

        public static bool AssertSimilar(FbxVector4 euler, FbxQuaternion actual,
                double tolerance = 1e-10, bool nothrow = false)
        {
            var expected = new FbxQuaternion();
            expected.ComposeSphericalXYZ(euler);
            return AssertSimilar(expected, actual, tolerance, nothrow);
        }

        [Test]
        public void TestEquality()
        {
            EqualityTester<FbxQuaternion>.TestEquality(
                    new FbxQuaternion(0.0, 0.1, 0.2, 0.3),
                    new FbxQuaternion(0.3, 0.2, 0.1, 0.0),
                    new FbxQuaternion(0.0, 0.1, 0.2, 0.3));
        }

        [Test]
        public void BasicTests ()
        {
            FbxQuaternion u, v;

            // make sure the no-arg constructor doesn't crash
            new FbxQuaternion();

            // test dispose
            using (new FbxQuaternion()) { }
            DisposeTester.TestDispose(new FbxQuaternion());

            // Test other constructors
            v = new FbxQuaternion(0.1, 0.2, 0.3, 0.4);
            u = new FbxQuaternion(v);
            Assert.AreEqual(v, u);
            u[0] = 0.5;
            Assert.AreEqual(0.5, u[0]);
            Assert.AreEqual(0.1, v[0]); // check that setting u doesn't set v

            // axis-angle constructor and setter
            v = new FbxQuaternion(new FbxVector4(1,2,3), 90);
            u = new FbxQuaternion();
            u.SetAxisAngle(new FbxVector4(1,2,3), 90);
            Assert.AreEqual(u, v);

            // euler
            v = new FbxQuaternion();
            v.ComposeSphericalXYZ(new FbxVector4(20, 30, 40));
            var euler = v.DecomposeSphericalXYZ();
            Assert.That(euler.X, Is.InRange(19.99, 20.01));
            Assert.That(euler.Y, Is.InRange(29.99, 30.01));
            Assert.That(euler.Z, Is.InRange(39.99, 40.01));
            Assert.AreEqual(0, euler.W);

            v = new FbxQuaternion(0.1, 0.2, 0.3);
            Assert.AreEqual(0.1, v[0]);
            Assert.AreEqual(0.2, v[1]);
            Assert.AreEqual(0.3, v[2]);
            Assert.AreEqual(1, v[3]); // w is assumed to be a homogenous coordinate

            v.Set(0.9, 0.8, 0.7, 0.6);
            Assert.AreEqual(0.9, v[0]);
            Assert.AreEqual(0.8, v[1]);
            Assert.AreEqual(0.7, v[2]);
            Assert.AreEqual(0.6, v[3]);
            v.Set(0.9, 0.8, 0.7);
            Assert.AreEqual(0.9, v[0]);
            Assert.AreEqual(0.8, v[1]);
            Assert.AreEqual(0.7, v[2]);

            v.SetAt(1, 2);
            Assert.AreEqual(2, v.GetAt(1));

            // Test operator[]
            v = new FbxQuaternion();
            v[0] = 0.1;
            Assert.AreEqual(0.1, v[0]);
            v[1] = 0.2;
            Assert.AreEqual(0.2, v[1]);
            v[2] = 0.3;
            Assert.AreEqual(0.3, v[2]);
            v[3] = 0.4;
            Assert.AreEqual(0.4, v[3]);
            v.SetAt(3, 0.5);
            Assert.AreEqual(0.5, v.GetAt(3));
            Assert.That(() => v[-1], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 4], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v.GetAt(-1), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v.GetAt( 4), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[-1] = 0.5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 4] = 0.5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v.SetAt(-1, 0.5), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v.SetAt( 4, 0.5), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());

            // Test W/X/Y/Z
            v.X = 0.1;
            Assert.AreEqual(0.1, v.X);
            v.Y = 0.2;
            Assert.AreEqual(0.2, v.Y);
            v.Z = 0.3;
            Assert.AreEqual(0.3, v.Z);
            v.W = 0.4;
            Assert.AreEqual(0.4, v.W);

            // call the multiply/divide/add/sub operators, make sure they're vaguely sane
            u = new FbxQuaternion(v);
            v = v * v;
            Assert.AreNotEqual(0, u.Compare(v, 1e-15)); // test compare can return false
            v = v * 9;
            v = 9 * v;
            v = v + 5;
            v = v - 5; // undo v + 5
            v = v + u;
            v = v - u; // undo v + u
            v = v / 81; // undo 9 * (v * 9)
            v = v / u; // undo v*v
            Assert.AreEqual(0, u.Compare(v)); // u and v are the same up to rounding
            Assert.AreEqual(u * u, u.Product(u));

            // unary negate and dot product
            Assert.AreEqual(0, (-u).Compare(-v));
            Assert.AreEqual(-0.3, v.DotProduct(-v), 1e-6);
            Assert.AreEqual(System.Math.Sqrt(0.3), v.Length(), 1e-6);
            v.Normalize();
            Assert.AreEqual(1, v.DotProduct(v), 1e-6);

            // various others where we assume that FBX works, just test that they don't crash
            v.Conjugate();
            v.Inverse();
            v.Slerp(u, 0.5);
        }
    }
}
