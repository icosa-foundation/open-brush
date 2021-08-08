// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using NUnit.Framework;
using System.Collections;
using Autodesk.Fbx;
using UnityEngine.TestTools.Utils;

namespace Autodesk.Fbx.UnitTests
{
    internal class FbxMeshTest : FbxGeometryTestBase<FbxMesh>
    {
        [Test]
        public void TestBasics()
        {
            base.TestBasics(CreateObject("mesh"), FbxNodeAttribute.EType.eMesh);

            using (FbxMesh mesh = CreateObject ("mesh")) {
                int polyCount = 0;
                int polyVertexCount = 0;

                mesh.InitControlPoints(4);
                mesh.SetControlPointAt(new FbxVector4(0,0,0), 0);
                mesh.SetControlPointAt(new FbxVector4(1,0,0), 1);
                mesh.SetControlPointAt(new FbxVector4(1,0,1), 2);
                mesh.SetControlPointAt(new FbxVector4(0,0,1), 3);
                mesh.BeginPolygon();
                mesh.AddPolygon(0); polyVertexCount++;
                mesh.AddPolygon(1); polyVertexCount++;
                mesh.AddPolygon(2); polyVertexCount++;
                mesh.AddPolygon(3); polyVertexCount++;
                mesh.EndPolygon();
                polyCount++;

                // Link a poly to a material (even though we don't have any).
                mesh.BeginPolygon(0);
                mesh.AddPolygon(0); polyVertexCount++;
                mesh.AddPolygon(1); polyVertexCount++;
                mesh.AddPolygon(2); polyVertexCount++;
                mesh.AddPolygon(3); polyVertexCount++;
                mesh.EndPolygon();
                polyCount++;

                // Link a poly to a material and texture (even though we don't have any).
                mesh.BeginPolygon(0, 0);
                mesh.AddPolygon(0); polyVertexCount++;
                mesh.AddPolygon(1); polyVertexCount++;
                mesh.AddPolygon(2); polyVertexCount++;
                mesh.AddPolygon(3); polyVertexCount++;
                mesh.EndPolygon();
                polyCount++;

                // Create a group.
                mesh.BeginPolygon(-1, -1, 0);
                mesh.AddPolygon(0); polyVertexCount++;
                mesh.AddPolygon(1); polyVertexCount++;
                mesh.AddPolygon(2); polyVertexCount++;
                mesh.AddPolygon(3); polyVertexCount++;
                mesh.EndPolygon();
                polyCount++;

                // Create a non-legacy group polygon.
                mesh.BeginPolygon(-1, -1, 0, false);
                mesh.AddPolygon(0); polyVertexCount++;
                mesh.AddPolygon(1); polyVertexCount++;
                mesh.AddPolygon(2); polyVertexCount++;
                mesh.AddPolygon(3); polyVertexCount++;
                mesh.EndPolygon();
                polyCount++;

                // Create a polygon with UV indices (even though we don't have any)
                mesh.BeginPolygon(0);
                mesh.AddPolygon(0, 0);  polyVertexCount++;
                mesh.AddPolygon(1, 1);  polyVertexCount++;
                mesh.AddPolygon(2, 2);  polyVertexCount++;
                mesh.AddPolygon(3, 3);  polyVertexCount++;
                mesh.EndPolygon();
                polyCount++;

                Assert.AreEqual (mesh.GetPolygonCount (), polyCount);
                Assert.AreEqual (mesh.GetPolygonSize (polyCount - 1), 4);
                Assert.AreEqual (mesh.GetPolygonVertex (polyCount - 1, 0), 0);
                Assert.AreEqual ( mesh.GetPolygonVertexCount (), polyVertexCount);
                Assert.AreEqual (mesh.GetPolygonCount (), polyCount);
            }
        }

