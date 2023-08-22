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
    public class DistributeSelectedCommand : BaseCommand
    {
        private int m_Axis;
        private BoundsTypes m_BoundsType;
        private List<(float, object)> m_ObjectList;
        private List<TrTransform> m_PreviousTransforms;
        private List<TrTransform> m_NewTransforms;

        public DistributeSelectedCommand(int axis, BoundsTypes boundsType, BaseCommand parent = null) : base(parent)
        {
            m_Axis = axis;
            m_BoundsType = boundsType;
            GetObjectList();
            CalcTransforms();
        }

        private void GetObjectList()
        {
            m_ObjectList = new List<(float, object)>();

            m_ObjectList.AddRange(SelectionManager.m_Instance
                .SelectedStrokes.Select(s =>
                {
                    switch (m_BoundsType)
                    {
                        case BoundsTypes.Min:
                            return (s.m_BatchSubset.m_Bounds.min[m_Axis], s);
                        case BoundsTypes.Max:
                            return (s.m_BatchSubset.m_Bounds.max[m_Axis], s);
                        case BoundsTypes.Gaps:
                            // TODO
                            return (s.m_BatchSubset.m_Bounds.center[m_Axis], (object)s);
                        default: // Center
                            return (s.m_BatchSubset.m_Bounds.center[m_Axis], (object)s);
                    }
                }));

            var widgets = SelectionManager.m_Instance.GetValidSelectedWidgets();
            m_ObjectList.AddRange(widgets.Select(w => (w.transform.position[m_Axis], (object)w)));
            m_ObjectList = m_ObjectList.OrderBy(x => x.Item1).ToList();
        }

        private void CalcTransforms()
        {
            m_NewTransforms = new List<TrTransform>();

            for (int i = 0; i < m_ObjectList.Count; i++)
            {
                var tuple = m_ObjectList[i];
                object o = tuple.Item2;
                float min = m_ObjectList.Select(x => x.Item1).Min();
                float max = m_ObjectList.Select(x => x.Item1).Max();
                float inc = (max - min) / (m_ObjectList.Count - 1);
                Debug.Log($"Min: {min} Max: {max} = {(max - min)} :: inc: {inc}");

                float pos = min + (inc * i);
                Debug.Log($"pos: {pos}");
                if (o.GetType() == typeof(Stroke))
                {
                    Stroke stroke = o as Stroke;
                    float offset;
                    var bounds = stroke!.m_BatchSubset.m_Bounds;
                    switch (m_BoundsType)
                    {
                        case BoundsTypes.Min:
                            offset = pos - bounds.min[m_Axis] - bounds.extents[m_Axis];
                            break;
                        case BoundsTypes.Max:
                            offset = pos - bounds.max[m_Axis] + bounds.extents[m_Axis];
                            break;
                        case BoundsTypes.Gaps:
                            // TODO
                            offset = pos - bounds.center[m_Axis];
                            break;
                        default:
                            offset = pos - bounds.center[m_Axis];
                            break;
                    }
                    var tr = TrTransform.T(new Vector3(
                        m_Axis == 0 ? offset : 0,
                        m_Axis == 1 ? offset : 0,
                        m_Axis == 2 ? offset : 0
                    ));
                    m_NewTransforms.Add(tr);
                }
                else
                {
                    GrabWidget widget = o as GrabWidget;
                    var tr = widget!.LocalTransform;
                    tr.translation = new Vector3(
                        m_Axis == 0 ? pos : tr.translation.x,
                        m_Axis == 1 ? pos : tr.translation.y,
                        m_Axis == 2 ? pos : tr.translation.z
                    );
                    m_NewTransforms.Add(tr);
                }
            }

            m_PreviousTransforms = m_NewTransforms.Select(t => t.inverse).ToList();
        }

        public override bool NeedsSave => true;

        protected override void OnRedo()
        {
            ApplyTransforms(m_NewTransforms);
        }

        protected override void OnUndo()
        {
            ApplyTransforms(m_PreviousTransforms);
        }

        protected void ApplyTransforms(List<TrTransform> transformList)
        {
            for (int i = 0; i < m_ObjectList.Count; i++)
            {
                var tuple = m_ObjectList[i];
                object o = tuple.Item2;
                if (o.GetType() == typeof(Stroke))
                {
                    Stroke stroke = o as Stroke;
                    stroke.Recreate(transformList[i]);
                }
                else
                {
                    GrabWidget widget = o as GrabWidget;
                    widget.LocalTransform = transformList[i];
                }
            }
        }
    }
} // namespace TiltBrush
