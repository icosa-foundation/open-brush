// Copyright 2023 The Open Brush Authors
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
using UnityEngine;
using System;
using TMPro;
using System.Linq;
using UnityEditor;
using UnityEngine.Profiling;

namespace TiltBrush.FrameAnimation
{
    public class AnimationUI_Manager : MonoBehaviour
    {
        int m_Fps = 8;
        float m_FrameOn;
        long m_Start;
        long m_Current;
        long m_Time;
        float m_FrameOffset = 1.22f;
        int m_TrackScrollOffset;
        int m_previousTrackScrollOffset = 0;

        bool m_Playing;
        bool m_Scrolling;
        int m_PreviousShowingFrame = -1;
        int m_PreviousCanvasBatches;
        CanvasScript m_LastCanvas;
        GameObject m_AnimationPathCanvas;
        bool m_AnimationMode = true;
        float m_SliderFrameSize = 0.12f; // Visual size of frame on timeline
        float m_TimelineOffset;

        [Header("Animation Performance Diagnostics")]
        [SerializeField] bool m_LogPerformanceStats;
        [SerializeField] bool m_UseDifferentialPlayback = true;
        [SerializeField] bool m_ShareEmptyCanvases = true;
        [SerializeField] bool m_ValidateSparseTimeline;
        AnimationPerformanceStats m_PerformanceStats;
        readonly Dictionary<CanvasScript, (int, int)> m_CanvasLocations = new();
        readonly Dictionary<CanvasScript, bool> m_DrawingOccupancy = new();
        readonly Dictionary<CanvasScript, HashSet<Stroke>> m_CanvasStrokes = new();
        readonly Dictionary<CanvasScript, HashSet<GrabWidget>> m_CanvasWidgets = new();
        readonly AnimationTimelineModel m_SparseTimeline = new();
        readonly Dictionary<CanvasScript, AnimationDrawingId> m_CanvasDrawingIds = new();
        readonly Dictionary<AnimationDrawingId, CanvasScript> m_DrawingCanvases = new();
        readonly Dictionary<AnimationDrawingId, int> m_UndoDrawingRefCounts = new();
        readonly HashSet<AnimationDrawingId> m_PendingDrawingDestruction = new();
        readonly Dictionary<int, CanvasScript> m_EmptyCanvasByTrackId = new();
        readonly HashSet<CanvasScript> m_EmptyCanvases = new();
        SketchMemoryScript m_SubscribedMemory;
        bool m_TimelineIndexesValid;
        bool m_ContentIndexInitialized;
        bool m_SparseTimelineDirty = true;
        int m_CachedTimelineLength;
        int m_NextTrackId = 1;
        long m_NextDrawingId = 1;
        long m_NextEmptySpanId = 1;

        public List<Track> Timeline;
        public GameObject timelineRef;
        public GameObject timelineNotchPrefab;
        public GameObject timelineFramePrefab;
        public GameObject timelineField;
        public GameObject textRef;
        public GameObject deleteFrameButton;
        public GameObject frameButtonPrefab;
        public List<GameObject> timelineNotches;
        public List<GameObject> timelineFrameObjects;
        public List<GameObject> trackNodesWidget;
        public GameObject frameNotchesWidget;

        int FrameOn => Math.Clamp((int)m_FrameOn, 0, GetTimelineLength() - 1);

        public struct Frame
        {
            public bool Visible;
            public bool Deleted;
            public bool FrameExists;
            public CanvasScript Canvas;
            public CameraPathWidget AnimatedPath;
            public long EmptySpanId;
        }

        public struct DeletedFrame
        {
            public Frame Frame;
            public int Length;
            public (int, int) Location;
        }

        public struct DeleteFrameOperation
        {
            public (int, int) Location;
            public CameraPathWidget RemovedPath;
            internal AnimationTimelineModel.Snapshot PreviousTimeline;
            internal List<CanvasScript> CreatedCanvases;
            internal List<CanvasScript> DisplacedCanvases;

            public bool Succeeded => PreviousTimeline != null;
        }

        public struct AddFrameOperation
        {
            public (int, int) Location;
            internal AnimationTimelineModel.Snapshot PreviousTimeline;
            internal List<CanvasScript> CreatedCanvases;

            public bool Succeeded => PreviousTimeline != null;
        }

        public struct FrameLengthOperation
        {
            public (int, int) Location;
            internal AnimationTimelineModel.Snapshot PreviousTimeline;
            internal List<CanvasScript> CreatedCanvases;
            internal List<CanvasScript> DisplacedCanvases;

            public bool Succeeded => PreviousTimeline != null;
        }

        public struct KeyFrameOperation
        {
            public (int, int) Location;
            public CanvasScript CreatedCanvas;
            public List<Stroke> CreatedStrokes;
            internal AnimationTimelineModel.Snapshot PreviousTimeline;
            internal List<CanvasScript> CreatedCanvases;
            internal List<CanvasScript> DisplacedCanvases;

            public bool Succeeded => CreatedCanvas != null;
        }

        public struct Track
        {
            public int Id;
            public List<Frame> Frames;
            public bool Visible;
            public bool Deleted;
        }

        public Frame NewFrame(CanvasScript canvas)
        {
            Frame thisframeLayer;
            thisframeLayer.Canvas = canvas;
            thisframeLayer.Visible = App.Scene.IsLayerVisible(canvas);
            thisframeLayer.Deleted = false;
            thisframeLayer.FrameExists = true;
            thisframeLayer.AnimatedPath = null;
            thisframeLayer.EmptySpanId = m_EmptyCanvases.Contains(canvas) ? m_NextEmptySpanId++ : 0;
            return thisframeLayer;
        }

        Track NewTrack()
        {
            Track thisFrame;
            thisFrame.Id = m_NextTrackId++;
            thisFrame.Frames = new List<Frame>();
            thisFrame.Visible = true;
            thisFrame.Deleted = false;
            return thisFrame;
        }

        void Awake()
        {
            App.Scene.animationUI_manager = this;
            m_PerformanceStats = new AnimationPerformanceStats(this)
            {
                Enabled = m_LogPerformanceStats
            };
        }

        private void OnDisable()
        {
            RemoveMemorySubscriptions();
        }

        private void EnsureMemorySubscriptions()
        {
            if (m_SubscribedMemory == SketchMemoryScript.m_Instance) return;
            RemoveMemorySubscriptions();
            m_SubscribedMemory = SketchMemoryScript.m_Instance;
            if (m_SubscribedMemory == null) return;
            m_SubscribedMemory.CommandPerformed += OnSketchCommandChanged;
            m_SubscribedMemory.CommandUndo += OnSketchCommandChanged;
            m_SubscribedMemory.CommandRedo += OnSketchCommandChanged;
        }

        private void RemoveMemorySubscriptions()
        {
            if (m_SubscribedMemory == null) return;
            m_SubscribedMemory.CommandPerformed -= OnSketchCommandChanged;
            m_SubscribedMemory.CommandUndo -= OnSketchCommandChanged;
            m_SubscribedMemory.CommandRedo -= OnSketchCommandChanged;
            m_SubscribedMemory = null;
        }

        private void OnSketchCommandChanged(BaseCommand command)
        {
            m_ContentIndexInitialized = false;
            InvalidateDrawingOccupancy();
        }

        private void InvalidateDrawingOccupancy()
        {
            m_DrawingOccupancy.Clear();
        }

        public void NotifyDrawingContentChanged(CanvasScript canvas)
        {
            if (canvas == null) return;
            m_DrawingOccupancy.Remove(canvas);
            if (m_EmptyCanvases.Contains(canvas) && GetFrameFilledWithoutStats(canvas))
            {
                PromoteEmptyCanvas(canvas);
            }
        }

        public void NotifyStrokeAdded(Stroke stroke)
        {
            if (stroke?.Canvas == null) return;
            EnsureDrawingContentIndex();
            AddIndexedContent(m_CanvasStrokes, stroke.Canvas, stroke);
            NotifyDrawingContentChanged(stroke.Canvas);
        }

        public void NotifyStrokeRemoved(Stroke stroke)
        {
            if (stroke?.Canvas == null) return;
            EnsureDrawingContentIndex();
            RemoveIndexedContent(m_CanvasStrokes, stroke.Canvas, stroke);
            NotifyDrawingContentChanged(stroke.Canvas);
        }

        public void NotifyWidgetAdded(GrabWidget widget)
        {
            if (widget?.Canvas == null || widget is CameraPathWidget) return;
            EnsureDrawingContentIndex();
            AddIndexedContent(m_CanvasWidgets, widget.Canvas, widget);
            NotifyDrawingContentChanged(widget.Canvas);
        }

        public void NotifyWidgetRemoved(GrabWidget widget)
        {
            if (widget?.Canvas == null || widget is CameraPathWidget) return;
            EnsureDrawingContentIndex();
            RemoveIndexedContent(m_CanvasWidgets, widget.Canvas, widget);
            NotifyDrawingContentChanged(widget.Canvas);
        }

        public void NotifyWidgetCanvasChanged(
            GrabWidget widget, CanvasScript previousCanvas, CanvasScript nextCanvas)
        {
            if (widget == null || widget is CameraPathWidget) return;
            EnsureDrawingContentIndex();
            if (previousCanvas != null)
            {
                RemoveIndexedContent(m_CanvasWidgets, previousCanvas, widget);
                NotifyDrawingContentChanged(previousCanvas);
            }
            if (nextCanvas != null)
            {
                AddIndexedContent(m_CanvasWidgets, nextCanvas, widget);
                NotifyDrawingContentChanged(nextCanvas);
            }
        }

        private static void AddIndexedContent<T>(
            Dictionary<CanvasScript, HashSet<T>> index, CanvasScript canvas, T content)
        {
            if (!index.TryGetValue(canvas, out HashSet<T> contents))
            {
                contents = new HashSet<T>();
                index.Add(canvas, contents);
            }
            contents.Add(content);
        }

        private static void RemoveIndexedContent<T>(
            Dictionary<CanvasScript, HashSet<T>> index, CanvasScript canvas, T content)
        {
            if (!index.TryGetValue(canvas, out HashSet<T> contents)) return;
            contents.Remove(content);
            if (contents.Count == 0) index.Remove(canvas);
        }

        private void EnsureDrawingContentIndex()
        {
            if (m_ContentIndexInitialized) return;
            m_CanvasStrokes.Clear();
            m_CanvasWidgets.Clear();
            if (SketchMemoryScript.m_Instance != null)
            {
                foreach (Stroke stroke in SketchMemoryScript.m_Instance.GetMemoryList)
                {
                    if (stroke.Canvas != null)
                    {
                        AddIndexedContent(m_CanvasStrokes, stroke.Canvas, stroke);
                    }
                }
            }
            if (Timeline != null)
            {
                foreach (CanvasScript canvas in Timeline
                    .SelectMany(track => track.Frames)
                    .Select(frame => frame.Canvas)
                    .Where(canvas => canvas != null)
                    .Distinct())
                {
                    foreach (GrabWidget widget in canvas.GetComponentsInChildren<GrabWidget>(true))
                    {
                        if (widget is CameraPathWidget) continue;
                        AddIndexedContent(m_CanvasWidgets, canvas, widget);
                    }
                }
            }
            m_ContentIndexInitialized = true;
        }

