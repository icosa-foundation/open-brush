// Copyright 2020 The Tilt Brush Authors
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
    public class RepaintStrokeCommand : BaseCommand
    {
        private List<Stroke> m_TargetStrokes;
        private List<Color> m_StartColors;
        private List<Guid> m_StartGuids;
        private List<float> m_StartSizes;
        private List<Color> m_EndColors;
        private List<Guid> m_EndGuids;
        private List<float> m_EndSizes;

        public RepaintStrokeCommand(
            Stroke stroke, Color newcolor, Guid newGuid, float newSize, BaseCommand parent = null) : base(parent)
        {
            m_TargetStrokes = new List<Stroke> { stroke };

            m_StartColors = new List<Color> { stroke.m_Color };
            m_StartGuids = new List<Guid> { stroke.m_BrushGuid };
            m_StartSizes = new List<float> { stroke.m_BrushSize };

            m_EndColors = new List<Color> { newcolor };
            m_EndGuids = new List<Guid> { newGuid };
            m_EndSizes = new List<float> { newSize };
        }

        public RepaintStrokeCommand(List<Stroke> strokes, List<Color> newcolors, List<Guid> newGuids, List<float> newSizes, BaseCommand parent = null) : base(parent)
        {
            m_TargetStrokes = strokes;

            m_StartColors = strokes.Select(s => s.m_Color).ToList();
            m_StartGuids = strokes.Select(s => s.m_BrushGuid).ToList();
            m_StartSizes = strokes.Select(s => s.m_BrushSize).ToList();

            m_EndColors = newcolors.ToList();
            m_EndGuids = newGuids.ToList();
            m_EndSizes = newSizes.ToList();
        }

        public override bool NeedsSave { get { return true; } }

        private void ApplyColorAndBrushToObject(Stroke stroke, Color color, Guid brushGuid, float brushSize)
        {
            stroke.m_Color = ColorPickerUtils.ClampLuminance(
                color, BrushCatalog.m_Instance.GetBrush(brushGuid).m_ColorLuminanceMin);
            stroke.m_BrushGuid = brushGuid;
            stroke.m_BrushSize = brushSize;
            stroke.InvalidateCopy();
            stroke.Uncreate();
            stroke.Recreate();
        }

        protected override void OnRedo()
        {
            for (var i = 0; i < m_TargetStrokes.Count; i++)
            {
                ApplyColorAndBrushToObject(m_TargetStrokes[i], m_EndColors[i], m_EndGuids[i], m_EndSizes[i]);
            }
        }

        protected override void OnUndo()
        {
            for (var i = 0; i < m_TargetStrokes.Count; i++)
            {
                ApplyColorAndBrushToObject(m_TargetStrokes[i], m_StartColors[i], m_StartGuids[i], m_StartSizes[i]);
            }
        }
    }
} // namespace TiltBrush
