// Copyright 2020 The Tilt Brush Authors
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
using UnityEngine;

namespace TiltBrush
{

    public interface IStrokePlaybackTimeline
    {
        long GetHeadTimeMs(Stroke stroke);
        long GetTailTimeMs(Stroke stroke);
        long GetControlPointTimeMs(Stroke stroke, uint timestampMs);
    }

    public class SketchTimeStrokePlaybackTimeline : IStrokePlaybackTimeline
    {
        public long GetHeadTimeMs(Stroke stroke) => stroke.HeadTimestampMs;
        public long GetTailTimeMs(Stroke stroke) => stroke.TailTimestampMs;
        public long GetControlPointTimeMs(Stroke stroke, uint timestampMs) => timestampMs;
    }

    public class RealTimeStrokePlaybackTimeline : IStrokePlaybackTimeline
    {
        private readonly Dictionary<Stroke, StrokeTimeSessionMetadata> m_strokeSessions;
        private readonly long m_startUtcMs;

        private RealTimeStrokePlaybackTimeline(
            Dictionary<Stroke, StrokeTimeSessionMetadata> strokeSessions,
            long startUtcMs)
        {
            m_strokeSessions = strokeSessions;
            m_startUtcMs = startUtcMs;
        }

        public static bool TryCreate(
            IEnumerable<Stroke> strokes,
            out RealTimeStrokePlaybackTimeline timeline)
        {
            var strokeSessions = new Dictionary<Stroke, StrokeTimeSessionMetadata>();
            long startUtcMs = long.MaxValue;
            bool hasStroke = false;

            foreach (var stroke in strokes)
            {
                if (stroke == null || stroke.m_ControlPoints == null ||
                    stroke.m_ControlPoints.Length == 0)
                {
                    continue;
                }

                hasStroke = true;
                if (!SketchMemoryScript.m_Instance.TryGetStrokeTimeSession(
                    stroke, out var session))
                {
                    timeline = null;
                    return false;
                }

                try
                {
                    DateTimeOffset.FromUnixTimeMilliseconds(session.StartUtcMs);
                    long headUtcMs = checked(
                        session.StartUtcMs +
                        ((long)stroke.HeadTimestampMs - session.StartSketchTimeMs));
                    long tailUtcMs = checked(
                        session.StartUtcMs +
                        ((long)stroke.TailTimestampMs - session.StartSketchTimeMs));
                    if (tailUtcMs < headUtcMs)
                    {
                        timeline = null;
                        return false;
                    }
                    startUtcMs = Math.Min(startUtcMs, headUtcMs);
                    strokeSessions.Add(stroke, session);
                }
                catch (OverflowException)
                {
                    timeline = null;
                    return false;
                }
                catch (ArgumentOutOfRangeException)
                {
                    timeline = null;
                    return false;
                }
            }

            if (!hasStroke)
            {
                timeline = null;
                return false;
            }

            timeline = new RealTimeStrokePlaybackTimeline(strokeSessions, startUtcMs);
            return true;
        }

        public long GetHeadTimeMs(Stroke stroke)
        {
            return GetControlPointTimeMs(stroke, stroke.HeadTimestampMs);
        }

        public long GetTailTimeMs(Stroke stroke)
        {
            return GetControlPointTimeMs(stroke, stroke.TailTimestampMs);
        }

        public long GetControlPointTimeMs(Stroke stroke, uint timestampMs)
        {
            var session = m_strokeSessions[stroke];
            return checked(
                session.StartUtcMs + ((long)timestampMs - session.StartSketchTimeMs) -
                m_startUtcMs);
        }
    }

    public class StrokePlaybackByTime : StrokePlayback
    {
        private LinkedListNode<Stroke> m_strokeNode;
        private IStrokePlaybackTimeline m_timeline;

        public LinkedListNode<Stroke> StrokeNode
        {
            get { return m_strokeNode; }
        }

        public void Init(LinkedListNode<Stroke> memoryObjectNode,
                         PointerScript pointer, CanvasScript canvas,
                         IStrokePlaybackTimeline timeline)
        {
            m_strokeNode = memoryObjectNode;
            m_timeline = timeline;
            BaseInit(memoryObjectNode.Value, pointer, canvas);
        }

