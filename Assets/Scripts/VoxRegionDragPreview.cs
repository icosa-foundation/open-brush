// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // Selection-lifetime geometry. Grabs reuse both meshes; stamps are instances
    // of the same payload mesh, not regenerated background or payload geometry.
    internal sealed class VoxRegionDragPreview : IDisposable
    {
        private readonly RuntimeVoxDocument m_Document;
        private readonly VoxFloatingRegion.Geometry m_Geometry;
        private readonly MeshRenderer m_OriginalRenderer;
        private readonly bool m_OriginalEnabled;
        private readonly VoxMaterialSet m_Materials;
        private readonly Material[] m_BackgroundMaterials, m_StampMaterials, m_FloatingMaterials;
        private readonly MaterialPropertyBlock m_Properties = new MaterialPropertyBlock();
        private readonly TrTransform m_Anchor;
        private readonly List<VoxFloatingRegion.StampNode> m_Stamps = new List<VoxFloatingRegion.StampNode>();
        private Mesh m_Background, m_Floating;
        private TrTransform m_Pose;
        private VoxFloatingRegion.StampNode m_StampHead;

        internal VoxRegionDragPreview(RuntimeVoxDocument document, VoxFloatingRegion floating,
            MeshRenderer originalRenderer, Material defaultMaterial, ushort batchId, IEnumerable<string> keywords)
        {
            m_Document = document;
            m_Geometry = floating.SourceGeometry;
            m_OriginalRenderer = originalRenderer;
            m_OriginalEnabled = originalRenderer.enabled;
            m_Anchor = VoxMeshBuilder.GetModelTransform(floating.AnchorOffset, floating.Model.GlobalRotation);
            m_Properties.SetFloat("_BatchID", batchId);
            m_Materials = new VoxMaterialSet(document, defaultMaterial,
                m_Geometry.Background.Values.Concat(m_Geometry.Cells.Values));
            m_BackgroundMaterials = m_Materials.Materials.Select(material => new Material(material)).ToArray();
            m_StampMaterials = m_Materials.Materials.Select(material => new Material(material)).ToArray();
            m_FloatingMaterials = m_Materials.Materials.Select(material => new Material(material)).ToArray();
            try
            {
                for (int i = 0; i < m_FloatingMaterials.Length; ++i)
                {
                    m_StampMaterials[i].renderQueue = m_BackgroundMaterials[i].renderQueue + 1;
                    m_FloatingMaterials[i].renderQueue = m_BackgroundMaterials[i].renderQueue + 2;
                    foreach (string keyword in keywords)
                    {
                        m_BackgroundMaterials[i].EnableKeyword(keyword);
                        m_StampMaterials[i].EnableKeyword(keyword);
                        m_FloatingMaterials[i].EnableKeyword(keyword);
                    }
                }
                m_Background = BuildMesh(m_Geometry.Background, "Voxel selection background");
                m_Floating = BuildMesh(m_Geometry.Cells, "Voxel selection payload");
                SetState(floating);
                originalRenderer.enabled = false;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal bool Matches(VoxFloatingRegion floating) => ReferenceEquals(m_Geometry, floating.SourceGeometry);

        private Mesh BuildMesh(IReadOnlyDictionary<Vector3Int, byte> cells, string name)
        {
            var model = new RuntimeVoxDocument.RuntimeModel(name, Vector3Int.one,
                sourceModelId: -1, isCopy: false,
                voxels: cells.ToDictionary(cell => cell.Key, cell => cell.Value));
            return new VoxMeshBuilder().GenerateOptimizedMesh(model, m_Document.Palette,
                m_Materials.PaletteSubmeshIndices, m_Materials.Materials.Count, m_Materials.PaletteOccludesFaces);
        }

        internal void SetPose(Vector3Int pivot, Quaternion rotation, Vector3Int translation, TrTransform basis)
        {
            m_Pose = TrTransform.TR((Vector3)pivot + translation - rotation * (Vector3)pivot, rotation) * basis;
        }

        internal void SetState(VoxFloatingRegion floating)
        {
            m_Pose = floating.Pose;
            var head = floating.Stamps;
            if (ReferenceEquals(head, m_StampHead)) return;
            if (head != null && ReferenceEquals(head.Previous, m_StampHead)) m_Stamps.Add(head);
            else if (m_StampHead != null && ReferenceEquals(m_StampHead.Previous, head))
                m_Stamps.RemoveAt(m_Stamps.Count - 1);
            else
            {
                m_Stamps.Clear();
                for (var stamp = head; stamp != null; stamp = stamp.Previous) m_Stamps.Add(stamp);
                m_Stamps.Reverse();
            }
            m_StampHead = head;
        }

        internal Bounds GetBounds(VoxFloatingRegion floating)
        {
            Vector3Int min = floating.HasBackground ? Vector3Int.Min(floating.CellMin, floating.BackgroundMin) : floating.CellMin;
            Vector3Int max = floating.HasBackground ? Vector3Int.Max(floating.CellMax, floating.BackgroundMax) : floating.CellMax;
            var bounds = new Bounds(m_Anchor * (((Vector3)min + max) * 0.5f), Vector3.zero);
            for (int x = 0; x < 2; ++x)
                for (int y = 0; y < 2; ++y)
                    for (int z = 0; z < 2; ++z)
                        bounds.Encapsulate(m_Anchor * new Vector3(x == 0 ? min.x - .5f : max.x + .5f,
                            y == 0 ? min.y - .5f : max.y + .5f, z == 0 ? min.z - .5f : max.z + .5f));
            return bounds;
        }

        internal void Draw(TrTransform widgetToWorld)
        {
            if (m_OriginalRenderer == null) return;
            TrTransform anchorToWorld = widgetToWorld * m_Anchor;
            DrawMesh(m_Background, anchorToWorld.ToMatrix4x4(), m_BackgroundMaterials);
            for (int i = 0; i < m_Stamps.Count; ++i)
                DrawMesh(m_Floating, (anchorToWorld * m_Stamps[i].Pose).ToMatrix4x4(), m_StampMaterials);
            DrawMesh(m_Floating, (anchorToWorld * m_Pose).ToMatrix4x4(), m_FloatingMaterials);
        }

        private void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material[] materials)
        {
            for (int i = 0; i < mesh.subMeshCount; ++i)
                if (mesh.GetIndexCount(i) != 0)
                    Graphics.DrawMesh(mesh, matrix, materials[i], m_OriginalRenderer.gameObject.layer,
                        null, i, m_Properties, m_OriginalRenderer.shadowCastingMode, m_OriginalRenderer.receiveShadows);
        }

        public void Dispose()
        {
            if (m_OriginalRenderer != null) m_OriginalRenderer.enabled = m_OriginalEnabled;
            Destroy(m_Background);
            Destroy(m_Floating);
            foreach (var material in m_BackgroundMaterials) Destroy(material);
            foreach (var material in m_StampMaterials) Destroy(material);
            foreach (var material in m_FloatingMaterials) Destroy(material);
            foreach (var material in m_Materials.OwnedMaterials) Destroy(material);
        }

        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}