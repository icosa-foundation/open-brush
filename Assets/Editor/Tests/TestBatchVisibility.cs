// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace TiltBrush
{
    public class TestBatchVisibility
    {
        sealed class Fixture : IDisposable
        {
            public readonly GameObject Object;
            public readonly Batch Batch;
            public readonly GeometryPool Geometry;
            public readonly Mesh Mesh;
            public readonly Bounds Bounds = new Bounds(Vector3.zero, Vector3.one * 100);
            public readonly int[] Indices = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };

            public Fixture(bool uint32)
            {
                Object = new GameObject("TestBatchVisibility") { hideFlags = HideFlags.HideAndDontSave };
                var filter = Object.AddComponent<MeshFilter>();
                Batch = Object.AddComponent<Batch>();
                Geometry = new GeometryPool { Layout = new GeometryPool.VertexLayout() };
                for (int i = 0; i < 9; ++i) Geometry.m_Vertices.Add(new Vector3(i, i % 2, 0));
                Geometry.m_Tris.AddRange(Indices);
                var mesh = new Mesh { indexFormat = uint32 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(Geometry.m_Vertices);
                mesh.SetTriangles(Geometry.m_Tris, 0);
                mesh.bounds = Bounds;
                filter.sharedMesh = mesh;
                // Batch owns its mesh clone; both meshes are explicitly disposed below.
                LogAssert.Expect(LogType.Error, "Instantiating mesh due to calling MeshFilter.mesh during edit mode. This will leak meshes. Please use MeshFilter.sharedMesh instead.");
                Mesh = filter.mesh;
                if (Mesh != mesh) UnityEngine.Object.DestroyImmediate(mesh);
                SetField("m_MeshFilter", filter);
                SetField("m_Geometry", Geometry);
                SetField("m_ParentPool", new BatchPool(new BatchManager()));
                Batch.m_Groups = new List<BatchSubset>();
                for (int i = 0; i < 3; ++i)
                {
                    Batch.m_Groups.Add(new BatchSubset
                    {
                        m_ParentBatch = Batch, m_Active = true,
                        m_StartVertIndex = i * 3, m_VertLength = 3,
                        m_iTriIndex = i * 3, m_nTriIndex = 3
                    });
                }
            }

            void SetField(string field, object value) =>
                typeof(Batch).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Batch, value);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Object);
                UnityEngine.Object.DestroyImmediate(Mesh);
                Geometry.Destroy();
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void SingleSubsetRoundTripsWithoutChangingOtherIndicesOrBounds(bool uint32, bool cold)
        {
            using (var fixture = new Fixture(uint32))
            {
                if (cold) fixture.Batch.ClearCachedGeometry();
                var subset = fixture.Batch.m_Groups[1];
                fixture.Batch.DisableSubset(subset);
                fixture.Batch.DisableSubset(subset); // Repeated hide must not overwrite the backup.
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 0, 0, 6, 7, 8 }, fixture.Mesh.triangles);
                Assert.AreEqual(fixture.Bounds, fixture.Mesh.bounds);
                Assert.AreEqual(9, fixture.Mesh.GetIndexCount(0));
                fixture.Batch.EnableSubset(subset);
                fixture.Batch.EnableSubset(subset);
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(fixture.Indices, fixture.Mesh.triangles);
                Assert.AreEqual(fixture.Bounds, fixture.Mesh.bounds);

                // A subsequent dirty range must not inherit data from the first upload.
                fixture.Batch.DisableSubset(fixture.Batch.m_Groups[2]);
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 0, 0, 0 }, fixture.Mesh.triangles);
            }
        }

        [Test]
        public void ScatteredEditsCoalesceAndRestoreWithoutMovingIndices()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Batch.DisableSubset(fixture.Batch.m_Groups[0]);
                fixture.Batch.DisableSubset(fixture.Batch.m_Groups[2]);
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(new[] { 0, 0, 0, 3, 4, 5, 0, 0, 0 }, fixture.Mesh.triangles);
                Assert.AreEqual(fixture.Bounds, fixture.Mesh.bounds);
                fixture.Batch.EnableSubset(fixture.Batch.m_Groups[2]);
                fixture.Batch.EnableSubset(fixture.Batch.m_Groups[0]);
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(fixture.Indices, fixture.Mesh.triangles);
            }
        }

        [Test]
        public void EmptyRangeAndCleanFlushLeaveTopologyUntouched()
        {
            using (var fixture = new Fixture(false))
            {
                var empty = new BatchSubset
                {
                    m_ParentBatch = fixture.Batch, m_Active = true,
                    m_StartVertIndex = 9, m_iTriIndex = 9
                };
                fixture.Batch.m_Groups.Add(empty);
                fixture.Batch.DisableSubset(empty);
                fixture.Batch.FlushMeshUpdates();
                fixture.Batch.EnableSubset(empty);
                fixture.Batch.FlushMeshUpdates();
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(fixture.Indices, fixture.Mesh.triangles);
                Assert.AreEqual(fixture.Bounds, fixture.Mesh.bounds);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ColdEditsSurviveFullReloadOrEvictionBeforeFlush(bool uint32, bool reloadVertices)
        {
            using (var fixture = new Fixture(uint32))
            {
                var vertices = fixture.Geometry.m_Vertices.ToArray();
                fixture.Batch.ClearCachedGeometry();
                Assert.IsFalse(fixture.Geometry.HasCachedGeometry);
                var subset = fixture.Batch.m_Groups[1];
                fixture.Batch.DisableSubset(subset);
                Assert.IsFalse(fixture.Geometry.IsGeometryResident);
                Assert.IsTrue(fixture.Geometry.HasCachedGeometry);
                Assert.IsNull(fixture.Geometry.m_Vertices);
                Assert.IsNull(fixture.Geometry.m_Normals);
                if (reloadVertices)
                {
                    var indices = fixture.Geometry.m_Tris;
                    fixture.Geometry.EnsureGeometryResident();
                    Assert.AreSame(indices, fixture.Geometry.m_Tris);
                    CollectionAssert.AreEqual(vertices, fixture.Geometry.m_Vertices);
                    fixture.Batch.FlushMeshUpdates();
                }
                else
                {
                    // Eviction must flush pending indices and release the index-only cache.
                    fixture.Batch.ClearCachedGeometry();
                    Assert.IsFalse(fixture.Geometry.HasCachedGeometry);
                }
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 0, 0, 0, 6, 7, 8 }, fixture.Mesh.triangles);
                fixture.Batch.EnableSubset(subset);
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(fixture.Indices, fixture.Mesh.triangles);
                fixture.Batch.ClearCachedGeometry();
                fixture.Geometry.EnsureGeometryResident();
                CollectionAssert.AreEqual(vertices, fixture.Geometry.m_Vertices);
                CollectionAssert.AreEqual(fixture.Indices, fixture.Geometry.m_Tris);
            }
        }

        [Test]
        public void AgedBatchPoolEvictsIndexOnlyCache()
        {
            using (var fixture = new Fixture(false))
            {
                fixture.Batch.ClearCachedGeometry();
                fixture.Batch.DisableSubset(fixture.Batch.m_Groups[1]);
                fixture.Batch.FlushMeshUpdates();
                var pool = fixture.Batch.ParentPool;
                pool.m_Batches = new List<Batch> { fixture.Batch };
                // The protected newest batches are never inspected by the eviction loop.
                for (int i = 0; i < BatchManager.kBatchesToLeaveMutable; ++i) pool.m_Batches.Add(null);
                typeof(BatchManager).GetField("m_CurrentTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(pool.Owner, BatchManager.kTimeUntilBatchImmutable + 1);
                pool.ClearCachedGeometryFromBatches();
                Assert.IsFalse(fixture.Geometry.HasCachedGeometry);
                fixture.Batch.EnableSubset(fixture.Batch.m_Groups[1]);
                fixture.Batch.FlushMeshUpdates();
                CollectionAssert.AreEqual(fixture.Indices, fixture.Mesh.triangles);
            }
        }
    }
}

