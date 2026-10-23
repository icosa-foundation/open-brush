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
using UnityEngine;

namespace TiltBrush
{
    /// The set of strokes that a single pass of a symmetry mode laid down together: the stroke
    /// the user drew plus the copies the symmetry made of it.
    ///
    /// A stroke drawn with symmetry has one even if it ends up being the only member.
    ///
    /// Strokes hold the group by reference, so peer lookup costs nothing and there is no
    /// registry to keep in sync; a group becomes garbage once its last stroke does. Identity is
    /// the object itself. Ids are assigned only when saving (see SketchWriter), which keeps the
    /// links local to a sketch and makes an additive load trivially free of collisions.
    public class SymmetryStrokeGroup
    {
        private readonly List<Stroke> m_Strokes = new List<Stroke>();

        /// What makes one copy more than its pointer's transform of the canonical stroke: a
        /// colour shift (a mirror colour option, or jitter) and a size ratio. Position comes from
        /// the mirror. Derivation keeps these fixed, so an edit to any copy carries to the rest
        /// with each keeping its own relationship to the group.
        public struct Instance
        {
            /// Hue, saturation and value added to the canonical stroke's colour.
            public Vector3 ColorShift;
            /// This copy's brush size over the canonical stroke's.
            public float SizeRatio;

            public static readonly Instance Identity = new Instance { SizeRatio = 1f };
        }

        private readonly Dictionary<Stroke, Instance> m_Instances =
            new Dictionary<Stroke, Instance>(new ReferenceComparer<Stroke>());
        // The canonical stroke the instances were measured against.
        private Stroke m_InstanceBasis;

        /// The single settings owner for this group. Non-widget symmetry uses a mirror record
        /// too, but has no fixed transform for spatial peer edits.
        public SymmetryMirror Mirror { get; }

        public SymmetryStrokeGroup(SymmetryMirror mirror)
        {
            Mirror = mirror;
        }

        /// The strokes in the group, in the order they were recorded, which for a freshly-drawn
        /// line is pointer order.
        public IReadOnlyList<Stroke> Strokes => m_Strokes;

        public int Count => m_Strokes.Count;

        /// The member the others are copies of: the stroke the user drew, at pointer index 0
        /// (or the lowest index present). Copies are derived from it by their pointer's
        /// transform, so the group stays an exact symmetric set.
        public Stroke Canonical
        {
            get
            {
                Stroke canonical = null;
                foreach (var stroke in m_Strokes)
                {
                    if (canonical == null ||
                        stroke.SymmetryPointerIndex < canonical.SymmetryPointerIndex)
                    {
                        canonical = stroke;
                    }
                }
                return canonical;
            }
        }

        /// The member to rebuild the others from: the canonical stroke if it is visible, else
        /// the visible member with the lowest pointer index. An erased member has missed every
        /// edit made since it was erased (edits skip erased copies; undo brings it back only
        /// after undoing them), so it is never a source while any member is visible.
        public Stroke DerivationSource
        {
            get
            {
                Stroke source = null;
                foreach (var stroke in m_Strokes)
                {
                    if (stroke.IsGeometryEnabled &&
                        (source == null || stroke.SymmetryPointerIndex < source.SymmetryPointerIndex))
                    {
                        source = stroke;
                    }
                }
                return source ?? Canonical;
            }
        }

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
            Mirror.Canvas ??= stroke.Canvas;
            m_Strokes.Add(stroke);
        }

        internal void Remove(Stroke stroke)
        {
            m_Strokes.Remove(stroke);
            m_Instances.Remove(stroke);
        }

        /// A member's instance data, measured from the members as they are the first time it is
        /// asked for. Call CaptureInstances while the group is consistent - when it forms, and
        /// before an edit changes one member - so the record predates the edit.
        public Instance InstanceOf(Stroke member)
        {
            Rebase();
            if (!m_Instances.TryGetValue(member, out var instance))
            {
                instance = Measure(member, m_InstanceBasis);
                m_Instances[member] = instance;
            }
            return instance;
        }

        /// Records instance data for every member that has none yet.
        public void CaptureInstances()
        {
            foreach (var stroke in m_Strokes) { InstanceOf(stroke); }
        }

        private static Instance Measure(Stroke member, Stroke basis)
        {
            if (basis == null || ReferenceEquals(member, basis)) { return Instance.Identity; }
            Color.RGBToHSV(basis.m_Color, out float h0, out float s0, out float v0);
            Color.RGBToHSV(member.m_Color, out float h1, out float s1, out float v1);
            return new Instance
            {
                ColorShift = new Vector3(Mathf.DeltaAngle(h0 * 360f, h1 * 360f) / 360f, s1 - s0, v1 - v0),
                SizeRatio = Mathf.Approximately(basis.m_BrushSize, 0f)
                    ? 1f
                    : member.m_BrushSize / basis.m_BrushSize,
            };
        }

        /// The canonical stroke can change when members leave or join; the instances are then
        /// re-expressed relative to the new one.
        private void Rebase()
        {
            var canonical = Canonical;
            if (ReferenceEquals(canonical, m_InstanceBasis)) { return; }
            if (m_InstanceBasis != null && canonical != null &&
                m_Instances.TryGetValue(canonical, out var newBasis))
            {
                var rebased = new Dictionary<Stroke, Instance>(new ReferenceComparer<Stroke>());
                foreach (var pair in m_Instances)
                {
                    rebased[pair.Key] = new Instance
                    {
                        ColorShift = pair.Value.ColorShift - newBasis.ColorShift,
                        SizeRatio = Mathf.Approximately(newBasis.SizeRatio, 0f)
                            ? 1f
                            : pair.Value.SizeRatio / newBasis.SizeRatio,
                    };
                }
                m_Instances.Clear();
                foreach (var pair in rebased) { m_Instances[pair.Key] = pair.Value; }
            }
            else
            {
                m_Instances.Clear();
            }
            m_InstanceBasis = canonical;
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
