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
    internal class FbxNullTest : FbxNodeAttributeBase<FbxNull>
    {
        [Test]
        public void TestBasics() {
            var thenull = CreateObject();
            base.TestBasics(thenull, FbxNodeAttribute.EType.eNull);

            Assert.AreEqual(100.0, thenull.GetSizeDefaultValue());
            TestGetter(FbxNull.sSize);
            TestGetter(FbxNull.sLook);
            Assert.AreEqual(thenull.Size, thenull.FindProperty(FbxNull.sSize));
            Assert.AreEqual(thenull.Look, thenull.FindProperty(FbxNull.sLook));

            thenull.Size.Set(7);
            thenull.Reset();
            Assert.AreEqual(FbxNull.sDefaultSize, thenull.Size.Get());
            Assert.AreEqual(FbxNull.sDefaultLook, thenull.Look.Get());
        }
    }
}
