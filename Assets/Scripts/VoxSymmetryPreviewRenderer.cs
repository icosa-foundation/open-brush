// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace TiltBrush
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct VoxPreviewBox
    {
        internal Vector4 Min;
        internal Vector4 Max;
        internal Color Color;
    }

    internal static class VoxPreviewBoxes
    {
        // Merge only complete rectangular volumes of identically colored cells.
        // A lattice-aligned cube remains one box; rotated rasterized boundaries
        // retain their steps without issuing a draw call for every voxel.
        internal static void Append(Dictionary<Vector3Int, Color32> cells, HashSet<Vector3Int> visited,
            List<VoxPreviewBox> boxes)
        {
            visited.Clear();
            foreach (var entry in cells)
            {
                var min = entry.Key;
                if (visited.Contains(min)) continue;
                var max = min;
                while (Available(cells, visited, new Vector3Int(max.x + 1, min.y, min.z), entry.Value)) ++max.x;
                while (PlaneAvailable(cells, visited, min.x, max.x, max.y + 1, max.y + 1, min.z, entry.Value)) ++max.y;
                while (PlaneAvailable(cells, visited, min.x, max.x, min.y, max.y, max.z + 1, entry.Value)) ++max.z;
                for (int x = min.x; x <= max.x; ++x)
                    for (int y = min.y; y <= max.y; ++y)
                        for (int z = min.z; z <= max.z; ++z) visited.Add(new Vector3Int(x, y, z));
                Color color = entry.Value;
                color.a = 0.85f;
                boxes.Add(new VoxPreviewBox
                {
                    Min = (Vector3)min - Vector3.one * 0.5f,
                    Max = (Vector3)max + Vector3.one * 0.5f, Color = color
                });
            }
        }

        private static bool Available(Dictionary<Vector3Int, Color32> cells, HashSet<Vector3Int> visited,
            Vector3Int cell, Color32 color)
            => !visited.Contains(cell) && cells.TryGetValue(cell, out var found) && found.Equals(color);

        private static bool PlaneAvailable(Dictionary<Vector3Int, Color32> cells, HashSet<Vector3Int> visited,
            int minX, int maxX, int minY, int maxY, int z, Color32 color)
        {
            for (int x = minX; x <= maxX; ++x)
                for (int y = minY; y <= maxY; ++y)
                    if (!Available(cells, visited, new Vector3Int(x, y, z), color)) return false;
            return true;
        }
    }

    internal sealed class VoxSymmetryPreviewRenderer : IDisposable
    {
        private readonly List<VoxPreviewBox> m_Boxes = new();
        private readonly HashSet<Vector3Int> m_Visited = new();
        private GraphicsBuffer m_Buffer;
        private Material m_Material;
        private MaterialPropertyBlock m_Properties;

        internal void Clear() => m_Boxes.Clear();

        internal void AddInvalid(Vector3Int min, Vector3Int max)
            => m_Boxes.Add(new VoxPreviewBox
            {
                Min = (Vector3)min - Vector3.one * 0.5f,
                Max = (Vector3)max + Vector3.one * 0.5f, Color = new Color(0.9f, 0.12f, 0.08f, 0.85f)
            });

        internal void Draw(TrTransform gridToCanvas, Dictionary<Vector3Int, Color32> cells)
        {
            VoxPreviewBoxes.Append(cells, m_Visited, m_Boxes);
            if (m_Boxes.Count == 0) return;
            TrTransform gridToWorld = App.Scene.ActiveCanvas.Pose * gridToCanvas;
            Vector3 min = m_Boxes[0].Min, max = m_Boxes[0].Max;
            foreach (var box in m_Boxes)
            {
                min = Vector3.Min(min, box.Min);
                max = Vector3.Max(max, box.Max);
            }
            var bounds = new Bounds(gridToWorld * ((min + max) * 0.5f),
                Vector3.one * Mathf.Max(0.1f, (max - min).magnitude * Mathf.Abs(gridToWorld.scale) + 0.1f));
            if (m_Material == null)
            {
                var shader = Resources.Load<Shader>("Shaders/VoxelBrushPreview");
                if (shader != null && shader.isSupported)
                    m_Material = new Material(shader) { enableInstancing = true };
            }
            if (m_Material == null)
            {
                VoxEditPreviewRenderer.DrawQuantizedBoxesFallback(gridToCanvas, m_Boxes, bounds);
                return;
            }
            if (m_Buffer == null || m_Buffer.count < m_Boxes.Count)
            {
                m_Buffer?.Dispose();
                m_Buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                    Mathf.NextPowerOfTwo(m_Boxes.Count), 48);
            }
            m_Buffer.SetData(m_Boxes);
            m_Properties ??= new MaterialPropertyBlock();
            m_Properties.SetBuffer("_Boxes", m_Buffer);
            m_Properties.SetMatrix("_ModelToWorld", gridToWorld.ToMatrix4x4());
            m_Properties.SetFloat("_LineWidth", 0.075f);
            var parameters = new RenderParams(m_Material)
            {
                matProps = m_Properties,
                worldBounds = bounds,
                layer = VoxEditPreviewRenderer.PreviewLayer,
            };
            Graphics.RenderPrimitives(parameters, MeshTopology.Triangles, m_Boxes.Count * 144);
        }

        public void Dispose()
        {
            m_Buffer?.Dispose();
            m_Buffer = null;
            if (m_Material != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(m_Material);
                else UnityEngine.Object.DestroyImmediate(m_Material);
                m_Material = null;
            }
        }
    }
}