        public override void ClearPlayback()
        {
            m_strokeNode = null;
            base.ClearPlayback();
        }

        protected override bool IsControlPointReady(PointerManager.ControlPoint controlPoint)
        {
            long currentTimeMs = (long)(App.Instance.CurrentSketchTime * 1000);
            return m_timeline.GetControlPointTimeMs(
                m_stroke, controlPoint.m_TimestampMs) <= currentTimeMs;
        }
    }

    // Playback using stroke timestamps and supporting layering in time and timeline scrub.
    //
    // When moving forward we need strokes ordered by head timestamp so that we can schedule
    // rendering, and when moving backward we need tail timestamp ordering so that we can
    // delete the minimum set of affected strokes.  Our stroke accounting has them exist in
    // one of three places:
    //     1) an "unrendered" linked list (ordered by head timestamp)
    //     2) assigned to a pointer for rendering
    //     3) a "rendered" linked list (ordered by tail timestamp)
    //
    // For our use patterns, the insertions into the ordered linked lists are
    // effectively O(1) complexity:
    //     * strokes are added to rendered list in order after each is completed, so
    //       insert will traverse at most num_pointers nodes
    //     * strokes are added to unrendered list in order from the head of rendered list, so
    //       insert will traverse at most num_overlapping_strokes nodes (i.e. number of
    //       strokes overlapping in time with the inserted stroke)
    public class ScenePlaybackByTimeLayered : IScenePlayback
    {
        // Array of pending stroke playbacks indexed by pointer.
        private StrokePlaybackByTime[] m_strokePlaybacks;
        private long m_lastTimeMs = 0;
        // List of unrendered strokes ordered by head timestamp, earliest first
        private SortedLinkedList<Stroke> m_unrenderedStrokes;
        // List of rendered strokes ordered by tail timestamp, latest first
        private SortedLinkedList<Stroke> m_renderedStrokes;
        private int m_strokeCount;
        private int m_maxPointerUnderrun = 0;
        private CanvasScript m_targetCanvas;
        private IStrokePlaybackTimeline m_timeline;
        private bool m_quickLoadRemaining;

        public int MaxPointerUnderrun { get { return m_maxPointerUnderrun; } }
        public int MemoryObjectsDrawn { get { return 0; } } // unimplemented

        // Input strokes must be ordered by head timestamp
        public ScenePlaybackByTimeLayered(
            IEnumerable<Stroke> strokes,
            IStrokePlaybackTimeline timeline = null)
        {
            m_timeline = timeline ?? new SketchTimeStrokePlaybackTimeline();
            m_targetCanvas = App.ActiveCanvas;
            m_unrenderedStrokes = new SortedLinkedList<Stroke>(
                (a, b) => (m_timeline.GetHeadTimeMs(a) < m_timeline.GetHeadTimeMs(b)),
                strokes);
            m_strokeCount = m_unrenderedStrokes.Count;
            m_renderedStrokes = new SortedLinkedList<Stroke>(
                (a, b) => (m_timeline.GetTailTimeMs(a) >= m_timeline.GetTailTimeMs(b)),
                new Stroke[] { });
            m_strokePlaybacks = new StrokePlaybackByTime[PointerManager.m_Instance.NumTransientPointers];
            for (int i = 0; i < m_strokePlaybacks.Length; ++i)
            {
                m_strokePlaybacks[i] = new StrokePlaybackByTime();
            }
        }

