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
    internal class FbxDouble4x4TestBase<T> : TestBase<T> where T: FbxDouble4x4
    {
        /// <summary>
        /// Test element access and Dispose().
        /// The 'mx' matrix is invalid after this.
        /// </summary>
        protected void TestElementAccessAndDispose(T mx)
        {
            var a = new FbxDouble4(1,2,3,4);
            var b = new FbxDouble4(5,6,7,8);
            var c = new FbxDouble4(9,8,7,6);
            var d = new FbxDouble4(5,4,3,2);

            mx.X = d;
            mx.Y = c;
            mx.Z = b;
            mx.W = a;
            Assert.AreEqual(d, mx.X);
            Assert.AreEqual(c, mx.Y);
            Assert.AreEqual(b, mx.Z);
            Assert.AreEqual(a, mx.W);

            mx[0] = a;
            mx[1] = b;
            mx[2] = c;
            mx[3] = d;
            Assert.AreEqual(a, mx[0]);
            Assert.AreEqual(b, mx[1]);
            Assert.AreEqual(c, mx[2]);
            Assert.AreEqual(d, mx[3]);
            Assert.That(() => mx[-1], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => mx[ 4], Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => mx[-1] = a, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            Assert.That(() => mx[ 4] = a, Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());

            mx.Dispose();
        }
    }

    internal class FbxDouble4x4Test : FbxDouble4x4TestBase<FbxDouble4x4>
    {
        [Test]
        public void TestEquality()
        {
            var a = new FbxDouble4(1,2,3,4);
            var b = new FbxDouble4(5,6,7,8);
            var c = new FbxDouble4(9,8,7,6);
            var d = new FbxDouble4(5,4,3,2);
            EqualityTester<FbxDouble4x4>.TestEquality(
                    new FbxDouble4x4(a, b, c, d),
                    new FbxDouble4x4(d, c, b, a),
                    new FbxDouble4x4(a, b, c, d));
        }

        /// <summary>
        /// Test the basics. Subclasses should override and add some calls
        /// e.g. to excercise all the constructors.
        /// </summary>
        [Test]
        public void TestBasics()
        {
            FbxDouble4x4 v;

            // We use these later.
            var a = new FbxDouble4(1,2,3,4);
            var b = new FbxDouble4(5,6,7,8);
            var c = new FbxDouble4(9,8,7,6);
            var d = new FbxDouble4(5,4,3,2);

            // make sure the no-arg constructor doesn't crash
            new FbxDouble4x4();

            // make sure we can dispose
            using (new FbxDouble4x4()) { }
            new FbxDouble4x4().Dispose();

            // Test that we can get elements and we can dispose.
            // Also tests the 4-arg constructor.
            base.TestElementAccessAndDispose(new FbxDouble4x4());

            // Test copy constructor
            v = new FbxDouble4x4(a,b,c,d);
            var u = new FbxDouble4x4(v);
            Assert.AreEqual(v, u);
            u[0] = c;
            Assert.AreEqual(c, u[0]);
            Assert.AreEqual(a, v[0]); // check that setting u doesn't set v

            // Test one-element constructor.
            v = new FbxDouble4x4(c);
            Assert.AreEqual(c, v[0]);
            Assert.AreEqual(c, v[1]);
            Assert.AreEqual(c, v[2]);
            Assert.AreEqual(c, v[3]);
        }
    }
}
