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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// One undo owner per drag; every participating group follows the same active mirror.
    /// Live updates use batches. Infrequent endpoint restoration may rebuild geometry.
    public static class SymmetryMirrorMove
    {
        internal const string LogPrefix = "[SymmetryMove-M1]";
        private static SymmetryMirror m_Mirror;
        private static SymmetrySettingsSnapshot m_Start;
        private static MoveMirrorStrokesCommand m_Command;
        private static List<GroupMove> m_Groups;
        private static List<SymmetryPeerEditing.BrokenLink> m_SkippedLinks;
        private static TrTransform m_TransformEach;
        private static bool m_TransformEachAfter;

        public static bool IsMoving => m_Command != null;

        internal static TrTransform PointerDelta(TrTransform start_GS, TrTransform current_GS,
            TrTransform canvasPose)
        {
            return canvasPose.inverse * current_GS * start_GS.inverse * canvasPose;
        }

        public static void Begin()
        {
            End();
            if (!SymmetryPeerEditing.Enabled || SymmetryMirrors.Active == null) { return; }
            var pm = PointerManager.m_Instance;
            m_Start = SymmetrySettingsSnapshot.FromCurrentSettings();
            if (m_Start.Mode != PointerManager.SymmetryMode.SinglePlane &&
                m_Start.Mode != PointerManager.SymmetryMode.MultiMirror) { return; }
            m_Mirror = SymmetryMirrors.Active;
            m_TransformEach = pm.m_SymmetryTransformEach;
            m_TransformEachAfter = pm.m_SymmetryTransformEachAfter;
            var worldTransforms = pm.GetSymmetriesForCurrentMode();
            m_Groups = new List<GroupMove>();
            m_SkippedLinks = new List<SymmetryPeerEditing.BrokenLink>();
            var seen = new HashSet<SymmetryStrokeGroup>();
            int skipped = 0;
            foreach (var stroke in SketchMemoryScript.AllStrokes())
            {
                var group = stroke.SymmetryPeerGroup;
                if (group == null || !ReferenceEquals(group.Mirror, m_Mirror) || !seen.Add(group))
                {
                    continue;
                }
                var move = GroupMove.TryCreate(group, m_Start, worldTransforms);
                if (move == null)
                {
                    ++skipped;
                    m_SkippedLinks.Add(new SymmetryPeerEditing.BrokenLink(group));
                }
                else { m_Groups.Add(move); }
            }
            // Empty/skipped groups still need matching widget and mirror-settings undo.
            m_Command = new MoveMirrorStrokesCommand(
                pm.SymmetryWidget, m_Mirror, m_Start, m_Groups, m_SkippedLinks);
            SketchMemoryScript.m_Instance.RecordCommand(m_Command);
            Debug.Log($"{LogPrefix} Begin: {m_Groups.Count} groups eligible, {skipped} skipped.");
        }

        public static void Update()
        {
            if (!IsMoving) { return; }
            var pm = PointerManager.m_Instance;
            var current = SymmetrySettingsSnapshot.FromCurrentSettings();
            if (!SymmetryPeerEditing.Enabled || !ReferenceEquals(m_Mirror, SymmetryMirrors.Active) ||
                !m_Start.HasCompatibleTopology(current) || m_TransformEach != pm.m_SymmetryTransformEach ||
                m_TransformEachAfter != pm.m_SymmetryTransformEachAfter)
            {
                // Freeze the completed part of the drag before accepting different inputs.
                Finish(current);
                return;
            }
            var worldTransforms = pm.GetSymmetriesForCurrentMode();
            foreach (var group in m_Groups) { group.Update(worldTransforms); }
        }

        public static void End()
        {
            if (!IsMoving) { return; }
            Update();
            if (IsMoving) { Finish(SymmetrySettingsSnapshot.FromCurrentSettings()); }
        }

        private static void Finish(SymmetrySettingsSnapshot settings)
        {
            bool moved = m_Start.WidgetTransform != settings.WidgetTransform ||
                !m_Start.PointerTransforms.SequenceEqual(settings.PointerTransforms);
            if (moved)
            {
                foreach (var link in m_SkippedLinks) { link.Break(); }
            }
            m_Command.Complete(settings, moved);
            m_Mirror.Settings = settings;
            Debug.Log($"{LogPrefix} End: widget, stroke endpoints and placement recorded together.");
            Forget();
        }

        // Only for teardown: the sketch and its command stack are being discarded.
        public static void Forget()
        {
            m_Command = null;
            m_Mirror = null;
            m_Start = null;
            m_Groups = null;
            m_SkippedLinks = null;
        }

        internal sealed class GroupMove
        {
            private sealed class Member
            {
                internal Stroke Stroke;
                internal int Index;
                internal PointerManager.ControlPoint[] Before;
                internal PointerManager.ControlPoint[] After;
                internal float BeforeScale;
                internal float AfterScale;
            }

            private readonly SymmetryStrokeGroup m_Group;
            private readonly SymmetryPeerEditing.BrokenLink m_Link;
            private readonly CanvasScript m_Canvas;
            private readonly TrTransform m_CanvasPose;
            private readonly List<Member> m_Members = new List<Member>();
            private readonly TrTransform[] m_MirrorStart;
            private readonly TrTransform[] m_Applied;
            private readonly TrTransform[] m_Steps;
            private bool m_Rejected;
            private bool m_Changed;

            private GroupMove(SymmetryStrokeGroup group, IList<TrTransform> worldTransforms)
            {
                m_Group = group;
                m_Link = new SymmetryPeerEditing.BrokenLink(group);
                m_Canvas = group.Mirror.Canvas;
                m_CanvasPose = m_Canvas.Pose;
                m_MirrorStart = new TrTransform[worldTransforms.Count];
                m_Applied = new TrTransform[worldTransforms.Count];
                m_Steps = new TrTransform[worldTransforms.Count];
                for (int i = 0; i < worldTransforms.Count; ++i)
                {
                    m_MirrorStart[i] = worldTransforms[i];
                    m_Applied[i] = TrTransform.identity;
                }
                foreach (var stroke in group.Strokes)
                {
                    m_Members.Add(new Member
                    {
                        Stroke = stroke,
                        Index = stroke.SymmetryPointerIndex,
                        Before = (PointerManager.ControlPoint[])stroke.m_ControlPoints.Clone(),
                        BeforeScale = stroke.m_BrushScale
                    });
                }
            }

            internal static GroupMove TryCreate(SymmetryStrokeGroup group,
                SymmetrySettingsSnapshot settings, IList<TrTransform> worldTransforms)
            {
                if (group.Count == 0 || group.Mirror?.Settings == null ||
                    !group.Mirror.Settings.HasCompatibleTopology(settings)) { return null; }
                var canvas = group.Strokes[0].Canvas;
                if (canvas == null || canvas == App.Scene.SelectionCanvas ||
                    canvas != group.Mirror.Canvas) { return null; }
                var indices = new HashSet<int>();
                foreach (var stroke in group.Strokes)
                {
                    int index = stroke.SymmetryPointerIndex;
                    if (index < 0 || index >= worldTransforms.Count || !indices.Add(index) ||
                        !Eligible(stroke, canvas) || !Invertible(worldTransforms[index]) ||
                        !Invertible(group.Mirror.Settings.PointerTransforms[index])) { return null; }
                }
                return Invertible(canvas.Pose) ? new GroupMove(group, worldTransforms) : null;
            }

            private static bool Invertible(TrTransform transform) =>
                transform.IsFinite() && transform.scale != 0 && transform.inverse.IsFinite();

            private static bool Eligible(Stroke stroke, CanvasScript canvas) =>
                stroke.Canvas == canvas && stroke.CanTransformGeometryInPlace &&
                stroke.IsGeometryEnabled && stroke.m_ControlPoints != null &&
                !SelectionManager.m_Instance.IsStrokeSelected(stroke);

            internal void Update(IList<TrTransform> worldTransforms)
            {
                if (m_Rejected) { return; }
                // Preflight the entire group. The following application loop is synchronous
                // and does not yield to selection or brush changes.
                if (m_Canvas == null || m_Canvas.Pose != m_CanvasPose ||
                    m_Group.Count != m_Members.Count)
                {
                    Reject();
                    return;
                }
                bool changed = false;
                foreach (var member in m_Members)
                {
                    int index = member.Index;
                    if (!Eligible(member.Stroke, m_Canvas) ||
                        !ReferenceEquals(member.Stroke.SymmetryPeerGroup, m_Group) ||
                        member.Stroke.SymmetryPointerIndex != index)
                    {
                        Reject();
                        return;
                    }
                    // Pointer zero remains stationary, including numerical noise.
                    var target = index == 0 ? TrTransform.identity :
                        PointerDelta(m_MirrorStart[index], worldTransforms[index], m_CanvasPose);
                    m_Steps[index] = target * m_Applied[index].inverse;
                    if (!Invertible(m_Steps[index])) { Reject(); return; }
                    changed |= m_Steps[index] != TrTransform.identity;
                }
                if (!changed) { return; }
                try
                {
                    foreach (var member in m_Members)
                    {
                        var step = m_Steps[member.Index];
                        if (step == TrTransform.identity) { continue; }
                        // Mark before the call so a geometry exception also restores this group.
                        m_Changed = true;
                        if (!member.Stroke.TransformGeometryInPlace(step))
                        {
                            Reject();
                            return;
                        }
                        m_Applied[member.Index] = step * m_Applied[member.Index];
                    }
                }
                catch (Exception exception)
                {
                    Reject();
                    Debug.LogError($"{LogPrefix} Group move rolled back: {exception}");
                }
            }

            private void Reject()
            {
                // Roll back this group's whole drag and exclude it from subsequent updates.
                // Restoration does not depend on the in-place batch operation that failed.
                if (m_Changed)
                {
                    foreach (var member in m_Members)
                    {
                        if (member.Index != 0) { RestoreMember(member, after: false); }
                    }
                }
                m_Link.Break();
                m_Rejected = true;
                Debug.LogWarning($"{LogPrefix} Group no longer eligible; link broken after rollback.");
            }

            internal void Complete()
            {
                if (m_Rejected || !m_Changed) { return; }
                foreach (var member in m_Members)
                {
                    member.After = (PointerManager.ControlPoint[])member.Stroke.m_ControlPoints.Clone();
                    member.AfterScale = member.Stroke.m_BrushScale;
                }
            }

            internal void Restore(bool after)
            {
                if (m_Rejected)
                {
                    if (after) { m_Link.Break(); }
                    else { m_Link.Restore(); }
                    return;
                }
                if (!m_Changed) { return; }
                foreach (var member in m_Members)
                {
                    if (member.Index != 0) { RestoreMember(member, after); }
                }
            }

            private void RestoreMember(Member member, bool after)
            {
                var points = (PointerManager.ControlPoint[])(after ? member.After : member.Before).Clone();
                float scale = after ? member.AfterScale : member.BeforeScale;
                // Selection can temporarily reparent a stroke without a history entry.
                var conversion = member.Stroke.Canvas.Pose.inverse * m_Canvas.Pose;
                for (int i = 0; i < points.Length; ++i)
                {
                    var pose = conversion * TrTransform.TR(points[i].m_Pos, points[i].m_Orient);
                    points[i].m_Pos = pose.translation;
                    points[i].m_Orient = pose.rotation;
                }
                member.Stroke.RestoreMirrorControlPoints(points, scale * conversion.scale);
            }
        }
    }
} // namespace TiltBrush
