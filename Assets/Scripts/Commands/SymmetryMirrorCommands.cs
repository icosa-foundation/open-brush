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
    /// Changes which linked mirror the widget stands for: a newly created one, an earlier one
    /// being recalled, or none (plain symmetry). Recalling puts the widget and settings where
    /// that mirror has them; undo puts back the previous mirror and settings.
    public class ActivateMirrorCommand : BaseCommand
    {
        private readonly SymmetryMirror m_Target;
        private readonly SymmetryMirror m_Previous;
        private readonly bool m_Created;
        private readonly SymmetrySettingsSnapshot m_PreviousSettings;

        public ActivateMirrorCommand(SymmetryMirror target, bool created, BaseCommand parent = null)
            : base(parent)
        {
            m_Target = target;
            m_Created = created;
            m_Previous = SymmetryMirrors.Active;
            m_PreviousSettings = SymmetrySettingsSnapshot.FromCurrentSettings();
        }

        public override bool NeedsSave => true;

        private bool MovesWidget => m_Target != null && !m_Created;

        protected override void OnRedo()
        {
            if (m_Created) { SymmetryMirrors.Register(m_Target); }
            if (MovesWidget) { SymmetryMirrors.ApplySettingsUnrecorded(m_Target.Settings); }
            SymmetryMirrors.Active = m_Target;
        }

        protected override void OnUndo()
        {
            SymmetryMirrors.Active = m_Previous;
            if (MovesWidget) { SymmetryMirrors.ApplySettingsUnrecorded(m_PreviousSettings); }
            if (m_Created) { SymmetryMirrors.Unregister(m_Target); }
            // A linked mirror stops any spin; back to plain, it spins as it did before.
            var spin = m_PreviousSettings?.Spin ?? Vector3.zero;
            if (m_Previous == null && spin != Vector3.zero)
            {
                PointerManager.m_Instance.SymmetryWidget.Spin(spin.x, spin.y, spin.z);
            }
        }
    }

    /// Helpers shared by the commands that carry a mirror's strokes through a settings change.
    internal static class MirrorStrokeEdits
    {
        /// Groups that can be carried member by member: none selected, all in the mirror's
        /// canvas, at least one visible.
        internal static bool IsEligible(SymmetryStrokeGroup group, SymmetryMirror mirror,
            int pointerCount)
        {
            bool anyVisible = false;
            foreach (var stroke in group.Strokes)
            {
                int index = stroke.SymmetryPointerIndex;
                if (index < 0 || index >= pointerCount || stroke.Canvas != mirror.Canvas ||
                    stroke.m_ControlPoints == null ||
                    SelectionManager.m_Instance.IsStrokeSelected(stroke))
                {
                    return false;
                }
                anyVisible |= stroke.IsGeometryEnabled;
            }
            return anyVisible;
        }

        /// Moves a stroke by a canvas-space left transform, in place where possible.
        internal static void ApplyLeftTransform(Stroke stroke, TrTransform xf)
        {
            if (stroke.TransformGeometryInPlace(xf)) { return; }
            var points = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone();
            for (int i = 0; i < points.Length; ++i)
            {
                var pose = xf * TrTransform.TR(points[i].m_Pos, points[i].m_Orient);
                points[i].m_Pos = pose.translation;
                points[i].m_Orient = pose.rotation;
            }
            stroke.RestoreMirrorControlPoints(points, stroke.m_BrushScale * Mathf.Abs(xf.scale));
        }

        /// Puts recorded control points back, converting from the canvas they were recorded in
        /// should the stroke have been reparented since (selection does this without history).
        internal static void RestorePoints(Stroke stroke, CanvasScript recordedIn,
            PointerManager.ControlPoint[] recorded, float brushScale)
        {
            var points = (PointerManager.ControlPoint[])recorded.Clone();
            var conversion = stroke.Canvas.Pose.inverse * recordedIn.Pose;
            for (int i = 0; i < points.Length; ++i)
            {
                var pose = conversion * TrTransform.TR(points[i].m_Pos, points[i].m_Orient);
                points[i].m_Pos = pose.translation;
                points[i].m_Orient = pose.rotation;
            }
            stroke.RestoreMirrorControlPoints(points, brushScale * conversion.scale);
        }

        /// For a group that can't be carried member by member: once the mirror has its new
        /// settings, move one visible member with its pointer and derive the rest from it; or,
        /// where even that can't be done (a member selected or outside the mirror's canvas),
        /// break its link undoably.
        internal static void AddFallback(SymmetryStrokeGroup group,
            SymmetrySettingsSnapshot before, SymmetrySettingsSnapshot after,
            List<RederiveSymmetryGroupCommand> rederives,
            List<SymmetryPeerEditing.BrokenLink> brokenLinks)
        {
            var source = group.DerivationSource;
            bool anySelected = false;
            foreach (var stroke in group.Strokes)
            {
                anySelected |= SelectionManager.m_Instance.IsStrokeSelected(stroke);
            }
            if (source != null && !anySelected &&
                RederiveSymmetryGroupCommand.CanDerive(source))
            {
                rederives.Add(new RederiveSymmetryGroupCommand(
                    source,
                    RederiveSymmetryGroupCommand.StepFor(source, before, after)));
            }
            else
            {
                brokenLinks.Add(new SymmetryPeerEditing.BrokenLink(group));
            }
        }

        /// Hides a stroke made by a command's constructor, without touching the tilt meter: the
        /// command's first redo shows it, and counts it then.
        internal static void HideUncounted(Stroke stroke)
        {
            switch (stroke.m_Type)
            {
                case Stroke.Type.BatchedBrushStroke:
                    stroke.m_BatchSubset.m_ParentBatch.DisableSubset(stroke.m_BatchSubset);
                    break;
                case Stroke.Type.BrushStroke:
                    stroke.m_Object.GetComponent<BaseBrushScript>()?.HideBrush(true);
                    break;
            }
        }
    }

    /// A settings change to a linked mirror that keeps its number of copies - wallpaper scale or
    /// skew, say. Each copy moves by how its own pointer moved; the strokes stay put otherwise.
    /// Consecutive changes to the same mirror (a slider being dragged) merge into one undo step.
    public class MoveMirrorSettingsCommand : BaseCommand
    {
        private readonly MirrorWidgetEdits m_Widgets;
        private class Member
        {
            internal Stroke Stroke;
            internal TrTransform Step;
            internal PointerManager.ControlPoint[] Before;
            internal float BeforeScale;
            internal PointerManager.ControlPoint[] After;
            internal float AfterScale;
        }

        private readonly SymmetryMirror m_Mirror;
        private readonly SymmetrySettingsSnapshot m_Before;
        private readonly SymmetrySettingsSnapshot m_After;
        private readonly CanvasScript m_Canvas;
        private readonly List<Member> m_Members = new List<Member>();
        private readonly List<SymmetryPeerEditing.BrokenLink> m_BrokenLinks =
            new List<SymmetryPeerEditing.BrokenLink>();
        private readonly List<RederiveSymmetryGroupCommand> m_Rederives =
            new List<RederiveSymmetryGroupCommand>();
        private bool m_Applied;

        internal MoveMirrorSettingsCommand(SymmetryMirror mirror,
            SymmetrySettingsSnapshot before, SymmetrySettingsSnapshot after)
        {
            m_Mirror = mirror;
            m_Before = before;
            m_After = after;
            m_Canvas = mirror.Canvas;
            m_Widgets = new MirrorWidgetEdits(mirror, before);
            int count = after.PointerTransforms.Count;
            foreach (var group in SymmetryMirrors.GroupsOf(mirror))
            {
                if (!MirrorStrokeEdits.IsEligible(group, mirror, count))
                {
                    MirrorStrokeEdits.AddFallback(group, before, after,
                        m_Rederives, m_BrokenLinks);
                    continue;
                }
                foreach (var stroke in group.Strokes)
                {
                    int index = stroke.SymmetryPointerIndex;
                    m_Members.Add(new Member
                    {
                        Stroke = stroke,
                        Step = after.PointerTransforms[index] *
                            before.PointerTransforms[index].inverse,
                        Before = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone(),
                        BeforeScale = stroke.m_BrushScale,
                    });
                }
            }
        }

        public override bool NeedsSave => true;

        public override bool Merge(BaseCommand other)
        {
            if (base.Merge(other)) { return true; }
            if (other is MoveMirrorSettingsCommand next && ReferenceEquals(next.m_Mirror, m_Mirror))
            {
                m_Children.Add(next);
                return true;
            }
            return false;
        }

        protected override void OnRedo()
        {
            foreach (var link in m_BrokenLinks) { link.Break(); }
            foreach (var member in m_Members)
            {
                if (!m_Applied)
                {
                    MirrorStrokeEdits.ApplyLeftTransform(member.Stroke, member.Step);
                    member.After = (PointerManager.ControlPoint[])member.Stroke.m_ControlPoints.Clone();
                    member.AfterScale = member.Stroke.m_BrushScale;
                }
                else
                {
                    MirrorStrokeEdits.RestorePoints(
                        member.Stroke, m_Canvas, member.After, member.AfterScale);
                }
            }
            if (!m_Applied) { m_Widgets.Update(m_After); }
            else { m_Widgets.Restore(true); }
            m_Applied = true;
            m_Mirror.Settings = m_After;
            SymmetryMirrors.ApplySettingsUnrecorded(m_After);
            foreach (var rederive in m_Rederives) { rederive.Redo(); }
        }

        protected override void OnUndo()
        {
            for (int i = m_Rederives.Count - 1; i >= 0; --i) { m_Rederives[i].Undo(); }
            m_Widgets.Restore(false);
            foreach (var member in m_Members)
            {
                MirrorStrokeEdits.RestorePoints(
                    member.Stroke, m_Canvas, member.Before, member.BeforeScale);
            }
            foreach (var link in m_BrokenLinks) { link.Restore(); }
            m_Mirror.Settings = m_Before;
            SymmetryMirrors.ApplySettingsUnrecorded(m_Before);
        }
    }

    /// A settings change to a linked mirror that changes how many copies it makes - point order,
    /// wallpaper group or repeats. Every edit reaches every member of a group, so the members are
    /// exact symmetric images of each other and the group can be rebuilt from any one of them:
    /// the old strokes are hidden and a new group drawn under the new settings takes their place.
    public class RegenerateMirrorStrokesCommand : BaseCommand
    {
        private readonly SymmetryMirror m_Mirror;
        private readonly SymmetrySettingsSnapshot m_Before;
        private readonly SymmetrySettingsSnapshot m_After;
        private readonly List<Stroke> m_OldStrokes = new List<Stroke>();
        private readonly List<Stroke> m_NewStrokes = new List<Stroke>();
        private readonly List<SymmetryPeerEditing.BrokenLink> m_BrokenLinks =
            new List<SymmetryPeerEditing.BrokenLink>();
        private bool m_Applied;

        internal RegenerateMirrorStrokesCommand(SymmetryMirror mirror,
            SymmetrySettingsSnapshot before, SymmetrySettingsSnapshot after)
        {
            m_Mirror = mirror;
            new RegenerateMirrorWidgetsCommand(mirror, before, after, this);
            m_Before = before;
            m_After = after;
            int beforeCount = before.PointerTransforms.Count;
            foreach (var group in SymmetryMirrors.GroupsOf(mirror))
            {
                // A group that can't be rebuilt can't stay linked: its pointer indices mean
                // nothing under the new settings.
                Stroke source = MirrorStrokeEdits.IsEligible(group, mirror, beforeCount)
                    ? ChooseSource(group)
                    : null;
                if (source == null)
                {
                    m_BrokenLinks.Add(new SymmetryPeerEditing.BrokenLink(group));
                    continue;
                }

                TrTransform fromSource =
                    before.PointerTransforms[source.SymmetryPointerIndex].inverse;
                var newGroup = new SymmetryStrokeGroup(mirror);

                // Each copy keeps its own appearance: a pointer index that survives keeps its
                // instance data; a new one gets what the mirror's colour shift gives a copy there.
                group.CaptureInstances();
                var sourceInstance = group.InstanceOf(source);
                var instances = new Dictionary<int, SymmetryStrokeGroup.Instance>();
                foreach (var member in group.Strokes)
                {
                    instances[member.SymmetryPointerIndex] = group.InstanceOf(member);
                }
                int count = after.PointerTransforms.Count;
                for (int i = 0; i < count; ++i)
                {
                    TrTransform xf = after.PointerTransforms[i] * fromSource;
                    var copy = SketchMemoryScript.m_Instance.DuplicateStroke(
                        source, source.Canvas,
                        xf == TrTransform.identity ? (TrTransform?)null : xf,
                        absoluteScale: true);
                    if (!instances.TryGetValue(i, out var instance))
                    {
                        instance = NewCopyInstance(group.Canonical ?? source, i, count);
                    }
                    SymmetryDerivation.ApplyAppearance(copy, source,
                        instance.ColorShift - sourceInstance.ColorShift,
                        Mathf.Approximately(sourceInstance.SizeRatio, 0f)
                            ? 1f
                            : instance.SizeRatio / sourceInstance.SizeRatio);
                    MirrorStrokeEdits.HideUncounted(copy);
                    copy.JoinSymmetryGroup(newGroup, i);
                    m_NewStrokes.Add(copy);
                }
                foreach (var stroke in group.Strokes)
                {
                    if (stroke.IsGeometryEnabled) { m_OldStrokes.Add(stroke); }
                }
            }
        }

        /// The appearance a newly added pointer index gets: the colour the mirror's colour shift
        /// gives a copy at that index of a group whose canonical stroke is 'canonical'.
        private static SymmetryStrokeGroup.Instance NewCopyInstance(Stroke canonical, int index,
            int count)
        {
            Color shifted = PointerManager.m_Instance.CalcColorShift(
                canonical.m_Color, index / (float)count);
            Color.RGBToHSV(canonical.m_Color, out float h0, out float s0, out float v0);
            Color.RGBToHSV(shifted, out float h1, out float s1, out float v1);
            return new SymmetryStrokeGroup.Instance
            {
                ColorShift = new Vector3(Mathf.DeltaAngle(h0 * 360f, h1 * 360f) / 360f,
                    s1 - s0, v1 - v0),
                SizeRatio = 1f,
            };
        }

        /// The member the group is rebuilt from: the one the user drew if it is still there.
        private static Stroke ChooseSource(SymmetryStrokeGroup group)
        {
            Stroke source = null;
            foreach (var stroke in group.Strokes)
            {
                if (!stroke.IsGeometryEnabled) { continue; }
                if (source == null || stroke.SymmetryPointerIndex < source.SymmetryPointerIndex)
                {
                    source = stroke;
                }
            }
            return source;
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            foreach (var link in m_BrokenLinks) { link.Break(); }
            foreach (var stroke in m_OldStrokes) { stroke.Hide(true); }
            foreach (var stroke in m_NewStrokes) { stroke.Hide(false); }
            m_Applied = true;
            m_Mirror.Settings = m_After;
            SymmetryMirrors.ApplySettingsUnrecorded(m_After);
        }

        protected override void OnUndo()
        {
            foreach (var stroke in m_NewStrokes) { stroke.Hide(true); }
            foreach (var stroke in m_OldStrokes) { stroke.Hide(false); }
            foreach (var link in m_BrokenLinks) { link.Restore(); }
            m_Applied = false;
            m_Mirror.Settings = m_Before;
            SymmetryMirrors.ApplySettingsUnrecorded(m_Before);
        }

        protected override void OnDispose()
        {
            // Discarded from the redo stack: the new strokes can never come back.
            if (m_Applied) { return; }
            foreach (var stroke in m_NewStrokes)
            {
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();
            }
        }
    }
} // namespace TiltBrush