        [Test]
        public void TestGetPolygonVertexNormal() {
            using (FbxMesh mesh = CreateObject("mesh")) {
                mesh.InitControlPoints(4);
                mesh.SetControlPointAt(new FbxVector4(0, 0, 0), 0);
                mesh.SetControlPointAt(new FbxVector4(1, 0, 0), 1);
                mesh.SetControlPointAt(new FbxVector4(1, 0, 1), 2);
                mesh.SetControlPointAt(new FbxVector4(0, 0, 1), 3);

                mesh.BeginPolygon();
                mesh.AddPolygon(0);
                mesh.AddPolygon(1);
                mesh.AddPolygon(2);
                mesh.AddPolygon(3);
                mesh.EndPolygon();

                // Add normals to the mesh
                FbxVector4 normal0 = new FbxVector4(0, 0, 1);
                FbxVector4 normal1 = new FbxVector4(0, 1, 0);
                FbxVector4 normal2 = new FbxVector4(0, 1, 1);
                FbxVector4 normal3 = new FbxVector4(0.301511344577764d, 0.904534033733291d, 0.301511344577764d);

                using (var fbxLayerElement = FbxLayerElementNormal.Create(mesh, "Normals")) {
                    // Set the Normals on the default layer
                    FbxLayer fbxLayer = mesh.GetLayer(0);
                    if (fbxLayer == null) {
                        mesh.CreateLayer();
                        fbxLayer = mesh.GetLayer(0);
                    }

                    fbxLayerElement.SetMappingMode(FbxLayerElement.EMappingMode.eByControlPoint);
                    fbxLayerElement.SetReferenceMode(FbxLayerElement.EReferenceMode.eDirect);
                    FbxLayerElementArray fbxElementArray = fbxLayerElement.GetDirectArray();

                    fbxElementArray.Add(normal0);
                    fbxElementArray.Add(normal1);
                    fbxElementArray.Add(normal2);
                    fbxElementArray.Add(normal3);

                    fbxLayer.SetNormals(fbxLayerElement);
                }

                FbxVector4 readNormal0; 
                FbxVector4 readNormal1; 
                FbxVector4 readNormal2;
                FbxVector4 readNormal3;

                // test if all normals can be read
                Assert.IsTrue(mesh.GetPolygonVertexNormal(0, 0, out readNormal0));
                Assert.IsTrue(mesh.GetPolygonVertexNormal(0, 1, out readNormal1));
                Assert.IsTrue(mesh.GetPolygonVertexNormal(0, 2, out readNormal2));
                Assert.IsTrue(mesh.GetPolygonVertexNormal(0, 3, out readNormal3));

                // test if the normals have the correct values
                Assert.That(normal0.X, Is.EqualTo(readNormal0.X).Using(FloatEqualityComparer.Instance));
                Assert.That(normal0.Y, Is.EqualTo(readNormal0.Y).Using(FloatEqualityComparer.Instance));                
                Assert.That(normal0.Z, Is.EqualTo(readNormal0.Z).Using(FloatEqualityComparer.Instance));
                Assert.That(normal0.W, Is.EqualTo(readNormal0.W).Using(FloatEqualityComparer.Instance));

                Assert.That(normal1.X, Is.EqualTo(readNormal1.X).Using(FloatEqualityComparer.Instance));
                Assert.That(normal1.Y, Is.EqualTo(readNormal1.Y).Using(FloatEqualityComparer.Instance));
                Assert.That(normal1.Z, Is.EqualTo(readNormal1.Z).Using(FloatEqualityComparer.Instance));
                Assert.That(normal1.W, Is.EqualTo(readNormal1.W).Using(FloatEqualityComparer.Instance));

                Assert.That(normal2.X, Is.EqualTo(readNormal2.X).Using(FloatEqualityComparer.Instance));
                Assert.That(normal2.Y, Is.EqualTo(readNormal2.Y).Using(FloatEqualityComparer.Instance));
                Assert.That(normal2.Z, Is.EqualTo(readNormal2.Z).Using(FloatEqualityComparer.Instance));
                Assert.That(normal2.W, Is.EqualTo(readNormal2.W).Using(FloatEqualityComparer.Instance));

                Assert.That(normal3.X, Is.EqualTo(readNormal3.X).Using(FloatEqualityComparer.Instance));
                Assert.That(normal3.Y, Is.EqualTo(readNormal3.Y).Using(FloatEqualityComparer.Instance));
                Assert.That(normal3.Z, Is.EqualTo(readNormal3.Z).Using(FloatEqualityComparer.Instance));
                Assert.That(normal3.W, Is.EqualTo(readNormal3.W).Using(FloatEqualityComparer.Instance));
            }
        }