        private void InvalidateTimelineStructure()
        {
            m_TimelineIndexesValid = false;
            m_SparseTimelineDirty = true;
            m_CanvasLocations.Clear();
            InvalidateDrawingOccupancy();
        }

        private AnimationDrawingId GetOrCreateDrawingId(CanvasScript canvas)
        {
            if (canvas == null || m_EmptyCanvases.Contains(canvas)) return AnimationDrawingId.Empty;
            if (m_CanvasDrawingIds.TryGetValue(canvas, out AnimationDrawingId drawingId))
            {
                return drawingId;
            }

            drawingId = new AnimationDrawingId(m_NextDrawingId++);
            m_CanvasDrawingIds.Add(canvas, drawingId);
            m_DrawingCanvases.Add(drawingId, canvas);
            return drawingId;
        }

        private void EnsureSparseTimeline()
        {
            if (!m_SparseTimelineDirty) return;

            RebuildEmptyCanvasRegistry();
            var trackIds = new List<int>();
            var trackVisibility = new List<bool>();
            var trackDeletion = new List<bool>();
            var modelFrames = new List<IReadOnlyList<AnimationTimelineModel.FrameValue>>();
            if (Timeline != null)
            {
                for (int trackIndex = 0; trackIndex < Timeline.Count; trackIndex++)
                {
                    Track track = Timeline[trackIndex];
                    if (track.Id == 0)
                    {
                        track.Id = m_NextTrackId++;
                        Timeline[trackIndex] = track;
                    }
                    trackIds.Add(track.Id);
                    trackVisibility.Add(track.Visible);
                    trackDeletion.Add(track.Deleted);
                    modelFrames.Add(track.Frames.Select(frame =>
                        new AnimationTimelineModel.FrameValue(
                            GetOrCreateDrawingId(frame.Canvas), frame.Deleted, frame.FrameExists,
                            frame.AnimatedPath, frame.EmptySpanId)).ToList());
                }
            }

            m_SparseTimeline.Rebuild(trackIds, trackVisibility, trackDeletion, modelFrames);
            m_CachedTimelineLength = m_SparseTimeline.Length;
            m_SparseTimelineDirty = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_ValidateSparseTimeline) ValidateSparseTimelineProjection();
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [ContextMenu("Validate Sparse Animation Timeline")]
        private void ValidateSparseTimeline()
        {
            EnsureSparseTimeline();
            ValidateSparseTimelineProjection();
        }

        private void ValidateSparseTimelineProjection()
        {
            if (Timeline == null || m_SparseTimeline.Tracks.Count != Timeline.Count)
            {
                Debug.LogError(
                    $"{AnimationPerformanceStats.LogPrefix} sparseMismatch=trackCount");
                return;
            }

            for (int trackIndex = 0; trackIndex < Timeline.Count; trackIndex++)
            {
                Track denseTrack = Timeline[trackIndex];
                AnimationTimelineModel.Track sparseTrack = m_SparseTimeline.Tracks[trackIndex];
                if (sparseTrack.Id != denseTrack.Id || sparseTrack.Visible != denseTrack.Visible ||
                    sparseTrack.Deleted != denseTrack.Deleted ||
                    sparseTrack.Length != denseTrack.Frames.Count)
                {
                    Debug.LogError(
                        $"{AnimationPerformanceStats.LogPrefix} sparseMismatch=track track={trackIndex}");
                    return;
                }

                for (int frameIndex = 0; frameIndex < denseTrack.Frames.Count; frameIndex++)
                {
                    Frame denseFrame = denseTrack.Frames[frameIndex];
                    var expected = new AnimationTimelineModel.FrameValue(
                        GetOrCreateDrawingId(denseFrame.Canvas), denseFrame.Deleted,
                        denseFrame.FrameExists, denseFrame.AnimatedPath, denseFrame.EmptySpanId);
                    if (!sparseTrack.TryResolve(
                            frameIndex, out AnimationTimelineModel.Span sparseSpan) ||
                        !sparseSpan.Value.Equals(expected))
                    {
                        Debug.LogError(
                            $"{AnimationPerformanceStats.LogPrefix} sparseMismatch=frame track={trackIndex} frame={frameIndex}");
                        return;
                    }
                }
            }

            Debug.Log($"{AnimationPerformanceStats.LogPrefix} sparseValidation=passed");
        }
#endif

        private void RebuildEmptyCanvasRegistry()
        {
            m_EmptyCanvasByTrackId.Clear();
            m_EmptyCanvases.Clear();
            if (Timeline == null) return;

            foreach (Track track in Timeline)
            {
                if (track.Frames == null) continue;
                foreach (Frame frame in track.Frames)
                {
                    if (frame.EmptySpanId == 0 || frame.Canvas == null) continue;
                    m_EmptyCanvases.Add(frame.Canvas);
                    if (!m_EmptyCanvasByTrackId.ContainsKey(track.Id))
                    {
                        m_EmptyCanvasByTrackId.Add(track.Id, frame.Canvas);
                    }
                }
            }
        }

        private CanvasScript GetCanvasForDrawing(AnimationDrawingId drawingId)
        {
            return !drawingId.IsEmpty && m_DrawingCanvases.TryGetValue(drawingId, out CanvasScript canvas)
                ? canvas
                : null;
        }

        private void DestroyTimelineCanvas(
            CanvasScript canvas, IEnumerable<Stroke> createdStrokes = null)
        {
            if (canvas == null) return;
            if (m_CanvasDrawingIds.TryGetValue(canvas, out AnimationDrawingId drawingId))
            {
                if (m_UndoDrawingRefCounts.TryGetValue(drawingId, out int references) &&
                    references > 0)
                {
                    m_PendingDrawingDestruction.Add(drawingId);
                    return;
                }
                m_PendingDrawingDestruction.Remove(drawingId);
                m_CanvasDrawingIds.Remove(canvas);
                m_DrawingCanvases.Remove(drawingId);
            }
            m_EmptyCanvases.Remove(canvas);
            m_CanvasStrokes.Remove(canvas);
            m_CanvasWidgets.Remove(canvas);
            m_DrawingOccupancy.Remove(canvas);
            foreach (int trackId in m_EmptyCanvasByTrackId
                .Where(pair => pair.Value == canvas)
                .Select(pair => pair.Key)
                .ToList())
            {
                m_EmptyCanvasByTrackId.Remove(trackId);
            }

            if (createdStrokes == null) App.Scene.DestroyCanvas(canvas);
            else App.Scene.DestroyCanvas(canvas, createdStrokes);
        }

        private CanvasScript GetOrCreateEmptyCanvas(int trackIndex, CanvasScript preferredCanvas = null)
        {
            if (trackIndex < 0 || trackIndex >= Timeline.Count) return preferredCanvas;
            int trackId = Timeline[trackIndex].Id;
            if (!m_ShareEmptyCanvases)
            {
                CanvasScript distinctCanvas = preferredCanvas != null
                    ? preferredCanvas
                    : App.Scene.AddCanvas();
                m_EmptyCanvases.Add(distinctCanvas);
                if (!m_EmptyCanvasByTrackId.ContainsKey(trackId))
                {
                    m_EmptyCanvasByTrackId.Add(trackId, distinctCanvas);
                }
                return distinctCanvas;
            }
            if (m_EmptyCanvasByTrackId.TryGetValue(trackId, out CanvasScript canvas) && canvas != null)
            {
                return canvas;
            }

            bool createdCanvas = preferredCanvas == null;
            canvas = preferredCanvas != null ? preferredCanvas : App.Scene.AddCanvas();
            m_EmptyCanvasByTrackId[trackId] = canvas;
            m_EmptyCanvases.Add(canvas);
            if (createdCanvas)
            {
                canvas.gameObject.name = $"Animation Empty Track {trackId}";
            }
            return canvas;
        }

        private void PromoteEmptyCanvas(CanvasScript populatedCanvas)
        {
            int trackIndex = -1;
            for (int i = 0; i < Timeline.Count; i++)
            {
                if (Timeline[i].Frames.Any(frame => frame.Canvas == populatedCanvas))
                {
                    trackIndex = i;
                    break;
                }
            }
            if (trackIndex < 0) return;

            int selectedFrame = Math.Clamp(FrameOn, 0, Timeline[trackIndex].Frames.Count - 1);
            EnsureSparseTimeline();
            if (!m_SparseTimeline.TryResolve(
                trackIndex, selectedFrame, out AnimationTimelineModel.Span selectedSpan))
            {
                return;
            }
            if (!m_ShareEmptyCanvases)
            {
                m_EmptyCanvases.Remove(populatedCanvas);
                Track distinctTrack = Timeline[trackIndex];
                for (int frameIndex = selectedSpan.StartFrame;
                    frameIndex < selectedSpan.EndFrameExclusive; frameIndex++)
                {
                    Frame frame = distinctTrack.Frames[frameIndex];
                    frame.EmptySpanId = 0;
                    distinctTrack.Frames[frameIndex] = frame;
                }
                Timeline[trackIndex] = distinctTrack;
                InvalidateTimelineStructure();
                return;
            }
            CanvasScript replacement = App.Scene.AddCanvas();
            replacement.gameObject.name = populatedCanvas.gameObject.name;
            replacement.LocalPose = populatedCanvas.LocalPose;
            replacement.gameObject.SetActive(populatedCanvas.gameObject.activeSelf);
            m_EmptyCanvases.Remove(populatedCanvas);
            m_EmptyCanvasByTrackId[Timeline[trackIndex].Id] = replacement;
            m_EmptyCanvases.Add(replacement);

            Track track = Timeline[trackIndex];
            for (int frameIndex = 0; frameIndex < track.Frames.Count; frameIndex++)
            {
                if (track.Frames[frameIndex].Canvas != populatedCanvas)
                {
                    continue;
                }
                Frame frame = track.Frames[frameIndex];
                if (selectedSpan.Contains(frameIndex))
                {
                    frame.EmptySpanId = 0;
                }
                else
                {
                    frame.Canvas = replacement;
                }
                track.Frames[frameIndex] = frame;
            }
            Timeline[trackIndex] = track;
            InvalidateTimelineStructure();
        }

        public CanvasScript GetOrCreateContentCanvas(int trackIndex, int frameIndex)
        {
            EnsureSparseTimeline();
            if (!m_SparseTimeline.TryResolve(
                trackIndex, frameIndex, out AnimationTimelineModel.Span span))
            {
                return App.Scene.MainCanvas;
            }

            CanvasScript existing = GetCanvasForDrawing(span.Value.DrawingId);
            if (existing != null) return existing;

            Track track = Timeline[trackIndex];
            CanvasScript emptyCanvas = track.Frames[span.StartFrame].Canvas;
            CanvasScript contentCanvas = App.Scene.AddCanvas();
            contentCanvas.gameObject.name = emptyCanvas != null
                ? emptyCanvas.gameObject.name
                : $"Animation Drawing Track {track.Id}";
            if (emptyCanvas != null)
            {
                contentCanvas.LocalPose = emptyCanvas.LocalPose;
                contentCanvas.gameObject.SetActive(emptyCanvas.gameObject.activeSelf);
            }
            for (int i = span.StartFrame; i < span.EndFrameExclusive; i++)
            {
                Frame frame = track.Frames[i];
                frame.Canvas = contentCanvas;
                frame.EmptySpanId = 0;
                track.Frames[i] = frame;
            }
            Timeline[trackIndex] = track;
            InvalidateTimelineStructure();
            return contentCanvas;
        }

