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

namespace TiltBrush.FrameAnimation
{
    /// Counts non-timeline drawing owners such as undo snapshots and in-progress saves.
    public sealed class AnimationDrawingReferenceTracker
    {
        private readonly Dictionary<AnimationDrawingId, int> m_References = new();

        public void Retain(IEnumerable<AnimationDrawingId> drawingIds)
        {
            if (drawingIds == null) throw new ArgumentNullException(nameof(drawingIds));
            foreach (AnimationDrawingId drawingId in drawingIds)
            {
                Retain(drawingId);
            }
        }

        public void Retain(AnimationDrawingId drawingId)
        {
            if (drawingId.IsEmpty) return;
            m_References.TryGetValue(drawingId, out int references);
            m_References[drawingId] = references + 1;
        }

        /// Returns true when this release removed the final retained reference.
        public bool Release(AnimationDrawingId drawingId)
        {
            if (!m_References.TryGetValue(drawingId, out int references)) return false;
            if (references > 1)
            {
                m_References[drawingId] = references - 1;
                return false;
            }
            m_References.Remove(drawingId);
            return true;
        }

        public bool IsRetained(AnimationDrawingId drawingId)
        {
            return m_References.ContainsKey(drawingId);
        }

        public int GetReferenceCount(AnimationDrawingId drawingId)
        {
            return m_References.TryGetValue(drawingId, out int references) ? references : 0;
        }

        public void Clear()
        {
            m_References.Clear();
        }
    }
}
