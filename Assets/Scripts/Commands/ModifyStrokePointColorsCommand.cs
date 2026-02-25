// Copyright 2026 The Open Brush Authors
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
using UnityEngine;

namespace TiltBrush
{
    public class ModifyStrokePointColorsCommand : BaseCommand
    {
        private readonly Stroke m_TargetStroke;
        private readonly List<Color32?> m_StartOverrideColors;
        private readonly ColorOverrideMode m_StartColorOverrideMode;
        private readonly List<Color32?> m_EndOverrideColors;
        private readonly ColorOverrideMode m_EndColorOverrideMode;

        public ModifyStrokePointColorsCommand(
            Stroke stroke,
            List<Color32?> newOverrideColors,
            ColorOverrideMode newColorOverrideMode,
            BaseCommand parent = null) : base(parent)
        {
            m_TargetStroke = stroke;
            m_StartOverrideColors = stroke.m_OverrideColors?.ToList();
            m_StartColorOverrideMode = stroke.m_ColorOverrideMode;
            m_EndOverrideColors = newOverrideColors?.ToList();
            m_EndColorOverrideMode = newColorOverrideMode;
        }

        public override bool NeedsSave => true;

        private void ApplyNewColorsToStroke(List<Color32?> overrideColors, ColorOverrideMode colorOverrideMode)
        {
            m_TargetStroke.m_OverrideColors = overrideColors?.ToList();
            m_TargetStroke.m_ColorOverrideMode = colorOverrideMode;
            m_TargetStroke.InvalidateCopy();
            m_TargetStroke.Uncreate();
            m_TargetStroke.Recreate();
        }

        protected override void OnRedo()
        {
            ApplyNewColorsToStroke(m_EndOverrideColors, m_EndColorOverrideMode);
        }

        protected override void OnUndo()
        {
            ApplyNewColorsToStroke(m_StartOverrideColors, m_StartColorOverrideMode);
        }
    }
} // namespace TiltBrush
