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
        private List<Stroke> m_Strokes;
        private IEnumerable<GrabWidget> m_Widgets;
        private CanvasScript m_Layer;
        // Strokes the symmetry drew alongside m_Strokes, each with the transform that mirrors this
        // command's onto it. Empty unless peer editing is on.
        private List<Stroke> m_PeerStrokes = new List<Stroke>();
        private List<TrTransform> m_PeerTransforms = new List<TrTransform>();

        public TransformItemsCommand(IEnumerable<Stroke> strokes, IEnumerable<GrabWidget> widgets,
                                     TrTransform xf, Vector3 pivot, BaseCommand parent = null) : base(parent)
        {
            m_Transform = xf;
            m_Pivot = pivot;
            m_Strokes = strokes?.ToList() ?? new List<Stroke>();
            m_Widgets = widgets ?? new List<GrabWidget>();

            // What the strokes actually undergo, pivot included; that is what the peers mirror.
            TrTransform xfAboutPivot =
                TrTransform.T(m_Pivot) * m_Transform * TrTransform.T(-m_Pivot);
            SymmetryPeerEditing.GatherPeerTransforms(
                m_Strokes, xfAboutPivot, m_PeerStrokes, m_PeerTransforms);
        }

        public override bool NeedsSave { get { return true; } }

        protected override void OnRedo()
        {
            TransformItems.Transform(m_Strokes, m_Widgets, m_Pivot, m_Transform);
            TransformItems.TransformEach(m_PeerStrokes, m_PeerTransforms);
        }

        protected override void OnUndo()
        {
            TransformItems.Transform(m_Strokes, m_Widgets, m_Pivot, m_Transform.inverse);
            TransformItems.TransformEach(
                m_PeerStrokes, m_PeerTransforms.Select(xf => xf.inverse).ToList());
        }

    }
} // namespace TiltBrush
