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

namespace TiltBrush.FrameAnimation
{
    /// Owns animation drawings and both directions of the temporary Canvas adapter index.
    public sealed class FrameDrawingRepository
    {
        private readonly Dictionary<AnimationDrawingId, FrameDrawing> m_ById = new();
        private readonly Dictionary<CanvasScript, FrameDrawing> m_ByCanvas = new();

        public int Count => m_ById.Count;
        public IEnumerable<FrameDrawing> Drawings => m_ById.Values;
        public IEnumerable<CanvasScript> Canvases => m_ById.Values.Select(drawing => drawing.Canvas);

        public FrameDrawing GetOrCreate(
            CanvasScript canvas, Func<AnimationDrawingId> createDrawingId)
        {
            if (canvas == null) throw new ArgumentNullException(nameof(canvas));
            if (createDrawingId == null) throw new ArgumentNullException(nameof(createDrawingId));
            if (m_ByCanvas.TryGetValue(canvas, out FrameDrawing drawing)) return drawing;

            AnimationDrawingId drawingId = createDrawingId();
            if (drawingId.IsEmpty || m_ById.ContainsKey(drawingId))
            {
                throw new InvalidOperationException($"Drawing ID {drawingId.Value} is not available.");
            }
            drawing = new FrameDrawing(drawingId, canvas);
            m_ById.Add(drawingId, drawing);
            m_ByCanvas.Add(canvas, drawing);
            return drawing;
        }

        public bool TryGet(AnimationDrawingId drawingId, out FrameDrawing drawing)
        {
            drawing = null;
            return !drawingId.IsEmpty && m_ById.TryGetValue(drawingId, out drawing);
        }

        public bool TryGet(CanvasScript canvas, out FrameDrawing drawing)
        {
            drawing = null;
            return canvas != null && m_ByCanvas.TryGetValue(canvas, out drawing);
        }

        public bool TryGetCanvas(AnimationDrawingId drawingId, out CanvasScript canvas)
        {
            canvas = null;
            if (!TryGet(drawingId, out FrameDrawing drawing)) return false;
            canvas = drawing.Canvas;
            return canvas != null;
        }

        public bool TryGetDrawingId(CanvasScript canvas, out AnimationDrawingId drawingId)
        {
            drawingId = default;
            if (!TryGet(canvas, out FrameDrawing drawing)) return false;
            drawingId = drawing.Id;
            return true;
        }

        public bool Remove(AnimationDrawingId drawingId)
        {
            if (!m_ById.TryGetValue(drawingId, out FrameDrawing drawing)) return false;
            m_ById.Remove(drawingId);
            if (drawing.Canvas != null) m_ByCanvas.Remove(drawing.Canvas);
            return true;
        }

        public bool Remove(CanvasScript canvas)
        {
            if (!TryGet(canvas, out FrameDrawing drawing)) return false;
            return Remove(drawing.Id);
        }

        public void Clear()
        {
            m_ById.Clear();
            m_ByCanvas.Clear();
        }

        /// Repairs only the adapter index. This is a development recovery path, not normal lookup.
        public bool TryRepairCanvasIndex(CanvasScript canvas, out FrameDrawing drawing)
        {
            drawing = null;
            if (canvas == null) return false;
            drawing = m_ById.Values.FirstOrDefault(candidate => candidate.Canvas == canvas);
            if (drawing == null) return false;
            m_ByCanvas[canvas] = drawing;
            return true;
        }

#if UNITY_EDITOR
        internal bool RemoveCanvasIndexForTests(CanvasScript canvas)
        {
            return canvas != null && m_ByCanvas.Remove(canvas);
        }
#endif
    }
}
