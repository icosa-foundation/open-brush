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
using UnityEngine;

namespace TiltBrush
{
    // Per-layer (per-dancer) time offsets for the post-production preview. Wraps the
    // playback timeline and shifts every time it reports for a stroke (start, end, each
    // control point) by that stroke's layer offset, so each dancer is drawn as if their
    // strokes happened earlier or later. One sketch clock, one playback, nothing on disk
    // changes.
    //
    // Convention matches the global offset: video time = sketch time + global + layer.
    // The plate follows the global offset only, so a stroke recorded at sketch time s on
    // layer L must appear when the sketch clock reaches s + layerOffset.
    //
    // Offsets are captured when the playback is built; changing one rebuilds the playback
    // (SketchMemoryScript.RebuildPlaybackKeepingTime) because strokes are kept sorted by
    // start time.
    public class PostProductionLayerTimeline : IStrokePlaybackTimeline
    {
        private readonly IStrokePlaybackTimeline m_Inner;
        private readonly Dictionary<CanvasScript, long> m_OffsetByCanvas;
        private readonly Dictionary<Stroke, long> m_OffsetByStroke = new Dictionary<Stroke, long>();

        // Returns the inner timeline unchanged when no layer has an offset, so ordinary
        // playback is unaffected.
        public static IStrokePlaybackTimeline Wrap(IStrokePlaybackTimeline inner)
        {
            var offsets = new Dictionary<CanvasScript, long>();
            if (App.Scene != null)
            {
                int index = 0;
                foreach (var canvas in App.Scene.LayerCanvases)
                {
                    int ms = PostProductionSettings.GetLayerTimeOffsetMs(index);
                    if (ms != 0) { offsets[canvas] = ms; }
                    index++;
                }
            }
            return offsets.Count == 0 ? inner : new PostProductionLayerTimeline(inner, offsets);
        }

        private PostProductionLayerTimeline(IStrokePlaybackTimeline inner, Dictionary<CanvasScript, long> offsets)
        {
            m_Inner = inner;
            m_OffsetByCanvas = offsets;
        }

        public long GetHeadTimeMs(Stroke stroke) => m_Inner.GetHeadTimeMs(stroke) + OffsetFor(stroke);
        public long GetTailTimeMs(Stroke stroke) => m_Inner.GetTailTimeMs(stroke) + OffsetFor(stroke);
        public long GetControlPointTimeMs(Stroke stroke, uint timestampMs) =>
            m_Inner.GetControlPointTimeMs(stroke, timestampMs) + OffsetFor(stroke);

        private long OffsetFor(Stroke stroke)
        {
            if (m_OffsetByStroke.TryGetValue(stroke, out long cached)) { return cached; }
            CanvasScript canvas = stroke.m_IntendedCanvas != null ? stroke.m_IntendedCanvas : App.Scene.MainCanvas;
            m_OffsetByCanvas.TryGetValue(canvas, out long offset);
            m_OffsetByStroke[stroke] = offset;
            return offset;
        }
    }
}
