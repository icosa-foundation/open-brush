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
    internal class FbxNodeAttributeBase<T> : Base<T> where T : FbxNodeAttribute
    {
        virtual public void TestBasics(T attr, FbxNodeAttribute.EType typ)
        {
            Assert.AreEqual(typ, attr.GetAttributeType());
            Assert.AreEqual(attr.Color, attr.FindProperty(FbxNodeAttribute.sColor));
            TestGetter(FbxNodeAttribute.sDefaultColor);
            Assert.AreEqual(0, attr.GetNodeCount());

            var node1 = FbxNode.Create(Manager, "node1");
            var node2 = FbxNode.Create(Manager, "node2");
            node1.SetNodeAttribute(attr);
            node2.SetNodeAttribute(attr);
            Assert.AreEqual(2, attr.GetNodeCount());
            Assert.AreEqual(node1, attr.GetNode());
            Assert.AreEqual(node2, attr.GetNode(1));
        }
    }

    internal class FbxNodeAttributeTest : FbxNodeAttributeBase<FbxNodeAttribute>
    {
        [Test]
        public void TestBasics() {
            TestBasics(CreateObject(), FbxNodeAttribute.EType.eUnknown);
        }
    }
}
