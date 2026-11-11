// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{

    /// A single mesh that contains geometry for multiple strokes.
    /// TODO: implement optional attributes
    public class Batch : MonoBehaviour
    {
        private BatchPool m_ParentPool;
        private MeshFilter m_MeshFilter;
        private bool m_bVertexDataDirty;
        private bool m_bTopologyDirty;
        // Visibility edits do not change index count or vertex data. Coalesce their index span.
        private int m_DirtyIndexStart = int.MaxValue;
        private int m_DirtyIndexEnd;
        // Shared main-thread scratch; never retain a converted index buffer per batch.
        private const int kMaxCachedIndexCount = 16384;
        private static ushort[] sm_IndexScratch = Array.Empty<ushort>();

        private GeometryPool m_Geometry;
        private Material m_InstantiatedMaterial;
        private int m_LastMeshUpdate; // BatchManager timestamp of the most-recent write to the Mesh

        /// Sorted by initial vert index
        /// (if this is violated, RemoveSubset() will fail)
        // Built at runtime by the batcher; never authored in a scene or prefab.
        [NonSerialized] public List<BatchSubset> m_Groups;
#if UNITY_EDITOR
        public bool m_EditorDebug;
        public Vector3 m_EditorDebugMeshScale;
#endif
        public Mesh m_EditorDebugMesh;  // Also used as temporary storage during Unity GLTF export
        public BrushDescriptor Brush => BrushCatalog.m_Instance.GetBrush(m_ParentPool.m_BrushGuid);

        /// Returns the BatchManager timestamp of the last time the Batch's mesh was written to.
        public int LastMeshUpdate { get { return m_LastMeshUpdate; } }
        public BatchPool ParentPool { get { return m_ParentPool; } }
        public GeometryPool Geometry { get { return m_Geometry; } }

        /// The material instance used by this batch.
        /// This is _not_ shared with brush.Material, or with any other batches.
        public Material InstantiatedMaterial { get { return m_InstantiatedMaterial; } }

        // An immutable global identifier for this batch.
        // Note that this identifier is only valid during run-time and should not be persisted.
        public ushort BatchId { get; private set; }

        static public Batch Create(BatchPool parentPool, Transform rParent, Bounds rBounds)
        {
            var brush = BrushCatalog.m_Instance.GetBrush(parentPool.m_BrushGuid);
            string name = string.Format("Batch_{0}_{1}", parentPool.m_Batches.Count, brush.m_Guid);
            GameObject newObj = new GameObject(name);

            Transform t = newObj.transform;
            t.parent = rParent;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            newObj.AddComponent<MeshFilter>();

            Renderer renderer = newObj.AddComponent<MeshRenderer>();
            if (brush.m_OverlayMaterial != null)
                renderer.materials = new Material[] { brush.Material, brush.m_OverlayMaterial };
            else
                renderer.material = brush.Material;

            var propertyBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            ushort batchId = GpuIntersector.GetNextBatchId();
            propertyBlock.SetFloat("_BatchID", batchId);
            renderer.SetPropertyBlock(propertyBlock);

            Batch batch = newObj.AddComponent<Batch>();
            batch.Init(parentPool, rBounds, batchId);
            // This forces instantiation, but we can detect and clean it up in Destroy()
            batch.m_InstantiatedMaterial = renderer.material;

            return batch;
        }

        public void ReplaceMaterial(Material newMaterial)
        {
            Renderer renderer = m_MeshFilter.gameObject.GetComponent<Renderer>();
            renderer.material = newMaterial;
            m_InstantiatedMaterial = newMaterial;
        }

        // Public only for use by BatchManager
        public void FlushMeshUpdates()
        {
            UpdateMesh();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (m_Groups == null || !m_EditorDebug) return;
            if (m_EditorDebugMesh == null) m_EditorDebugMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (m_EditorDebugMeshScale == Vector3.zero) m_EditorDebugMeshScale = new Vector3(0.1f, 0.1f, 0.7f);
            foreach (var subset in m_Groups)
            {
                var stroke = subset.m_Stroke;
                if (!subset.m_Active || stroke.m_Type == Stroke.Type.NotCreated) continue;
                foreach (var cp in stroke.m_ControlPoints)
                {
                    var tr = TrTransform.TR(cp.m_Pos, cp.m_Orient);
                    tr = stroke.Canvas.Pose * tr;
                    Gizmos.DrawMesh(m_EditorDebugMesh, tr.translation, tr.rotation, m_EditorDebugMeshScale);
                    Debug.DrawRay(tr.translation, tr.rotation * Vector3.forward);
                }
            }
        }
#endif

        void Init(BatchPool parentPool, Bounds bounds, ushort batchId)
        {
            BatchId = batchId;
            m_ParentPool = parentPool;
            parentPool.m_Batches.Add(this);

            m_Groups = new List<BatchSubset>();
            m_MeshFilter = GetComponent<MeshFilter>();
            Debug.Assert(m_MeshFilter.sharedMesh == null);

            m_Geometry = new GeometryPool();

            var rNewMesh = new Mesh();
            if (App.UserConfig.Flags.LargeMeshSupport)
            {
                rNewMesh.indexFormat = IndexFormat.UInt32;
            }
            rNewMesh.MarkDynamic();

            gameObject.layer = ParentPool.Owner.Canvas.gameObject.layer;

            // This is a fix for b/27266757. I don't know precisely why it works.
            //
            // I think the mesh needs to spend "some amount of time" with a non-zero-length
            // vtx buffer. If this line is followed by .vertices = new Vector3[0], the bug
            // appears again. The mysterious thing is that immediately after creation, we
            // start filling up .vertices. Why does the first assignment need to happen here,
            // instead of waiting just a few ms for the mesh to be updated with real data?
            //
            // This seems related to how and when Unity decides to upload mesh data to the GPU.
            rNewMesh.vertices = new Vector3[1];

            // TODO: why set bounds?
            rNewMesh.bounds = bounds;
            m_MeshFilter.mesh = rNewMesh;

            // Instantiate mesh so we can destroy rNewMesh; destroy rNewMesh to protect
            // against it leaking if/when someone reads m_MeshFilter.mesh.
            bool instantiationSucceeded = (m_MeshFilter.mesh != rNewMesh);
            Debug.Assert(instantiationSucceeded);
            DestroyImmediate(rNewMesh);

            m_bVertexDataDirty = false;
            m_bTopologyDirty = false;
        }

        /// Reduces memory usage, but causes the next mesh update to be a little more expensive.
        public void ClearCachedGeometry()
        {
            if (m_Geometry.HasCachedGeometry)
            {
                SelfCheck();
                UpdateMesh();
                m_Geometry.MakeGeometryNotResident(m_MeshFilter.sharedMesh);
                SelfCheck();
            }
        }

        /// Destroys the batch and all resources+objects owned by it.
        /// The batch is no longer usable after this.
        public void Destroy()
        {
            m_ParentPool = null;

            // Writing a BatchSubset.Destroy() wouldn't be worth it; there's nothing really to destroy
            foreach (var subset in m_Groups)
            {
                subset.m_ParentBatch = null;
            }
            m_Groups = null;

            // Don't bother with mesh.Clear() since we're about to destroy it.
            // m_MeshFilter.mesh.Clear();

            // Don't bother with m_Geometry.Reset(). Internally it uses List<>.Clear()
            // which wastes time zeroing out the list entries. That's wasted work since
            // we're going to garbage the whole thing.
            // m_Geometry.Reset();

            // I don't think we want to do this until GeometryPool.Free() is smart enough
            // to limit the number of instances on the freelist.
            // GeometryPool.Free(m_Geometry);
            m_Geometry.Destroy();
            m_Geometry = null;

            Destroy(m_InstantiatedMaterial);

            Destroy(m_MeshFilter.mesh);
            m_MeshFilter = null;
            Destroy(gameObject);
        }

        // Only for use by CPU-based intersection tool.
        // This is mostly-dead code.
        public void GetTriangles(out Vector3[] aVerts, out int nVerts,
                                 out int[] aTris, out int nTris)
        {
            // It's not okay for a dead codepath to force geometry to become resident
            Debug.Assert(m_Geometry.IsGeometryResident);
            aVerts = m_Geometry.m_Vertices.GetBackingArray();
            nVerts = m_Geometry.m_Vertices.Count;
            aTris = m_Geometry.m_Tris.GetBackingArray();
            nTris = m_Geometry.m_Tris.Count;
        }

        /// Returns false if this many extra verts would push the batch
        /// over its (internal) soft size limit.
        /// Note that empty batches will accept verts up to the Unity VB limit.
        public bool HasSpaceFor(int nVert)
        {
            // OneStrokePerBatch flag forces all strokes on this canvas into separate gameobjects
            if (ParentPool.Owner.OneStrokePerBatch && m_Groups.Count >= 1) return false;
            // The limit above which we try not to go (This must be a multiple of 3)
            int max_verts = App.UserConfig.Flags.LargeMeshSupport ? 2147483646 : 15999;
            return m_Geometry.NumVerts + nVert <= max_verts;
        }

        internal static Bounds GetBoundsFor(List<Vector3> aVert, int iVert, int nVert,
                                            TrTransform? leftTransform = null)
        {
            return GetBoundsFor(aVert.GetBackingArray(), iVert, nVert, leftTransform);
        }

        static Bounds GetBoundsFor(Vector3[] aVert, int iVert, int nVert,
                                   TrTransform? leftTransform = null)
        {
            if (nVert == 0)
            {
                var uninitializedBounds = new Bounds();
                uninitializedBounds.SetMinMax(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue),
                    new Vector3(float.MinValue, float.MinValue, float.MinValue));
                return uninitializedBounds;
            }

            Vector3 center, size;
            Matrix4x4 leftTransformMatrix = leftTransform.HasValue
                ? leftTransform.Value.ToMatrix4x4()
                : Matrix4x4.identity;
            MathUtils.GetBoundsFor(leftTransformMatrix, iVert, iVert + nVert, aVert, out center, out size);
            return new Bounds(center, size);
        }

        // Add data from the passed arrays to our geometry.
        // This API will be removed when MasterBrush is removed
        void AppendVertexData(
            int nVert, Vector3[] vertices,
            Vector3[] normals,
            Vector2[] uvs,
            List<Vector3> uvws,
            Color32[] colors,
            Vector4[] tangents)
        {
            Debug.Assert(m_Geometry.IsGeometryResident); // Caller's responsibility
            m_Geometry.m_Vertices.AddRange(vertices, 0, nVert);
            m_Geometry.m_Normals.AddRange(normals, 0, nVert);
            if (m_Geometry.Layout.texcoord0.size == 2)
            {
                m_Geometry.m_Texcoord0.v2.AddRange(uvs, 0, nVert);
            }
            else if (m_Geometry.Layout.texcoord0.size == 3)
            {
                m_Geometry.m_Texcoord0.v3.AddRange(uvws, 0, nVert);
            }
            m_Geometry.m_Colors.AddRange(colors, 0, nVert);
            m_Geometry.m_Tangents.AddRange(tangents, 0, nVert);
        }

        // This API will be removed when MasterBrush is removed
        void AppendTriangleData(int iVertOffset, int nTriIndices, int[] tris)
        {
            Debug.Assert(m_Geometry.IsGeometryResident); // Caller's responsibility
            if (nTriIndices > 100)
            {
                int destStart = m_Geometry.m_Tris.Count;
                m_Geometry.m_Tris.SetCount(destStart + nTriIndices);
                int[] destTris = m_Geometry.m_Tris.GetBackingArray();
                for (int i = 0; i < nTriIndices; ++i)
                {
                    destTris[destStart + i] = tris[i] + iVertOffset;
                }
            }
            else
            {
                for (int i = 0; i < nTriIndices; ++i)
                {
                    m_Geometry.m_Tris.Add(iVertOffset + tris[i]);
                }
            }
        }

        public void DelayedUpdateMesh()
        {
            // Mark it dirty and it'll get taken care of later unless we're inactive.
            m_bVertexDataDirty = true;
            m_bTopologyDirty = true;
            if (!gameObject.activeSelf)
            {
                UpdateMesh();
            }
        }

        void LateUpdate()
        {
            UpdateMesh();
        }

        void OnWillRenderObject()
        {
            UpdateMesh();
        }


        private void MarkTopologyDirty(int indexStart, int indexEnd)
        {
            m_bTopologyDirty = true;
            m_DirtyIndexStart = Math.Min(m_DirtyIndexStart, indexStart);
            m_DirtyIndexEnd = Math.Max(m_DirtyIndexEnd, indexEnd);
        }

        private void ResetDirtyIndexRange()
        {
            m_DirtyIndexStart = int.MaxValue;
            m_DirtyIndexEnd = 0;
        }

        private void UpdateTopology(Mesh mesh)
        {
            // A cold tail removal changes the submesh length, even when its dirty range is
            // entirely beyond the remaining indices. Keep the vertex buffer and bounds intact.
            if (mesh.GetIndexCount(0) != m_Geometry.NumTriIndices)
            {
                mesh.SetTriangles(m_Geometry.m_Tris, 0, calculateBounds: false);
                return;
            }
            int start = m_DirtyIndexStart;
            int count = m_DirtyIndexEnd - start;
            if (count <= 0) return;

            // Dense/scattered edits are cheaper to convert in Unity than in a managed loop.
            // Preserve the existing conservative bounds: visibility edits never move vertices.
            if (count >= m_Geometry.NumTriIndices / 2 ||
                (mesh.indexFormat == IndexFormat.UInt16 && count > kMaxCachedIndexCount))
            {
                mesh.SetTriangles(m_Geometry.m_Tris, 0, calculateBounds: false);
            }
            else if (mesh.indexFormat == IndexFormat.UInt32)
            {
                mesh.SetIndexBufferData(m_Geometry.m_Tris, start, start, count);
            }
            else
            {
                if (sm_IndexScratch.Length < count)
                {
                    sm_IndexScratch = new ushort[Mathf.NextPowerOfTwo(count)];
                }
                var indices = m_Geometry.m_Tris.GetBackingArray();
                for (int i = 0; i < count; ++i)
                {
                    sm_IndexScratch[i] = checked((ushort)indices[start + i]);
                }
                // The raw upload element width must match the mesh's UInt16 index format.
                mesh.SetIndexBufferData(sm_IndexScratch, 0, start, count);
            }
        }

        void UpdateMesh()
        {
            // Intro sketch is weird and has Batch components with no parents, m_Geometry, etc
            if (m_ParentPool == null)
            {
                return;
            }

            SelfCheck();
            if (m_bVertexDataDirty)
            {
                // Making !resident clears dirtiness; and adding dirtiness requires resident.
                Debug.Assert(m_Geometry.IsGeometryResident, "Impossible! Dirty but not resident");
                m_bVertexDataDirty = false;
                m_bTopologyDirty = false; // The topology gets updated in CopyToMesh().
                m_Geometry.CopyToMesh(m_MeshFilter.mesh);
                ResetDirtyIndexRange();
                Bounds bounds = m_MeshFilter.mesh.bounds;
                bounds.Expand(Brush.m_BoundsPadding *
                    2 * App.METERS_TO_UNITS * Vector3.one);
                m_MeshFilter.mesh.bounds = bounds;
                m_LastMeshUpdate = m_ParentPool.Owner.CurrentTimestamp;
            }
            else if (m_bTopologyDirty)
            {
                // Visibility edits can have resident indices while vertex channels stay evicted.
                Debug.Assert(m_Geometry.m_Tris != null, "Impossible! Dirty but no indices");
                m_bTopologyDirty = false;
                UpdateTopology(m_MeshFilter.mesh);
                ResetDirtyIndexRange();
                m_LastMeshUpdate = m_ParentPool.Owner.CurrentTimestamp;
            }
            SelfCheck();
        }

        public void CopyToMesh(BatchSubset subset, GameObject obj)
        {
            SelfCheck();
            int iVert = subset.m_StartVertIndex;
            int nVert = subset.m_VertLength;
            m_Geometry.CopyToMesh(obj.GetComponent<MeshFilter>().mesh,
                iVert, nVert,
                subset.m_iTriIndex, subset.m_nTriIndex);

            MeshRenderer objRenderer = obj.GetComponent<MeshRenderer>();
            // Temporary workaround for b/31346571
            objRenderer.material = GetComponent<MeshRenderer>().material;
        }

        /// Returns a new subset containing the passed geometry.
        /// raises ArgumentOutOfRangeException if not enough room.
        /// Pass:
        ///   rMasterBrush - Geometry, all of which will be copied into the new subset
        public BatchSubset AddSubset(int nVert, int nTris, MasterBrush rMasterBrush)
        {
            SelfCheck();
            // If we're not empty, the caller should never have tried to add the subset,
            // because it's caller's responsibility to check HasSpaceFor().
            // If we're empty, allow anything (up to the Unity limit).
            if (!HasSpaceFor(nVert) && (m_Geometry.NumVerts > 0))
            {
                throw new ArgumentOutOfRangeException("nVert");
            }
            if (m_Geometry.NumVerts == 0)
            {
                m_Geometry.Layout = rMasterBrush.VertexLayout.Value;
            }

            BatchSubset child = new BatchSubset();
            child.m_ParentBatch = this;
            m_Groups.Add(child);
            child.m_Active = true;
            child.m_StartVertIndex = m_Geometry.NumVerts;
            child.m_VertLength = nVert;
            child.m_iTriIndex = m_Geometry.NumTriIndices;
            child.m_nTriIndex = nTris;
            // This is normally true -- unless the geometry has been welded
            // Debug.Assert(nVert % 3 == 0);
            child.m_Bounds = GetBoundsFor(rMasterBrush.m_Vertices, 0, nVert);

            if (nVert > 0)
            {
                m_Geometry.EnsureGeometryResident();
                AppendVertexData(nVert, rMasterBrush.m_Vertices,
                    rMasterBrush.m_Normals,
                    rMasterBrush.m_UVs,
                    rMasterBrush.m_UVWs,
                    rMasterBrush.m_Colors,
                    rMasterBrush.m_Tangents);
                AppendTriangleData(child.m_StartVertIndex, child.m_nTriIndex, rMasterBrush.m_Tris);
                DelayedUpdateMesh();
            }

            SelfCheck();
            return child;
        }

        /// Returns a new subset containing the passed geometry.
        /// raises ArgumentOutOfRangeException if not enough room.
        /// Pass:
        ///   geom - Geometry, all of which will be copied into the new subset
        public BatchSubset AddSubset(GeometryPool geom)
        {
            return AddSubset(geom, 0, geom.NumVerts, 0, geom.NumTriIndices);
        }

        /// Returns a new subset containing the passed geometry.
        /// raises ArgumentOutOfRangeException if not enough room.
        ///
        /// Make sure the triangle indices refer only to verts inside the
        /// passed range of verts.
        ///
        /// Pass:
        ///   geom          - Geometry, a subset of which will be copied into the new subset
        ///   i/nVert       - start/count of verts to copy
        ///   i/nTriIndex   - start/count of triangle indices to copy.
        ///   leftTransform - optional transform to transform the subset.
        ///
        public BatchSubset AddSubset(
            GeometryPool geom, int iVert, int nVert, int iTriIndex, int nTriIndex, TrTransform? leftTransform = null)
        {
            // If we're not empty, the caller should never have tried to add the subset,
            // because it's caller's responsibility to check HasSpaceFor().
            // If we're empty, allow anything (up to the Unity limit).
            SelfCheck();
            if (!HasSpaceFor(nVert) && (m_Geometry.NumVerts > 0))
            {
                throw new ArgumentOutOfRangeException("nVert");
            }
            if (m_Geometry.NumVerts == 0)
            {
                m_Geometry.Layout = geom.Layout;
            }

            BatchSubset child = new BatchSubset();
            child.m_ParentBatch = this;
            m_Groups.Add(child);
            child.m_Active = true;
            child.m_StartVertIndex = m_Geometry.NumVerts;
            child.m_VertLength = nVert;
            child.m_iTriIndex = m_Geometry.NumTriIndices;
            child.m_nTriIndex = nTriIndex;
            if (nVert > 0)
            {
                m_Geometry.Append(geom, iVert, nVert, iTriIndex, nTriIndex, leftTransform);
                // The destination already contains transformed positions. Computing bounds here
                // avoids restoring an entire cold source batch just to read this subset's bounds.
                child.m_Bounds = GetBoundsFor(m_Geometry.m_Vertices, child.m_StartVertIndex, nVert);
                DelayedUpdateMesh();
            }
            else
            {
                child.m_Bounds = GetBoundsFor(Array.Empty<Vector3>(), 0, 0);
            }

            SelfCheck();
            return child;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void SelfCheck()
        {
            if (m_Groups.Count > 0)
            {
                var lastSubset = m_Groups[m_Groups.Count - 1];
                if (lastSubset.m_StartVertIndex + lastSubset.m_VertLength != m_Geometry.NumVerts)
                {
                    Debug.LogError("Failed self check: last subset == verts");
                }
                if (lastSubset.m_iTriIndex + lastSubset.m_nTriIndex != m_Geometry.NumTriIndices)
                {
                    Debug.LogError("Failed self check: last subset == tris");
                }
            }
            if (m_Geometry.IsGeometryResident)
            {
                int geomVert = m_Geometry.NumVerts;
                int meshVert = m_MeshFilter.sharedMesh.vertexCount;
                // Index-only truncation may leave an unused vertex-buffer tail.
                if (!m_bVertexDataDirty && geomVert > meshVert)
                {
                    Debug.LogError("Failed self check: live geometry exceeds mesh vertices");
                }
                if (!m_bTopologyDirty && m_Geometry.NumTriIndices != m_MeshFilter.sharedMesh.GetIndexCount(0))
                {
                    Debug.LogError("Failed self check: geom tris == mesh tris");
                }
            }
        }

        public void RemoveSubset(BatchSubset subset)
        {
            // Often O(1) because it's the last one
            SelfCheck();
            int iSubset = m_Groups.LastIndexOf(subset);
            if (iSubset < 0)
            {
                Debug.Assert(false, "Not found");
                return;
            }

            // Could do some compaction, but this case is not very common.
            // Just disable the triangles. If all subsets after this one are
            // freed, we'll reclaim the space then.
            DisableSubset(subset);

            bool updateVertices = false;
            bool truncatedTail = false;
            // If this is the last subset, we can free up some space.
            if (iSubset == m_Groups.Count - 1)
            {
                truncatedTail = true;
                // It would be incorrect to simply subtract from Num{Verts,TriIndices}, because
                // there may be dead space before this subset.
                Debug.Assert(subset.m_StartVertIndex + subset.m_VertLength == m_Geometry.NumVerts);
                Debug.Assert(subset.m_iTriIndex + subset.m_nTriIndex == m_Geometry.NumTriIndices);
                int newNumVert, newNumIndices;
                if (iSubset > 0)
                {
                    var prev = m_Groups[iSubset - 1];
                    newNumVert = prev.m_StartVertIndex + prev.m_VertLength;
                    newNumIndices = prev.m_iTriIndex + prev.m_nTriIndex;
                }
                else
                {
                    newNumVert = newNumIndices = 0;
                }

                if (newNumVert == 0 && newNumIndices == 0)
                {
                    // NumVerts restores cold vertex channels before resizing. An empty batch
                    // has nothing to preserve, so discard its backing data without reading it.
                    // Reset retains the layout and reuses arrays that are already resident.
                    m_Geometry.Reset();
                    updateVertices = true;
                }
                // Cold pools keep their vertex buffer and need only the index update below.
                else if (!m_Geometry.TryTruncateColdMesh(newNumVert, newNumIndices))
                {
                    m_Geometry.NumVerts = newNumVert;
                    m_Geometry.NumTriIndices = newNumIndices;
                    updateVertices = true;
                }
            }

            m_Groups.RemoveAt(iSubset);
            subset.m_ParentBatch = null;
            // Inactive batches upload immediately; their subset metadata must be consistent first.
            if (updateVertices) DelayedUpdateMesh();
            else if (truncatedTail)
            {
                // An already-hidden subset makes DisableSubset return without dirtying indices.
                MarkTopologyDirty(m_Geometry.NumTriIndices, m_Geometry.NumTriIndices);
                if (!gameObject.activeSelf) UpdateMesh();
            }
            SelfCheck();
        }

        public void DisableSubset(BatchSubset subset)
        {
            SelfCheck();
            if (!subset.m_Active)
            {
                return;
            }

            Debug.Assert(subset.m_Active);
            subset.m_Active = false;
            if (subset.m_TriangleBackup == null)
            {
                subset.m_TriangleBackup = new ushort[subset.m_nTriIndex];
            }

            m_Geometry.EnsureIndicesResident();
            var aTris = m_Geometry.m_Tris.GetBackingArray();
            int t0 = subset.m_iTriIndex;
            int t1 = subset.m_iTriIndex + subset.m_nTriIndex;
            // TODO: Possibly could optimize this in C++ for 4.4% of time in selection.
            for (int t = t0; t < t1; ++t)
            {
                subset.m_TriangleBackup[t - t0] = (ushort)aTris[t];
                aTris[t] = 0;
            }
            MarkTopologyDirty(t0, t1);
            SelfCheck();
        }

        public void EnableSubset(BatchSubset subset)
        {
            SelfCheck();
            if (subset.m_Active)
            {
                return;
            }

            Debug.Assert(!subset.m_Active);
            subset.m_Active = true;
            m_Geometry.EnsureIndicesResident();
            var aTris = m_Geometry.m_Tris.GetBackingArray();
            int t0 = subset.m_iTriIndex;
            int t1 = subset.m_iTriIndex + subset.m_nTriIndex;
            for (int t = t0; t < t1; ++t)
            {
                aTris[t] = subset.m_TriangleBackup[t - t0];
            }
            MarkTopologyDirty(t0, t1);
            SelfCheck();
        }

        public void RegisterHighlight()
        {
            UpdateMesh();
            App.Instance.SelectionEffect.RegisterMesh(m_MeshFilter);
        }
    }
} // namespace TiltBrush
