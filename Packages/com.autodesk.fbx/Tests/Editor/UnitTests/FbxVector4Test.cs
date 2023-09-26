// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************
using NUnit.Framework;
using Autodesk.Fbx;
using System.Collections.Generic;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxVector4Test
    {
        [Test]
        public void TestEquality()
        {
            EqualityTester<FbxVector4>.TestEquality(
                    new FbxVector4(0, 1, 2, 3),
                    new FbxVector4(3, 2, 1, 0),
                    new FbxVector4(0, 1, 2, 3));
        }

        /// <summary>
        /// Check that two vectors are similar, interpreting them as 4-tuples
        /// of doubles.
        ///
        /// Pass 'nothrow' as true if you want a bool yes/no. By default we
        /// throw an NUnit exception if the vectors don't match.
        /// </summary>
        public static bool AssertSimilarXYZW(FbxVector4 expected, FbxVector4 actual,
                double tolerance = 1e-10, bool nothrow = false)
        {
            if (System.Math.Abs(expected.X - actual.X) <= tolerance &&
                    System.Math.Abs(expected.Y - actual.Y) <= tolerance &&
                    System.Math.Abs(expected.Z - actual.Z) <= tolerance &&
                    System.Math.Abs(expected.Z - actual.Z) <= tolerance) {
                return true;
            }

            if (!nothrow) {
                Assert.AreEqual(expected, actual);
            }
            return false;
        }

        /// <summary>
        /// Check that two vectors are similar, interpreting them as XYZ
        /// vectors (ignoring W).
        ///
        /// Pass 'nothrow' as true if you want a bool yes/no. By default we
        /// throw an NUnit exception if the vectors don't match.
        /// </summary>
        public static bool AssertSimilarXYZ(FbxVector4 expected, FbxVector4 actual,
                double tolerance = 1e-10, bool nothrow = false)
        {
            if (System.Math.Abs(expected.X - actual.X) <= tolerance &&
                    System.Math.Abs(expected.Y - actual.Y) <= tolerance &&
                    System.Math.Abs(expected.Z - actual.Z) <= tolerance) {
                return true;
            }

            if (!nothrow) {
                Assert.AreEqual(expected, actual);
            }
            return false;
        }

        /// <summary>
        /// Check that two vectors are similar, interpreting them as XYZ euler angles,
        /// ignoring W.
        ///
        /// Pass 'nothrow' as true if you want a bool yes/no. By default we
        /// throw an NUnit exception if the vectors don't match.
        /// </summary>
        public static bool AssertSimilarEuler(FbxVector4 expected, FbxVector4 actual,
                double tolerance = 1e-10, bool nothrow = false)
        {
            if (expected == actual) {
                return true;
            }

            var q1 = new FbxQuaternion(); q1.ComposeSphericalXYZ(expected);
            var q2 = new FbxQuaternion(); q2.ComposeSphericalXYZ(actual);

            // Check if the quaternions match.
            if (FbxQuaternionTest.AssertSimilar(q1, q2, System.Math.Sqrt(tolerance), nothrow: true)) {
                return true;
            }

            if (!nothrow) {
                Assert.AreEqual(expected, actual, "Quaternions don't match: " + q1 + " versus " + q2);
            }
            return false;
        }

        [Test]
        public void BasicTests ()
        {
            FbxVector4 v;

            // make sure the no-arg constructor doesn't crash
            new FbxVector4();

            // Test other constructors
            v = new FbxVector4(1, 2, 3, 4);
            var u = new FbxVector4(v);
            Assert.AreEqual(v, u);
            u[0] = 5;
            Assert.AreEqual(5, u[0]);
            Assert.AreEqual(1, v[0]); // check that setting u doesn't set v

            v = new FbxVector4(1, 2, 3);
            Assert.AreEqual(1, v[3]); // w is assumed to be a homogenous coordinate
            v = new FbxVector4(new FbxDouble3(1, 2, 3));
            Assert.AreEqual(1, v[3]); // w is assumed to be a homogenous coordinate
            Assert.AreEqual(1, v[0]);
            Assert.AreEqual(2, v[1]);
            Assert.AreEqual(3, v[2]);

            // Test operator[]
            v = new FbxVector4();
            v[0] = 1;
            Assert.AreEqual(1, v[0]);
            v[1] = 2;
            Assert.AreEqual(2, v[1]);
            v[2] = 3;
            Assert.AreEqual(3, v[2]);
            v[3] = 4;
            Assert.AreEqual(4, v[3]);
            Assert.That(() => v[-1], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 4], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[-1] = 5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 4] = 5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());

            // Test 4-argument constructor and members X/Y/Z/W
            v = new FbxVector4(1, 2, 3, 4);
            Assert.AreEqual(1, v.X);
            Assert.AreEqual(2, v.Y);
            Assert.AreEqual(3, v.Z);
            Assert.AreEqual(4, v.W);
            v.X = 3;
            v.Y = 4;
            v.Z = 5;
            v.W = 6;
            Assert.AreEqual(3, v.X);
            Assert.AreEqual(4, v.Y);
            Assert.AreEqual(5, v.Z);
            Assert.AreEqual(6, v.W);

            // Test that we can scale by a scalar.
            // This isn't covered below because this isn't legal in C++
            // (at least in FBX SDK 2017.1)
            u = 5 * v;
            Assert.AreEqual(5 * v.X, u.X);
            Assert.AreEqual(5 * v.Y, u.Y);
            Assert.AreEqual(5 * v.Z, u.Z);
            Assert.AreEqual(5 * v.W, u.W);
        }

        ///////////////////////////////////////////////////////////////////////////
        // Test that our results match the C++.
        ///////////////////////////////////////////////////////////////////////////

        static FbxVector4 Vector(double d) { return new FbxVector4(d,d,d,d); }

        static FbxVector4 Vector(double [] d) {
            if (d.Length == 1) { return Vector(d[0]); }
            else {
                Assert.AreEqual(4, d.Length);
                return new FbxVector4(d[0],d[1],d[2],d[3]);
            }
        }

        static Dictionary<string, CppMatchingHelper.TestCommand<FbxVector4>> s_commands = new Dictionary<string, CppMatchingHelper.TestCommand<FbxVector4>> {
            { "-a", (FbxVector4 a, FbxVector4 b) => { return -a; } },
            { "a + 2", (FbxVector4 a, FbxVector4 b) => { return a + 2; } },
            { "a - 2", (FbxVector4 a, FbxVector4 b) => { return a - 2; } },
            { "a * 2", (FbxVector4 a, FbxVector4 b) => { return a * 2; } },
            { "a / 2", (FbxVector4 a, FbxVector4 b) => { return a / 2; } },
            { "a + b", (FbxVector4 a, FbxVector4 b) => { return a + b; } },
            { "a - b", (FbxVector4 a, FbxVector4 b) => { return a - b; } },
            { "a * b", (FbxVector4 a, FbxVector4 b) => { return a * b; } },
            { "a / b", (FbxVector4 a, FbxVector4 b) => { return a / b; } },
            { "a.Length()", (FbxVector4 a, FbxVector4 b) => { return Vector(a.Length()); } },
            { "a.SquareLength()", (FbxVector4 a, FbxVector4 b) => { return Vector(a.SquareLength()); } },
            { "a.DotProduct(b)", (FbxVector4 a, FbxVector4 b) => { return Vector(a.DotProduct(b)); } },
            { "a.CrossProduct(b)", (FbxVector4 a, FbxVector4 b) => { return a.CrossProduct(b); } },
            { "a.Distance(b)", (FbxVector4 a, FbxVector4 b) => { return Vector(a.Distance(b)); } },
        };

        static bool ApproximatelyEqualX(FbxVector4 expected, FbxVector4 actual) {
            Assert.AreEqual(expected.X, actual.X, 1e-8);
            return System.Math.Abs(expected.X - actual.X) < 1e-8;
        }

        static Dictionary<string, CppMatchingHelper.AreSimilar<FbxVector4>> s_custom_compare = new Dictionary<string, CppMatchingHelper.AreSimilar<FbxVector4>> {
            { "a.Length()", ApproximatelyEqualX },
            { "a.Distance(b)", ApproximatelyEqualX }
        };

        [Ignore("Fails if imported from a package because of Vector.cpp dependency")]
        [Test]
        public void MatchingTests ()
        {
            CppMatchingHelper.MatchingTest<FbxVector4>(
                    "vector_test.txt",
                    "FbxVector4",
                    Vector,
                    s_commands,
                    s_custom_compare);
        }
    }
}
