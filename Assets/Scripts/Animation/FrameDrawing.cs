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

namespace TiltBrush.FrameAnimation
{
    /// Logical identity and mutable-content boundary for one animation drawing.
    ///
    /// Phase 4A keeps a universal Canvas backing so established authoring and rendering code
    /// remains unchanged. Later phases can attach a render proxy without changing timeline
    /// identity or the ownership rules represented here.
    public sealed class FrameDrawing
    {
        public AnimationDrawingId Id { get; }
        public CanvasScript Canvas { get; private set; }
        public long ContentRevision { get; private set; }

        internal FrameDrawing(AnimationDrawingId id, CanvasScript canvas)
        {
            if (id.IsEmpty) throw new ArgumentException("A drawing requires a non-empty ID.", nameof(id));
            Id = id;
            AttachCanvas(canvas);
        }

        internal void AttachCanvas(CanvasScript canvas)
        {
            Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        }

        internal void MarkContentChanged()
        {
            ContentRevision++;
        }
    }
}
