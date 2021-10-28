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
    internal class FbxColorTest
    {
        [Test]
        public void TestEquality()
        {
            EqualityTester<FbxColor>.TestEquality(
                    new FbxColor(0.0, 0.1, 0.2, 0.3),
                    new FbxColor(0.3, 0.2, 0.1, 0.0),
                    new FbxColor(0.0, 0.1, 0.2, 0.3));
        }

        /// <summary>
        /// Test the basics. Subclasses should override and add some calls
        /// e.g. to excercise all the constructors.
        /// </summary>
        [Test]
        public void TestBasics()
        {
            FbxColor c;
            c = new FbxColor(0.1, 0.2, 0.3, 0.5);
            Assert.AreEqual(0.1, c.mRed);
            Assert.AreEqual(0.2, c.mGreen);
            Assert.AreEqual(0.3, c.mBlue);
            Assert.AreEqual(0.5, c.mAlpha);

            c = new FbxColor(0.1, 0.2, 0.3);
            Assert.AreEqual(0.1, c.mRed);
            Assert.AreEqual(0.2, c.mGreen);
            Assert.AreEqual(0.3, c.mBlue);
            Assert.AreEqual(1.0, c.mAlpha);

            c = new FbxColor(new FbxDouble3(0.1, 0.2, 0.3), 0.5);
            Assert.AreEqual(0.1, c.mRed);
            Assert.AreEqual(0.2, c.mGreen);
            Assert.AreEqual(0.3, c.mBlue);
            Assert.AreEqual(0.5, c.mAlpha);

            c = new FbxColor(new FbxDouble4(0.1, 0.2, 0.3, 0.5));
            Assert.AreEqual(0.1, c.mRed);
            Assert.AreEqual(0.2, c.mGreen);
            Assert.AreEqual(0.3, c.mBlue);
            Assert.AreEqual(0.5, c.mAlpha);

            Assert.IsTrue(c.IsValid());
            c.mRed = -1;
            c.mGreen = 1e6;
            Assert.IsFalse(c.IsValid());

            c.Set(1, 2, 3, 5);
            Assert.AreEqual(1, c.mRed);
            Assert.AreEqual(2, c.mGreen);
            Assert.AreEqual(3, c.mBlue);
            Assert.AreEqual(5, c.mAlpha);
            Assert.AreEqual(1, c[0]);
            Assert.AreEqual(2, c[1]);
            Assert.AreEqual(3, c[2]);
            Assert.AreEqual(5, c[3]);
            Assert.IsFalse(c.IsValid());

            c[0] = 0.1;
            Assert.AreEqual(0.1, c[0]);
            c[1] = 0.2;
            Assert.AreEqual(0.2, c[1]);
            c[2] = 0.3;
            Assert.AreEqual(0.3, c[2]);
            c[3] = 0.5;
            Assert.AreEqual(0.5, c[3]);
        }
    }
}
