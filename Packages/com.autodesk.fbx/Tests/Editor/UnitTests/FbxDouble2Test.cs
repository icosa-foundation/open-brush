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
    /// <summary>
    /// Run some tests that any vector type should be able to pass.
    /// If you add tests here, you probably want to add them to the other
    /// FbxDouble* test classes.
    /// </summary>
    internal class FbxDouble2Test
    {
        [Test]
        public void TestEquality()
        {
            EqualityTester<FbxDouble2>.TestEquality(
                    new FbxDouble2(0, 1),
                    new FbxDouble2(1, 0),
                    new FbxDouble2(0, 1));
        }

        /// <summary>
        /// Test the basics.
        /// </summary>
        [Test]
        public void TestBasics()
        {
            FbxDouble2 v;

            // make sure the no-arg constructor doesn't crash
            new FbxDouble2();

            // Test other constructors
            v = new FbxDouble2(1, 2);
            var u = new FbxDouble2(v);
            Assert.AreEqual(v, u);
            u[0] = 5;
            Assert.AreEqual(5, u[0]);
            Assert.AreEqual(1, v[0]); // check that setting u doesn't set v
            var w = new FbxDouble2(3);
            Assert.AreEqual(3, w[0]);
            Assert.AreEqual(3, w[1]);

            // Test operator[]
            v = new FbxDouble2();
            v[0] = 1;
            Assert.AreEqual(1, v[0]);
            v[1] = 2;
            Assert.AreEqual(2, v[1]);
            Assert.That(() => v[-1], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 2], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[-1] = 5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => v[ 2] = 5, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());

            // Test 2-argument constructor and members X/Y
            v = new FbxDouble2(1, 2);
            Assert.AreEqual(1, v.X);
            Assert.AreEqual(2, v.Y);
            v.X = 3;
            v.Y = 4;
            Assert.AreEqual(3, v.X);
            Assert.AreEqual(4, v.Y);
        }
    }
}
