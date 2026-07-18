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

namespace TiltBrush.FrameAnimation
{
    /// Development-only counters for identifying animation scaling costs. Counter recording is
    /// inert unless explicitly enabled on AnimationUI_Manager.
    internal sealed class AnimationPerformanceStats
    {
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
                    .SelectMany(track => track.Frames ?? new List<AnimationUI_Manager.Frame>())
                    .Select(frame => frame.Canvas)
                    .Where(canvas => canvas != null)
                    .Distinct()
                    .ToList();
            int emptyCells = timeline?.Sum(track =>
                track.Frames?.Count(frame => frame.Canvas == null ||
                    !m_Manager.GetFrameFilledWithoutStats(frame.Canvas)) ?? 0) ?? 0;
            m_Manager.GetSparseTimelineCounts(out int spans, out int emptySpans);
            int emptyCanvases = timeline == null
                ? 0
                : timeline
                    .SelectMany(track => track.Frames ?? new List<AnimationUI_Manager.Frame>())
                    .Where(frame => frame.EmptySpanId != 0 && frame.Canvas != null)
                    .Select(frame => frame.Canvas)
                    .Distinct()
                    .Count();
            int batches = uniqueCanvases.Sum(canvas => canvas.BatchManager?.CountBatches() ?? 0);
            int batchPools = uniqueCanvases.Sum(canvas => canvas.BatchManager?.GetNumBatchPools() ?? 0);
            int vertices = uniqueCanvases.Sum(canvas => canvas.BatchManager?.CountAllBatchVertices() ?? 0);
            int triangles = uniqueCanvases.Sum(canvas => canvas.BatchManager?.CountAllBatchTriangles() ?? 0);
            int meshes = uniqueCanvases.Sum(canvas =>
                canvas.GetComponentsInChildren<MeshFilter>(true).Length);
            int renderers = uniqueCanvases.Sum(canvas =>
                canvas.GetComponentsInChildren<Renderer>(true).Length);
            int materialSlots = uniqueCanvases.Sum(canvas =>
                canvas.GetComponentsInChildren<Renderer>(true)
                    .Sum(renderer => renderer.sharedMaterials.Length));
            List<Mesh> meshAssets = uniqueCanvases
                .SelectMany(canvas => canvas.GetComponentsInChildren<MeshFilter>(true))
                .Select(filter => filter.sharedMesh)
                .Where(mesh => mesh != null)
                .Distinct()
                .ToList();
            long meshBytes = meshAssets.Sum(mesh => Profiler.GetRuntimeMemorySizeLong(mesh));
            int materialInstances = uniqueCanvases
                .SelectMany(canvas => canvas.GetComponentsInChildren<Renderer>(true))
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .Distinct()
                .Count();
            int sceneCanvases = App.Scene?.AllCanvases.Count() ?? 0;
            int visibleCanvases = uniqueCanvases.Count(canvas => canvas.gameObject.activeInHierarchy);
            int strokes = 0;
            int widgets = 0;
            foreach (CanvasScript canvas in uniqueCanvases)
            {
                m_Manager.GetDrawingContentCountsForStats(
                    canvas, out int drawingStrokes, out int drawingWidgets);
                strokes += drawingStrokes;
                widgets += drawingWidgets;
            }
            long managedBytes = GC.GetTotalMemory(false);

            Debug.Log($"{LogPrefix} tracks={tracks} cells={cells} spans={spans} emptyCells={emptyCells} emptySpans={emptySpans} sceneCanvases={sceneCanvases} uniqueDrawingCanvases={uniqueCanvases.Count} visibleCanvases={visibleCanvases} emptyCanvases={emptyCanvases} strokes={strokes} widgets={widgets} meshes={meshes} meshBytes={meshBytes} renderers={renderers} materialSlots={materialSlots} materialInstances={materialInstances} batchPools={batchPools} batches={batches} vertices={vertices} triangles={triangles} meshGeometryUploads={s_MeshGeometryUploads} meshTopologyUploads={s_MeshTopologyUploads} layerEvents={s_LayerEvents} globalStrokeScans={s_GlobalStrokeScans} updates={m_UpdateCalls} focusCalls={m_FocusFrameCalls} hideVisits={m_HideFrameVisits} visibilityRequests={m_CanvasVisibilityRequests} locationQueries={m_LocationQueries} locationCells={m_LocationCellsVisited} occupancyQueries={m_OccupancyQueries} timelineResets={m_TimelineResets} managedBytes={managedBytes} allocatedBytes={Profiler.GetTotalAllocatedMemoryLong()} reservedBytes={Profiler.GetTotalReservedMemoryLong()} unusedReservedBytes={Profiler.GetTotalUnusedReservedMemoryLong()}");

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
