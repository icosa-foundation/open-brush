// Copyright 2024 The Open Brush Authors
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

namespace TiltBrush
{
    /// The set of strokes that a single pass of a symmetry mode laid down together: the stroke
    /// the user drew plus the copies the symmetry made of it.
    ///
    /// The group is also the record of how those strokes were made, so a stroke drawn with
    /// symmetry always has one, even in the rare case where it ends up being the only member.
    ///
    /// Strokes hold the group by reference, so peer lookup costs nothing and there is no
    /// registry to keep in sync; a group becomes garbage once its last stroke does. Identity is
    /// the object itself. Ids are assigned only when saving (see SketchWriter), which keeps the
    /// links local to a sketch and makes an additive load trivially free of collisions.
    public class SymmetryStrokeGroup
    {
        private readonly List<Stroke> m_Strokes = new List<Stroke>();

        /// The symmetry settings that were in place when this group was drawn; may be null for
        /// groups loaded from a sketch that didn't record them. Immutable, and usually shared
        /// with every other group drawn with the same settings.
        public SymmetrySettingsSnapshot Settings { get; }

        public SymmetryStrokeGroup(SymmetrySettingsSnapshot settings)
        {
            Settings = settings;
        }

        /// The strokes in the group, in the order they were recorded, which for a freshly-drawn
        /// line is pointer order.
        public IReadOnlyList<Stroke> Strokes => m_Strokes;

        public int Count => m_Strokes.Count;

        /// The strokes in the group other than the passed one.
        public IEnumerable<Stroke> PeersOf(Stroke stroke)
        {
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                if (!ReferenceEquals(m_Strokes[i], stroke))
                {
                    yield return m_Strokes[i];
                }
            }
        }

        /// Only called by Stroke.JoinSymmetryGroup, which guarantees a stroke joins once.
        internal void Add(Stroke stroke)
        {
            m_Strokes.Add(stroke);
        }

        internal void Remove(Stroke stroke)
        {
            m_Strokes.Remove(stroke);
        }

        /// Empties the group; its strokes stop being peers of each other.
        public void Disband()
        {
            // Copy, because leaving mutates m_Strokes.
            foreach (var stroke in m_Strokes.ToArray())
            {
                stroke.LeaveSymmetryGroup();
            }
        }
    }
} // namespace TiltBrush