        private void EnsureTimelineIndexes()
        {
            if (m_TimelineIndexesValid) return;

            RebuildTimelineIndexes();
        }

        private void RebuildTimelineIndexes()
        {
            EnsureSparseTimeline();

            m_CanvasLocations.Clear();
            if (Timeline != null)
            {
                for (int trackIndex = 0; trackIndex < Timeline.Count; trackIndex++)
                {
                    List<Frame> frames = Timeline[trackIndex].Frames;
                    if (frames == null) continue;
                    for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
                    {
                        CanvasScript canvas = frames[frameIndex].Canvas;
                        if (canvas != null && !m_CanvasLocations.ContainsKey(canvas))
                        {
                            m_CanvasLocations.Add(canvas, (trackIndex, frameIndex));
                        }
                    }
                }
            }
            m_TimelineIndexesValid = true;
        }

        private bool LocationStillMatches(CanvasScript canvas, (int, int) location)
        {
            return location.Item1 >= 0 && location.Item1 < Timeline.Count &&
                location.Item2 >= 0 && location.Item2 < Timeline[location.Item1].Frames.Count &&
                Timeline[location.Item1].Frames[location.Item2].Canvas == canvas;
        }

        public void StartTimeline()
        {
            InvalidateTimelineStructure();
            m_EmptyCanvasByTrackId.Clear();
            m_EmptyCanvases.Clear();
            m_CanvasDrawingIds.Clear();
            m_DrawingCanvases.Clear();
            m_UndoDrawingRefCounts.Clear();
            m_PendingDrawingDestruction.Clear();
            m_CanvasStrokes.Clear();
            m_CanvasWidgets.Clear();
            m_ContentIndexInitialized = false;
            m_NextTrackId = 1;
            m_NextDrawingId = 1;
            m_NextEmptySpanId = 1;
            Timeline = new List<Track>();
            Track mainTrack = NewTrack();
            Frame originFrame = NewFrame(App.Scene.m_MainCanvas);
            mainTrack.Frames.Add(originFrame);
            Timeline.Add(mainTrack);
            App.Scene.animationUI_manager = this;
            FocusFrame(0);
            timelineNotches = new List<GameObject>();
            timelineFrameObjects = new List<GameObject>();
            ResetTimeline();

            if (m_AnimationPathCanvas == null)
            {
                m_AnimationPathCanvas = new GameObject("AnimationPaths");
                m_AnimationPathCanvas.transform.parent = App.Scene.gameObject.transform;
                m_AnimationPathCanvas.AddComponent<CanvasScript>();
            }
        }

        private void HideFrame(int hidingFrame, int frameOn)
        {
            foreach (Track track in Timeline)
            {
                m_PerformanceStats?.RecordHideFrameVisit();
                if (hidingFrame >= track.Frames.Count) { continue; }
                if (frameOn < track.Frames.Count && track.Frames[hidingFrame].Canvas.Equals(track.Frames[frameOn].Canvas)) continue;

                Frame thisFrame = track.Frames[hidingFrame];

                m_PerformanceStats?.RecordCanvasVisibilityRequest();
                App.Scene.HideCanvas(thisFrame.Canvas);
                thisFrame.Visible = false;
                track.Frames[hidingFrame] = thisFrame;
            }
        }

        private void ShowFrame(int frameIndex)
        {
            if (m_PreviousShowingFrame == frameIndex) return;

            for (int i = 0; i < Timeline.Count; i++)
            {
                if (frameIndex >= Timeline[i].Frames.Count) { continue; }
                Frame thisFrame = Timeline[i].Frames[frameIndex];
                if (Timeline[i].Visible && !thisFrame.Deleted && !Timeline[i].Deleted)
                {
                    m_PerformanceStats?.RecordCanvasVisibilityRequest();
                    App.Scene.ShowCanvas(thisFrame.Canvas);
                }
                else
                {
                    m_PerformanceStats?.RecordCanvasVisibilityRequest();
                    App.Scene.HideCanvas(thisFrame.Canvas);
                    thisFrame.Visible = false;
                }
                Timeline[i].Frames[frameIndex] = thisFrame;
            }
            m_PreviousShowingFrame = frameIndex;
        }

        public bool GetFrameFilled(int track, int frame)
        {
            m_PerformanceStats?.RecordOccupancyQuery();
            if (track < 0 || track >= Timeline.Count ||
                frame < 0 || frame >= Timeline[track].Frames.Count)
            {
                return false;
            }

            Frame timelineFrame = Timeline[track].Frames[frame];
            return timelineFrame.AnimatedPath != null || GetFrameFilledWithoutStats(timelineFrame.Canvas);
        }

        internal bool GetFrameFilledWithoutStats(CanvasScript canvas)
        {
            if (canvas == null) return false;
            if (m_DrawingOccupancy.TryGetValue(canvas, out bool occupied)) return occupied;
            EnsureDrawingContentIndex();
            bool hasActiveStrokes = m_CanvasStrokes.TryGetValue(
                canvas, out HashSet<Stroke> strokes) && strokes.Any(stroke =>
                stroke.IsGeometryEnabled &&
                (stroke.m_Type != Stroke.Type.BatchedBrushStroke ||
                    stroke.m_BatchSubset.m_VertLength > 0));
            bool hasActiveWidgets = m_CanvasWidgets.TryGetValue(
                canvas, out HashSet<GrabWidget> widgets) && widgets.Any(widget =>
                widget != null && widget.gameObject.activeSelf &&
                widget.transform.IsChildOf(canvas.transform));
            occupied = hasActiveStrokes || hasActiveWidgets;
            m_DrawingOccupancy[canvas] = occupied;
            return occupied;
        }

        public bool GetFrameFilled(CanvasScript canvas)
        {
            (int, int) loc = GetCanvasLocation(canvas);
            return GetFrameFilled(loc.Item1, loc.Item2);
        }

        private bool IsTimelineLocationValid(int track, int frame)
        {
            return track >= 0 && track < Timeline.Count &&
                frame >= 0 && frame < Timeline[track].Frames.Count;
        }

        public bool CanAddKeyFrame(int track, int frame)
        {
            if (!IsTimelineLocationValid(track, frame)) return false;

            (int, int) nextIndex = GetFollowingFrameIndex(track, frame);
            return nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count ||
                GetFrameFilled(nextIndex.Item1, nextIndex.Item2);
        }

        public void SelectFollowingEmptyFrame(int track, int frame)
        {
            if (!IsTimelineLocationValid(track, frame)) return;

            (int, int) nextIndex = GetFollowingFrameIndex(track, frame);
            if (nextIndex.Item2 < Timeline[nextIndex.Item1].Frames.Count &&
                !GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
            {
                SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);
            }
        }

        public bool CanDeleteKeyFrame(int track, int frame)
        {
            return IsTimelineLocationValid(track, frame) &&
                (track != 0 || frame != 0) && GetFrameFilled(track, frame);
        }

        public bool CanMoveKeyFrame(bool moveRight, int track, int frame)
        {
            if (!IsTimelineLocationValid(track, frame) || !GetFrameFilled(track, frame))
            {
                return false;
            }

            if (!moveRight)
            {
                return frame > 0 && !GetFrameFilled(track, frame - 1);
            }

            (int, int) nextIndex = GetFollowingFrameIndex(track, frame);
            return nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count ||
                !GetFrameFilled(nextIndex.Item1, nextIndex.Item2);
        }

        public bool CanSplitKeyFrame(int track, int frame)
        {
            if (!IsTimelineLocationValid(track, frame) || !GetFrameFilled(track, frame))
            {
                return false;
            }

            int frameLength = GetFrameLength(track, frame);
            return FrameOn > frame && FrameOn < frame + frameLength;
        }

        public bool CanDuplicateKeyFrame(int track, int frame)
        {
            return IsTimelineLocationValid(track, frame) && GetFrameFilled(track, frame);
        }

        public void AddAnimationPath(CameraPathWidget pathwidget, int trackNum, int frameNum)
        {
            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(m_AnimationPathCanvas.transform);

            pathwidget.SetPathAnimation(true);

            (int, int) Loc = (trackNum, frameNum);
            pathwidget.Path.timelineLocation = Loc;

            if (Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath != null)
            {
                WidgetManager.m_Instance.DeleteCameraPath(Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath);
            }

            int i = GetFollowingFrameIndex(Loc.Item1, Loc.Item2).Item2 - Loc.Item2;

            for (int c = 0; c < i; c++)
            {
                Frame changingFrame = Timeline[Loc.Item1].Frames[Loc.Item2 + c];
                changingFrame.AnimatedPath = pathwidget;
                Timeline[Loc.Item1].Frames[Loc.Item2 + c] = changingFrame;
            }
        }

        public void AddAnimationPath(CameraPathWidget pathwidget)
        {
            GameObject moveTransform = pathwidget.gameObject;
            moveTransform.transform.SetParent(m_AnimationPathCanvas.transform);
            pathwidget.SetPathAnimation(true);
            (int, int) Loc = GetCanvasLocation(App.Scene.ActiveCanvas);
            pathwidget.Path.timelineLocation = Loc;

            if (!GetFrameFilled(Loc.Item1, Loc.Item2))
            {
                TiltBrush.WidgetManager.m_Instance.UnregisterGrabWidget(pathwidget.gameObject);
                Destroy(pathwidget.gameObject);
                return;
            }

            if (Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath != null)
            {
                WidgetManager.m_Instance.DeleteCameraPath(Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath);
            }

            List<Frame> framesChanging = new List<Frame>();
            int i = GetFollowingFrameIndex(Loc.Item1, Loc.Item2).Item2 - Loc.Item2;

            for (int c = 0; c < i; c++)
            {
                Frame changingFrame = Timeline[Loc.Item1].Frames[Loc.Item2 + c];
                changingFrame.AnimatedPath = pathwidget;
                Timeline[Loc.Item1].Frames[Loc.Item2 + c] = changingFrame;
            }

            ResetTimeline();
        }

        public CanvasScript AddLayerRefresh(CanvasScript canvasAdding)
        {
            InvalidateTimelineStructure();
            int timelineLength = GetTimelineLength();
            Track addingTrack = NewTrack();
            m_EmptyCanvasByTrackId[addingTrack.Id] = canvasAdding;
            m_EmptyCanvases.Add(canvasAdding);
            Frame addingFrame;
            for (int i = 0; i < timelineLength; i++)
            {
                CanvasScript emptyCanvas = m_ShareEmptyCanvases || i == 0
                    ? canvasAdding
                    : App.Scene.AddCanvas();
                m_EmptyCanvases.Add(emptyCanvas);
                addingFrame = NewFrame(emptyCanvas);
                addingTrack.Frames.Add(addingFrame);
            }
            Timeline.Add(addingTrack);
            ResetTimeline();
            return canvasAdding;
        }

