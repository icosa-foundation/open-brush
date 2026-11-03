// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // Build two meshes at the edit boundary. Ordinary drag frames only submit
    // those meshes with a new rigid transform; the document is untouched.
    internal sealed class VoxRegionDragPreview : IDisposable
    {
        private readonly RuntimeVoxDocument m_Document;
        private readonly MeshRenderer m_OriginalRenderer;
        private readonly bool m_OriginalEnabled;
        private readonly VoxMaterialSet m_Materials;
        private readonly Material[] m_BackgroundMaterials, m_FloatingMaterials;
        private readonly MaterialPropertyBlock m_Properties = new MaterialPropertyBlock();
        private readonly TrTransform m_Anchor;
        private Mesh m_Background, m_Floating;
        private TrTransform m_Pose = TrTransform.identity;

        internal VoxRegionDragPreview(RuntimeVoxDocument document, VoxFloatingRegion floating,
            MeshRenderer originalRenderer, Material defaultMaterial, ushort batchId, IEnumerable<string> keywords)
        {
            m_Document = document;
            m_OriginalRenderer = originalRenderer;
            m_OriginalEnabled = originalRenderer.enabled;
            m_Anchor = VoxMeshBuilder.GetModelTransform(floating.AnchorOffset, floating.Model.GlobalRotation);
            m_Properties.SetFloat("_BatchID", batchId);
            m_Materials = new VoxMaterialSet(document, defaultMaterial,
                floating.Background.Values.Concat(floating.Cells.Values));
            m_BackgroundMaterials = m_Materials.Materials.Select(material => new Material(material)).ToArray();
            m_FloatingMaterials = m_Materials.Materials.Select(material => new Material(material)).ToArray();
            try
            {
                for (int i = 0; i < m_FloatingMaterials.Length; ++i)
                {
                    m_FloatingMaterials[i].renderQueue = m_BackgroundMaterials[i].renderQueue + 1;
                    foreach (string keyword in keywords)
                    {
                        m_BackgroundMaterials[i].EnableKeyword(keyword);
                        m_FloatingMaterials[i].EnableKeyword(keyword);
                    }
                }
                m_Background = BuildMesh(floating.Background, "Voxel drag background");
                m_Floating = BuildMesh(floating.Cells, "Voxel drag selection");
                originalRenderer.enabled = false;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private Mesh BuildMesh(IReadOnlyDictionary<Vector3Int, byte> cells, string name)
        {
            var model = new RuntimeVoxDocument.RuntimeModel(name, Vector3Int.one,
                sourceModelId: -1, isCopy: false,
                voxels: cells.ToDictionary(cell => cell.Key, cell => cell.Value));
            return new VoxMeshBuilder().GenerateOptimizedMesh(model, m_Document.Palette,
                m_Materials.PaletteSubmeshIndices, m_Materials.Materials.Count, m_Materials.PaletteOccludesFaces);
        }

        internal void SetPose(Vector3Int pivot, Quaternion rotation, Vector3Int translation)
        {
            m_Pose = TrTransform.TR((Vector3)pivot + translation - rotation * (Vector3)pivot, rotation);
        }

        internal void RebuildBackground(VoxFloatingRegion floating)
        {
            Mesh replacement = BuildMesh(floating.Background, "Voxel drag background");
            Destroy(m_Background);
            m_Background = replacement;
        }

        internal void Draw(TrTransform widgetToWorld)
        {
            if (m_OriginalRenderer == null) return;
            TrTransform anchorToWorld = widgetToWorld * m_Anchor;
            DrawMesh(m_Background, anchorToWorld.ToMatrix4x4(), m_BackgroundMaterials);
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