        // Continue drawing stroke for this frame, returning true if more rendering is pending.
        public bool Update()
        {
            long currentTimeMs = m_quickLoadRemaining
                ? long.MaxValue
                : (long)(App.Instance.CurrentSketchTime * 1000);

            // Handle a jump back in time by resetting corresponding in-flight or completed strokes
            // to the undrawn state.
            if (currentTimeMs < m_lastTimeMs)
            {
                // any stroke in progress is implicated by rewind-- clear the stroke's playback
                foreach (var stroke in m_strokePlaybacks)
                {
                    if (!stroke.IsDone())
                    {
                        var pendingNode = stroke.StrokeNode;
                        stroke.ClearPlayback();
                        SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(pendingNode.Value);
                        m_unrenderedStrokes.Insert(pendingNode);
                    }
                }
                // delete any stroke having final timestamp > new current time
                while (m_renderedStrokes.Count > 0 &&
                    m_timeline.GetTailTimeMs(m_renderedStrokes.First.Value) > currentTimeMs)
                {
                    var node = m_renderedStrokes.PopFirst();
                    if (node.Value.IsVisibleForPlayback)
                    {
                        // TODO: remove SketchMemory cyclical dependency
                        // TODO: sub-stroke unrender to eliminate needless geometry thrashing within a frame
                        SketchMemoryScript.m_Instance.UnrenderStrokeMemoryObject(node.Value);
                    }
                    m_unrenderedStrokes.Insert(node);
                }
            }

            int pendingStrokes = 0;
            if (currentTimeMs != 0)
            {
                for (int i = 0; i < m_strokePlaybacks.Length; ++i)
                {
                    var stroke = m_strokePlaybacks[i];
                    // update any pending stroke from last frame
                    stroke.Update();
                    if (stroke.IsDone() && stroke.StrokeNode != null)
                    {
                        m_renderedStrokes.Insert(stroke.StrokeNode);
                        stroke.ClearPlayback();
                    }
                    // grab and play available strokes, until one is left pending
                    while (stroke.IsDone() && m_unrenderedStrokes.Count > 0 &&
                        (m_timeline.GetHeadTimeMs(m_unrenderedStrokes.First.Value) <= currentTimeMs ||
                        !m_unrenderedStrokes.First.Value.IsVisibleForPlayback))
                    {
                        var node = m_unrenderedStrokes.PopFirst();
                        if (node.Value.IsVisibleForPlayback)
                        {
                            stroke.Init(
                                node, PointerManager.m_Instance.GetTransientPointer(i),
                                m_targetCanvas, m_timeline);
                            stroke.Update();
                            if (stroke.IsDone())
                            {
                                m_renderedStrokes.Insert(stroke.StrokeNode);
                                stroke.ClearPlayback();
                            }
                        }
                        else
                        {
                            m_renderedStrokes.Insert(node);
                        }
                    }
                    if (!stroke.IsDone())
                    {
                        ++pendingStrokes;
                    }
                }

                // check for pointer underrun
                int underrun = 0;
                foreach (var obj in m_unrenderedStrokes)
                {
                    if (!obj.IsVisibleForPlayback)
                    {
                        continue;
                    }
                    if (m_timeline.GetHeadTimeMs(obj) <= currentTimeMs)
                    {
                        ++underrun;
                    }
                    else
                    {
                        break;
                    }
                }
                m_maxPointerUnderrun = Mathf.Max(m_maxPointerUnderrun, underrun);
            }

            Debug.Assert(
                m_renderedStrokes.Count + pendingStrokes + m_unrenderedStrokes.Count == m_strokeCount);
            m_lastTimeMs = currentTimeMs;
            return !(m_unrenderedStrokes.Count == 0 && pendingStrokes == 0);
        }

        public void AddStroke(Stroke stroke)
        {
            // We expect call when user has completed stroke, so add to rendered list.  List
            // is sorted by end time and we expect new node to land at the head.
            m_renderedStrokes.Insert(stroke.m_PlaybackNode);
            ++m_strokeCount;
        }

        public void RemoveStroke(Stroke stroke)
        {
            // Only allowed for strokes in rendered or unrendered list.  In current use from ClearRedo,
            // it will always be unrendered.
            Debug.Assert(stroke.m_PlaybackNode.List != null);
            stroke.m_PlaybackNode.List.Remove(stroke.m_PlaybackNode); // O(1)
            --m_strokeCount;
        }

        public void QuickLoadRemaining()
        {
            m_quickLoadRemaining = true;
            App.Instance.CurrentSketchTime = float.MaxValue;
        }
    }

} // namespace TiltBrush