        public (int, int) GetCanvasLocation(CanvasScript canvas)
        {
            EnsureSparseTimeline();
            m_PerformanceStats?.RecordLocationQuery(canvas != null ? 1 : 0);
            if (canvas == null) return (-1, -1);
            if (m_EmptyCanvases.Contains(canvas))
            {
                for (int trackIndex = 0; trackIndex < Timeline.Count; trackIndex++)
                {
                    List<Frame> frames = Timeline[trackIndex].Frames;
                    if (FrameOn >= 0 && FrameOn < frames.Count && frames[FrameOn].Canvas == canvas)
                    {
                        return (trackIndex, FrameOn);
                    }
                    int frameIndex = frames.FindIndex(frame => frame.Canvas == canvas);
                    if (frameIndex >= 0) return (trackIndex, frameIndex);
                }
                return (-1, -1);
            }

            AnimationDrawingId drawingId = GetOrCreateDrawingId(canvas);
            if (m_SparseTimeline.TryGetDrawingLocation(drawingId, out (int, int) location) &&
                LocationStillMatches(canvas, location))
            {
                return location;
            }

            // Timeline is temporarily exposed as a mutable compatibility view. Detect callers
            // that mutate it without invalidating the sparse projection and recover safely.
            m_SparseTimelineDirty = true;
            EnsureSparseTimeline();
            return m_SparseTimeline.TryGetDrawingLocation(drawingId, out location) ? location : (-1, -1);
        }

        public CanvasScript GetTimelineCanvas(int trackIndex, int frameIndex)
        {
            EnsureSparseTimeline();
            if (m_SparseTimeline.TryResolve(trackIndex, frameIndex, out AnimationTimelineModel.Span span))
            {
                CanvasScript canvas = GetCanvasForDrawing(span.Value.DrawingId);
                if (canvas != null) return canvas;
                return !m_ShareEmptyCanvases && frameIndex < Timeline[trackIndex].Frames.Count
                    ? Timeline[trackIndex].Frames[frameIndex].Canvas
                    : GetOrCreateEmptyCanvas(trackIndex);
            }
            return App.Scene.MainCanvas;
        }

        public List<List<CanvasScript>> GetTrackCanvases()
        {
            EnsureSparseTimeline();
            var timelineCanvases = new List<List<CanvasScript>>();
            for (int l = 0; l < GetTimelineLength(); l++)
            {
                var canvasFrames = new List<CanvasScript>();
                for (int i = 0; i < Timeline.Count; i++)
                {
                    if (l >= Timeline[i].Frames.Count || Timeline[i].Deleted == true) { continue; }
                    canvasFrames.Add(GetTimelineCanvas(i, l));
                }
                timelineCanvases.Add(canvasFrames);
            }
            return timelineCanvases;
        }

        public List<int> GetTrackFrameLengths(int trackIndex)
        {
            EnsureSparseTimeline();
            if (trackIndex < 0 || trackIndex >= m_SparseTimeline.Tracks.Count)
            {
                return new List<int>();
            }
            return m_SparseTimeline.Tracks[trackIndex].Spans
                .Select(span => span.Duration)
                .ToList();
        }

        internal void GetSparseTimelineCounts(out int spanCount, out int emptySpanCount)
        {
            EnsureSparseTimeline();
            spanCount = m_SparseTimeline.Tracks.Sum(track => track.Spans.Count);
            emptySpanCount = m_SparseTimeline.Tracks.Sum(track =>
                track.Spans.Count(span => span.Value.DrawingId.IsEmpty));
        }

        public List<int> ActiveTrackIndexes()
        {
            List<int> activeTrackIndexes = new();
            for (int trackIndex = 0; trackIndex < Timeline.Count; trackIndex++)
            {
                if (Timeline[trackIndex].Deleted) continue;
                activeTrackIndexes.Add(trackIndex);
            }
            return activeTrackIndexes;
        }

        public void UpdateLayerVisibilityRefresh(CanvasScript canvas)
        {
            bool visible = canvas.gameObject.activeSelf;
            (int, int) canvasIndex = GetCanvasLocation(canvas);

            if (canvasIndex.Item2 != -1)
            {
                Track thisTrack = Timeline[canvasIndex.Item1];
                thisTrack.Visible = visible;

                for (int i = 0; i < thisTrack.Frames.Count; i++)
                {
                    Frame changingFrame = thisTrack.Frames[i];
                    changingFrame.Visible = visible;
                    thisTrack.Frames[i] = changingFrame;
                }
                Timeline[canvasIndex.Item1] = thisTrack;
                InvalidateTimelineStructure();
            }
        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas)
        {
            (int, int) canvasIndex = GetCanvasLocation(canvas);
            if (canvasIndex.Item2 != -1)
            {
                Track thisTrack = Timeline[canvasIndex.Item1];
                thisTrack.Deleted = true;
                Timeline[canvasIndex.Item1] = thisTrack;
            }
            ResetTimeline();
        }

        public void MarkLayerAsNotDeleteRefresh(CanvasScript canvas)
        {
            (int, int) canvasIndex = GetCanvasLocation(canvas);
            if (canvasIndex.Item2 != -1)
            {
                Track thisTrack = Timeline[canvasIndex.Item1];
                thisTrack.Deleted = false;
                Timeline[canvasIndex.Item1] = thisTrack;
            }
            ResetTimeline();
        }

        public int GetTimelineLength()
        {
            EnsureSparseTimeline();
            int currentLength = Timeline == null || Timeline.Count == 0
                ? 0
                : Timeline.Max(track => track.Frames?.Count ?? 0);
            if (currentLength != m_CachedTimelineLength)
            {
                // See GetCanvasLocation(): this guards the compatibility view until Phase 3 makes
                // the sparse model authoritative.
                m_SparseTimelineDirty = true;
                EnsureSparseTimeline();
            }
            return m_CachedTimelineLength;
        }

        public void ResetTimeline()
        {
            InvalidateTimelineStructure();
            EnsureTimelineIndexes();
            m_PerformanceStats?.RecordTimelineReset();
            UpdateNodes();
            UpdateTimelineSlider();
            UpdateTimelineNob();
            UpdateTrackScroll();
            UpdateUI();
            App.Scene.TriggerLayersUpdate();
        }

        private void RefreshTimelineScroll()
        {
            UpdateTimelineSlider();
            UpdateTrackScroll();
            UpdateUI(updateTimelineLayout: false);
        }

        private void RefreshTimelineOccupancy()
        {
            InvalidateDrawingOccupancy();
            UpdateNodes();
            UpdateUI(updateTimelineLayout: false);
        }

        private void UpdateNodes()
        {
            int nodeCount; // always 8, unless we increase fps
            int trackFrameCount;
            int nodeToMake; // 9 - 8

            nodeCount = frameNotchesWidget.gameObject.transform.childCount; // always 8, unless we increase fps
            trackFrameCount = GetTimelineLength();
            nodeToMake = trackFrameCount - nodeCount; // 9 - 8

            if (nodeToMake > 0)
            {
                float posModifier = nodeCount;
                for (int make = 0; make < nodeToMake; make++)
                {
                    GameObject newFrame = Instantiate(timelineNotchPrefab, frameNotchesWidget.transform, false);
                    // HARD CODED. MUST GET Vector and Scale info from FrameButton1
                    newFrame.transform.localPosition = new Vector3(posModifier * 0.1971429f, 0, 0.0087f); // 1.9... is the spacing between framebuttons 0 and 1
                    newFrame.transform.FindChild("Num").GetComponent<TextMeshPro>().text = "" + (posModifier + 1);

                    posModifier = posModifier + 1;
                }
            }

            List<int> activeTrackIndexes = ActiveTrackIndexes();

            foreach (GameObject trackNodes in trackNodesWidget)
            {
                trackNodes.SetActive(false);
            }

            int start = Math.Clamp(-m_TrackScrollOffset, 0, activeTrackIndexes.Count);
            int end = Math.Min(start + trackNodesWidget.Count, activeTrackIndexes.Count);
            for (int localIndex = 0; localIndex < end - start; localIndex++)
            {
                int activeTrackIndex = start + localIndex;
                if (activeTrackIndex < 0 || activeTrackIndex >= activeTrackIndexes.Count) continue;

                int scrolledTrack = activeTrackIndexes[activeTrackIndex];
                if (scrolledTrack < 0 || scrolledTrack >= Timeline.Count) continue;

                if (Timeline[scrolledTrack].Frames.Count > 0) // check if there is a frame here.
                {
                    trackNodesWidget[localIndex].SetActive(true);

                    nodeCount = trackNodesWidget[localIndex].gameObject.transform.childCount; // always 8, unless we increase fps
                    trackFrameCount = Timeline[scrolledTrack].Frames.Count;
                    nodeToMake = trackFrameCount - nodeCount; // 9 - 8

                    if (nodeToMake > 0)
                    {
                        double posModifier = nodeCount;
                        for (int make = 0; make < nodeToMake; make++)
                        {
                            GameObject newFrame = Instantiate(frameButtonPrefab, trackNodesWidget[localIndex].transform, false);
                            // TODO : HARD CODED. MUST GET Vector and Scale info from FrameButton1
                            newFrame.transform.localPosition = new Vector3((float)posModifier * (float)0.1971429, 0, -0.029f); // 1.9... is the spacing between framebuttons 0 and 1
                            float scale = 0.16175f;
                            newFrame.transform.localScale = new Vector3(scale, scale, scale);
                            posModifier = posModifier + 1;
                        }

                        nodeCount = trackNodesWidget[localIndex].transform.childCount;
                    }

                    for (int hideNode = 0; hideNode < nodeCount; hideNode++)
                    {
                        Transform frameButton = trackNodesWidget[localIndex].transform
                            .GetChild(hideNode).GetChild(0);
                        bool frameExists = hideNode < trackFrameCount;
                        frameButton.gameObject.SetActive(frameExists);
                        if (!frameExists) continue;

                        // trackNodesWidget[localIndex].transform.GetChild(hideNode).gameObject.SetActive(false); // already handled in UpdateTimelineSlider below
                        foreach (Transform buttonState in frameButton) // hide all button state
                        {
                            buttonState.gameObject.SetActive(false); // trackNodesWidget[t].transform.GetChild(hideNode).GetChild(0).GetChild(X).gameObject.SetActive(false); 
                        }
                    }

                    int loopLimitFrames = Math.Min(nodeCount, trackFrameCount);
                    for (int frameNum = 0; frameNum < loopLimitFrames; frameNum++)
                    {
                        // trackNodesWidget[localIndex].transform.GetChild(frameNum).gameObject.SetActive(true); // already handled in UpdateTimelineSlider below
                        var frameButton = trackNodesWidget[localIndex].transform.GetChild(frameNum).GetChild(0); // f is tracknodes; 0 is the control, which is labled "1" in the prefab

                        frameButton.gameObject.GetComponent<FrameButton>().SetButtonCoordinate(scrolledTrack, frameNum); // 0 is the "1" that contains the FrameButton component.

                        //// COPY/PASTE FROM OG. Begin setting the button formatting.
                        bool filled = GetFrameFilled(scrolledTrack, frameNum); // using boolean as an ON and OFF switch. So buttonState index 0 and 1.
                        bool backwardsConnect;
                        bool forwardConnect;

                        backwardsConnect = frameNum > 0 &&
                            FramesShareSpan(scrolledTrack, frameNum, frameNum - 1);
                        forwardConnect = frameNum < Timeline[scrolledTrack].Frames.Count - 1 &&
                            FramesShareSpan(scrolledTrack, frameNum, frameNum + 1);
                        frameButton.GetChild(Convert.ToInt32(filled)).gameObject.SetActive(true);

                        int backBox = 6; // buttonState index 6
                        frameButton.GetChild(backBox).gameObject.SetActive(true);

                        // Set behind colours depending whether frame is active
                        Color backColor;
                        if (filled)
                        {
                            if (Timeline[scrolledTrack].Frames[frameNum].Canvas.Equals(App.Scene.ActiveCanvas))
                            {
                                backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f); // neutralgray
                            }
                            else
                            {
                                backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f); // black
                            }
                        }
                        else
                        {
                            (int, int) index = GetCanvasLocation(App.Scene.ActiveCanvas);
                            if (index.Item1 == scrolledTrack && frameNum == FrameOn)
                            {
                                backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f); // neutralgray
                            }
                            else
                            {
                                backColor = new Color(0 / 255f, 0 / 255f, 0 / 255f); // black
                            }
                        }

                        frameButton.GetChild(backBox).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox + 1).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox + 2).gameObject.GetComponent<SpriteRenderer>().color = backColor;

