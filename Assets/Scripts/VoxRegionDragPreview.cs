// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    // Only the lifted payload has a separate mesh. The committed background and
    // stamps use the normal chunk renderer.
    internal sealed class VoxRegionDragPreview : IDisposable
    {
        internal readonly IReadOnlyDictionary<Vector3Int, byte> Payload;
        private readonly Mesh m_Mesh;
        private readonly Material[] m_Materials;
        private readonly MaterialPropertyBlock m_Properties = new();
        private readonly TrTransform m_Anchor;
        private TrTransform m_Pose;
        private readonly List<string> m_Keywords = new();

        internal VoxRegionDragPreview(RuntimeVoxDocument document, VoxFloatingRegion region,
            VoxMaterialSet layout, Material[] materials, ushort batchId)
        {
            Payload = region.Payload;
            m_Anchor = VoxMeshBuilder.GetModelTransform(region.AnchorOffset, region.Model.GlobalRotation);
            m_Pose = region.Pose;
            m_Properties.SetFloat("_BatchID", batchId);
            m_Materials = materials.Select(material => new Material(material) { renderQueue = material.renderQueue + 1 }).ToArray();
            m_Mesh = new VoxMeshBuilder().GenerateRegionMesh(region, document.Palette,
                layout.PaletteSubmeshIndices, materials.Length, layout.PaletteOccludesFaces);
        }
        internal void SetPose(TrTransform pose) => m_Pose = pose;
        internal void Draw(TrTransform widget, int layer, IReadOnlyList<string> keywords)
        {
            bool changed = keywords.Count != m_Keywords.Count;
            for (int i = 0; !changed && i < keywords.Count; ++i) changed |= keywords[i] != m_Keywords[i];
            if (changed)
            {
                foreach (var material in m_Materials)
                {
                    foreach (var keyword in m_Keywords) material.DisableKeyword(keyword);
                    foreach (var keyword in keywords) material.EnableKeyword(keyword);
                }
                m_Keywords.Clear();
                m_Keywords.AddRange(keywords);
            }
            Matrix4x4 matrix = (widget * m_Anchor * m_Pose).ToMatrix4x4();
            for (int i = 0; i < m_Mesh.subMeshCount; ++i)
                if (m_Mesh.GetIndexCount(i) != 0)
                    Graphics.DrawMesh(m_Mesh, matrix, m_Materials[i], layer, null, i, m_Properties);
        }
        public void Dispose()
        {
            Destroy(m_Mesh);
            foreach (var material in m_Materials) Destroy(material);
        }
        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}