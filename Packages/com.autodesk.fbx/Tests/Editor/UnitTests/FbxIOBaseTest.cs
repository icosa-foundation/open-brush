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

    internal class FbxIOBaseTest<T> : Base<T> where T: FbxIOBase
    {
        [Test]
        public virtual void TestBasics()
        {
            using (var iobase = CreateObject()) { iobase.Initialize("/no/such/file.fbx"); }
            using (var iobase = CreateObject()) { iobase.Initialize("/no/such/file.fbx", -1); }
            using (var iobase = CreateObject()) { iobase.Initialize("/no/such/file.fbx", -1, FbxIOSettings.Create(Manager, "")); }
            using (var iobase = CreateObject()) { iobase.Initialize("/no/such/file.fbx", -1, null); }

            using (var iobase = CreateObject()) {
                Assert.IsFalse(iobase.GetStatus().Error());
                iobase.Initialize("/no/such/file.fbx");
                Assert.AreEqual("/no/such/file.fbx", iobase.GetFileName());
            }
        }
    }

    internal class FbxIOBaseTestClass : FbxIOBaseTest<FbxIOBase> { }
}