                        if (backwardsConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 2).gameObject.SetActive(true); // buttonState index 2 or 3; respectively empty connect left, filled connect left
                            frameButton.GetChild(backBox + 1).gameObject.SetActive(true); // buttonState index 7: back box left
                        }

                        if (forwardConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 4).gameObject.SetActive(true); // buttonState index 4 or 5; respectively empty connect right, filled connect right 
                            frameButton.GetChild(backBox + 2).gameObject.SetActive(true); // buttonState index 8: back box right
                        }
                    }
                }
            }
        }

        public void UpdateTrackScroll(int scrollOffset, float scrollHeight)
        {
            m_TrackScrollOffset = scrollOffset;
            for (int i = 0; i < timelineFrameObjects.Count; i++)
            {
                GameObject frameWrapper = timelineFrameObjects[i].transform.GetChild(0).gameObject;

                for (int c = 0; c < frameWrapper.transform.GetChildCount(); c++)
                {
                    GameObject frameObject = frameWrapper.transform.GetChild(c).gameObject;
                    Vector3 thisPos = frameObject.transform.localPosition;
                    thisPos.y = -scrollOffset * m_FrameOffset;
                    frameObject.transform.localPosition = thisPos;

                    int thisFrameOffset = c + scrollOffset;
                    if (thisFrameOffset >= 7 || thisFrameOffset < 0)
                    {
                        frameObject.SetActive(false);
                    }
                    else
                    {
                        frameObject.SetActive(true);
                    }
                }
            }
        }

        public void UpdateTrackScroll()
        {
            int scrollOffsetLocal = m_TrackScrollOffset;
            for (int i = 0; i < timelineFrameObjects.Count; i++)
            {
                GameObject frameWrapper = timelineFrameObjects[i].transform.GetChild(0).gameObject;

#if UNITY_EDITOR
                EditorGUIUtility.PingObject(frameWrapper);
#endif
                for (int c = 0; c < frameWrapper.transform.GetChildCount(); c++)
                {
                    GameObject frameObject = frameWrapper.transform.GetChild(c).gameObject;

                    Vector3 thisPos = frameObject.transform.localPosition;
                    thisPos.y = -scrollOffsetLocal * m_FrameOffset;
                    frameObject.transform.localPosition = thisPos;

                    int thisFrameOffset = c + scrollOffsetLocal;

                    if (thisFrameOffset >= 7 || thisFrameOffset < 0)
                    {
                        frameObject.SetActive(false);
                    }
                    else
                    {
                        frameObject.SetActive(true);
                    }
                }
            }
        }

        public void UpdateTimelineSlider()
        {
            float meshLength = timelineRef.GetComponent<TimelineSlider>().m_MeshScale.x;
            for (int t = 0; t < timelineField.transform.childCount; t++)
            {
                Transform trackT = timelineField.transform.GetChild(t);
                Vector3 newPosition;
                Quaternion quaternion;
                trackT.GetLocalPositionAndRotation(out newPosition, out quaternion);

                newPosition.x = (-m_TimelineOffset * meshLength) - 0.8f; // 1.64567f is mesh length; -0.8f is the X position on the prefab
                                                                         // TODO: probably need to get a global for this...
                trackT.localPosition = newPosition;
                trackT.localRotation = quaternion;

                for (int f = 0; f < trackT.transform.childCount; f++)
                {
                    Vector3 nodeVector = trackT.GetChild(f).transform.position;
                    Vector3 relativePosition = timelineField.transform.InverseTransformPoint(nodeVector);

                    trackT.GetChild(f).gameObject.SetActive(relativePosition.x >= -0.85f && relativePosition.x <= 0.85f);
                }
            }
        }

        public void SelectTimelineFrame(int trackNum, int frameNum)
        {
            if (!IsTimelineLocationValid(trackNum, frameNum)) return;

            App.Scene.ActiveCanvas = Timeline[trackNum].Frames[frameNum].Canvas;
            m_FrameOn = Math.Clamp((int)frameNum, 0, Timeline[trackNum].Frames.Count - 1);
            FocusFrame(frameNum);
            ResetTimeline();
            UpdateTimelineNob();
        }

        public void UpdateTimelineNob()
        {
            float newVal = (float)(m_FrameOn - 0.01) * m_SliderFrameSize - m_TimelineOffset;

            if (newVal >= 0.9f)
            {
                m_TimelineOffset += newVal - 0.9f;
            }

            if (newVal <= 0.1f)
            {
                m_TimelineOffset += newVal - 0.1f;
            }

            float max = m_SliderFrameSize * (float)GetTimelineLength() - 1;
            m_TimelineOffset = Math.Clamp(m_TimelineOffset, 0, max < 0 ? 0 : max);

            float clampedval = (float)newVal;
            clampedval = Math.Clamp(clampedval, 0, 1);
            timelineRef.GetComponent<TimelineSlider>().SetSliderValue(clampedval);
        }

        public void updateFrameInfo()
        {
            float adjustedFrameOn = Math.Min(m_FrameOn + 1, GetTimelineLength());
            textRef.GetComponent<TextMeshPro>().text = (adjustedFrameOn.ToString("0")) + " / " + GetTimelineLength();
        }

        public void UpdateUI(bool timelineInput = false, bool updateTimelineLayout = true)
        {
            updateFrameInfo();
            float previousTimelineOffset = m_TimelineOffset;
            if (!timelineInput) UpdateTimelineNob();
            if (updateTimelineLayout || !Mathf.Approximately(previousTimelineOffset, m_TimelineOffset))
            {
                UpdateTimelineSlider();
            }

            deleteFrameButton.GetComponent<RemoveKeyFrameButton>().SetButtonAvailable(
                App.Scene.ActiveCanvas != null && App.Scene.ActiveCanvas != Timeline[0].Frames[0].Canvas &&
                GetFrameFilled(App.Scene.ActiveCanvas)
            );
        }

        public void focusFrameNum(int frameNum)
        {
            FocusFrame(frameNum);
        }

        private HashSet<CanvasScript> GetVisibleCanvasesAtFrame(
            int frameIndex, bool includeEmptyCanvases = false)
        {
            EnsureSparseTimeline();
            var visibleCanvases = new HashSet<CanvasScript>();
            for (int trackIndex = 0; trackIndex < m_SparseTimeline.Tracks.Count; trackIndex++)
            {
                AnimationTimelineModel.Track track = m_SparseTimeline.Tracks[trackIndex];
                if (!track.Visible || track.Deleted ||
                    !track.TryResolve(frameIndex, out AnimationTimelineModel.Span span))
                {
                    continue;
                }

                CanvasScript canvas = GetCanvasForDrawing(span.Value.DrawingId);
                if (canvas == null && includeEmptyCanvases)
                {
                    canvas = !m_ShareEmptyCanvases &&
                        frameIndex < Timeline[trackIndex].Frames.Count
                        ? Timeline[trackIndex].Frames[frameIndex].Canvas
                        : GetOrCreateEmptyCanvas(trackIndex);
                }
                if (!span.Value.Deleted && canvas != null)
                {
                    visibleCanvases.Add(canvas);
                }
            }
            return visibleCanvases;
        }

        private void SetCanvasActive(CanvasScript canvas, bool active)
        {
            if (canvas == null || canvas.gameObject.activeSelf == active) return;
            m_PerformanceStats?.RecordCanvasVisibilityRequest();
            canvas.gameObject.SetActive(active);
        }

        private void ApplyDifferentialFrameVisibility(int previousFrame, int nextFrame)
        {
            HashSet<CanvasScript> previousCanvases = GetVisibleCanvasesAtFrame(previousFrame);
            HashSet<CanvasScript> nextCanvases = GetVisibleCanvasesAtFrame(nextFrame);
            var canvasesToHide = new List<CanvasScript>();
            var canvasesToShow = new List<CanvasScript>();
            AnimationSetDiff.GetChanges(
                previousCanvases, nextCanvases, canvasesToHide, canvasesToShow);

            foreach (CanvasScript canvas in canvasesToHide)
            {
                SetCanvasActive(canvas, false);
            }
            foreach (CanvasScript canvas in canvasesToShow)
            {
                SetCanvasActive(canvas, true);
            }
        }

        private void ApplyFullFrameVisibility(int frameIndex, bool includeEmptyCanvases = true)
        {
            HashSet<CanvasScript> visibleCanvases = GetVisibleCanvasesAtFrame(
                frameIndex, includeEmptyCanvases);
            foreach (CanvasScript canvas in Timeline
                .SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null)
                .Distinct())
            {
                SetCanvasActive(canvas, visibleCanvases.Contains(canvas));
            }
        }

        private void ApplyLegacyFrameVisibility(int frameIndex)
        {
            for (int i = 0; i < GetTimelineLength(); i++)
            {
                if (i != frameIndex) HideFrame(i, frameIndex);
            }
            ShowFrame(frameIndex);
        }

        private void FocusFrame(
            int frameIndex, bool timelineInput = false, bool forceFullVisibilityRefresh = true,
            bool playbackUpdate = false)
        {
            m_PerformanceStats?.RecordFocusFrame();
            Profiler.BeginSample("OB_ANIM_SCALE.FocusFrame");

            int previousFrame = m_PreviousShowingFrame;
            if (!m_UseDifferentialPlayback)
            {
                ApplyLegacyFrameVisibility(frameIndex);
            }
            else if (forceFullVisibilityRefresh || previousFrame < 0)
            {
                ApplyFullFrameVisibility(frameIndex, includeEmptyCanvases: !playbackUpdate);
            }
            else
            {
                ApplyDifferentialFrameVisibility(previousFrame, frameIndex);
            }

            App.Scene.m_LayerCanvases = new List<CanvasScript>();
            for (int i = 0; i < Timeline.Count; i++)
            {
                if (frameIndex >= Timeline[i].Frames.Count) continue;

                if (i == 0)
                {
                    App.Scene.m_MainCanvas = Timeline[i].Frames[frameIndex].Canvas;
                    continue;
                }
                CanvasScript canvas = Timeline[i].Frames[frameIndex].Canvas;
                if (canvas != null) App.Scene.m_LayerCanvases.Add(canvas);
            }

            (int, int) previousActiveCanvas = GetCanvasLocation(App.Scene.ActiveCanvas);
            if (previousActiveCanvas.Item1 != -1 && frameIndex < Timeline[previousActiveCanvas.Item1].Frames.Count)
            {
                CanvasScript nextActiveCanvas = Timeline[previousActiveCanvas.Item1].Frames[frameIndex].Canvas;
                if (nextActiveCanvas != null) App.Scene.ActiveCanvas = nextActiveCanvas;
            }

            m_PreviousShowingFrame = frameIndex;
            UpdateUI(timelineInput, updateTimelineLayout: !playbackUpdate);
            if (!playbackUpdate) App.Scene.TriggerLayersUpdate();
            Profiler.EndSample();
        }

        public DeleteFrameOperation RemoveKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);
            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            Frame deletedFrame = Timeline[index.Item1].Frames[index.Item2];

            App.Scene.HideCanvas(deletedFrame.Canvas);
            for (int l = index.Item2; l < nextIndex.Item2; l++)
            {
                CanvasScript replacementCanvas = GetOrCreateEmptyCanvas(index.Item1);
                Frame removingFrame = NewFrame(replacementCanvas);
                Timeline[index.Item1].Frames[l] = removingFrame;
            }

            FillandCleanTimeline();
            SelectTimelineFrame(index.Item1, Math.Clamp(index.Item2, 0, GetTimelineLength() - 1));
            ResetTimeline();
            return CompleteDeleteFrameOperation(index, deletedFrame.AnimatedPath, previousTimeline);
        }

        public (int, int) GetFollowingFrameIndex(int trackNum, int frameNum)
        {
            EnsureSparseTimeline();
            return m_SparseTimeline.TryResolve(
                trackNum, frameNum, out AnimationTimelineModel.Span span)
                ? (trackNum, span.EndFrameExclusive)
                : (trackNum, frameNum);
        }

        private bool FramesShareSpan(int trackNum, int firstFrame, int secondFrame)
        {
            EnsureSparseTimeline();
            return m_SparseTimeline.TryResolve(
                    trackNum, firstFrame, out AnimationTimelineModel.Span firstSpan) &&
                m_SparseTimeline.TryResolve(
                    trackNum, secondFrame, out AnimationTimelineModel.Span secondSpan) &&
                firstSpan.StartFrame == secondSpan.StartFrame;
        }

        public int GetTimelineMaxCanvas()
        {
            int maxLength = 0;

            for (int t = 0; t < Timeline.Count; t++)
            {
                for (int f = 0; f < Timeline[t].Frames.Count; f++)
                {
                    if (f > maxLength && GetFrameFilled(t, f))
                    {
                        maxLength = f;
                    }
                }
            }
            return maxLength;
        }

        public void CleanTimeline()
        {
            InvalidateTimelineStructure();
            int maxTimeline = GetTimelineMaxCanvas();
            var newTimeline = new List<Track>();
            var removedCanvases = new HashSet<CanvasScript>();

            for (int t = 0; t < Timeline.Count; t++)
            {
                Track addingTrack = NewTrack();
                addingTrack.Id = Timeline[t].Id;
                addingTrack.Visible = Timeline[t].Visible;
                addingTrack.Deleted = Timeline[t].Deleted;
                newTimeline.Add(addingTrack);
                for (int f = 0; f < Timeline[t].Frames.Count; f++)
                {
                    if (f <= maxTimeline)
                    {
                        newTimeline[t].Frames.Add(Timeline[t].Frames[f]);
                    }
                    else
                    {
                        removedCanvases.Add(Timeline[t].Frames[f].Canvas);
                    }
                }
            }

            HashSet<CanvasScript> retainedCanvases = GetAllTimelineCanvases(newTimeline);
            Timeline = newTimeline;
            InvalidateTimelineStructure();
            foreach (CanvasScript canvas in removedCanvases)
            {
                if (canvas != null && !retainedCanvases.Contains(canvas))
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
        }

        public void FillTimeline()
        {
            InvalidateTimelineStructure();
            int maxTimeline = GetTimelineLength();
            var newTimeline = new List<Track>();

            for (int t = 0; t < Timeline.Count; t++)
            {
                Track addingTrack = NewTrack();
                addingTrack.Id = Timeline[t].Id;
                addingTrack.Visible = Timeline[t].Visible;
                addingTrack.Deleted = Timeline[t].Deleted;
                newTimeline.Add(addingTrack);
                int f;
                for (f = 0; f < Timeline[t].Frames.Count; f++)
                {
                    newTimeline[t].Frames.Add(Timeline[t].Frames[f]);
                }

                if (f < maxTimeline)
                {
                    while (f < maxTimeline)
                    {
                        Frame addingFrame = NewFrame(GetOrCreateEmptyCanvas(t));
                        newTimeline[t].Frames.Add(addingFrame);
                        f++;
                    }
                }
            }
            Timeline = newTimeline;
            InvalidateTimelineStructure();
        }

        // Make sure there aren't too many or too few empty frames
        public void FillandCleanTimeline()
        {
            FillTimeline();
            CleanTimeline();
        }

        public (int, int) MoveKeyFrame(bool moveRight, int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);
            bool failure = false;

            if (moveRight)
            {
                if (nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count)
                {
                    Frame emptyFrame = NewFrame(GetOrCreateEmptyCanvas(index.Item1));
                    Frame movedFrame = Timeline[index.Item1].Frames[index.Item2];
                    Timeline[index.Item1].Frames[index.Item2] = emptyFrame;
                    Timeline[nextIndex.Item1].Frames.Insert(Timeline[nextIndex.Item1].Frames.Count, movedFrame);
                }
                else if (!GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
                {
                    Frame tempFrame = Timeline[nextIndex.Item1].Frames[nextIndex.Item2];
                    Timeline[nextIndex.Item1].Frames[nextIndex.Item2] = Timeline[index.Item1].Frames[index.Item2];
                    Timeline[index.Item1].Frames[index.Item2] = tempFrame;
                }
                else
                {
                    failure = true;
                }
            }
            else
            {
                if (index.Item2 > 0 && !GetFrameFilled(index.Item1, index.Item2 - 1))
                {
                    int frameLength = GetFrameLength(index.Item1, index.Item2);
                    Frame tempFrame = Timeline[index.Item1].Frames[index.Item2 - 1];
                    Timeline[index.Item1].Frames[index.Item2 - 1] = Timeline[index.Item1].Frames[index.Item2 + frameLength - 1];
                    Timeline[index.Item1].Frames[index.Item2 + frameLength - 1] = tempFrame;
                }
                else
                {
                    failure = true;
                }
            }
            if (failure) return (-1, -1);
            FillandCleanTimeline();

            if (moveRight)
            {
                SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);
                return (index.Item1, index.Item2 + 1);
            }
            SelectTimelineFrame(index.Item1, index.Item2 - 1);
            return (index.Item1, index.Item2 - 1);
        }

        // For loading the scene
        // TODO Hidden by overloads
        public void AddKeyFrame(int trackNum)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum, Timeline[trackNum].Frames.Count - 1);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            if (nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count)
            {
                Frame addingFrame = NewFrame(GetOrCreateEmptyCanvas(nextIndex.Item1));
                Timeline[nextIndex.Item1].Frames.Insert(Timeline[nextIndex.Item1].Frames.Count, addingFrame);
                nextIndex.Item2 = Timeline[nextIndex.Item1].Frames.Count - 1;
            }
            else if (GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
            {
                Frame addingFrame = NewFrame(GetOrCreateEmptyCanvas(nextIndex.Item1));
                Timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);
            }
            InvalidateTimelineStructure();
        }

        public AddFrameOperation AddKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            (int, int) insertingAt;
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            if (nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count)
            {
                Frame addingFrame = NewFrame(GetOrCreateEmptyCanvas(nextIndex.Item1));
                Timeline[nextIndex.Item1].Frames.Insert(Timeline[nextIndex.Item1].Frames.Count, addingFrame);
                nextIndex.Item2 = Timeline[nextIndex.Item1].Frames.Count - 1;
                insertingAt = (nextIndex.Item1, Timeline[nextIndex.Item1].Frames.Count - 1);
            }
            else if (GetFrameFilled(nextIndex.Item1, nextIndex.Item2))
            {
                Frame addingFrame = NewFrame(GetOrCreateEmptyCanvas(nextIndex.Item1));
                Timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2, addingFrame);
                insertingAt = nextIndex;
            }
            else
            {
                insertingAt = nextIndex;
            }

            ResetTimeline();
            FillTimeline();
            SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);

            HashSet<CanvasScript> previousCanvases = GetTimelineCanvases(previousTimeline);
            List<CanvasScript> createdCanvases = GetDrawingCanvases(Timeline)
                .Where(canvas => !previousCanvases.Contains(canvas)).ToList();
            return new AddFrameOperation
            {
                Location = insertingAt,
                PreviousTimeline = previousTimeline,
                CreatedCanvases = createdCanvases,
            };
        }

        public void UndoAddFrameOperation(AddFrameOperation operation, (int, int) selection)
        {
            if (!operation.Succeeded) return;
            InvalidateTimelineStructure();

            RestoreTimelineSnapshot(operation.PreviousTimeline);
            ReleaseTimelineSnapshot(operation.PreviousTimeline);
            SelectTimelineFrame(selection.Item1, selection.Item2);

            foreach (CanvasScript canvas in operation.CreatedCanvases)
            {
                if (canvas != null)
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
            ResetTimeline();
        }

        public void DiscardAddFrameOperationUndoState(AddFrameOperation operation)
        {
            if (!operation.Succeeded) return;
            ReleaseTimelineSnapshot(operation.PreviousTimeline);
        }

        // TODO this is hidden by overload
        public void ExtendKeyFrame(int trackNum)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum, Timeline[trackNum].Frames.Count - 1);
            Frame addingFrame = Timeline[index.Item1].Frames[index.Item2];
            Timeline[index.Item1].Frames.Insert(index.Item2 + 1, addingFrame);
            InvalidateTimelineStructure();
        }

        public FrameLengthOperation ExtendKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            if (!GetFrameFilled(index.Item1, index.Item2))
            {
                return default;
            }

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            int frameLength = GetFrameLength(index.Item1, index.Item2);
            int insertIndex = index.Item2 + frameLength;

            if (insertIndex >= Timeline[index.Item1].Frames.Count ||
                GetFrameFilled(index.Item1, insertIndex))
            {
                for (int l = 0; l < Timeline.Count; l++)
                {
                    Frame addingFrame;
                    if (l == index.Item1)
                    {
                        addingFrame = Timeline[l].Frames[index.Item2];
                    }
                    else
                    {
                        addingFrame = NewFrame(GetOrCreateEmptyCanvas(l));
                    }
                    while (Timeline[l].Frames.Count < insertIndex)
                    {
                        Timeline[l].Frames.Add(NewFrame(GetOrCreateEmptyCanvas(l)));
                    }
                    Timeline[l].Frames.Insert(insertIndex, addingFrame);
                }
            }
            else
            {
                Frame addingFrame = Timeline[index.Item1].Frames[index.Item2];
                Timeline[index.Item1].Frames[insertIndex] = addingFrame;
            }

            InvalidateTimelineStructure();
            m_FrameOn++;
            FocusFrame((int)m_FrameOn);
            ResetTimeline();
            return CompleteFrameLengthOperation(index, previousTimeline);
        }

        public FrameLengthOperation ReduceKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            int frameLength = GetFrameLength(index.Item1, index.Item2);
            if (frameLength <= 1)
            {
                return default;
            }

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            Frame emptyFrame = NewFrame(GetOrCreateEmptyCanvas(index.Item1));
            Timeline[index.Item1].Frames[index.Item2 + frameLength - 1] = emptyFrame;

            InvalidateTimelineStructure();
            m_FrameOn--;
            FocusFrame(FrameOn);
            ResetTimeline();
            FillandCleanTimeline();
            return CompleteFrameLengthOperation(index, previousTimeline);
        }

        private CanvasScript ReplicateStrokesToNewCanvas(List<Stroke> oldStrokes, out List<Stroke> newStrokes)
        {
            CanvasScript newCanvas = App.Scene.AddCanvas();
            newStrokes = oldStrokes
                .Select(stroke => SketchMemoryScript.m_Instance.DuplicateStroke(
                    stroke, App.Scene.SelectionCanvas, null)).ToList();

            for (int i = 0; i < oldStrokes.Count; i++)
            {
                if (oldStrokes.Count == newStrokes.Count && (oldStrokes[i].m_Type == Stroke.Type.NotCreated || !oldStrokes[i].IsGeometryEnabled))
                {
                    // using SketchMemory of oldStrokes to mark Uncreated strokes on newStrokes. Otherwise, Uncreated strokes will be re-made.
                    newStrokes[i].Uncreate();
                }
                else
                {
                    Debug.LogWarning("Unexpected. Count of oldStrokes must match newStrokes.");
                }
            }

            Dictionary<int, List<Stroke>> strokeGroups = new Dictionary<int, List<Stroke>>();

            foreach (var stroke in newStrokes)
            {
                if (stroke.Group != SketchGroupTag.None)
                {
                    if (strokeGroups.TryGetValue(stroke.Group.GetHashCode(), out List<Stroke> group))
                    {
                        group.Add(stroke);
                    }
                    else
                    {
                        strokeGroups[stroke.Group.GetHashCode()] = new List<Stroke> { stroke };
                    }
                }

                switch (stroke.m_Type)
                {
                    case Stroke.Type.BrushStroke:
                        BaseBrushScript brushScript = stroke.m_Object.GetComponent<BaseBrushScript>();
                        if (brushScript)
                        {
                            brushScript.HideBrush(false);
                        }
                        break;
                    case Stroke.Type.BatchedBrushStroke:
                        stroke.m_BatchSubset.m_ParentBatch.EnableSubset(stroke.m_BatchSubset);
                        break;
                }

                if (stroke.m_Type != Stroke.Type.NotCreated)
                {
                    TiltMeterScript.m_Instance.AdjustMeter(stroke, up: true);
                    stroke.SetParentKeepWorldPosition(newCanvas);
                }
            }

            foreach (var sg in strokeGroups)
            {
                GroupManager.MoveStrokesToNewGroups(sg.Value, null);
            }

            return newCanvas;
        }

        private AnimationTimelineModel.Snapshot CreateTimelineSnapshot()
        {
            EnsureSparseTimeline();
            AnimationTimelineModel.Snapshot snapshot = m_SparseTimeline.CreateSnapshot();
            foreach (AnimationDrawingId drawingId in snapshot.DrawingIds)
            {
                m_UndoDrawingRefCounts.TryGetValue(drawingId, out int references);
                m_UndoDrawingRefCounts[drawingId] = references + 1;
            }
            return snapshot;
        }

        private void ReleaseTimelineSnapshot(AnimationTimelineModel.Snapshot snapshot)
        {
            if (snapshot == null) return;
            foreach (AnimationDrawingId drawingId in snapshot.DrawingIds)
            {
                if (!m_UndoDrawingRefCounts.TryGetValue(drawingId, out int references)) continue;
                if (references > 1)
                {
                    m_UndoDrawingRefCounts[drawingId] = references - 1;
                    continue;
                }

                m_UndoDrawingRefCounts.Remove(drawingId);
                if (!m_PendingDrawingDestruction.Remove(drawingId)) continue;
                EnsureSparseTimeline();
                if (!m_SparseTimeline.TryGetDrawingLocation(drawingId, out _) &&
                    m_DrawingCanvases.TryGetValue(drawingId, out CanvasScript canvas))
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
        }

        private HashSet<CanvasScript> GetTimelineCanvases(
            AnimationTimelineModel.Snapshot snapshot)
        {
            return new HashSet<CanvasScript>(snapshot.DrawingIds
                .Select(GetCanvasForDrawing)
                .Where(canvas => canvas != null));
        }

        private HashSet<CanvasScript> GetDrawingCanvases(List<Track> timeline)
        {
            return new HashSet<CanvasScript>(timeline.SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null && !m_EmptyCanvases.Contains(canvas)));
        }

        private static HashSet<CanvasScript> GetAllTimelineCanvases(List<Track> timeline)
        {
            return new HashSet<CanvasScript>(timeline.SelectMany(track => track.Frames)
                .Select(frame => frame.Canvas)
                .Where(canvas => canvas != null));
        }

        private void RestoreTimelineSnapshot(AnimationTimelineModel.Snapshot snapshot)
        {
            m_SparseTimeline.Restore(snapshot);
            m_CachedTimelineLength = m_SparseTimeline.Length;
            m_SparseTimelineDirty = false;

            Timeline = new List<Track>(m_SparseTimeline.Tracks.Count);
            for (int trackIndex = 0; trackIndex < m_SparseTimeline.Tracks.Count; trackIndex++)
            {
                AnimationTimelineModel.Track sparseTrack = m_SparseTimeline.Tracks[trackIndex];
                Timeline.Add(new Track
                {
                    Id = sparseTrack.Id,
                    Frames = new List<Frame>(sparseTrack.Length),
                    Visible = sparseTrack.Visible,
                    Deleted = sparseTrack.Deleted,
                });

                foreach (AnimationTimelineModel.Span span in sparseTrack.Spans)
                {
                    CanvasScript canvas = GetCanvasForDrawing(span.Value.DrawingId);
                    if (span.Value.DrawingId.IsEmpty)
                    {
                        canvas = GetOrCreateEmptyCanvas(trackIndex);
                    }
                    for (int frameIndex = span.StartFrame;
                        frameIndex < span.EndFrameExclusive; frameIndex++)
                    {
                        Timeline[trackIndex].Frames.Add(new Frame
                        {
                            Visible = sparseTrack.Visible,
                            Deleted = span.Value.Deleted,
                            FrameExists = span.Value.FrameExists,
                            Canvas = canvas,
                            AnimatedPath = span.Value.PathToken as CameraPathWidget,
                            EmptySpanId = span.Value.SpanIdentity,
                        });
                    }
                }
            }

            RebuildEmptyCanvasRegistry();
            m_TimelineIndexesValid = false;
            m_CanvasLocations.Clear();
            InvalidateDrawingOccupancy();
        }

        private DeleteFrameOperation CompleteDeleteFrameOperation(
            (int, int) location, CameraPathWidget removedPath,
            AnimationTimelineModel.Snapshot previousTimeline)
        {
            HashSet<CanvasScript> previousCanvases = GetTimelineCanvases(previousTimeline);
            HashSet<CanvasScript> currentCanvases = GetDrawingCanvases(Timeline);
            return new DeleteFrameOperation
            {
                Location = location,
                RemovedPath = removedPath,
                PreviousTimeline = previousTimeline,
                CreatedCanvases = currentCanvases
                    .Where(canvas => !previousCanvases.Contains(canvas)).ToList(),
                DisplacedCanvases = previousCanvases
                    .Where(canvas => !currentCanvases.Contains(canvas)).ToList(),
            };
        }

        public void UndoDeleteFrameOperation(DeleteFrameOperation operation)
        {
            if (!operation.Succeeded) return;
            InvalidateTimelineStructure();

            RestoreTimelineSnapshot(operation.PreviousTimeline);
            ReleaseTimelineSnapshot(operation.PreviousTimeline);
            SelectTimelineFrame(operation.Location.Item1, operation.Location.Item2);

            foreach (CanvasScript canvas in operation.CreatedCanvases)
            {
                if (canvas != null)
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
            ResetTimeline();
        }

        public void DiscardDeleteFrameOperationUndoState(DeleteFrameOperation operation)
        {
            if (!operation.Succeeded) return;
            ReleaseTimelineSnapshot(operation.PreviousTimeline);

            if (operation.RemovedPath != null &&
                !Timeline.SelectMany(track => track.Frames)
                    .Any(frame => frame.AnimatedPath == operation.RemovedPath))
            {
                WidgetManager.m_Instance.DeleteCameraPath(operation.RemovedPath);
            }

            HashSet<CanvasScript> currentCanvases = GetDrawingCanvases(Timeline);
            foreach (CanvasScript canvas in operation.DisplacedCanvases)
            {
                if (canvas != null && !currentCanvases.Contains(canvas))
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
        }

        private FrameLengthOperation CompleteFrameLengthOperation(
            (int, int) location, AnimationTimelineModel.Snapshot previousTimeline)
        {
            HashSet<CanvasScript> previousCanvases = GetTimelineCanvases(previousTimeline);
            HashSet<CanvasScript> currentCanvases = GetDrawingCanvases(Timeline);
            return new FrameLengthOperation
            {
                Location = location,
                PreviousTimeline = previousTimeline,
                CreatedCanvases = currentCanvases
                    .Where(canvas => !previousCanvases.Contains(canvas)).ToList(),
                DisplacedCanvases = previousCanvases
                    .Where(canvas => !currentCanvases.Contains(canvas)).ToList(),
            };
        }

        public void UndoFrameLengthOperation(FrameLengthOperation operation, (int, int) selection)
        {
            if (!operation.Succeeded) return;
            InvalidateTimelineStructure();

            RestoreTimelineSnapshot(operation.PreviousTimeline);
            ReleaseTimelineSnapshot(operation.PreviousTimeline);
            SelectTimelineFrame(selection.Item1, selection.Item2);

            foreach (CanvasScript canvas in operation.CreatedCanvases)
            {
                if (canvas != null)
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
            ResetTimeline();
        }

        public void DiscardFrameLengthOperationUndoState(FrameLengthOperation operation)
        {
            if (!operation.Succeeded) return;
            ReleaseTimelineSnapshot(operation.PreviousTimeline);

            HashSet<CanvasScript> currentCanvases = GetDrawingCanvases(Timeline);
            foreach (CanvasScript canvas in operation.DisplacedCanvases)
            {
                if (canvas != null && !currentCanvases.Contains(canvas))
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
        }

        private KeyFrameOperation CompleteKeyFrameOperation(
            (int, int) location, CanvasScript createdCanvas, List<Stroke> createdStrokes,
            AnimationTimelineModel.Snapshot previousTimeline)
        {
            HashSet<CanvasScript> previousCanvases = GetTimelineCanvases(previousTimeline);
            HashSet<CanvasScript> currentCanvases = GetDrawingCanvases(Timeline);
            List<CanvasScript> createdCanvases = currentCanvases
                .Where(canvas => !previousCanvases.Contains(canvas)).ToList();
            List<CanvasScript> displacedCanvases = previousCanvases
                .Where(canvas => !currentCanvases.Contains(canvas)).ToList();

            return new KeyFrameOperation
            {
                Location = location,
                CreatedCanvas = createdCanvas,
                CreatedStrokes = createdStrokes,
                PreviousTimeline = previousTimeline,
                CreatedCanvases = createdCanvases,
                DisplacedCanvases = displacedCanvases,
            };
        }

        public void UndoKeyFrameOperation(KeyFrameOperation operation, (int, int) selection)
        {
            if (!operation.Succeeded) return;
            InvalidateTimelineStructure();

            RestoreTimelineSnapshot(operation.PreviousTimeline);
            ReleaseTimelineSnapshot(operation.PreviousTimeline);
            SelectTimelineFrame(selection.Item1, selection.Item2);

            foreach (CanvasScript canvas in operation.CreatedCanvases)
            {
                if (canvas != null)
                {
                    IEnumerable<Stroke> createdStrokes = canvas == operation.CreatedCanvas
                        ? operation.CreatedStrokes
                        : null;
                    DestroyTimelineCanvas(canvas, createdStrokes);
                }
            }
            ResetTimeline();
        }

        public void DiscardKeyFrameOperationUndoState(KeyFrameOperation operation)
        {
            if (!operation.Succeeded) return;
            ReleaseTimelineSnapshot(operation.PreviousTimeline);

            HashSet<CanvasScript> currentCanvases = GetDrawingCanvases(Timeline);
            foreach (CanvasScript canvas in operation.DisplacedCanvases)
            {
                if (canvas != null && !currentCanvases.Contains(canvas))
                {
                    DestroyTimelineCanvas(canvas);
                }
            }
        }

        public KeyFrameOperation SplitKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);

            CanvasScript oldCanvas = Timeline[index.Item1].Frames[index.Item2].Canvas;

            List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                .Where(x => x.Canvas == oldCanvas).ToList();

            int frameLength = GetFrameLength(index.Item1, index.Item2);
            int splittingIndex = FrameOn;
            if (splittingIndex <= index.Item2 || splittingIndex > index.Item2 + frameLength - 1)
            {
                return new KeyFrameOperation { Location = (-1, -1) };
            }

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            CanvasScript newCanvas = ReplicateStrokesToNewCanvas(oldStrokes, out List<Stroke> newStrokes);

            for (int f = splittingIndex; f < index.Item2 + frameLength; f++)
            {
                Frame addingFrame = NewFrame(newCanvas);
                Timeline[index.Item1].Frames[f] = addingFrame;
            }

            InvalidateTimelineStructure();
            SelectTimelineFrame(index.Item1, splittingIndex);
            ResetTimeline();
            return CompleteKeyFrameOperation(
                (index.Item1, splittingIndex), newCanvas, newStrokes, previousTimeline);
        }

        public KeyFrameOperation DuplicateKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            InvalidateTimelineStructure();

            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);

            CanvasScript oldCanvas = Timeline[index.Item1].Frames[index.Item2].Canvas;

            List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                .Where(x => x.Canvas == oldCanvas).ToList();

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            CanvasScript newCanvas = ReplicateStrokesToNewCanvas(oldStrokes, out List<Stroke> newStrokes);

            int frameLength = GetFrameLength(index.Item1, index.Item2);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            for (int f = 0; f < frameLength; f++)
            {
                if (nextIndex.Item2 + f < Timeline[nextIndex.Item1].Frames.Count &&
                    !GetFrameFilled(nextIndex.Item1, nextIndex.Item2 + f))
                {
                    Frame addingFrame = NewFrame(newCanvas);

                    Timeline[nextIndex.Item1].Frames[nextIndex.Item2 + f] = addingFrame;
                }
                else
                {
                    Frame addingFrame = NewFrame(newCanvas);
                    Timeline[nextIndex.Item1].Frames.Insert(nextIndex.Item2 + f, addingFrame);
                }
            }

            InvalidateTimelineStructure();
            FillTimeline();
            SelectTimelineFrame(nextIndex.Item1, nextIndex.Item2);
            ResetTimeline();
            return CompleteKeyFrameOperation(nextIndex, newCanvas, newStrokes, previousTimeline);
        }

        public void TimelineSlideDown(bool down)
        {
            m_Scrolling = down;
        }

        public void TimelineSlide(float Value)
        {
            gameObject.GetComponent<TiltBrush.Layers.AnimationLayerUI_Manager>().OnDisable();
            m_FrameOn = ((float)(Value + m_TimelineOffset) / m_SliderFrameSize);

            int timelineLength = GetTimelineLength();
            m_FrameOn = m_FrameOn >= timelineLength ? timelineLength : m_FrameOn;
            m_FrameOn = m_FrameOn < 0 ? 0 : m_FrameOn;

            FocusFrame(FrameOn, true);
            UpdateLayerTransforms();

            // Scrolling the timeline
            if (Value < 0.1f)
            {
                m_TimelineOffset -= 0.05f;
            }
            if (Value > 0.9f)
            {
                m_TimelineOffset += 0.05f;
            }

            float max = m_SliderFrameSize * (float)timelineLength - 1;
            m_TimelineOffset = Math.Clamp(m_TimelineOffset, 0, max < 0 ? 0 : max);
            gameObject.GetComponent<TiltBrush.Layers.AnimationLayerUI_Manager>().OnEnable();
        }

        public void StartAnimation()
        {
            m_Start = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            ApplyFullFrameVisibility(FrameOn, includeEmptyCanvases: false);
            m_Playing = true;
        }

        public void StopAnimation()
        {
            m_Playing = false;
            App.Scene.TriggerLayersUpdate();
        }

        public bool GetChanging()
        {
            return m_Playing || m_Scrolling;
        }

        public void ToggleAnimation()
        {
            if (m_Playing) { StopAnimation(); }
            else StartAnimation();
        }

        public int GetFrameLength(int trackOn, int frameOn)
        {
            EnsureSparseTimeline();
            return m_SparseTimeline.TryResolve(trackOn, frameOn, out AnimationTimelineModel.Span span)
                ? span.Duration
                : 0;
        }

        public float GetSmoothAnimationTime(Track trackOn)
        {
            EnsureSparseTimeline();
            int trackIndex = Timeline.FindIndex(track => track.Id == trackOn.Id);
            if (trackIndex < 0 ||
                !m_SparseTimeline.TryResolve(trackIndex, FrameOn, out AnimationTimelineModel.Span span))
            {
                return 0;
            }
            return (m_FrameOn - span.StartFrame) / span.Duration;
        }

        public void UpdateLayerTransforms()
        {
            int frameInt = FrameOn;

            // Update layer animation transforms
            if (frameInt < 0) return;
            for (int t = 0; t < Timeline.Count; t++)
            {
                if (frameInt >= Timeline[t].Frames.Count) { continue; }
                if (Timeline[t].Frames[frameInt].AnimatedPath != null)
                {
                    float canvasTime = GetSmoothAnimationTime(Timeline[t]) * (Timeline[t].Frames[frameInt].AnimatedPath.Path.NumPositionKnots - 1);
                    PathT pathTime = new TiltBrush.PathT(canvasTime);
                    PathT pathStart = new TiltBrush.PathT(0);
                    TrTransform pathPosition = TrTransform.FromTransform(Timeline[t].Frames[frameInt].AnimatedPath.gameObject.transform);
                    TrTransform posStart = App.Scene.Pose.inverse * TrTransform.TR(Timeline[t].Frames[frameInt].AnimatedPath.Path.GetPosition(pathStart), Timeline[t].Frames[frameInt].AnimatedPath.Path.GetRotation(pathStart));
                    TrTransform posNow = App.Scene.Pose.inverse * TrTransform.TR(Timeline[t].Frames[frameInt].AnimatedPath.Path.GetPosition(pathTime), Timeline[t].Frames[frameInt].AnimatedPath.Path.GetRotation(pathTime));
                    TrTransform posDifference = posNow * posStart.inverse;
                    Timeline[t].Frames[frameInt].Canvas.LocalPose = posDifference;
                    TrTransform pathPositionConstant = (pathPosition);
                    Timeline[t].Frames[frameInt].AnimatedPath.gameObject.transform.position = pathPositionConstant.translation;
                    Timeline[t].Frames[frameInt].AnimatedPath.gameObject.transform.rotation = pathPositionConstant.rotation;
                }
            }
        }

        void Update()
        {
            m_PerformanceStats ??= new AnimationPerformanceStats(this);
            m_PerformanceStats.Enabled = m_LogPerformanceStats;
            m_PerformanceStats.RecordUpdate();
            EnsureMemorySubscriptions();
            if (m_LastCanvas != App.Scene.ActiveCanvas)
            {
                m_PreviousCanvasBatches = 0;
            }
            m_LastCanvas = App.Scene.ActiveCanvas;

            int currentBatchPools = App.Scene.ActiveCanvas.BatchManager.GetNumBatchPools();

            if (m_TrackScrollOffset != m_previousTrackScrollOffset)
            {
                m_previousTrackScrollOffset = m_TrackScrollOffset;
                RefreshTimelineScroll();
            }

            if (currentBatchPools != 0 && m_PreviousCanvasBatches != currentBatchPools)
            {
                RefreshTimelineOccupancy();
                m_PreviousCanvasBatches = currentBatchPools;
            }

            if (m_Playing)
            {
                m_Time = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                m_Current = m_Time - m_Start;
                m_FrameOn = m_Current / (1000f / m_Fps);

                m_FrameOn %= GetTimelineLength();
                if (AnimationSetDiff.ShouldApplyFrame(m_PreviousShowingFrame, FrameOn))
                {
                    FocusFrame(
                        FrameOn, forceFullVisibilityRefresh: false, playbackUpdate: true);
                }

                // Update layer animation transforms
                UpdateLayerTransforms();
            }

            m_PerformanceStats.UpdateAndMaybeLog();
        }
    }
} // namespace TiltBrush.FrameAnimation
