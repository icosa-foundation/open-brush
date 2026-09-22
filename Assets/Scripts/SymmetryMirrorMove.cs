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
    /// Carries the strokes of the active mirror along as the user moves the mirror.
    ///
    /// The strokes follow live, every frame. Each one's geometry is transformed where it lies
    /// inside its batch, which costs a pass over that stroke's vertices and a mesh update for the
    /// batch it is in - no regeneration, no re-batching. Strokes drawn together share batches, so
    /// the per-frame cost is closer to the number of batches involved than the number of strokes.
    ///
    /// The whole move is recorded as one command when the mirror is let go of. It is recorded
    /// rather than performed, because the strokes have already moved.
    ///
    /// The symmetry transforms are read from PointerManager as the mirror moves rather than
    /// derived here, so what the strokes follow is exactly what drawing would produce at each
    /// pose. Only groups drawn under the same number of pointers as the mirror now has can
    /// follow: a group drawn at a different order has no correspondence to the mirror's current
    /// copies, and moving 6 strokes to 8 positions isn't a transform.
    public static class SymmetryMirrorMove
    {
        private static SymmetryMirror m_Mirror;
        private static SymmetrySettingsSnapshot m_MirrorSettingsAtStart;
        // The symmetry transforms as of the last frame the strokes were moved to.
        private static List<TrTransform> m_Previous;

        private static readonly List<Stroke> m_Strokes = new List<Stroke>();
        private static readonly List<int> m_PointerIndices = new List<int>();
        // Per stroke, everything it has moved by so far this drag.
        private static readonly List<TrTransform> m_Applied = new List<TrTransform>();
        private static readonly List<SymmetryStrokeGroup> m_Groups = new List<SymmetryStrokeGroup>();
        private static readonly List<SymmetrySettingsSnapshot> m_GroupSettingsAtStart =
            new List<SymmetrySettingsSnapshot>();

        public static bool IsMoving => m_Mirror != null;

        /// Call when the user takes hold of the mirror.
        public static void Begin()
        {
            Forget();
            if (!SymmetryPeerEditing.Enabled) { return; }

            var mirror = SymmetryMirrors.Active;
            if (mirror == null) { return; }
            var transforms = PointerManager.m_Instance.GetSymmetryTransforms_CS();
            if (transforms.Count == 0) { return; }

            Gather(mirror, transforms.Count);
            if (m_Strokes.Count == 0 && m_Groups.Count == 0) { return; }

            m_Mirror = mirror;
            m_MirrorSettingsAtStart = mirror.Settings;
            m_Previous = transforms;
        }

        /// Call every frame while it is held.
        public static void Update()
        {
            if (m_Mirror == null) { return; }
            var now = PointerManager.m_Instance.GetSymmetryTransforms_CS();
            if (now.Count != m_Previous.Count) { return; }

            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                int index = m_PointerIndices[i];
                TrTransform step = now[index] * m_Previous[index].inverse;
                if (!step.IsFinite() || step == TrTransform.identity) { continue; }
                if (m_Strokes[i].TransformGeometryInPlace(step))
                {
                    m_Applied[i] = step * m_Applied[i];
                }
            }
            m_Previous = now;
        }

        /// Call when the user lets go. Records what happened so that undo puts it back.
        public static void End()
        {
            var mirror = m_Mirror;
            if (mirror == null) { Forget(); return; }

            Update();

            var strokes = new List<Stroke>();
            var transforms = new List<TrTransform>();
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                if (m_Applied[i] == TrTransform.identity) { continue; }
                strokes.Add(m_Strokes[i]);
                transforms.Add(m_Applied[i]);
            }

            var settingsNow = SymmetrySettingsSnapshot.FromCurrentSettings();
            mirror.Settings = settingsNow;

            if (strokes.Count > 0)
            {
                // The groups' records have to say where their strokes are now, or a later edit
                // mirrored onto a peer would be worked out from where they used to be.
                var groupSettingsAfter = new List<SymmetrySettingsSnapshot>();
                foreach (var group in m_Groups)
                {
                    groupSettingsAfter.Add(
                        group.Settings.WithPointerTransforms(m_Previous));
                }
                for (int i = 0; i < m_Groups.Count; ++i)
                {
                    m_Groups[i].Settings = groupSettingsAfter[i];
                }

                SketchMemoryScript.m_Instance.RecordCommand(
                    new MoveMirrorStrokesCommand(
                        mirror, m_MirrorSettingsAtStart, settingsNow,
                        strokes, transforms,
                        new List<SymmetryStrokeGroup>(m_Groups),
                        new List<SymmetrySettingsSnapshot>(m_GroupSettingsAtStart),
                        groupSettingsAfter));
            }
            Forget();
        }

        /// The strokes that can follow this mirror, and the groups they belong to.
        private static void Gather(SymmetryMirror mirror, int pointerCount)
        {
            var eligible = new HashSet<SymmetryStrokeGroup>();
            var skipped = new HashSet<SymmetryStrokeGroup>();
            foreach (var stroke in SketchMemoryScript.AllStrokes())
            {
                var group = stroke.SymmetryPeerGroup;
                if (group == null || !ReferenceEquals(group.Mirror, mirror)) { continue; }
                if (skipped.Contains(group)) { continue; }

                if (!eligible.Contains(group))
                {
                    var placement = group.Settings?.PointerTransforms;
                    if (placement == null || placement.Count != pointerCount)
                    {
                        skipped.Add(group);
                        continue;
                    }
                    eligible.Add(group);
                    m_Groups.Add(group);
                    m_GroupSettingsAtStart.Add(group.Settings);
                }

                int index = stroke.SymmetryPointerIndex;
                // Pointer 0 is the stroke the user drew: its transform is the identity at both
                // ends of any move, so it stays put and the copies rearrange around it.
                if (index <= 0 || index >= pointerCount) { continue; }
                if (!stroke.IsGeometryEnabled) { continue; }
                // A selected stroke is in the selection canvas being moved by something else.
                if (SelectionManager.m_Instance.IsStrokeSelected(stroke)) { continue; }

                m_Strokes.Add(stroke);
                m_PointerIndices.Add(index);
                m_Applied.Add(TrTransform.identity);
            }
        }

        private static void Forget()
        {
            m_Mirror = null;
            m_MirrorSettingsAtStart = null;
            m_Previous = null;
            m_Strokes.Clear();
            m_PointerIndices.Clear();
            m_Applied.Clear();
            m_Groups.Clear();
            m_GroupSettingsAtStart.Clear();
        }
    }
} // namespace TiltBrush