        [Test]
        public void TestBeginBadPolygonCreation()
        {
            // Add before begin. This crashes in native FBX SDK.
            using (FbxMesh mesh = CreateObject ("mesh")) {
                Assert.That(() => mesh.AddPolygon(0), Throws.Exception.TypeOf<FbxMesh.BadBracketingException>());
            }

            // End before begin. This is benign in native FBX SDK.
            using (FbxMesh mesh = CreateObject ("mesh")) {
                Assert.That(() => mesh.EndPolygon(), Throws.Exception.TypeOf<FbxMesh.BadBracketingException>());
            }

            // Begin during begin. This is benign in native FBX SDK.
            using (FbxMesh mesh = CreateObject ("mesh")) {
                mesh.BeginPolygon();
                Assert.That(() => mesh.BeginPolygon(), Throws.Exception.TypeOf<FbxMesh.BadBracketingException>());
            }

            // Negative polygon index. Benign in FBX SDK, but it will crash some importers.
            using (FbxMesh mesh = CreateObject ("mesh")) {
                mesh.BeginPolygon ();
                Assert.That(() => mesh.AddPolygon (-1), Throws.Exception.TypeOf<System.ArgumentOutOfRangeException>());
            }
        }
    }

    internal class FbxMeshBadBracketingExceptionTest {
        [Test]
        public void BasicTests()
        {
            // BadBracketingException()
            var xcp = new FbxMesh.BadBracketingException();
            xcp.HelpLink = "http://127.0.0.1";
            Assert.AreEqual("http://127.0.0.1", xcp.HelpLink);
            Assert.AreNotEqual("", xcp.Message);
            xcp.Source = "source";
            Assert.AreEqual("source", xcp.Source);
            Assert.AreNotEqual("", xcp.StackTrace);
            Assert.IsNull(xcp.InnerException);
            Assert.AreEqual(xcp, xcp.GetBaseException());
            Assert.IsNull(xcp.TargetSite);
            Assert.IsNotNull(xcp.Data);
            Assert.AreEqual(typeof(FbxMesh.BadBracketingException), xcp.GetType());

            // BadBracketingException(string message)
            xcp = new FbxMesh.BadBracketingException("oops");
            xcp.HelpLink = "http://127.0.0.1";
            Assert.AreEqual("http://127.0.0.1", xcp.HelpLink);
            Assert.AreNotEqual("", xcp.Message);
            xcp.Source = "source";
            Assert.AreEqual("source", xcp.Source);
            Assert.AreNotEqual("", xcp.StackTrace);
            Assert.IsNull(xcp.InnerException);
            Assert.AreEqual(xcp, xcp.GetBaseException());
            Assert.IsNull(xcp.TargetSite);
            Assert.IsNotNull(xcp.Data);
            Assert.AreEqual(typeof(FbxMesh.BadBracketingException), xcp.GetType());

            // BadBracketingException(string message, System.Exception innerException)
            xcp = new FbxMesh.BadBracketingException("oops", new System.Exception());
            xcp.HelpLink = "http://127.0.0.1";
            Assert.AreEqual("http://127.0.0.1", xcp.HelpLink);
            Assert.AreNotEqual("", xcp.Message);
            xcp.Source = "source";
            Assert.AreEqual("source", xcp.Source);
            Assert.AreNotEqual("", xcp.StackTrace);
            Assert.IsNotNull(xcp.InnerException);

            // The base exception becomes the inner exception here since this represents a chain of exceptions
            Assert.AreNotEqual(xcp, xcp.GetBaseException());
            Assert.AreEqual(xcp.InnerException, xcp.GetBaseException());
            Assert.IsNull(xcp.TargetSite);
            Assert.IsNotNull(xcp.Data);
            Assert.AreEqual(typeof(FbxMesh.BadBracketingException), xcp.GetType());


        }
    }
}
