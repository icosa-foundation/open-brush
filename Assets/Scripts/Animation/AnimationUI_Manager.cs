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
        [SerializeField, Min(1)] int m_TimelineFramePoolSize = 10;
        AnimationPerformanceStats m_PerformanceStats;
        readonly Dictionary<CanvasScript, bool> m_DrawingOccupancy = new();
        readonly Dictionary<CanvasScript, HashSet<Stroke>> m_CanvasStrokes = new();
        readonly Dictionary<CanvasScript, HashSet<GrabWidget>> m_CanvasWidgets = new();
        readonly AnimationTimelineModel m_SparseTimeline = new();
        readonly Dictionary<CanvasScript, AnimationDrawingId> m_CanvasDrawingIds = new();
        readonly Dictionary<AnimationDrawingId, CanvasScript> m_DrawingCanvases = new();
        readonly AnimationDrawingReferenceTracker m_DrawingReferences = new();
        readonly HashSet<AnimationDrawingId> m_PendingDrawingDestruction = new();
        readonly HashSet<AnimationDrawingId> m_PendingDrawingDemotion = new();
        readonly HashSet<CanvasScript> m_CanvasesBeingDestroyed = new();
        readonly Dictionary<int, CanvasScript> m_EmptyCanvasByTrackId = new();
        readonly Dictionary<CanvasScript, int> m_EmptyCanvasTrackIds = new();
        readonly HashSet<CanvasScript> m_EmptyCanvases = new();
        SketchMemoryScript m_SubscribedMemory;
        bool m_ContentIndexInitialized;
        bool m_SparseTimelineDirty = true;
        int m_CachedTimelineLength;
        int m_NextTrackId = 1;
        long m_NextDrawingId = 1;
        long m_NextEmptySpanId = 1;

        private sealed class DrawingReferenceLease : IDisposable
        {
            private Action m_Release;

            internal DrawingReferenceLease(Action release)
            {
                m_Release = release;
            }

            public void Dispose()
            {
                Action release = m_Release;
                m_Release = null;
                release?.Invoke();
            }
        }

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
            if (m_PerformanceStats != null) m_PerformanceStats.Enabled = false;
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

        public void NotifyCanvasWillBeDestroyed(CanvasScript canvas)
        {
            if (canvas != null) m_CanvasesBeingDestroyed.Add(canvas);
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
            CanvasScript canvas = stroke.Canvas;
            EnsureDrawingContentIndex();
            RemoveIndexedContent(m_CanvasStrokes, canvas, stroke);
            NotifyDrawingContentChanged(canvas);
            TryDemoteDrawingCanvas(canvas);
        }

        public void NotifyStrokeCanvasChanged(
            Stroke stroke, CanvasScript previousCanvas, CanvasScript nextCanvas)
        {
            if (stroke == null || previousCanvas == nextCanvas) return;
            EnsureDrawingContentIndex();
            if (previousCanvas != null)
            {
                RemoveIndexedContent(m_CanvasStrokes, previousCanvas, stroke);
                NotifyDrawingContentChanged(previousCanvas);
            }
            if (nextCanvas != null)
            {
                AddIndexedContent(m_CanvasStrokes, nextCanvas, stroke);
                NotifyDrawingContentChanged(nextCanvas);
            }
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
            CanvasScript canvas = widget.Canvas;
            EnsureDrawingContentIndex();
            RemoveIndexedContent(m_CanvasWidgets, canvas, widget);
            NotifyDrawingContentChanged(canvas);
            TryDemoteDrawingCanvas(canvas);
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
                AnimationPerformanceStats.RecordGlobalStrokeScan();
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
            m_SparseTimelineDirty = true;
            InvalidateDrawingOccupancy();
        }

        private void InvalidateTimelineProjection()
        {
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

            if (Timeline != null)
            {
                for (int trackIndex = 0; trackIndex < Timeline.Count; trackIndex++)
                {
                    Track track = Timeline[trackIndex];
                    if (track.Id != 0) continue;
                    track.Id = m_NextTrackId++;
                    Timeline[trackIndex] = track;
                }
            }
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

        private AnimationTimelineModel.FrameValue NewEmptyFrameValue()
        {
            return new AnimationTimelineModel.FrameValue(
                AnimationDrawingId.Empty, spanIdentity: m_NextEmptySpanId++);
        }

        private AnimationTimelineModel.FrameValue NewDrawingFrameValue(CanvasScript canvas)
        {
            return new AnimationTimelineModel.FrameValue(GetOrCreateDrawingId(canvas));
        }

        private void ApplySparseTimelineEdit(
            Action<List<AnimationTimelineModel.EditableTrack>> edit)
        {
            Profiler.BeginSample("OB_ANIM_SCALE.SparseEditAndProjection");
            EnsureSparseTimeline();
            m_SparseTimeline.ApplyEdit(edit);
            ProjectSparseTimelineToCompatibilityView();
            Profiler.EndSample();
        }

        private bool IsSparseFrameFilled(AnimationTimelineModel.FrameValue value)
        {
            if (value.PathToken != null) return true;
            CanvasScript canvas = GetCanvasForDrawing(value.DrawingId);
            return canvas != null && GetFrameFilledWithoutStats(canvas);
        }

        private int GetSparseTimelineContentLength(
            List<AnimationTimelineModel.EditableTrack> tracks)
        {
            return AnimationTimelineOperations.GetContentLength(tracks, IsSparseFrameFilled);
        }

        private void PadSparseTracks(
            List<AnimationTimelineModel.EditableTrack> tracks, int length)
        {
            AnimationTimelineOperations.PadTracks(tracks, length, NewEmptyFrameValue);
        }

        private void ProjectSparseTimelineToCompatibilityView()
        {
            m_CachedTimelineLength = m_SparseTimeline.Length;
            m_SparseTimelineDirty = false;

            List<Track> previousTimeline = Timeline;
            var previousEmptyCanvases = new HashSet<CanvasScript>(m_EmptyCanvases);
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
                        CanvasScript preferredCanvas = null;
                        if (!m_ShareEmptyCanvases && previousTimeline != null)
                        {
                            int previousTrackIndex = previousTimeline.FindIndex(
                                track => track.Id == sparseTrack.Id);
                            if (previousTrackIndex >= 0 &&
                                span.StartFrame < previousTimeline[previousTrackIndex].Frames.Count)
                            {
                                CanvasScript candidate = previousTimeline[previousTrackIndex]
                                    .Frames[span.StartFrame].Canvas;
                                if (candidate != null && previousEmptyCanvases.Contains(candidate))
                                {
                                    preferredCanvas = candidate;
                                }
                            }
                        }
                        canvas = GetOrCreateEmptyCanvas(trackIndex, preferredCanvas);
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
            foreach (CanvasScript unusedCanvas in previousEmptyCanvases.Where(
                canvas => canvas != null && !m_EmptyCanvases.Contains(canvas)))
            {
                DestroyTimelineCanvas(unusedCanvas);
            }
            InvalidateTimelineProjection();
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
            m_EmptyCanvasTrackIds.Clear();
            m_EmptyCanvases.Clear();
            if (Timeline == null) return;

            foreach (Track track in Timeline)
            {
                if (track.Frames == null) continue;
                foreach (Frame frame in track.Frames)
                {
                    if (frame.EmptySpanId == 0 || frame.Canvas == null) continue;
                    m_EmptyCanvases.Add(frame.Canvas);
                    m_EmptyCanvasTrackIds[frame.Canvas] = track.Id;
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
                EnsureSparseTimeline();
                bool activeTimelineOwner = m_SparseTimeline.TryGetDrawingLocation(drawingId, out _);
                bool activeEditingOwner = App.Scene != null && App.Scene.ActiveCanvas == canvas;
                if (activeTimelineOwner || activeEditingOwner ||
                    m_DrawingReferences.IsRetained(drawingId))
                {
                    m_PendingDrawingDestruction.Add(drawingId);
                    return;
                }
                m_PendingDrawingDestruction.Remove(drawingId);
                m_CanvasDrawingIds.Remove(canvas);
                m_DrawingCanvases.Remove(drawingId);
            }
            m_EmptyCanvases.Remove(canvas);
            m_EmptyCanvasTrackIds.Remove(canvas);
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
            if (trackIndex < 0) return preferredCanvas;
            int trackId;
            if (trackIndex < m_SparseTimeline.Tracks.Count)
            {
                trackId = m_SparseTimeline.Tracks[trackIndex].Id;
            }
            else if (Timeline != null && trackIndex < Timeline.Count)
            {
                trackId = Timeline[trackIndex].Id;
            }
            else
            {
                return preferredCanvas;
            }
            if (!m_ShareEmptyCanvases)
            {
                CanvasScript distinctCanvas = preferredCanvas != null
                    ? preferredCanvas
                    : App.Scene.AddCanvas();
                m_EmptyCanvases.Add(distinctCanvas);
                m_EmptyCanvasTrackIds[distinctCanvas] = trackId;
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
            m_EmptyCanvasTrackIds[canvas] = trackId;
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
            CanvasScript replacement = null;
            if (m_ShareEmptyCanvases)
            {
                replacement = App.Scene.AddCanvas();
                replacement.gameObject.name = populatedCanvas.gameObject.name;
                replacement.LocalPose = populatedCanvas.LocalPose;
                replacement.gameObject.SetActive(populatedCanvas.gameObject.activeSelf);
            }
            m_EmptyCanvases.Remove(populatedCanvas);
            m_EmptyCanvasTrackIds.Remove(populatedCanvas);
            if (replacement != null)
            {
                m_EmptyCanvasByTrackId[Timeline[trackIndex].Id] = replacement;
                m_EmptyCanvases.Add(replacement);
                m_EmptyCanvasTrackIds[replacement] = Timeline[trackIndex].Id;
            }
            else
            {
                m_EmptyCanvasByTrackId.Remove(Timeline[trackIndex].Id);
            }

            AnimationDrawingId drawingId = GetOrCreateDrawingId(populatedCanvas);
            ApplySparseTimelineEdit(tracks =>
            {
                for (int frameIndex = selectedSpan.StartFrame;
                    frameIndex < selectedSpan.EndFrameExclusive; frameIndex++)
                {
                    AnimationTimelineModel.FrameValue value = tracks[trackIndex].Frames[frameIndex];
                    tracks[trackIndex].Frames[frameIndex] =
                        new AnimationTimelineModel.FrameValue(
                            drawingId, value.Deleted, value.FrameExists, value.PathToken);
                }
            });
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
            AnimationDrawingId drawingId = GetOrCreateDrawingId(contentCanvas);
            ApplySparseTimelineEdit(tracks =>
            {
                for (int i = span.StartFrame; i < span.EndFrameExclusive; i++)
                {
                    AnimationTimelineModel.FrameValue value = tracks[trackIndex].Frames[i];
                    tracks[trackIndex].Frames[i] = new AnimationTimelineModel.FrameValue(
                        drawingId, value.Deleted, value.FrameExists, value.PathToken);
                }
            });
            return contentCanvas;
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
            m_EmptyCanvasTrackIds.Clear();
            m_EmptyCanvases.Clear();
            m_CanvasDrawingIds.Clear();
            m_DrawingCanvases.Clear();
            m_DrawingReferences.Clear();
            m_PendingDrawingDestruction.Clear();
            m_PendingDrawingDemotion.Clear();
            m_CanvasesBeingDestroyed.Clear();
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

        internal void GetDrawingContentCountsForStats(
            CanvasScript canvas, out int strokeCount, out int widgetCount)
        {
            EnsureDrawingContentIndex();
            strokeCount = m_CanvasStrokes.TryGetValue(canvas, out HashSet<Stroke> strokes)
                ? strokes.Count
                : 0;
            widgetCount = m_CanvasWidgets.TryGetValue(canvas, out HashSet<GrabWidget> widgets)
                ? widgets.Count
                : 0;
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

            CameraPathWidget previousPath = Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath;
            if (previousPath != null)
            {
                WidgetManager.m_Instance.DeleteCameraPath(previousPath);
            }

            int i = GetFollowingFrameIndex(Loc.Item1, Loc.Item2).Item2 - Loc.Item2;
            ApplySparseTimelineEdit(tracks =>
            {
                for (int c = 0; c < i; c++)
                {
                    AnimationTimelineModel.FrameValue value = tracks[Loc.Item1].Frames[Loc.Item2 + c];
                    tracks[Loc.Item1].Frames[Loc.Item2 + c] =
                        new AnimationTimelineModel.FrameValue(
                            value.DrawingId, value.Deleted, value.FrameExists,
                            pathwidget, value.SpanIdentity);
                }
            });
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

            CameraPathWidget previousPath = Timeline[Loc.Item1].Frames[Loc.Item2].AnimatedPath;
            if (previousPath != null)
            {
                WidgetManager.m_Instance.DeleteCameraPath(previousPath);
            }

            int i = GetFollowingFrameIndex(Loc.Item1, Loc.Item2).Item2 - Loc.Item2;
            ApplySparseTimelineEdit(tracks =>
            {
                for (int c = 0; c < i; c++)
                {
                    AnimationTimelineModel.FrameValue value = tracks[Loc.Item1].Frames[Loc.Item2 + c];
                    tracks[Loc.Item1].Frames[Loc.Item2 + c] =
                        new AnimationTimelineModel.FrameValue(
                            value.DrawingId, value.Deleted, value.FrameExists,
                            pathwidget, value.SpanIdentity);
                }
            });

            ResetTimeline();
        }

        public CanvasScript AddLayerRefresh(CanvasScript canvasAdding)
        {
            EnsureSparseTimeline();
            int timelineLength = GetTimelineLength();
            Track addingTrack = NewTrack();
            m_EmptyCanvasByTrackId[addingTrack.Id] = canvasAdding;
            m_EmptyCanvases.Add(canvasAdding);
            AnimationTimelineModel.FrameValue emptyValue = NewEmptyFrameValue();
            ApplySparseTimelineEdit(tracks =>
            {
                var frames = new List<AnimationTimelineModel.FrameValue>(timelineLength);
                for (int i = 0; i < timelineLength; i++) frames.Add(emptyValue);
                tracks.Add(new AnimationTimelineModel.EditableTrack(
                    addingTrack.Id, true, false, frames));
            });
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
                if (!m_EmptyCanvasTrackIds.TryGetValue(canvas, out int trackId) ||
                    !m_SparseTimeline.TryGetTrackIndex(trackId, out int trackIndex))
                {
                    return (-1, -1);
                }
                if (!m_ShareEmptyCanvases)
                {
                    int frameIndex = Timeline[trackIndex].Frames.FindIndex(
                        frame => frame.Canvas == canvas);
                    return frameIndex >= 0 ? (trackIndex, frameIndex) : (-1, -1);
                }
                if (FrameOn >= 0 &&
                    m_SparseTimeline.TryResolve(
                        trackIndex, FrameOn, out AnimationTimelineModel.Span currentSpan) &&
                    currentSpan.Value.DrawingId.IsEmpty)
                {
                    return (trackIndex, FrameOn);
                }
                AnimationTimelineModel.Span firstEmptySpan = m_SparseTimeline.Tracks[trackIndex]
                    .Spans.FirstOrDefault(span => span.Value.DrawingId.IsEmpty);
                return firstEmptySpan.Duration > 0
                    ? (trackIndex, firstEmptySpan.StartFrame)
                    : (-1, -1);
            }

            if (!m_CanvasDrawingIds.TryGetValue(canvas, out AnimationDrawingId drawingId))
            {
                return (-1, -1);
            }
            if (m_SparseTimeline.TryGetDrawingLocation(drawingId, out (int, int) location) &&
                LocationStillMatches(canvas, location))
            {
                return location;
            }

            return (-1, -1);
        }

        public (int, int) GetSerializableTimelineLocation((int, int) runtimeLocation)
        {
            EnsureSparseTimeline();
            int runtimeTrack = runtimeLocation.Item1;
            if (!m_SparseTimeline.TryGetSerializableTrackIndex(
                runtimeTrack, out int serializedTrack))
            {
                return (-1, -1);
            }
            return (serializedTrack, runtimeLocation.Item2);
        }

        public (int, int) GetSerializableCanvasLocation(CanvasScript canvas)
        {
            return GetSerializableTimelineLocation(GetCanvasLocation(canvas));
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

        public IEnumerable<(CanvasScript Canvas, int Frame, int Track)>
            GetAnimationDrawingSaveLocations()
        {
            EnsureSparseTimeline();
            foreach (AnimationTimelineModel.SerializableDrawingLocation location in
                m_SparseTimeline.EnumerateSerializableDrawingLocations())
            {
                CanvasScript canvas = GetCanvasForDrawing(location.DrawingId);
                if (canvas != null)
                {
                    yield return (canvas, location.Frame, location.Track);
                }
            }
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
            EnsureSparseTimeline();
            List<int> activeTrackIndexes = new();
            for (int trackIndex = 0; trackIndex < m_SparseTimeline.Tracks.Count; trackIndex++)
            {
                if (m_SparseTimeline.Tracks[trackIndex].Deleted) continue;
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
                SetTrackVisibility(canvasIndex.Item1, visible);
            }
        }

        public void SetTrackVisibility(int trackIndex, bool visible)
        {
            EnsureSparseTimeline();
            if (trackIndex < 0 || trackIndex >= m_SparseTimeline.Tracks.Count) return;
            ApplySparseTimelineEdit(tracks => tracks[trackIndex].Visible = visible);
        }

        public void MarkLayerAsDeleteRefresh(CanvasScript canvas)
        {
            (int, int) canvasIndex = GetCanvasLocation(canvas);
            if (canvasIndex.Item2 != -1)
            {
                ApplySparseTimelineEdit(tracks => tracks[canvasIndex.Item1].Deleted = true);
            }
            ResetTimeline();
        }

        public void MarkLayerAsNotDeleteRefresh(CanvasScript canvas)
        {
            (int, int) canvasIndex = GetCanvasLocation(canvas);
            if (canvasIndex.Item2 != -1)
            {
                ApplySparseTimelineEdit(tracks => tracks[canvasIndex.Item1].Deleted = false);
            }
            ResetTimeline();
        }

        public int GetTimelineLength()
        {
            EnsureSparseTimeline();
            return m_CachedTimelineLength;
        }

        public void ResetTimeline()
        {
            Profiler.BeginSample("OB_ANIM_SCALE.StructuralTimelineRefresh");
            InvalidateTimelineProjection();
            m_PerformanceStats?.RecordTimelineReset();
            UpdateNodes();
            UpdateTimelineSlider();
            UpdateTimelineNob();
            UpdateTrackScroll();
            UpdateUI();
            App.Scene.TriggerLayersUpdate();
            Profiler.EndSample();
        }

        private void RefreshTimelineScroll()
        {
            UpdateNodes();
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
            Profiler.BeginSample("OB_ANIM_SCALE.VisibleTimelineCells");
            const float frameSpacing = 0.1971429f;
            int timelineLength = GetTimelineLength();
            int poolSize = Math.Max(1, m_TimelineFramePoolSize);
            int firstVisibleFrame = GetFirstPooledTimelineFrame(timelineLength, poolSize);

            while (frameNotchesWidget.transform.childCount < poolSize)
            {
                Instantiate(timelineNotchPrefab, frameNotchesWidget.transform, false);
            }
            for (int slot = 0; slot < frameNotchesWidget.transform.childCount; slot++)
            {
                Transform notch = frameNotchesWidget.transform.GetChild(slot);
                int frameIndex = firstVisibleFrame + slot;
                bool visible = slot < poolSize && frameIndex < timelineLength;
                notch.gameObject.SetActive(visible);
                if (!visible) continue;
                notch.localPosition = new Vector3(frameIndex * frameSpacing, 0, 0.0087f);
                notch.FindChild("Num").GetComponent<TextMeshPro>().text = $"{frameIndex + 1}";
            }

            List<int> activeTrackIndexes = ActiveTrackIndexes();
            (int, int) activeCanvasLocation = GetCanvasLocation(App.Scene.ActiveCanvas);

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

                int trackFrameCount = Timeline[scrolledTrack].Frames.Count;
                if (trackFrameCount > 0)
                {
                    trackNodesWidget[localIndex].SetActive(true);
                    Transform trackTransform = trackNodesWidget[localIndex].transform;
                    while (trackTransform.childCount < poolSize)
                    {
                        GameObject newFrame = Instantiate(
                            frameButtonPrefab, trackTransform, false);
                        newFrame.transform.localScale = new Vector3(0.16175f, 0.16175f, 0.16175f);
                    }

                    for (int slot = 0; slot < trackTransform.childCount; slot++)
                    {
                        Transform frameWrapper = trackTransform.GetChild(slot);
                        Transform frameButton = frameWrapper.GetChild(0);
                        int frameIndex = firstVisibleFrame + slot;
                        bool frameExists = slot < poolSize && frameIndex < trackFrameCount;
                        frameWrapper.gameObject.SetActive(frameExists);
                        if (!frameExists) continue;
                        frameWrapper.localPosition = new Vector3(
                            frameIndex * frameSpacing, 0, -0.029f);

                        foreach (Transform buttonState in frameButton)
                        {
                            buttonState.gameObject.SetActive(false);
                        }
                        frameButton.GetComponent<FrameButton>().SetButtonCoordinate(
                            scrolledTrack, frameIndex);

                        bool filled = GetFrameFilled(scrolledTrack, frameIndex);
                        bool backwardsConnect = frameIndex > 0 &&
                            FramesShareSpan(scrolledTrack, frameIndex, frameIndex - 1);
                        bool forwardConnect = frameIndex < trackFrameCount - 1 &&
                            FramesShareSpan(scrolledTrack, frameIndex, frameIndex + 1);
                        frameButton.GetChild(Convert.ToInt32(filled)).gameObject.SetActive(true);

                        int backBox = 6;
                        frameButton.GetChild(backBox).gameObject.SetActive(true);

                        Color backColor;
                        if (filled)
                        {
                            if (Timeline[scrolledTrack].Frames[frameIndex].Canvas == App.Scene.ActiveCanvas)
                            {
                                backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                            }
                            else
                            {
                                backColor = Color.black;
                            }
                        }
                        else
                        {
                            if (activeCanvasLocation.Item1 == scrolledTrack && frameIndex == FrameOn)
                            {
                                backColor = new Color(150 / 255f, 150 / 255f, 150 / 255f);
                            }
                            else
                            {
                                backColor = Color.black;
                            }
                        }

                        frameButton.GetChild(backBox).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox + 1).gameObject.GetComponent<SpriteRenderer>().color = backColor;
                        frameButton.GetChild(backBox + 2).gameObject.GetComponent<SpriteRenderer>().color = backColor;

                        if (backwardsConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 2).gameObject.SetActive(true);
                            frameButton.GetChild(backBox + 1).gameObject.SetActive(true);
                        }

                        if (forwardConnect)
                        {
                            frameButton.GetChild(Convert.ToInt32(filled) + 4).gameObject.SetActive(true);
                            frameButton.GetChild(backBox + 2).gameObject.SetActive(true);
                        }
                    }
                }
            }
            Profiler.EndSample();
        }

        private int GetFirstPooledTimelineFrame(int timelineLength, int poolSize)
        {
            if (timelineLength <= poolSize) return 0;
            int firstFrame = Mathf.FloorToInt(m_TimelineOffset / m_SliderFrameSize) - 1;
            return Math.Clamp(firstFrame, 0, timelineLength - poolSize);
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
            UpdateNodes();
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
            bool viewportChanged = !Mathf.Approximately(
                previousTimelineOffset, m_TimelineOffset);
            if (viewportChanged) UpdateNodes();
            if (updateTimelineLayout || viewportChanged)
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
            Profiler.BeginSample("OB_ANIM_SCALE.FrameVisibility");
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
            Profiler.EndSample();

            Profiler.BeginSample("OB_ANIM_SCALE.ActiveCanvasRebuild");
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
            Profiler.EndSample();

            m_PreviousShowingFrame = frameIndex;
            UpdateUI(timelineInput, updateTimelineLayout: !playbackUpdate);
            if (!playbackUpdate) App.Scene.TriggerLayersUpdate();
            Profiler.EndSample();
        }

        public DeleteFrameOperation RemoveKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);
            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            Frame deletedFrame = Timeline[index.Item1].Frames[index.Item2];

            App.Scene.HideCanvas(deletedFrame.Canvas);
            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.RemoveSpan(
                    tracks, index.Item1, index.Item2, nextIndex.Item2 - index.Item2,
                    NewEmptyFrameValue, IsSparseFrameFilled);
            });

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
            HashSet<CanvasScript> previousCanvases = GetDrawingCanvases(Timeline);
            ApplySparseTimelineEdit(tracks =>
            {
                int contentLength = GetSparseTimelineContentLength(tracks);
                foreach (AnimationTimelineModel.EditableTrack track in tracks)
                {
                    if (track.Frames.Count > contentLength)
                    {
                        track.Frames.RemoveRange(
                            contentLength, track.Frames.Count - contentLength);
                    }
                }
            });
            HashSet<CanvasScript> retainedCanvases = GetDrawingCanvases(Timeline);
            foreach (CanvasScript canvas in previousCanvases.Where(
                canvas => !retainedCanvases.Contains(canvas)))
            {
                DestroyTimelineCanvas(canvas);
            }
        }

        public void FillTimeline()
        {
            int maxTimeline = GetTimelineLength();
            ApplySparseTimelineEdit(tracks => PadSparseTracks(tracks, maxTimeline));
        }

        // Make sure there aren't too many or too few empty frames
        public void FillandCleanTimeline()
        {
            FillTimeline();
            CleanTimeline();
        }

        public (int, int) MoveKeyFrame(bool moveRight, int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            int frameLength = GetFrameLength(index.Item1, index.Item2);
            int followingFrame = index.Item2 + frameLength;
            if (moveRight && followingFrame < Timeline[index.Item1].Frames.Count &&
                GetFrameFilled(index.Item1, followingFrame))
            {
                return (-1, -1);
            }
            if (!moveRight && (index.Item2 == 0 ||
                GetFrameFilled(index.Item1, index.Item2 - 1)))
            {
                return (-1, -1);
            }
            int movedFrame = index.Item2;
            bool moved = false;
            ApplySparseTimelineEdit(tracks =>
            {
                moved = AnimationTimelineOperations.MoveSpan(
                    tracks, index.Item1, index.Item2, frameLength, moveRight,
                    NewEmptyFrameValue, IsSparseFrameFilled, out movedFrame);
            });
            if (!moved) return (-1, -1);

            (int, int) movedTo = (index.Item1, movedFrame);
            SelectTimelineFrame(movedTo.Item1, movedTo.Item2);
            return movedTo;
        }

        // For loading the scene
        // TODO Hidden by overloads
        public void AddKeyFrame(int trackNum)
        {
            (int, int) index = (trackNum, Timeline[trackNum].Frames.Count - 1);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            ApplySparseTimelineEdit(tracks =>
            {
                bool insert = nextIndex.Item2 < tracks[nextIndex.Item1].Frames.Count &&
                    IsSparseFrameFilled(tracks[nextIndex.Item1].Frames[nextIndex.Item2]);
                AnimationTimelineOperations.InsertEmptyKey(
                    tracks, nextIndex.Item1, nextIndex.Item2, insert,
                    NewEmptyFrameValue, alignTracks: false);
            });
        }

        public AddFrameOperation AddKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            (int, int) insertingAt;
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            bool append = nextIndex.Item2 >= Timeline[nextIndex.Item1].Frames.Count;
            bool insert = !append && GetFrameFilled(nextIndex.Item1, nextIndex.Item2);
            insertingAt = append
                ? (nextIndex.Item1, Timeline[nextIndex.Item1].Frames.Count)
                : nextIndex;
            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.InsertEmptyKey(
                    tracks, nextIndex.Item1, nextIndex.Item2, insert,
                    NewEmptyFrameValue, alignTracks: true);
            });

            ResetTimeline();
            SelectTimelineFrame(insertingAt.Item1, insertingAt.Item2);

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
            InvalidateTimelineProjection();

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
            (int, int) index = (trackNum, Timeline[trackNum].Frames.Count - 1);
            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineModel.FrameValue value = tracks[index.Item1].Frames[index.Item2];
                tracks[index.Item1].Frames.Insert(index.Item2 + 1, value);
            });
        }

        public FrameLengthOperation ExtendKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            if (!GetFrameFilled(index.Item1, index.Item2))
            {
                return default;
            }

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            int frameLength = GetFrameLength(index.Item1, index.Item2);
            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.ExtendSpan(
                    tracks, index.Item1, index.Item2, frameLength,
                    NewEmptyFrameValue, IsSparseFrameFilled);
            });

            m_FrameOn++;
            FocusFrame((int)m_FrameOn);
            ResetTimeline();
            return CompleteFrameLengthOperation(index, previousTimeline);
        }

        public FrameLengthOperation ReduceKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);
            int frameLength = GetFrameLength(index.Item1, index.Item2);
            if (frameLength <= 1)
            {
                return default;
            }

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.ReduceSpan(
                    tracks, index.Item1, index.Item2, frameLength,
                    NewEmptyFrameValue, IsSparseFrameFilled);
            });

            m_FrameOn--;
            FocusFrame(FrameOn);
            ResetTimeline();
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
            m_DrawingReferences.Retain(snapshot.DrawingIds);
            return snapshot;
        }

        public IDisposable RetainTimelineDrawingsForSave(
            IEnumerable<CanvasScript> additionalCanvases = null)
        {
            EnsureSparseTimeline();
            AnimationTimelineModel.Snapshot snapshot = m_SparseTimeline.CreateSnapshot();
            var drawingIds = new HashSet<AnimationDrawingId>(snapshot.DrawingIds);
            if (additionalCanvases != null)
            {
                foreach (CanvasScript canvas in additionalCanvases)
                {
                    if (canvas != null && m_CanvasDrawingIds.TryGetValue(
                        canvas, out AnimationDrawingId drawingId))
                    {
                        drawingIds.Add(drawingId);
                    }
                }
            }
            m_DrawingReferences.Retain(drawingIds);
            return new DrawingReferenceLease(() => ReleaseSaveDrawingReferences(drawingIds));
        }

        public void RetainDrawingForEditing(CanvasScript canvas)
        {
            if (canvas == null) return;
            EnsureSparseTimeline();
            if (m_CanvasDrawingIds.TryGetValue(canvas, out AnimationDrawingId drawingId))
            {
                m_DrawingReferences.Retain(drawingId);
            }
        }

        public void ReleaseDrawingForEditing(CanvasScript canvas)
        {
            if (canvas == null ||
                !m_CanvasDrawingIds.TryGetValue(canvas, out AnimationDrawingId drawingId))
            {
                return;
            }
            m_DrawingReferences.Release(drawingId);
            TryDemoteDrawingCanvas(canvas);
            TryDemotePendingDrawing(drawingId);
            TryDestroyPendingDrawing(drawingId);
        }

        private void ReleaseSaveDrawingReferences(IEnumerable<AnimationDrawingId> drawingIds)
        {
            foreach (AnimationDrawingId drawingId in drawingIds)
            {
                m_DrawingReferences.Release(drawingId);
                TryDemotePendingDrawing(drawingId);
                TryDestroyPendingDrawing(drawingId);
            }
        }

        private void ReleaseTimelineSnapshot(AnimationTimelineModel.Snapshot snapshot)
        {
            if (snapshot == null) return;
            foreach (AnimationDrawingId drawingId in snapshot.DrawingIds)
            {
                m_DrawingReferences.Release(drawingId);
                TryDemotePendingDrawing(drawingId);
                TryDestroyPendingDrawing(drawingId);
            }
        }

        private void TryDemotePendingDrawing(AnimationDrawingId drawingId)
        {
            if (!m_PendingDrawingDemotion.Contains(drawingId) ||
                !m_DrawingCanvases.TryGetValue(drawingId, out CanvasScript canvas))
            {
                return;
            }
            TryDemoteDrawingCanvas(canvas);
        }

        private void TryDemoteDrawingCanvas(CanvasScript canvas)
        {
            if (canvas == null || m_CanvasesBeingDestroyed.Contains(canvas) ||
                !m_CanvasDrawingIds.TryGetValue(
                canvas, out AnimationDrawingId drawingId))
            {
                return;
            }

            EnsureDrawingContentIndex();
            bool hasStrokeOwners = m_CanvasStrokes.TryGetValue(
                canvas, out HashSet<Stroke> strokes) && strokes.Count > 0;
            bool hasWidgetOwners = m_CanvasWidgets.TryGetValue(
                canvas, out HashSet<GrabWidget> widgets) && widgets.Count > 0;
            if (hasStrokeOwners || hasWidgetOwners)
            {
                m_PendingDrawingDemotion.Remove(drawingId);
                return;
            }

            EnsureSparseTimeline();
            var owningTracks = new List<int>();
            for (int trackIndex = 0; trackIndex < m_SparseTimeline.Tracks.Count; trackIndex++)
            {
                AnimationTimelineModel.Track track = m_SparseTimeline.Tracks[trackIndex];
                bool ownsDrawing = false;
                foreach (AnimationTimelineModel.Span span in track.Spans)
                {
                    if (span.Value.DrawingId != drawingId) continue;
                    if (span.Value.PathToken != null)
                    {
                        m_PendingDrawingDemotion.Remove(drawingId);
                        return;
                    }
                    ownsDrawing = true;
                }
                if (ownsDrawing) owningTracks.Add(trackIndex);
            }
            if (owningTracks.Count == 0)
            {
                m_PendingDrawingDemotion.Remove(drawingId);
                return;
            }
            if (owningTracks.Count != 1 || m_DrawingReferences.IsRetained(drawingId))
            {
                m_PendingDrawingDemotion.Add(drawingId);
                return;
            }

            int owningTrack = owningTracks[0];
            int trackId = m_SparseTimeline.Tracks[owningTrack].Id;
            m_EmptyCanvasByTrackId.TryGetValue(trackId, out CanvasScript existingEmptyCanvas);
            bool keepExistingEmptyCanvas = existingEmptyCanvas != null &&
                existingEmptyCanvas != canvas && App.Scene.ActiveCanvas != canvas;

            if (!keepExistingEmptyCanvas)
            {
                m_CanvasDrawingIds.Remove(canvas);
                m_DrawingCanvases.Remove(drawingId);
                m_EmptyCanvasByTrackId[trackId] = canvas;
                m_EmptyCanvasTrackIds[canvas] = trackId;
                m_EmptyCanvases.Add(canvas);
            }

            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.ReplaceDrawingWithEmptySpans(
                    tracks, drawingId, NewEmptyFrameValue);
            });

            m_PendingDrawingDemotion.Remove(drawingId);
            if (keepExistingEmptyCanvas)
            {
                DestroyTimelineCanvas(canvas);
            }
        }

        private void TryDestroyPendingDrawing(AnimationDrawingId drawingId)
        {
            if (!m_PendingDrawingDestruction.Contains(drawingId) ||
                !m_DrawingCanvases.TryGetValue(drawingId, out CanvasScript canvas))
            {
                return;
            }
            DestroyTimelineCanvas(canvas);
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

        private void RestoreTimelineSnapshot(AnimationTimelineModel.Snapshot snapshot)
        {
            m_SparseTimeline.Restore(snapshot);
            ProjectSparseTimelineToCompatibilityView();
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
            InvalidateTimelineProjection();

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
            InvalidateTimelineProjection();

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
            InvalidateTimelineProjection();

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
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);

            CanvasScript oldCanvas = Timeline[index.Item1].Frames[index.Item2].Canvas;

            AnimationPerformanceStats.RecordGlobalStrokeScan();
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
            AnimationTimelineModel.FrameValue newDrawing = NewDrawingFrameValue(newCanvas);

            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.ReplaceRange(
                    tracks, index.Item1, splittingIndex,
                    index.Item2 + frameLength - splittingIndex, newDrawing);
            });

            SelectTimelineFrame(index.Item1, splittingIndex);
            ResetTimeline();
            return CompleteKeyFrameOperation(
                (index.Item1, splittingIndex), newCanvas, newStrokes, previousTimeline);
        }

        public KeyFrameOperation DuplicateKeyFrame(int trackNum = -1, int frameNum = -1)
        {
            (int, int) index = (trackNum == -1 || frameNum == -1) ? GetCanvasLocation(App.Scene.ActiveCanvas) : (trackNum, frameNum);

            CanvasScript oldCanvas = Timeline[index.Item1].Frames[index.Item2].Canvas;

            AnimationPerformanceStats.RecordGlobalStrokeScan();
            List<Stroke> oldStrokes = SketchMemoryScript.m_Instance.GetMemoryList
                .Where(x => x.Canvas == oldCanvas).ToList();

            AnimationTimelineModel.Snapshot previousTimeline = CreateTimelineSnapshot();
            CanvasScript newCanvas = ReplicateStrokesToNewCanvas(oldStrokes, out List<Stroke> newStrokes);
            AnimationTimelineModel.FrameValue newDrawing = NewDrawingFrameValue(newCanvas);

            int frameLength = GetFrameLength(index.Item1, index.Item2);
            (int, int) nextIndex = GetFollowingFrameIndex(index.Item1, index.Item2);

            ApplySparseTimelineEdit(tracks =>
            {
                AnimationTimelineOperations.DuplicateRange(
                    tracks, nextIndex.Item1, nextIndex.Item2, frameLength, newDrawing,
                    NewEmptyFrameValue, IsSparseFrameFilled);
            });

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
            UpdateNodes();
            UpdateTimelineSlider();
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
            Profiler.BeginSample("OB_ANIM_SCALE.AnimationTransforms");
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
            Profiler.EndSample();
        }

        void Update()
        {
            Profiler.BeginSample("OB_ANIM_SCALE.AnimationUpdate");
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
            Profiler.EndSample();
        }
    }
} // namespace TiltBrush.FrameAnimation
