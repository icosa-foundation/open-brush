// Copyright 2022 The Tilt Brush Authors
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
    public class SetTransformsFromListCommand : BaseCommand
    {
        private List<TrTransform> m_FinalTransforms;
        private List<TrTransform> m_StartingTransforms;
        private List<Stroke> m_Strokes;
        private List<GrabWidget> m_Widgets;

        public SetTransformsFromListCommand(List<Stroke> strokes, List<GrabWidget> widgets,
                                    List<TrTransform> xforms, BaseCommand parent = null) : base(parent)
        {
            m_FinalTransforms = xforms;
            m_Strokes = strokes ?? new List<Stroke>();
            m_Widgets = widgets ?? new List<GrabWidget>();
            m_StartingTransforms = m_Strokes.Select(s => TrTransform.T(s.m_BatchSubset.m_Bounds.center)).ToList();
            m_StartingTransforms.AddRange(m_Widgets.Select(x => x.LocalTransform).ToList());
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            for (var i = 0; i < m_FinalTransforms.Count; i++)
            {
                if (i < m_Strokes.Count)
                {
                    var stroke = m_Strokes[i];
                    stroke.RecreateAt(m_FinalTransforms[i]);
                }
                else
                {
                    var widget = m_Widgets[i - m_Strokes.Count];
                    widget.LocalTransform = m_FinalTransforms[i];
                }
            }
        }

        protected override void OnUndo()
        {
            for (var i = 0; i < m_StartingTransforms.Count; i++)
            {
                if (i < m_Strokes.Count)
                {
                    var stroke = m_Strokes[i];
                    stroke.RecreateAt(m_StartingTransforms[i]);
                }
                else
                {
                    var widget = m_Widgets[i - m_Strokes.Count];
                    widget.LocalTransform = m_StartingTransforms[i];
                }
            }
        }
    }
} // namespace TiltBrush
