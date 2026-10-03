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
    /// Copies in a linked group are derived: each is one member's geometry carried through the
    /// mirror's transform for its pointer. Only the copy's own colour and size relationship to
    /// the group (a mirror colour shift, jitter) is its own, and that is kept as it is.
    internal static class SymmetryDerivation
    {
        /// Everything about a stroke's geometry that derivation replaces.
        internal sealed class Shape
        {
            internal PointerManager.ControlPoint[] Points;
            internal bool[] Drops;
            internal float BrushScale;
            internal List<Color32?> OverrideColors;
            internal ColorOverrideMode OverrideMode;

            internal static Shape Of(Stroke stroke) => new Shape
            {
                Points = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone(),
                Drops = DropsOf(stroke),
                BrushScale = stroke.m_BrushScale,
                OverrideColors = stroke.m_OverrideColors != null
                    ? new List<Color32?>(stroke.m_OverrideColors)
                    : null,
                OverrideMode = stroke.m_ColorOverrideMode,
            };

            internal void ApplyTo(Stroke stroke)
            {
                stroke.ReplaceDerivedData(Points, Drops, BrushScale, OverrideColors, OverrideMode);
            }
        }

        private static bool[] DropsOf(Stroke stroke) =>
            stroke.m_ControlPointsToDrop != null
                ? (bool[])stroke.m_ControlPointsToDrop.Clone()
                : new bool[stroke.m_ControlPoints.Length];

        /// The shape 'target' takes as a copy of 'source', two members of one linked group.
        /// False when the mirror has no transform for either pointer, or the two aren't both in
        /// the mirror's canvas (a selected stroke is staged in another canvas).
        internal static bool TryDerive(Stroke source, Stroke target, out Shape shape)
        {
            shape = null;
            var group = source.SymmetryPeerGroup;
            var mirror = group?.Mirror;
            if (mirror?.Settings == null || !ReferenceEquals(group, target.SymmetryPeerGroup) ||
                source.Canvas != mirror.Canvas || target.Canvas != mirror.Canvas)
            {
                return false;
            }
            var transforms = mirror.Settings.PointerTransforms;
            int from = source.SymmetryPointerIndex;
            int to = target.SymmetryPointerIndex;
            if (transforms == null || from < 0 || to < 0 ||
                from >= transforms.Count || to >= transforms.Count)
            {
                return false;
            }
            TrTransform xf = transforms[to] * transforms[from].inverse;
            if (!xf.IsFinite()) { return false; }

            var points = (PointerManager.ControlPoint[])source.m_ControlPoints.Clone();
            for (int i = 0; i < points.Length; ++i)
            {
                var pose = xf * TrTransform.TR(points[i].m_Pos, points[i].m_Orient);
                points[i].m_Pos = pose.translation;
                points[i].m_Orient = pose.rotation;
            }

            // Point colours follow the source, offset by how the two strokes' colours differ.
            List<Color32?> overrides = null;
            if (source.m_OverrideColors != null)
            {
                overrides = new List<Color32?>(source.m_OverrideColors.Count);
                foreach (var color in source.m_OverrideColors)
                {
                    if (!color.HasValue)
                    {
                        overrides.Add(null);
                        continue;
                    }
                    Color32 shifted = SymmetryPeerEditing.OffsetColorLike(
                        color.Value, source.m_Color, target.m_Color);
                    shifted.a = color.Value.a;
                    overrides.Add(shifted);
                }
            }

            // The copy keeps its own brush size relationship; m_BrushScale is the geometric
            // part, which the transform carries.
            shape = new Shape
            {
                Points = points,
                Drops = DropsOf(source),
                BrushScale = source.m_BrushScale * Mathf.Abs(xf.scale),
                OverrideColors = overrides,
                OverrideMode = source.m_ColorOverrideMode,
            };
            return true;
        }
    }

    /// Makes every other member of a linked group an exact copy of one member again. This is
    /// what an edit that can't be mirrored onto each copy does instead of breaking the group:
    /// the edit is applied to one member, and the rest are derived from it.
    ///
    /// Both the copies' prior shapes and the derived ones are recorded on the first redo, so a
    /// parent command that edits the strokes first (redone before its children, undone after)
    /// is neither lost nor undone twice. Members stay erased or not as they were.
    public class RederiveSymmetryGroupCommand : BaseCommand
    {
        private readonly Stroke m_Source;
        private readonly List<Stroke> m_Targets = new List<Stroke>();
        private readonly List<SymmetryDerivation.Shape> m_Before =
            new List<SymmetryDerivation.Shape>();
        private List<SymmetryDerivation.Shape> m_After;
        // Moves the source first, for a mirror whose settings changed under the group.
        private readonly TrTransform? m_SourceStep;
        private SymmetryDerivation.Shape m_SourceBefore;
        private SymmetryDerivation.Shape m_SourceAfter;

        public RederiveSymmetryGroupCommand(Stroke source, BaseCommand parent = null)
            : this(source, null, parent)
        {
        }

        /// 'sourceStep' is a canvas-space move applied to the source before the others are
        /// derived from it: when the mirror's settings change, every pointer but the first moves,
        /// so a source drawn by another pointer has to move with its pointer first.
        public RederiveSymmetryGroupCommand(Stroke source, TrTransform? sourceStep,
            BaseCommand parent = null) : base(parent)
        {
            m_Source = source;
            if (sourceStep.HasValue && sourceStep.Value != TrTransform.identity)
            {
                m_SourceStep = sourceStep;
            }
            var group = source.SymmetryPeerGroup;
            if (group == null) { return; }
            foreach (var stroke in group.Strokes)
            {
                if (!ReferenceEquals(stroke, source)) { m_Targets.Add(stroke); }
            }
        }

        /// True if every other member can be derived from 'source' where it is now.
        public static bool CanDerive(Stroke source)
        {
            var group = source?.SymmetryPeerGroup;
            if (group?.Mirror == null) { return false; }
            foreach (var stroke in group.Strokes)
            {
                if (!ReferenceEquals(stroke, source) &&
                    !SymmetryPeerEditing.TryGetPeerSymmetryTransform(source, stroke, out _))
                {
                    return false;
                }
            }
            return true;
        }

        /// The member the others are derived from.
        public Stroke Source => m_Source;

        public override bool NeedsSave => true;

        /// Derives again from the source as it is now; for a tool still dragging.
        public void Refresh()
        {
            m_After = null;
            OnRedo();
        }

        /// The move a mirror settings change gives 'source', from the pointer transforms before
        /// and after; null if it doesn't move or the transforms don't cover its pointer. A mirror
        /// drag holds the first pointer's strokes still (holdFirstPointer); a settings change
        /// moves every pointer by its own change.
        public static TrTransform? StepFor(Stroke source,
            SymmetrySettingsSnapshot before, SymmetrySettingsSnapshot after, bool holdFirstPointer)
        {
            int index = source.SymmetryPointerIndex;
            var from = before?.PointerTransforms;
            var to = after?.PointerTransforms;
            if (index < 0 || (holdFirstPointer && index == 0) || from == null || to == null ||
                index >= from.Count || index >= to.Count)
            {
                return null;
            }
            TrTransform step = to[index] * from[index].inverse;
            return step.IsFinite() ? step : (TrTransform?)null;
        }

        protected override void OnRedo()
        {
            if (m_SourceStep.HasValue)
            {
                if (m_SourceAfter == null)
                {
                    m_SourceBefore = SymmetryDerivation.Shape.Of(m_Source);
                    MirrorStrokeEdits.ApplyLeftTransform(m_Source, m_SourceStep.Value);
                    m_SourceAfter = SymmetryDerivation.Shape.Of(m_Source);
                }
                else
                {
                    m_SourceAfter.ApplyTo(m_Source);
                }
            }
            if (m_Before.Count == 0)
            {
                foreach (var target in m_Targets)
                {
                    m_Before.Add(SymmetryDerivation.Shape.Of(target));
                }
            }
            if (m_After == null)
            {
                m_After = new List<SymmetryDerivation.Shape>(m_Targets.Count);
                foreach (var target in m_Targets)
                {
                    SymmetryDerivation.TryDerive(m_Source, target, out var shape);
                    m_After.Add(shape);
                }
            }
            for (int i = 0; i < m_Targets.Count; ++i)
            {
                m_After[i]?.ApplyTo(m_Targets[i]);
            }
        }

        protected override void OnUndo()
        {
            if (m_After != null)
            {
                for (int i = 0; i < m_Targets.Count; ++i)
                {
                    if (m_After[i] != null) { m_Before[i].ApplyTo(m_Targets[i]); }
                }
            }
            m_SourceBefore?.ApplyTo(m_Source);
        }
    }
} // namespace TiltBrush
