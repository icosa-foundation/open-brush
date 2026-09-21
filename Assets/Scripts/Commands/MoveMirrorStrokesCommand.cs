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
using System.Linq;

namespace TiltBrush
{
    /// Moves the strokes of a mirror to follow the mirror itself.
    ///
    /// Each stroke moves by its own copy's worth of the mirror's move: the stroke drawn by the
    /// symmetry's pointer j moves by Mj_new * Mj_old.inverse. The stroke the user actually drew
    /// is pointer 0, whose transform is the identity at both ends, so it stays put and the copies
    /// rearrange around it.
    ///
    /// Only groups drawn under the same number of pointers as the mirror now has are moved. A
    /// group drawn at a different order has no correspondence to the mirror's current copies, so
    /// it keeps its placement until there is a feature that can regenerate it.
    public class MoveMirrorStrokesCommand : BaseCommand
    {
        private readonly List<Stroke> m_Strokes = new List<Stroke>();
        private readonly List<TrTransform> m_Transforms = new List<TrTransform>();
        private readonly List<SymmetryStrokeGroup> m_Groups = new List<SymmetryStrokeGroup>();
        private readonly List<SymmetrySettingsSnapshot> m_GroupSettingsBefore =
            new List<SymmetrySettingsSnapshot>();
        private readonly List<SymmetrySettingsSnapshot> m_GroupSettingsAfter =
            new List<SymmetrySettingsSnapshot>();

        private readonly SymmetryMirror m_Mirror;
        private readonly SymmetrySettingsSnapshot m_MirrorSettingsBefore;
        private readonly SymmetrySettingsSnapshot m_MirrorSettingsAfter;

        public bool MovesAnything => m_Strokes.Count > 0;

        public MoveMirrorStrokesCommand(
            SymmetryMirror mirror,
            IList<TrTransform> before, IList<TrTransform> after,
            SymmetrySettingsSnapshot settingsAfter,
            BaseCommand parent = null) : base(parent)
        {
            m_Mirror = mirror;
            m_MirrorSettingsBefore = mirror.Settings;
            m_MirrorSettingsAfter = settingsAfter;

            foreach (var group in GroupsOf(mirror))
            {
                var placement = group.Settings?.PointerTransforms;
                if (placement == null || placement.Count != after.Count) { continue; }

                bool moved = false;
                foreach (var stroke in group.Strokes)
                {
                    int index = stroke.SymmetryPointerIndex;
                    if (index <= 0 || index >= after.Count) { continue; }
                    if (!stroke.IsGeometryEnabled) { continue; }
                    // A stroke that is currently selected is in the selection canvas, being moved
                    // by something else; leave it where the user put it.
                    if (SelectionManager.m_Instance.IsStrokeSelected(stroke)) { continue; }

                    TrTransform xf = after[index] * before[index].inverse;
                    if (!xf.IsFinite() || xf == TrTransform.identity) { continue; }
                    m_Strokes.Add(stroke);
                    m_Transforms.Add(xf);
                    moved = true;
                }

                if (moved)
                {
                    // The group's record has to say where its strokes are now, or a later edit
                    // mirrored onto a peer would be worked out from where they used to be.
                    m_Groups.Add(group);
                    m_GroupSettingsBefore.Add(group.Settings);
                    m_GroupSettingsAfter.Add(group.Settings.WithPointerTransforms(after));
                }
            }
        }

        /// The groups of strokes drawn under this mirror. Groups are only reachable through their
        /// strokes, which is cheap enough for something that happens when a mirror is let go of.
        private static IEnumerable<SymmetryStrokeGroup> GroupsOf(SymmetryMirror mirror)
        {
            var seen = new HashSet<SymmetryStrokeGroup>();
            foreach (var stroke in SketchMemoryScript.AllStrokes())
            {
                var group = stroke.SymmetryPeerGroup;
                if (group != null && ReferenceEquals(group.Mirror, mirror) && seen.Add(group))
                {
                    yield return group;
                }
            }
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            TransformItems.TransformEach(m_Strokes, m_Transforms);
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                m_Groups[i].Settings = m_GroupSettingsAfter[i];
            }
            m_Mirror.Settings = m_MirrorSettingsAfter;
        }

        protected override void OnUndo()
        {
            TransformItems.TransformEach(
                m_Strokes, m_Transforms.Select(xf => xf.inverse).ToList());
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                m_Groups[i].Settings = m_GroupSettingsBefore[i];
            }
            m_Mirror.Settings = m_MirrorSettingsBefore;
        }
    }
} // namespace TiltBrush
