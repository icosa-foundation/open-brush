// Copyright 2026 The Open Brush Authors
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
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiltBrush.FrameAnimation
{
    [Flags]
    public enum FrameDrawingProxyIncompatibility
    {
        None = 0,
        MissingCanvas = 1 << 0,
        EmptyDrawing = 1 << 1,
        NonBatchedStroke = 1 << 2,
        MissingBatch = 1 << 3,
        UnsupportedBatch = 1 << 4,
        WidgetContent = 1 << 5,
        AnimatedPath = 1 << 6,
    }

    public readonly struct FrameDrawingProxyCompatibility
    {
        public FrameDrawingProxyIncompatibility Reasons { get; }
        public int StrokeCount { get; }
        public int BatchCount { get; }
        public int WidgetCount { get; }
        public bool IsEligible => Reasons == FrameDrawingProxyIncompatibility.None;

        internal FrameDrawingProxyCompatibility(
            FrameDrawingProxyIncompatibility reasons, int strokeCount, int batchCount,
            int widgetCount)
        {
            Reasons = reasons;
            StrokeCount = strokeCount;
            BatchCount = batchCount;
            WidgetCount = widgetCount;
        }
    }

    /// Classifies one drawing without changing its Canvas, batches, or active state.
    public static class FrameDrawingProxyClassifier
    {
        public static FrameDrawingProxyCompatibility Classify(
            FrameDrawing drawing, IEnumerable<Stroke> strokes,
            IEnumerable<GrabWidget> widgets, bool hasAnimatedPath,
            Func<Batch, bool> supportsBatch = null)
        {
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));
            strokes ??= Enumerable.Empty<Stroke>();
            widgets ??= Enumerable.Empty<GrabWidget>();
            supportsBatch ??= DefaultSupportsBatch;

            FrameDrawingProxyIncompatibility reasons = FrameDrawingProxyIncompatibility.None;
            if (drawing.Canvas == null) reasons |= FrameDrawingProxyIncompatibility.MissingCanvas;
            if (hasAnimatedPath) reasons |= FrameDrawingProxyIncompatibility.AnimatedPath;

            int strokeCount = 0;
            var batches = new HashSet<Batch>();
            foreach (Stroke stroke in strokes)
            {
                if (stroke == null) continue;
                strokeCount++;
                if (stroke.m_Type != Stroke.Type.BatchedBrushStroke)
                {
                    reasons |= FrameDrawingProxyIncompatibility.NonBatchedStroke;
                    continue;
                }
                Batch batch = stroke.m_BatchSubset?.m_ParentBatch;
                if (batch == null)
                {
                    reasons |= FrameDrawingProxyIncompatibility.MissingBatch;
                    continue;
                }
                batches.Add(batch);
                if (!supportsBatch(batch))
                {
                    reasons |= FrameDrawingProxyIncompatibility.UnsupportedBatch;
                }
            }
            if (strokeCount == 0) reasons |= FrameDrawingProxyIncompatibility.EmptyDrawing;

            int widgetCount = widgets.Count(widget =>
                widget != null && !(widget is CameraPathWidget));
            if (widgetCount > 0) reasons |= FrameDrawingProxyIncompatibility.WidgetContent;
            return new FrameDrawingProxyCompatibility(
                reasons, strokeCount, batches.Count, widgetCount);
        }

        private static bool DefaultSupportsBatch(Batch batch)
        {
            if (batch == null || batch.Geometry == null) return false;
            MeshFilter filter = batch.GetComponent<MeshFilter>();
            Renderer renderer = batch.GetComponent<Renderer>();
            return filter != null && filter.sharedMesh != null && renderer != null;
        }
    }

    /// Contract for a playback-only drawing representation. Phase 4B defines this boundary but
    /// deliberately provides no normal-session implementation or activation path.
    public interface IFrameDrawingRenderProxy : IDisposable
    {
        AnimationDrawingId DrawingId { get; }
        long SourceRevision { get; }
        GameObject Root { get; }
        bool IsVisible { get; }
        void Synchronize(FrameDrawing drawing);
        void SetVisible(bool visible);
    }

    /// Owns Unity objects created exclusively for a proxy and destroys each one exactly once.
    public sealed class FrameDrawingProxyResources : IDisposable
    {
        private readonly HashSet<Object> m_OwnedObjects = new();
        private bool m_Disposed;

        public int Count => m_OwnedObjects.Count;

        public T Own<T>(T resource) where T : Object
        {
            if (m_Disposed) throw new ObjectDisposedException(nameof(FrameDrawingProxyResources));
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            m_OwnedObjects.Add(resource);
            return resource;
        }

        public bool Release(Object resource)
        {
            return resource != null && m_OwnedObjects.Remove(resource);
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            foreach (Object resource in m_OwnedObjects.Where(resource => resource != null))
            {
                if (Application.isPlaying) Object.Destroy(resource);
                else Object.DestroyImmediate(resource);
            }
            m_OwnedObjects.Clear();
        }
    }

    public readonly struct FrameDrawingRenderMetrics : IEquatable<FrameDrawingRenderMetrics>
    {
        public int Batches { get; }
        public int Meshes { get; }
        public int Renderers { get; }
        public int MaterialSlots { get; }
        public int Vertices { get; }
        public long Indices { get; }

        public FrameDrawingRenderMetrics(
            int batches, int meshes, int renderers, int materialSlots, int vertices, long indices)
        {
            Batches = batches;
            Meshes = meshes;
            Renderers = renderers;
            MaterialSlots = materialSlots;
            Vertices = vertices;
            Indices = indices;
        }

        public static FrameDrawingRenderMetrics Capture(GameObject root, int batches = 0)
        {
            if (root == null) return default;
            Mesh[] meshes = root.GetComponentsInChildren<MeshFilter>(true)
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Distinct()
                .ToArray();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            long indices = 0;
            foreach (Mesh mesh in meshes)
            {
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    indices += (long)mesh.GetIndexCount(subMesh);
                }
            }
            return new FrameDrawingRenderMetrics(
                batches, meshes.Length, renderers.Length,
                renderers.Sum(renderer => renderer.sharedMaterials.Length),
                meshes.Sum(mesh => mesh.vertexCount), indices);
        }

        public static FrameDrawingRenderMetrics Capture(CanvasScript canvas)
        {
            return canvas == null
                ? default
                : Capture(canvas.gameObject, canvas.BatchManager?.CountBatches() ?? 0);
        }

        public static FrameDrawingRenderMetrics CaptureBatches(CanvasScript canvas)
        {
            if (canvas == null) return default;
            Batch[] batches = canvas.GetComponentsInChildren<Batch>(true);
            Mesh[] meshes = batches
                .Select(batch => batch.GetComponent<MeshFilter>()?.sharedMesh)
                .Where(mesh => mesh != null)
                .Distinct()
                .ToArray();
            Renderer[] renderers = batches
                .Select(batch => batch.GetComponent<Renderer>())
                .Where(renderer => renderer != null)
                .ToArray();
            long indices = 0;
            foreach (Mesh mesh in meshes)
            {
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    indices += (long)mesh.GetIndexCount(subMesh);
                }
            }
            return new FrameDrawingRenderMetrics(
                batches.Length, meshes.Length, renderers.Length,
                renderers.Sum(renderer => renderer.sharedMaterials.Length),
                meshes.Sum(mesh => mesh.vertexCount), indices);
        }

        public bool Equals(FrameDrawingRenderMetrics other)
        {
            return Batches == other.Batches && Meshes == other.Meshes &&
                Renderers == other.Renderers && MaterialSlots == other.MaterialSlots &&
                Vertices == other.Vertices && Indices == other.Indices;
        }

        public override bool Equals(object obj)
        {
            return obj is FrameDrawingRenderMetrics other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Batches;
                hash = (hash * 397) ^ Meshes;
                hash = (hash * 397) ^ Renderers;
                hash = (hash * 397) ^ MaterialSlots;
                hash = (hash * 397) ^ Vertices;
                hash = (hash * 397) ^ Indices.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(
            FrameDrawingRenderMetrics left, FrameDrawingRenderMetrics right) => left.Equals(right);

        public static bool operator !=(
            FrameDrawingRenderMetrics left, FrameDrawingRenderMetrics right) => !left.Equals(right);
    }

    public readonly struct FrameDrawingRenderComparison
    {
        public FrameDrawingRenderMetrics Source { get; }
        public FrameDrawingRenderMetrics Proxy { get; }
        public bool Matches => Source == Proxy;

        public FrameDrawingRenderComparison(
            FrameDrawingRenderMetrics source, FrameDrawingRenderMetrics proxy)
        {
            Source = source;
            Proxy = proxy;
        }
    }
}
