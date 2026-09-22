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
    /// Records strokes having followed their mirror, so that the move can be undone.
    ///
    /// The strokes are moved as the mirror is dragged, by SymmetryMirrorMove, so this command is
    /// recorded rather than performed: it holds the total each stroke moved by, undoes it, and
    /// puts it back on redo.
    public class MoveMirrorStrokesCommand : BaseCommand
    {
        private readonly List<Stroke> m_Strokes;
        private readonly List<TrTransform> m_Transforms;
        private readonly List<SymmetryStrokeGroup> m_Groups;
        private readonly List<SymmetrySettingsSnapshot> m_GroupSettingsBefore;
        private readonly List<SymmetrySettingsSnapshot> m_GroupSettingsAfter;

        private readonly SymmetryMirror m_Mirror;
        private readonly SymmetrySettingsSnapshot m_MirrorSettingsBefore;
        private readonly SymmetrySettingsSnapshot m_MirrorSettingsAfter;

        public MoveMirrorStrokesCommand(
            SymmetryMirror mirror,
            SymmetrySettingsSnapshot mirrorSettingsBefore,
            SymmetrySettingsSnapshot mirrorSettingsAfter,
            List<Stroke> strokes, List<TrTransform> transforms,
            List<SymmetryStrokeGroup> groups,
            List<SymmetrySettingsSnapshot> groupSettingsBefore,
            List<SymmetrySettingsSnapshot> groupSettingsAfter,
            BaseCommand parent = null) : base(parent)
        {
            m_Mirror = mirror;
            m_MirrorSettingsBefore = mirrorSettingsBefore;
            m_MirrorSettingsAfter = mirrorSettingsAfter;
            m_Strokes = strokes;
            m_Transforms = transforms;
            m_Groups = groups;
            m_GroupSettingsBefore = groupSettingsBefore;
            m_GroupSettingsAfter = groupSettingsAfter;
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                m_Strokes[i].TransformGeometryInPlace(m_Transforms[i]);
            }
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                m_Groups[i].Settings = m_GroupSettingsAfter[i];
            }
            m_Mirror.Settings = m_MirrorSettingsAfter;
        }

        protected override void OnUndo()
        {
            for (int i = 0; i < m_Strokes.Count; ++i)
            {
                m_Strokes[i].TransformGeometryInPlace(m_Transforms[i].inverse);
            }
            for (int i = 0; i < m_Groups.Count; ++i)
            {
                m_Groups[i].Settings = m_GroupSettingsBefore[i];
            }
            m_Mirror.Settings = m_MirrorSettingsBefore;
        }
    }
} // namespace TiltBrush
