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
    internal class FbxDeformerTestBase<T> : Base<T> where T : FbxDeformer
    {
        virtual public void TestBasics(T deformer, FbxDeformer.EDeformerType type)
        {
            Assert.AreEqual (type, deformer.GetDeformerType ());
        }
    }

    internal class FbxDeformerTest : FbxDeformerTestBase<FbxDeformer> {
        [Test]
        public void TestBasics()
        {
            TestBasics (CreateObject (), FbxDeformer.EDeformerType.eUnknown);
        }
    }
}
