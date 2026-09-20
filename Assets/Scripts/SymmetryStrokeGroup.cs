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

using System;
using System.Collections.Generic;

namespace TiltBrush
{
    /// The set of strokes that were laid down together by a single pass of a symmetry mode:
    /// the stroke the user drew plus the copies the symmetry made of it.
    ///
    /// Membership is maintained at runtime and round-trips through the .tilt file via the
    /// group's Guid (see SketchWriter.StrokeExtension.SymmetryGroup).
    public class SymmetryStrokeGroup
    {
        private readonly List<Stroke> m_Strokes = new List<Stroke>();

        public Guid Id { get; }

        /// The symmetry settings that were in place when this group was drawn. May be null for
        /// groups loaded from a sketch that didn't record them.
        public SymmetrySettingsSnapshot Settings { get; internal set; }

        internal SymmetryStrokeGroup(Guid id, SymmetrySettingsSnapshot settings)
        {
            Id = id;
            Settings = settings;
        }

        /// All the strokes in the group, including the one the user drew directly.
        /// Order is the order in which the strokes joined, which for freshly-drawn strokes is
        /// pointer order.
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

        internal void Add(Stroke stroke)
        {
            if (!m_Strokes.Contains(stroke))
            {
                m_Strokes.Add(stroke);
            }
        }

        internal void Remove(Stroke stroke)
        {
            m_Strokes.Remove(stroke);
            if (m_Strokes.Count == 0)
            {
                SymmetryStrokeGroups.Forget(this);
            }
        }

        /// Removes every stroke from the group and unregisters it. The strokes keep their record
        /// of the symmetry settings; they just stop being peers of each other.
        public void Disband()
        {
            // Copy, because leaving mutates m_Strokes.
            foreach (var stroke in m_Strokes.ToArray())
            {
                stroke.LeaveSymmetryGroup();
            }
            SymmetryStrokeGroups.Forget(this);
        }
    }

    /// Registry of the symmetry groups in the current sketch, keyed by group Guid.
    public static class SymmetryStrokeGroups
    {
        private static readonly Dictionary<Guid, SymmetryStrokeGroup> m_Groups =
            new Dictionary<Guid, SymmetryStrokeGroup>();

        /// Creates and registers a new, empty group.
        public static SymmetryStrokeGroup Create(SymmetrySettingsSnapshot settings)
        {
            var group = new SymmetryStrokeGroup(Guid.NewGuid(), settings);
            m_Groups[group.Id] = group;
            return group;
        }

        /// Returns the group with this id, or null if there isn't one.
        public static SymmetryStrokeGroup Get(Guid id)
        {
            if (id == Guid.Empty) { return null; }
            m_Groups.TryGetValue(id, out var group);
            return group;
        }

        /// Returns the group with this id, creating it if necessary. Used when loading, where
        /// strokes arrive one at a time and each carries a copy of the group's settings.
        public static SymmetryStrokeGroup GetOrCreate(Guid id, SymmetrySettingsSnapshot settings)
        {
            if (id == Guid.Empty) { return null; }
            if (!m_Groups.TryGetValue(id, out var group))
            {
                group = new SymmetryStrokeGroup(id, settings);
                m_Groups[id] = group;
            }
            else if (group.Settings == null)
            {
                group.Settings = settings;
            }
            return group;
        }

        /// Gives the passed strokes' groups fresh ids, so that strokes merged into the current
        /// sketch don't become peers of strokes that happen to share a group id.
        public static void RemapGroupIds(IEnumerable<Stroke> strokes)
        {
            var oldToNew = new Dictionary<Guid, SymmetryStrokeGroup>();
            foreach (var stroke in strokes)
            {
                var oldId = stroke.SymmetryGroupId;
                if (oldId == Guid.Empty) { continue; }
                if (!oldToNew.TryGetValue(oldId, out var newGroup))
                {
                    newGroup = Create(stroke.m_SymmetrySettings);
                    oldToNew[oldId] = newGroup;
                }
                int pointerIndex = stroke.SymmetryPointerIndex;
                stroke.LeaveSymmetryGroup();
                stroke.JoinSymmetryGroup(newGroup, pointerIndex);
            }
        }

        internal static void Forget(SymmetryStrokeGroup group)
        {
            if (group != null && m_Groups.TryGetValue(group.Id, out var registered) &&
                ReferenceEquals(registered, group))
            {
                m_Groups.Remove(group.Id);
            }
        }

        /// Drops every group. Called when the sketch is cleared.
        public static void Clear()
        {
            m_Groups.Clear();
        }
    }
} // namespace TiltBrush
