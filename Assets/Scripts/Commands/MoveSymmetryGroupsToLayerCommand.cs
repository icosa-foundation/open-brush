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

namespace TiltBrush
{
    /// Moves whole linked groups into another layer when one of their copies goes there.
    ///
    /// A mirror's transforms are expressed in its own canvas, so a group can't stay with its
    /// mirror in another layer. Each group takes every copy with it (erased ones stay erased) and
    /// is re-homed under a new linked mirror in the target layer: the old mirror with its
    /// transforms re-expressed in the target layer's space. One new mirror serves all the moved
    /// groups of each old mirror. The old mirror keeps any groups that stay behind.
    ///
    /// Copies already in the target layer, or in the selection (a deselect parent command puts
    /// them in the target layer), are moved by the parent command, which also puts them back.
    public class MoveSymmetryGroupsToLayerCommand : BaseCommand
    {
        private class Member
        {
            internal Stroke Stroke;
            internal int Index;
            internal CanvasScript From;
            internal bool Moves;
        }

        private class GroupMove
        {
            internal SymmetryStrokeGroup OldGroup;
            internal SymmetryStrokeGroup NewGroup;
            internal readonly List<Member> Members = new List<Member>();
        }

        private readonly CanvasScript m_Target;
        private readonly List<GroupMove> m_Groups = new List<GroupMove>();
        private readonly List<SymmetryMirror> m_NewMirrors = new List<SymmetryMirror>();

        /// Pass moveStrokes: false when the parent command moves every stroke itself (squashing
        /// a layer), so only the groups are re-homed.
        public MoveSymmetryGroupsToLayerCommand(IEnumerable<SymmetryStrokeGroup> groups,
            CanvasScript target, BaseCommand parent = null, bool moveStrokes = true)
            : base(parent)
        {
            m_Target = target;
            var newMirrors = new Dictionary<SymmetryMirror, SymmetryMirror>();
            foreach (var group in groups.Distinct())
            {
                var oldMirror = group?.Mirror;
                if (oldMirror?.Settings == null || oldMirror.Canvas == null ||
                    oldMirror.Canvas == target)
                {
                    continue;
                }
                if (!newMirrors.TryGetValue(oldMirror, out var newMirror))
                {
                    newMirror = new SymmetryMirror(Guid.NewGuid(), ReExpress(oldMirror, target))
                    {
                        Canvas = target
                    };
                    newMirrors.Add(oldMirror, newMirror);
                    m_NewMirrors.Add(newMirror);
                }
                var move = new GroupMove
                {
                    OldGroup = group,
                    NewGroup = new SymmetryStrokeGroup(newMirror)
                };
                foreach (var stroke in group.Strokes)
                {
                    move.Members.Add(new Member
                    {
                        Stroke = stroke,
                        Index = stroke.SymmetryPointerIndex,
                        From = stroke.Canvas,
                        Moves = moveStrokes && stroke.Canvas != target &&
                            stroke.Canvas != App.Scene.SelectionCanvas
                    });
                }
                m_Groups.Add(move);
            }
        }

        /// The mirror's settings with each pointer transform carried from its canvas into
        /// 'target': C = target⁻¹ · from, P' = C · P · C⁻¹.
        private static SymmetrySettingsSnapshot ReExpress(SymmetryMirror mirror, CanvasScript target)
        {
            TrTransform change = target.Pose.inverse * mirror.Canvas.Pose;
            var transforms = mirror.Settings.PointerTransforms
                .Select(xf => change * xf * change.inverse)
                .ToList();
            return mirror.Settings.WithPointerTransforms(transforms);
        }

        public override bool NeedsSave => m_Groups.Count > 0;

        protected override void OnRedo()
        {
            foreach (var mirror in m_NewMirrors) { SymmetryMirrors.Register(mirror); }
            foreach (var move in m_Groups)
            {
                foreach (var member in move.Members)
                {
                    if (member.Moves) { MoveKeepingVisibility(member.Stroke, m_Target); }
                    member.Stroke.SetSymmetryGroup(move.NewGroup, member.Index);
                }
            }
        }

        protected override void OnUndo()
        {
            foreach (var move in m_Groups)
            {
                foreach (var member in move.Members)
                {
                    if (member.Moves) { MoveKeepingVisibility(member.Stroke, member.From); }
                    member.Stroke.SetSymmetryGroup(move.OldGroup, member.Index);
                }
            }
            foreach (var mirror in m_NewMirrors) { SymmetryMirrors.Unregister(mirror); }
        }

        /// Reparents keeping the stroke where it is in the world, and erased if it was.
        private static void MoveKeepingVisibility(Stroke stroke, CanvasScript canvas)
        {
            bool hadGeometry = stroke.m_Type != Stroke.Type.NotCreated;
            bool wasEnabled = hadGeometry && stroke.IsGeometryEnabled;
            stroke.SetParentKeepWorldPosition(canvas);
            if (hadGeometry && !wasEnabled && stroke.IsGeometryEnabled)
            {
                MirrorStrokeEdits.HideUncounted(stroke);
            }
        }
    }
} // namespace TiltBrush
