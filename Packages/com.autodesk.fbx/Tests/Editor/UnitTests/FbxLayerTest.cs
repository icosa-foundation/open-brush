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
    internal class FbxLayerTest : TestBase<FbxLayer>
    {

        private FbxMesh m_fbxMesh;
        private FbxManager m_fbxManager;
        private FbxLayer m_fbxLayer;

        [SetUp]
        public void Init ()
        {
            m_fbxManager = FbxManager.Create ();
            m_fbxMesh = FbxMesh.Create (m_fbxManager, "");
            m_fbxLayer = m_fbxMesh.GetLayer (0);
            if (m_fbxLayer == null)
            {
                m_fbxMesh.CreateLayer ();
                m_fbxLayer = m_fbxMesh.GetLayer (0 /* default layer */);
            }
        }

        [TearDown]
        public void Term ()
        {
            m_fbxManager.Destroy ();
        }

        [Test]
        public void TestSetNormals ()
        {
            // make sure nothing crashes

            m_fbxLayer.SetNormals (FbxLayerElementNormal.Create (m_fbxMesh, ""));
            Assert.IsNotNull (m_fbxLayer.GetNormals ());

            // test null
            m_fbxLayer.SetNormals(null);
            Assert.IsNull (m_fbxLayer.GetNormals ());

            // test destroyed
            FbxLayerElementNormal normals = FbxLayerElementNormal.Create (m_fbxMesh, "");
            normals.Dispose ();
            m_fbxLayer.SetNormals (normals);
        }

        [Test]
        public void TestSetBinormals ()
        {
            // make sure nothing crashes

            m_fbxLayer.SetBinormals (FbxLayerElementBinormal.Create (m_fbxMesh, ""));
            Assert.IsNotNull (m_fbxLayer.GetBinormals ());

            // test null
            m_fbxLayer.SetBinormals(null);
            Assert.IsNull (m_fbxLayer.GetBinormals ());

            // test destroyed
            FbxLayerElementBinormal binormals = FbxLayerElementBinormal.Create (m_fbxMesh, "");
            binormals.Dispose ();
            m_fbxLayer.SetBinormals (binormals);
        }

        [Test]
        public void TestSetTangents ()
        {
            // make sure nothing crashes

            m_fbxLayer.SetTangents (FbxLayerElementTangent.Create (m_fbxMesh, ""));
            Assert.IsNotNull (m_fbxLayer.GetTangents ());

            // test null
            m_fbxLayer.SetTangents(null);
            Assert.IsNull (m_fbxLayer.GetTangents ());

            // test destroyed
            FbxLayerElementTangent tangents = FbxLayerElementTangent.Create (m_fbxMesh, "");
            tangents.Dispose ();
            m_fbxLayer.SetTangents (tangents);
        }

        [Test]
        public void TestSetVertexColors ()
        {
            // make sure nothing crashes

            m_fbxLayer.SetVertexColors (FbxLayerElementVertexColor.Create (m_fbxMesh, ""));
            Assert.IsNotNull (m_fbxLayer.GetVertexColors ());

            // test null
            m_fbxLayer.SetVertexColors(null);
            Assert.IsNull (m_fbxLayer.GetVertexColors ());

            // test destroyed
            FbxLayerElementVertexColor vertexColor = FbxLayerElementVertexColor.Create (m_fbxMesh, "");
            vertexColor.Dispose ();
            m_fbxLayer.SetVertexColors(vertexColor);
        }

        [Test]
        public void TestSetMaterials()
        {
            // make sure nothing crashes

            m_fbxLayer.SetMaterials(FbxLayerElementMaterial.Create (m_fbxMesh, ""));
            Assert.IsNotNull (m_fbxLayer.GetMaterials ());

            // test null
            m_fbxLayer.SetMaterials(null);
            Assert.IsNull (m_fbxLayer.GetMaterials ());

            // test destroyed
            FbxLayerElementMaterial material = FbxLayerElementMaterial.Create (m_fbxMesh, "");
            material.Dispose ();
            m_fbxLayer.SetMaterials(material);
        }

        [Test]
        public void TestSetUVs ()
        {
            // make sure nothing crashes

            m_fbxLayer.SetUVs (FbxLayerElementUV.Create (m_fbxMesh, ""));

            // test with type identifier
            m_fbxLayer.SetUVs(FbxLayerElementUV.Create (m_fbxMesh, ""), FbxLayerElement.EType.eEdgeCrease);
            // TODO: why does this return null?
            Assert.IsNull(m_fbxLayer.GetUVs(FbxLayerElement.EType.eEdgeCrease));

            // test null
            m_fbxLayer.SetUVs(null);
            Assert.IsNull (m_fbxLayer.GetUVs ());

            // test destroyed
            FbxLayerElementUV uvs = FbxLayerElementUV.Create (m_fbxMesh, "");
            uvs.Dispose ();
            m_fbxLayer.SetUVs (uvs);
        }

        [Test]
        public void TestDispose()
        {
            // make sure that calling SetNormals on a disposed layer throws
            m_fbxLayer.Dispose ();
            Assert.That(() => m_fbxLayer.SetNormals (FbxLayerElementNormal.Create(m_fbxMesh, "")),
                Throws.Exception.TypeOf<System.ArgumentNullException>());
        }

        /* Test all the equality functions we can find. */
        [Test]
        public void TestEquality() {
            var aIndex = m_fbxMesh.CreateLayer();
            var bIndex = m_fbxMesh.CreateLayer();
            var a = m_fbxMesh.GetLayer(aIndex);
            var b = m_fbxMesh.GetLayer(bIndex);
            var acopy = m_fbxMesh.GetLayer(aIndex);
            EqualityTester<FbxLayer>.TestEquality(a, b, acopy);
        }
    }
}
