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
    public class AlignSelectedCommand : BaseCommand
    {
        private int m_Axis;
        private BoundsTypes m_AlignBoundsType;
        private List<Stroke> m_SelectedStrokes;
        private List<GrabWidget> m_ValidSelectedWidgets;
        private List<TrTransform> m_PreviousStrokeTransforms;
        private List<TrTransform> m_NewStrokeTransforms;
        private List<TrTransform> m_PreviousWidgetTransforms;
        private List<TrTransform> m_NewWidgetTransforms;

        public AlignSelectedCommand(int axis, BoundsTypes alignBoundsType, BaseCommand parent = null) : base(parent)
        {
            m_Axis = axis;
            m_AlignBoundsType = alignBoundsType;
            m_SelectedStrokes = SelectionManager.m_Instance.SelectedStrokes.ToList();
            m_ValidSelectedWidgets = SelectionManager.m_Instance.GetValidSelectedWidgets();

            // Get the position in x, y or z of the desired alignment plane
            float anchorValue = GetAnchorPosition();

            // Find the transforms needed to move each object to the alignment plane
            m_NewStrokeTransforms = m_SelectedStrokes.Select(s => CalcTransform(s, anchorValue)).ToList();
            m_NewWidgetTransforms = m_ValidSelectedWidgets.Select(w => CalcTransform(w, anchorValue)).ToList();

            m_PreviousStrokeTransforms = m_NewStrokeTransforms.Select(tr => tr.inverse).ToList();
            m_PreviousWidgetTransforms = m_NewWidgetTransforms.Select(tr => tr.inverse).ToList();
        }

        private TrTransform CalcTransform(Stroke stroke, float anchorValue)
        {
            // TODO respect groups

            float offset = m_AlignBoundsType switch
            {
                BoundsTypes.Min => anchorValue - stroke.m_BatchSubset.m_Bounds.min[m_Axis],
                BoundsTypes.Center => anchorValue - stroke.m_BatchSubset.m_Bounds.center[m_Axis],
                BoundsTypes.Max => anchorValue - stroke.m_BatchSubset.m_Bounds.max[m_Axis]
            };

            return TrTransform.T(new Vector3(
                m_Axis == 0 ? offset : 0,
                m_Axis == 1 ? offset : 0,
                m_Axis == 2 ? offset : 0
            ));
        }

        private TrTransform CalcTransform(GrabWidget widget, float anchorValue)
        {
            // TODO respect groups

            float offset = m_AlignBoundsType switch
            {
                BoundsTypes.Min => anchorValue - widget.GetBounds_SelectionCanvasSpace().min[m_Axis],
                BoundsTypes.Center => anchorValue - widget.LocalTransform.translation[m_Axis],
                BoundsTypes.Max => anchorValue - widget.GetBounds_SelectionCanvasSpace().max[m_Axis]
            };

            return TrTransform.T(new Vector3(
                m_Axis == 0 ? offset : 0,
                m_Axis == 1 ? offset : 0,
                m_Axis == 2 ? offset : 0
            ));
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            for (int i = 0; i < m_SelectedStrokes.Count; i++)
                m_SelectedStrokes[i].Recreate(m_NewStrokeTransforms[i]);
            for (int i = 0; i < m_ValidSelectedWidgets.Count; i++)
                m_ValidSelectedWidgets[i].LocalTransform = m_NewWidgetTransforms[i] * m_ValidSelectedWidgets[i].LocalTransform;
        }

        protected override void OnUndo()
        {
            for (int i = 0; i < m_SelectedStrokes.Count; i++)
                m_SelectedStrokes[i].Recreate(m_PreviousStrokeTransforms[i]);

            for (int i = 0; i < m_ValidSelectedWidgets.Count; i++)
                m_ValidSelectedWidgets[i].LocalTransform = m_PreviousWidgetTransforms[i] * m_ValidSelectedWidgets[i].LocalTransform;
        }

        private List<float> GetPositionList()
        {
            var positionList = new List<float>();

            // Strokes
            positionList.AddRange(SelectionManager.m_Instance
                .SelectedStrokes.Select(s => m_AlignBoundsType switch
                {
                    BoundsTypes.Min => s.m_BatchSubset.m_Bounds.min[m_Axis],
                    BoundsTypes.Center => s.m_BatchSubset.m_Bounds.center[m_Axis],
                    BoundsTypes.Max => s.m_BatchSubset.m_Bounds.max[m_Axis]
                }
            ));

            // Widgets
            positionList.AddRange(
                m_ValidSelectedWidgets.Select(
                    w => m_AlignBoundsType switch
                        {
                            BoundsTypes.Min => w.GetBounds_SelectionCanvasSpace().min[m_Axis],
                            BoundsTypes.Center => w.GetBounds_SelectionCanvasSpace().center[m_Axis],
                            BoundsTypes.Max => w.GetBounds_SelectionCanvasSpace().max[m_Axis]
                        }
                )
            );

            return positionList;
        }

        private float GetAnchorPosition()
        {
            var positions = GetPositionList();
            if (positions.Count == 0) return 0;
            return m_AlignBoundsType switch
            {
                BoundsTypes.Min => positions.Min(),
                BoundsTypes.Center => positions.Average(),
                BoundsTypes.Max => positions.Max()
            };
        }
    }
} // namespace TiltBrush
