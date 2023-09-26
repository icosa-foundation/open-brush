// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxStatusTest
    {
        [Test]
        public void TestBasics()
        {
            // test constructor
            FbxStatus status = new FbxStatus ();
            Assert.IsNotNull (status);

            // test dispose
            status.Dispose ();
            using (new FbxStatus ()) {}

            // test comparing code and status
            status = new FbxStatus(FbxStatus.EStatusCode.eIndexOutOfRange);
            Assert.AreEqual(FbxStatus.EStatusCode.eIndexOutOfRange, status.GetCode());
            Assert.IsTrue(FbxStatus.EStatusCode.eIndexOutOfRange == status);
            Assert.IsTrue(status == FbxStatus.EStatusCode.eIndexOutOfRange);
            Assert.IsTrue(FbxStatus.EStatusCode.eInvalidParameter != status);
            Assert.IsTrue(status != FbxStatus.EStatusCode.eInvalidParameter);

            // test copy ctor and clear (it only modifies status2, not status)
            var status2 = new FbxStatus(status);
            status2.Clear();
            Assert.IsTrue(status.Error());
            Assert.IsFalse(status2.Error());

            // test SetCode
            status2.SetCode(FbxStatus.EStatusCode.eIndexOutOfRange);
            Assert.AreEqual(status, status2);
            status2.SetCode(FbxStatus.EStatusCode.eInvalidParameter, "wrong");
            Assert.AreEqual("wrong", status2.GetErrorString());

            // test equality
            EqualityTester<FbxStatus>.TestEquality(status, status2, new FbxStatus(status));
        }
    }
}
