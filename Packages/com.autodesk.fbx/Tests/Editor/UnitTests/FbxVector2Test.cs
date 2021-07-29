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
    internal class FbxVector2Test
    {
        [Test]
        public void TestEquality()
        {
            EqualityTester<FbxVector2>.TestEquality(
                    new FbxVector2(0, 1),
                    new FbxVector2(3, 2),
                    new FbxVector2(0, 1));
        }

        [Test]
        public void BasicTests ()
        {
            FbxVector2 v;

            // make sure the no-arg constructor doesn't crash
            new FbxVector2();

            // Test other constructors
            v = new FbxVector2(5);
            Assert.AreEqual(5, v.X);
            Assert.AreEqual(5, v.Y);

            v = new FbxVector2(1, 2);
            var u = new FbxVector2(v);
            Assert.AreEqual(v, u);
            u[0] = 5;
            Assert.AreEqual(5, u[0]);
            Assert.AreEqual(1, v[0]); // check that setting u doesn't set v
            Assert.AreEqual(1, v.X);
            Assert.AreEqual(2, v.Y);

            var d2 = new FbxDouble2(5, 6);
            v = new FbxVector2(d2);
            Assert.AreEqual(5, v.X);
            Assert.AreEqual(6, v.Y);

            // Test operator[]
            v = new FbxVector2();
            v[0] = 1;
            Assert.AreEqual(1, v[0]);
            v[1] = 2;
            Assert.AreEqual(2, v[1]);
            Assert.That(() => v[-1], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 2], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[-1] = 5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 2] = 5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());

            // Test that we can scale by a scalar.
            // This isn't covered below because this isn't legal in C++
            // (at least in FBX SDK 2017.1)
            u = 5 * v;
            Assert.AreEqual(5 * v.X, u.X);
            Assert.AreEqual(5 * v.Y, u.Y);
        }

        ///////////////////////////////////////////////////////////////////////////
        // Test that our results match the C++.
        ///////////////////////////////////////////////////////////////////////////

        static FbxVector2 Vector(double d) { return new FbxVector2(d,d); }
        static FbxVector2 Vector(double[] d) {
            return d.Length == 1 ? Vector(d[0]) : new FbxVector2(d[0], d[1]);
        }

        static Dictionary<string, CppMatchingHelper.TestCommand<FbxVector2>> s_commands = new Dictionary<string, CppMatchingHelper.TestCommand<FbxVector2>> {
            { "-a", (FbxVector2 a, FbxVector2 b) => { return -a; } },
            { "a + 2", (FbxVector2 a, FbxVector2 b) => { return a + 2; } },
            { "a - 2", (FbxVector2 a, FbxVector2 b) => { return a - 2; } },
            { "a * 2", (FbxVector2 a, FbxVector2 b) => { return a * 2; } },
            { "a / 2", (FbxVector2 a, FbxVector2 b) => { return a / 2; } },
            { "a + b", (FbxVector2 a, FbxVector2 b) => { return a + b; } },
            { "a - b", (FbxVector2 a, FbxVector2 b) => { return a - b; } },
            { "a * b", (FbxVector2 a, FbxVector2 b) => { return a * b; } },
            { "a / b", (FbxVector2 a, FbxVector2 b) => { return a / b; } },
            { "a.Length()", (FbxVector2 a, FbxVector2 b) => { return Vector(a.Length()); } },
            { "a.SquareLength()", (FbxVector2 a, FbxVector2 b) => { return Vector(a.SquareLength()); } },
            { "a.DotProduct(b)", (FbxVector2 a, FbxVector2 b) => { return Vector(a.DotProduct(b)); } },
            { "a.Distance(b)", (FbxVector2 a, FbxVector2 b) => { return Vector(a.Distance(b)); } },
        };

        static Dictionary<string, CppMatchingHelper.AreSimilar<FbxVector2>> s_custom_compare = new Dictionary<string, CppMatchingHelper.AreSimilar<FbxVector2>> {
            { "a.Length()", (FbxVector2 a, FbxVector2 b) => { Assert.AreEqual(a.X, b.X, 1e-8); return true; } },
            { "a.Distance(b)", (FbxVector2 a, FbxVector2 b) => { Assert.AreEqual(a.X, b.X, 1e-8); return true; } },
        };

        [Ignore("Fails if imported from a package because of Vector.cpp dependency")]
        [Test]
        public void MatchingTests ()
        {
            CppMatchingHelper.MatchingTest<FbxVector2>(
                    "vector_test.txt",
                    "FbxVector2",
                    Vector,
                    s_commands,
                    s_custom_compare);
        }
    }
}
