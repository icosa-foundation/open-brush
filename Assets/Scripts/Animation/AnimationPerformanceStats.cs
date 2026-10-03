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

using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace TiltBrush.FrameAnimation
{
    /// Development-only counters for identifying animation scaling costs. Counter recording is
    /// inert unless explicitly enabled on AnimationUI_Manager.
    internal sealed class AnimationPerformanceStats
    {
#if UNITY_EDITOR
        internal readonly struct CounterSnapshot
        {
            internal long UpdateCalls { get; }
            internal long FocusFrameCalls { get; }
            internal long HideFrameVisits { get; }
            internal long CanvasVisibilityRequests { get; }
            internal long LocationQueries { get; }
            internal long LocationCellsVisited { get; }
            internal long OccupancyQueries { get; }
            internal long TimelineResets { get; }
            internal long LayerEvents { get; }
            internal long GlobalStrokeScans { get; }
            internal long MeshGeometryUploads { get; }
            internal long MeshTopologyUploads { get; }

            internal CounterSnapshot(
                long updateCalls, long focusFrameCalls, long hideFrameVisits,
                long canvasVisibilityRequests, long locationQueries,
                long locationCellsVisited, long occupancyQueries, long timelineResets,
                long layerEvents, long globalStrokeScans, long meshGeometryUploads,
                long meshTopologyUploads)
            {
                UpdateCalls = updateCalls;
                FocusFrameCalls = focusFrameCalls;
                HideFrameVisits = hideFrameVisits;
                CanvasVisibilityRequests = canvasVisibilityRequests;
                LocationQueries = locationQueries;
                LocationCellsVisited = locationCellsVisited;
                OccupancyQueries = occupancyQueries;
                TimelineResets = timelineResets;
                LayerEvents = layerEvents;
                GlobalStrokeScans = globalStrokeScans;
                MeshGeometryUploads = meshGeometryUploads;
                MeshTopologyUploads = meshTopologyUploads;
            }
        }
#endif

        internal readonly struct OperationTimer : IDisposable
        {
            private readonly string m_Operation;
            private readonly Stopwatch m_Stopwatch;

            internal OperationTimer(string operation, bool enabled)
            {
                m_Operation = operation;
                m_Stopwatch = enabled ? Stopwatch.StartNew() : null;
            }

            public void Dispose()
            {
                if (m_Stopwatch == null) return;
                m_Stopwatch.Stop();
                UnityEngine.Debug.Log(
                    $"{LogPrefix} operation={m_Operation} elapsedMs={m_Stopwatch.Elapsed.TotalMilliseconds:F3}");
            }
        }

        private readonly struct DrawingGeometryStats
        {
            internal int Batches { get; }
            internal int BatchPools { get; }
            internal int Vertices { get; }
            internal int Indices { get; }
            internal int Meshes { get; }
            internal int Renderers { get; }
            internal int MaterialSlots { get; }
            internal int MaterialInstances { get; }
            internal long CpuMeshBytes { get; }
            internal long EstimatedGpuMeshBytes { get; }

            internal DrawingGeometryStats(CanvasScript canvas)
            {
                Batches = canvas.BatchManager?.CountBatches() ?? 0;
                BatchPools = canvas.BatchManager?.GetNumBatchPools() ?? 0;
                Vertices = canvas.BatchManager?.CountAllBatchVertices() ?? 0;

                MeshFilter[] filters = canvas.GetComponentsInChildren<MeshFilter>(true);
                List<Mesh> meshes = filters.Select(filter => filter.sharedMesh)
                    .Where(mesh => mesh != null)
                    .Distinct()
                    .ToList();
                Meshes = meshes.Count;
                Renderers = canvas.GetComponentsInChildren<Renderer>(true).Length;
                Renderer[] renderers = canvas.GetComponentsInChildren<Renderer>(true);
                MaterialSlots = renderers.Sum(renderer => renderer.sharedMaterials.Length);
                MaterialInstances = renderers.SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Distinct()
                    .Count();
                Indices = meshes.Sum(GetMeshIndexCount);
                CpuMeshBytes = meshes.Sum(mesh => Profiler.GetRuntimeMemorySizeLong(mesh));
                EstimatedGpuMeshBytes = meshes.Sum(GetEstimatedGpuMeshBytes);
            }

            private static int GetMeshIndexCount(Mesh mesh)
            {
                long count = 0;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    count += (long)mesh.GetIndexCount(subMesh);
                }
                return count > int.MaxValue ? int.MaxValue : (int)count;
            }

            private static long GetEstimatedGpuMeshBytes(Mesh mesh)
            {
                long vertexBytes = 0;
                for (int stream = 0; stream < mesh.vertexBufferCount; stream++)
                {
                    vertexBytes += (long)mesh.vertexCount * mesh.GetVertexBufferStride(stream);
                }
                long indexBytes = (long)GetMeshIndexCount(mesh) *
                    (mesh.indexFormat == IndexFormat.UInt32 ? sizeof(uint) : sizeof(ushort));
                return vertexBytes + indexBytes;
            }
        }

        internal const string LogPrefix = "[OB_ANIM_SCALE]";
        private const float k_LogIntervalSeconds = 5f;

        private readonly AnimationUI_Manager m_Manager;
        private float m_NextLogTime;

        private long m_UpdateCalls;
        private long m_FocusFrameCalls;
        private long m_HideFrameVisits;
        private long m_CanvasVisibilityRequests;
        private long m_LocationQueries;
        private long m_LocationCellsVisited;
        private long m_OccupancyQueries;
        private long m_TimelineResets;
        private static bool s_InstrumentationEnabled;
        private static long s_LayerEvents;
        private static long s_GlobalStrokeScans;
        private static long s_MeshGeometryUploads;
        private static long s_MeshTopologyUploads;

        private bool m_Enabled;
        internal bool Enabled
        {
            get => m_Enabled;
            set
            {
                m_Enabled = value;
                s_InstrumentationEnabled = value;
            }
        }

        internal AnimationPerformanceStats(AnimationUI_Manager manager)
        {
            m_Manager = manager;
        }

        internal void RecordUpdate()
        {
            if (Enabled) m_UpdateCalls++;
        }

        internal void RecordFocusFrame()
        {
            if (Enabled) m_FocusFrameCalls++;
        }

        internal void RecordHideFrameVisit()
        {
            if (Enabled) m_HideFrameVisits++;
        }

        internal void RecordCanvasVisibilityRequest()
        {
            if (Enabled) m_CanvasVisibilityRequests++;
        }

        internal void RecordLocationQuery(int cellsVisited)
        {
            if (!Enabled) return;
            m_LocationQueries++;
            m_LocationCellsVisited += cellsVisited;
        }

        internal void RecordLocationCellsVisited(int cellsVisited)
        {
            if (Enabled) m_LocationCellsVisited += cellsVisited;
        }

        internal void RecordOccupancyQuery()
        {
            if (Enabled) m_OccupancyQueries++;
        }

        internal void RecordTimelineReset()
        {
            if (Enabled) m_TimelineResets++;
        }

        internal static void RecordMeshUpload(bool geometryChanged)
        {
            if (!s_InstrumentationEnabled) return;
            if (geometryChanged) s_MeshGeometryUploads++;
            else s_MeshTopologyUploads++;
        }

        internal static void RecordLayerEvent()
        {
            if (s_InstrumentationEnabled) s_LayerEvents++;
        }

        internal static void RecordGlobalStrokeScan()
        {
            if (s_InstrumentationEnabled) s_GlobalStrokeScans++;
        }

        internal static OperationTimer MeasureOperation(string operation)
        {
            return new OperationTimer(operation, s_InstrumentationEnabled);
        }

#if UNITY_EDITOR
        internal CounterSnapshot CaptureCounters()
        {
            return new CounterSnapshot(
                m_UpdateCalls, m_FocusFrameCalls, m_HideFrameVisits,
                m_CanvasVisibilityRequests, m_LocationQueries, m_LocationCellsVisited,
                m_OccupancyQueries, m_TimelineResets, s_LayerEvents, s_GlobalStrokeScans,
                s_MeshGeometryUploads, s_MeshTopologyUploads);
        }

        internal void ResetCounters()
        {
            ResetIntervalCounters();
        }
#endif

        internal void UpdateAndMaybeLog()
        {
            if (!Enabled || !Debug.isDebugBuild || Time.unscaledTime < m_NextLogTime) return;
            m_NextLogTime = Time.unscaledTime + k_LogIntervalSeconds;

            List<AnimationUI_Manager.Track> timeline = m_Manager.Timeline;
            int tracks = timeline?.Count ?? 0;
            int cells = timeline?.Sum(track => track.Frames?.Count ?? 0) ?? 0;
            List<CanvasScript> uniqueCanvases = timeline == null
                ? new List<CanvasScript>()
                : timeline
                    .SelectMany(track => track.Frames?.SpanFrames ??
                        Enumerable.Empty<AnimationUI_Manager.Frame>())
                    .Select(frame => frame.Canvas)
                    .Where(canvas => canvas != null)
                    .Distinct()
                    .ToList();
            int emptyCells = m_Manager.GetSparseEmptyCellCount();
            m_Manager.GetSparseTimelineCounts(out int spans, out int emptySpans);
            int emptyCanvases = timeline == null
                ? 0
                : timeline
                    .SelectMany(track => track.Frames?.SpanFrames ??
                        Enumerable.Empty<AnimationUI_Manager.Frame>())
                    .Where(frame => frame.EmptySpanId != 0 && frame.Canvas != null)
                    .Select(frame => frame.Canvas)
                    .Distinct()
                    .Count();
            List<(CanvasScript Canvas, AnimationDrawingId Id)> drawings = uniqueCanvases
                .Select(canvas => (Canvas: canvas, HasId: m_Manager.TryGetDrawingIdForStats(
                    canvas, out AnimationDrawingId id), Id: id))
                .Where(item => item.HasId)
                .Select(item => (item.Canvas, item.Id))
                .ToList();
            var drawingStats = drawings.ToDictionary(
                drawing => drawing.Canvas,
                drawing => new DrawingGeometryStats(drawing.Canvas));
            int batches = drawingStats.Values.Sum(stats => stats.Batches);
            int batchPools = drawingStats.Values.Sum(stats => stats.BatchPools);
            int vertices = drawingStats.Values.Sum(stats => stats.Vertices);
            int indices = drawingStats.Values.Sum(stats => stats.Indices);
            int meshes = drawingStats.Values.Sum(stats => stats.Meshes);
            int renderers = drawingStats.Values.Sum(stats => stats.Renderers);
            int materialSlots = drawingStats.Values.Sum(stats => stats.MaterialSlots);
            int materialInstances = drawingStats.Values.Sum(stats => stats.MaterialInstances);
            long meshBytes = drawingStats.Values.Sum(stats => stats.CpuMeshBytes);
            long estimatedGpuMeshBytes = drawingStats.Values.Sum(
                stats => stats.EstimatedGpuMeshBytes);
            int sceneCanvases = App.Scene?.AllCanvases.Count() ?? 0;
            List<(CanvasScript Canvas, AnimationDrawingId Id)> visibleDrawings = drawings
                .Where(drawing => drawing.Canvas.gameObject.activeInHierarchy)
                .ToList();
            int strokes = 0;
            int widgets = 0;
            foreach ((CanvasScript canvas, AnimationDrawingId _) in drawings)
            {
                m_Manager.GetDrawingContentCountsForStats(
                    canvas, out int drawingStrokes, out int drawingWidgets);
                strokes += drawingStrokes;
                widgets += drawingWidgets;
            }
            long managedBytes = GC.GetTotalMemory(false);

            Debug.Log($"{LogPrefix} tracks={tracks} cells={cells} spans={spans} emptyCells={emptyCells} emptySpans={emptySpans} sceneCanvases={sceneCanvases} uniqueDrawingCanvases={drawings.Count} visibleDrawingCanvases={visibleDrawings.Count} emptyCanvases={emptyCanvases} strokes={strokes} widgets={widgets} meshes={meshes} meshBytes={meshBytes} estimatedGpuMeshBytes={estimatedGpuMeshBytes} renderers={renderers} materialSlots={materialSlots} materialInstances={materialInstances} batchPools={batchPools} batches={batches} vertices={vertices} indices={indices} meshGeometryUploads={s_MeshGeometryUploads} meshTopologyUploads={s_MeshTopologyUploads} layerEvents={s_LayerEvents} globalStrokeScans={s_GlobalStrokeScans} updates={m_UpdateCalls} focusCalls={m_FocusFrameCalls} hideVisits={m_HideFrameVisits} visibilityRequests={m_CanvasVisibilityRequests} locationQueries={m_LocationQueries} locationCells={m_LocationCellsVisited} occupancyQueries={m_OccupancyQueries} timelineResets={m_TimelineResets} managedBytes={managedBytes} allocatedBytes={Profiler.GetTotalAllocatedMemoryLong()} reservedBytes={Profiler.GetTotalReservedMemoryLong()} unusedReservedBytes={Profiler.GetTotalUnusedReservedMemoryLong()}");

            foreach ((CanvasScript canvas, AnimationDrawingId drawingId) in drawings)
            {
                DrawingGeometryStats stats = drawingStats[canvas];
                m_Manager.GetDrawingContentCountsForStats(
                    canvas, out int drawingStrokes, out int drawingWidgets);
                Debug.Log($"{LogPrefix} drawing={drawingId.Value} visible={canvas.gameObject.activeInHierarchy} strokes={drawingStrokes} widgets={drawingWidgets} batchPools={stats.BatchPools} batches={stats.Batches} meshes={stats.Meshes} renderers={stats.Renderers} materialSlots={stats.MaterialSlots} materialInstances={stats.MaterialInstances} vertices={stats.Vertices} indices={stats.Indices} meshBytes={stats.CpuMeshBytes} estimatedGpuMeshBytes={stats.EstimatedGpuMeshBytes}");
            }

            Debug.Log($"{LogPrefix} visibleFrame={m_Manager.AppliedFrameForStats} drawings={visibleDrawings.Count} batches={visibleDrawings.Sum(drawing => drawingStats[drawing.Canvas].Batches)} vertices={visibleDrawings.Sum(drawing => drawingStats[drawing.Canvas].Vertices)} indices={visibleDrawings.Sum(drawing => drawingStats[drawing.Canvas].Indices)} meshBytes={visibleDrawings.Sum(drawing => drawingStats[drawing.Canvas].CpuMeshBytes)} estimatedGpuMeshBytes={visibleDrawings.Sum(drawing => drawingStats[drawing.Canvas].EstimatedGpuMeshBytes)}");

            ResetIntervalCounters();
        }

        private void ResetIntervalCounters()
        {
            m_UpdateCalls = 0;
            m_FocusFrameCalls = 0;
            m_HideFrameVisits = 0;
            m_CanvasVisibilityRequests = 0;
            m_LocationQueries = 0;
            m_LocationCellsVisited = 0;
            m_OccupancyQueries = 0;
            m_TimelineResets = 0;
            s_LayerEvents = 0;
            s_GlobalStrokeScans = 0;
            s_MeshGeometryUploads = 0;
            s_MeshTopologyUploads = 0;
        }
    }
}
