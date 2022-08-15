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
            
            float anchorValue = GetAnchorPosition(m_Axis);
            
            m_NewStrokeTransforms = m_SelectedStrokes.Select(s => CalcTransform(s, anchorValue)).ToList();
            m_PreviousStrokeTransforms = m_NewStrokeTransforms.Select(s => s.inverse).ToList();
            
            m_PreviousWidgetTransforms = m_ValidSelectedWidgets.Select(s => s.LocalTransform).ToList();
            m_NewWidgetTransforms = m_ValidSelectedWidgets.Select(w => CalcTransform(w, anchorValue)).ToList();
        }

        private TrTransform CalcTransform(Stroke stroke, float anchorValue)
        {

            // TODO respect groups

            float offset = 0;
            switch (m_AlignBoundsType)
            {
                case BoundsTypes.Min:
                    offset = anchorValue - stroke.m_BatchSubset.m_Bounds.min[m_Axis];
                    break;
                case BoundsTypes.Center:
                    offset = anchorValue - stroke.m_BatchSubset.m_Bounds.center[m_Axis];
                    break;
                case BoundsTypes.Max:
                    offset = anchorValue - stroke.m_BatchSubset.m_Bounds.max[m_Axis];
                    break;
            }

            var tr = TrTransform.T(new Vector3(
                m_Axis == 0 ? offset : 0,
                m_Axis == 1 ? offset : 0,
                m_Axis == 2 ? offset : 0
            ));
            return tr;
        }

        private TrTransform CalcTransform(GrabWidget widget, float anchorValue)
        {
            var tr = widget.LocalTransform;
            tr.translation = new Vector3(
                m_Axis == 0 ? anchorValue : tr.translation.x,
                m_Axis == 1 ? anchorValue : tr.translation.y,
                m_Axis == 2 ? anchorValue : tr.translation.z
            );
            return tr;
        }
        
        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            for (int i = 0; i < m_SelectedStrokes.Count; i++)
            {
                m_SelectedStrokes[i].Recreate(m_NewStrokeTransforms[i]);
            }

            for (int i = 0; i < m_ValidSelectedWidgets.Count; i++)
            {
                m_ValidSelectedWidgets[i].LocalTransform = m_NewWidgetTransforms[i];
            }
        }

        protected override void OnUndo()
        {
            for (int i = 0; i < m_SelectedStrokes.Count; i++)
            {
                m_SelectedStrokes[i].Recreate(m_PreviousStrokeTransforms[i]);
            }

            for (int i = 0; i < m_ValidSelectedWidgets.Count; i++)
            {
                m_ValidSelectedWidgets[i].LocalTransform = m_PreviousWidgetTransforms[i];
            }
        }

        private List<float> GetPositionList(int axis)
        {
            var positionList = new List<float>();
            positionList.AddRange(SelectionManager.m_Instance
                .SelectedStrokes.Select(s =>
                {
                    switch (m_AlignBoundsType)
                    {
                        case BoundsTypes.Min:
                            return s.m_BatchSubset.m_Bounds.min[axis];
                        case BoundsTypes.Max:
                            return s.m_BatchSubset.m_Bounds.max[axis];
                        default:
                            return s.m_BatchSubset.m_Bounds.center[axis];
                    }
                }));
            positionList.AddRange(m_ValidSelectedWidgets.Select(w => w.transform.position[axis]));
            return positionList;
        }

        private float GetAnchorPosition(int axis)
        {
            var items = GetPositionList(axis);
            return items.Count > 0 ? items.Average() : 0;
        }
        
    }
} // namespace TiltBrush
