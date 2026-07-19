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
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TiltBrush.FrameAnimation
{
    /// Reusable playback proxy for the batched mesh renderers owned by one Canvas-backed drawing.
    /// Meshes and materials are shared with the source; this object owns only proxy GameObjects.
    public sealed class CanvasBatchRenderProxy : IFrameDrawingRenderProxy
    {
        private FrameDrawingProxyResources m_ContentResources = new();
        private int m_BatchCount;

        public AnimationDrawingId DrawingId { get; private set; }
        public long SourceRevision { get; private set; } = -1;
        public GameObject Root { get; }
        public bool IsVisible => Root.activeSelf;
        public FrameDrawingRenderMetrics Metrics =>
            FrameDrawingRenderMetrics.Capture(Root, m_BatchCount);
        public FrameDrawingRenderComparison LastComparison { get; internal set; }

        public CanvasBatchRenderProxy(int trackId)
        {
            Root = new GameObject($"Animation Render Proxy Track {trackId}");
            Root.SetActive(false);
        }

        public void Synchronize(FrameDrawing drawing)
        {
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));
            if (drawing.Canvas == null)
            {
                throw new InvalidOperationException("A Canvas-backed proxy requires a source Canvas.");
            }
            SynchronizeRootTransform(drawing.Canvas);
            if (DrawingId == drawing.Id && SourceRevision == drawing.ContentRevision) return;

            Batch[] sourceBatches = drawing.Canvas.GetComponentsInChildren<Batch>(true);
            foreach (Batch sourceBatch in sourceBatches)
            {
                MeshFilter sourceFilter = sourceBatch.GetComponent<MeshFilter>();
                MeshRenderer sourceRenderer = sourceBatch.GetComponent<MeshRenderer>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null ||
                    sourceRenderer == null)
                {
                    throw new InvalidOperationException(
                        $"Drawing {drawing.Id.Value} contains an incomplete batch renderer.");
                }
            }

            bool wasVisible = Root.activeSelf;
            Root.SetActive(false);
            foreach (Transform child in Root.transform)
            {
                child.gameObject.SetActive(false);
            }
            m_ContentResources.Dispose();
            m_ContentResources = new FrameDrawingProxyResources();
            m_BatchCount = 0;
            foreach (Batch sourceBatch in sourceBatches)
            {
                CreateBatchRenderer(drawing.Canvas, sourceBatch);
                m_BatchCount++;
            }
            DrawingId = drawing.Id;
            SourceRevision = drawing.ContentRevision;
            Root.SetActive(wasVisible);
        }

        public void SetVisible(bool visible)
        {
            if (Root.activeSelf != visible) Root.SetActive(visible);
        }

        public void Dispose()
        {
            m_ContentResources.Dispose();
            if (Root != null)
            {
                Root.SetActive(false);
                if (Application.isPlaying) Object.Destroy(Root);
                else Object.DestroyImmediate(Root);
            }
        }

        private void SynchronizeRootTransform(CanvasScript canvas)
        {
            Transform source = canvas.transform;
            Root.transform.SetParent(source.parent, false);
            Root.transform.localPosition = source.localPosition;
            Root.transform.localRotation = source.localRotation;
            Root.transform.localScale = source.localScale;
        }

        private void CreateBatchRenderer(CanvasScript canvas, Batch sourceBatch)
        {
            var proxyObject = m_ContentResources.Own(
                new GameObject($"Proxy {sourceBatch.gameObject.name}"));
            proxyObject.transform.SetParent(Root.transform, false);
            Matrix4x4 relative = canvas.transform.worldToLocalMatrix *
                sourceBatch.transform.localToWorldMatrix;
            proxyObject.transform.localPosition = relative.GetColumn(3);
            proxyObject.transform.localRotation = relative.rotation;
            proxyObject.transform.localScale = relative.lossyScale;

            MeshFilter sourceFilter = sourceBatch.GetComponent<MeshFilter>();
            MeshRenderer sourceRenderer = sourceBatch.GetComponent<MeshRenderer>();
            proxyObject.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            MeshRenderer proxyRenderer = proxyObject.AddComponent<MeshRenderer>();
            CopyRendererState(sourceRenderer, proxyRenderer);
        }

        private static void CopyRendererState(MeshRenderer source, MeshRenderer destination)
        {
            destination.sharedMaterials = source.sharedMaterials;
            destination.enabled = source.enabled;
            destination.shadowCastingMode = source.shadowCastingMode;
            destination.receiveShadows = source.receiveShadows;
            destination.lightProbeUsage = source.lightProbeUsage;
            destination.reflectionProbeUsage = source.reflectionProbeUsage;
            destination.motionVectorGenerationMode = source.motionVectorGenerationMode;
            destination.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            destination.sortingLayerID = source.sortingLayerID;
            destination.sortingOrder = source.sortingOrder;
            var propertyBlock = new MaterialPropertyBlock();
            source.GetPropertyBlock(propertyBlock);
            destination.SetPropertyBlock(propertyBlock);
        }
    }

    /// Maintains at most one reusable current-frame proxy per animation track.
    public sealed class FrameDrawingPlaybackProxyController : IDisposable
    {
        private readonly Dictionary<int, CanvasBatchRenderProxy> m_TrackProxies = new();
        private readonly HashSet<int> m_TracksUsedThisFrame = new();

        public int VisibleProxyCount => m_TrackProxies.Values.Count(proxy => proxy.IsVisible);
        public int SynchronizationCount { get; private set; }

        public void BeginFrame()
        {
            m_TracksUsedThisFrame.Clear();
        }

        public bool TryShow(
            int trackId, FrameDrawing drawing, out FrameDrawingRenderComparison comparison)
        {
            comparison = default;
            if (drawing?.Canvas == null) return false;
            if (!m_TrackProxies.TryGetValue(trackId, out CanvasBatchRenderProxy proxy))
            {
                proxy = new CanvasBatchRenderProxy(trackId);
                m_TrackProxies.Add(trackId, proxy);
            }

            bool requiresSynchronization = proxy.DrawingId != drawing.Id ||
                proxy.SourceRevision != drawing.ContentRevision;
            try
            {
                proxy.Synchronize(drawing);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"{AnimationPerformanceStats.LogPrefix} proxySyncFailed " +
                    $"drawing={drawing.Id.Value} track={trackId} error={exception.Message}");
                proxy.SetVisible(false);
                return false;
            }

            if (requiresSynchronization)
            {
                SynchronizationCount++;
                proxy.LastComparison = new FrameDrawingRenderComparison(
                    FrameDrawingRenderMetrics.CaptureBatches(drawing.Canvas), proxy.Metrics);
            }
            comparison = proxy.LastComparison;
            if (!comparison.Matches)
            {
                Debug.LogWarning(
                    $"{AnimationPerformanceStats.LogPrefix} proxyComparisonFailed " +
                    $"drawing={drawing.Id.Value} track={trackId}");
                proxy.SetVisible(false);
                return false;
            }
            proxy.SetVisible(true);
            m_TracksUsedThisFrame.Add(trackId);
            return true;
        }

        public void EndFrame()
        {
            foreach (KeyValuePair<int, CanvasBatchRenderProxy> pair in m_TrackProxies)
            {
                if (!m_TracksUsedThisFrame.Contains(pair.Key)) pair.Value.SetVisible(false);
            }
        }

        public void HideAll()
        {
            m_TracksUsedThisFrame.Clear();
            foreach (CanvasBatchRenderProxy proxy in m_TrackProxies.Values)
            {
                proxy.SetVisible(false);
            }
        }

        public bool TryGetProxy(int trackId, out CanvasBatchRenderProxy proxy)
        {
            return m_TrackProxies.TryGetValue(trackId, out proxy);
        }

        public void Dispose()
        {
            foreach (CanvasBatchRenderProxy proxy in m_TrackProxies.Values) proxy.Dispose();
            m_TrackProxies.Clear();
            m_TracksUsedThisFrame.Clear();
        }
    }
}
