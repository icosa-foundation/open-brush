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
    public class TransformItemsCommand : BaseCommand
    {
        private TrTransform m_StartTransform;
        private TrTransform m_Transform;
        private Vector3 m_Pivot;
        private IEnumerable<Stroke> m_Strokes;
        private IEnumerable<GrabWidget> m_Widgets;
        private CanvasScript m_Layer;

        public TransformItemsCommand(IEnumerable<Stroke> strokes, IEnumerable<GrabWidget> widgets,
                                     TrTransform xf, Vector3 pivot, BaseCommand parent = null) : base(parent)
        {
            m_Transform = xf;
            m_Pivot = pivot;
            m_Strokes = strokes ?? new List<Stroke>();
            m_Widgets = widgets ?? new List<GrabWidget>();
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            TransformItems.Transform(m_Strokes, m_Widgets, m_Pivot, m_Transform);
        }

        protected override void OnUndo()
        {
            TransformItems.Transform(m_Strokes, m_Widgets, m_Pivot, m_Transform.inverse);
        }

    }
} // namespace TiltBrush
