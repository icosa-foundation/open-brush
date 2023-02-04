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
    internal class FbxLayerContainerBase<T> : FbxNodeAttributeBase<T> where T:FbxLayerContainer
    {
        override public void TestBasics(T layerContainer, FbxNodeAttribute.EType typ)
        {
            base.TestBasics(layerContainer, typ);

            int index = layerContainer.CreateLayer ();
            Assert.GreaterOrEqual (index, 0); // check an index is returned (-1 is error)

            // make sure doesn't crash and returns expected value
            Assert.IsNotNull (layerContainer.GetLayer (index));
            Assert.IsNull (layerContainer.GetLayer (int.MinValue));
            Assert.IsNull (layerContainer.GetLayer (int.MaxValue));
            Assert.AreEqual (layerContainer.GetLayerCount (), 1);
            Assert.AreEqual (layerContainer.GetLayerCount (FbxLayerElement.EType.eUnknown), 0);
            Assert.AreEqual (layerContainer.GetLayerCount (FbxLayerElement.EType.eUnknown, true), 0);

        }
    }

    internal class FbxLayerContainerTest : FbxLayerContainerBase<FbxLayerContainer>
    {
        [Test]
        public void TestBasics() {
            base.TestBasics(CreateObject(), FbxNodeAttribute.EType.eUnknown);
        }
    }
}
