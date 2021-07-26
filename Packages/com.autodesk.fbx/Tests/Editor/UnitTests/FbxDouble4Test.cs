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
    internal class FbxDouble4Test
    {
        [Test]
        public void TestEquality()
        {
            EqualityTester<FbxDouble4>.TestEquality(
                    new FbxDouble4(0, 1, 2, 3),
                    new FbxDouble4(3, 2, 1, 0),
                    new FbxDouble4(0, 1, 2, 3));
        }

        /// <summary>
        /// Test the basics. Subclasses should override and add some calls
        /// e.g. to excercise all the constructors.
        /// </summary>
        [Test]
        public void TestBasics()
        {
            FbxDouble4 v;

            // make sure the no-arg constructor doesn't crash
            new FbxDouble4();

            // Test other constructors
            v = new FbxDouble4(1, 2, 3, 4);
            var u = new FbxDouble4(v);
            Assert.AreEqual(v, u);
            u[0] = 5;
            Assert.AreEqual(5, u[0]);
            Assert.AreEqual(1, v[0]); // check that setting u doesn't set v
            var w = new FbxDouble4(3);
            Assert.AreEqual(3, w[0]);
            Assert.AreEqual(3, w[1]);
            Assert.AreEqual(3, w[2]);
            Assert.AreEqual(3, w[3]);

            // Test operator[]
            v = new FbxDouble4();
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
            v = new FbxDouble4(1, 2, 3, 4);
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

            v = new FbxDouble4(5);
            Assert.AreEqual(5, v.X);
            Assert.AreEqual(5, v.Y);
            Assert.AreEqual(5, v.Z);
            Assert.AreEqual(5, v.W);
        }
    }
}
