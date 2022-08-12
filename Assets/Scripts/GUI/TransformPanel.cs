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
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public enum TransformPanelToggleType
    {
        LockTranslateX,
        LockTranslateY,
        LockTranslateZ,
        LockRotateX,
        LockRotateY,
        LockRotateZ,
        LockSnapTranslateX,
        LockSnapTranslateY,
        LockSnapTranslateZ
    }

    public enum TransformPanelActionType
    {
        AlignX,
        AlignY,
        AlignZ,
        DistributeX,
        DistributeY,
        DistributeZ,
    }

    public enum BoundsTypes
    {
        Min,
        Center,
        Max,
    }

    public class TransformPanel : BasePanel
    {

        public GrabWidget m_LastWidget;

        public TextMeshPro m_LabelForTranslationX;
        public TextMeshPro m_LabelForTranslationY;
        public TextMeshPro m_LabelForTranslationZ;

        public TextMeshPro m_LabelForRotationX;
        public TextMeshPro m_LabelForRotationY;
        public TextMeshPro m_LabelForRotationZ;

        private BoundsTypes m_AlignBoundsType = BoundsTypes.Center;
        private BoundsTypes m_DistributeBoundsType = BoundsTypes.Center;
        
        void Update()
        {
            BaseUpdate();
            TrTransform activeTr = TrTransform.identity;
            if (SketchControlsScript.m_Instance.CurrentGrabWidget != null)
            {
                m_LastWidget = SketchControlsScript.m_Instance.CurrentGrabWidget;
            }

            if (SelectionManager.m_Instance.HasSelection)
            {
                m_LastWidget = null;

                activeTr = SelectionManager.m_Instance.SelectionTransform;
                activeTr.translation += App.Scene.SelectionCanvas.GetCanvasBoundingBox(true).center;
            }
            else if (m_LastWidget!=null && m_LastWidget.Canvas!=null)
            {
                activeTr = m_LastWidget.Canvas.LocalPose;
            }

            m_LabelForTranslationX.text = FormatValue(activeTr.translation.x);
            m_LabelForTranslationY.text = FormatValue(activeTr.translation.y);
            m_LabelForTranslationZ.text = FormatValue(activeTr.translation.z);
            m_LabelForRotationX.text = FormatValue(activeTr.translation.x);
            m_LabelForRotationY.text = FormatValue(activeTr.translation.y);
            m_LabelForRotationZ.text = FormatValue(activeTr.translation.z);
        }

        private string FormatValue(float val)
        {
            // 2 digits after the decimal, 5 digits maximum
            return (Mathf.FloorToInt(val*100)/100f).ToString("G5");
        }

        public void HandleToggle(TransformPanelToggleButton btn)
        {
            switch (btn.m_ButtonType)
            {
                case TransformPanelToggleType.LockRotateX:
                    SelectionManager.m_Instance.m_LockRotationX = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockRotateY:
                    SelectionManager.m_Instance.m_LockRotationY = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockRotateZ:
                    SelectionManager.m_Instance.m_LockRotationZ = btn.ToggleState;
                    break;
                
                case TransformPanelToggleType.LockTranslateX:
                    SelectionManager.m_Instance.m_LockTranslationX = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockTranslateY:
                    SelectionManager.m_Instance.m_LockTranslationY = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockTranslateZ:
                    SelectionManager.m_Instance.m_LockTranslationZ = btn.ToggleState;
                    break;
                
                case TransformPanelToggleType.LockSnapTranslateX:
                    SelectionManager.m_Instance.m_LockSnapTranslationX = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockSnapTranslateY:
                    SelectionManager.m_Instance.m_LockSnapTranslationY = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockSnapTranslateZ:
                    SelectionManager.m_Instance.m_LockSnapTranslationZ = btn.ToggleState;
                    break;
            }
        }

        public void HandleAction(TransformPanelActionButton btn)
        {
            switch (btn.m_ButtonType)
            {
                case TransformPanelActionType.AlignX:
                    Align(0);
                    break;
                case TransformPanelActionType.AlignY:
                    Align(1);
                    break;
                case TransformPanelActionType.AlignZ:
                    Align(2);
                    break;
                case TransformPanelActionType.DistributeX:
                    Distribute(0);
                    break;
                case TransformPanelActionType.DistributeY:
                    Distribute(1);
                    break;
                case TransformPanelActionType.DistributeZ:
                    Distribute(2);
                    break;
            }
        }

        public void HandleAlignStateButton(int state)
        {
            m_AlignBoundsType = (BoundsTypes)state;
        }

        public void HandleDistributeStateButton(int state)
        {
            m_DistributeBoundsType = (BoundsTypes)state;
        }
        
        private void Align(int axis)
        {
            float val = GetAnchorPosition(axis);
            
            // TODO respect groups
            
            foreach (var stroke in SelectionManager.m_Instance.SelectedStrokes)
            {

                float offset = 0;
                switch (m_AlignBoundsType)
                {
                    case BoundsTypes.Min:
                        offset = val - stroke.m_BatchSubset.m_Bounds.min[axis];
                        break;
                    case BoundsTypes.Center:
                        offset = val - stroke.m_BatchSubset.m_Bounds.center[axis];
                        break;
                    case BoundsTypes.Max:
                        offset = val - stroke.m_BatchSubset.m_Bounds.max[axis];
                        break;
                }
                
                var tr = TrTransform.T(new Vector3(
                    axis==0 ? offset : 0,
                    axis==1 ? offset : 0,
                    axis==2 ? offset : 0
                ));
                stroke.Recreate(tr);
            }
            
            foreach (GrabWidget widget in GetValidSelectedWidgets())
            {
                var tr = widget.LocalTransform;
                tr.translation = new Vector3(
                    axis==0 ? val : tr.translation.x,
                    axis==1 ? val : tr.translation.y,
                    axis==2 ? val : tr.translation.z
                );
                widget.LocalTransform = tr;
            }
        }

        private void Distribute(int axis)
        {
            var objectList = new List<(float, object)>();
            
            objectList.AddRange(SelectionManager.m_Instance
                .SelectedStrokes.Select(s =>
                {
                    switch (m_DistributeBoundsType)
                    {
                        case BoundsTypes.Min:
                            return (s.m_BatchSubset.m_Bounds.min[axis], s);
                        case BoundsTypes.Max:
                            return (s.m_BatchSubset.m_Bounds.max[axis], s);
                        default:
                            return (s.m_BatchSubset.m_Bounds.center[axis], (object)s);
                    }
                }));
            
            var widgets = GetValidSelectedWidgets();
            objectList.AddRange(widgets.Select(w=>(w.transform.position[axis], (object)w)));
            objectList = objectList.OrderBy(x=>x.Item1).ToList();

            float min = objectList.Select(x => x.Item1).Min();
            float max = objectList.Select(x => x.Item1).Max();
            float inc = (max - min) / (objectList.Count - 1);
            float pos = min;
            foreach (var tuple in objectList)
            {
                object o = tuple.Item2;
                if (o.GetType() == typeof(Stroke))
                {
                    Stroke stroke = o as Stroke;
                    var offset = pos - stroke!.m_BatchSubset.m_Bounds.min[axis];
                    var tr = TrTransform.T(new Vector3(
                        axis==0 ? offset : 0,
                        axis==1 ? offset : 0,
                        axis==2 ? offset : 0
                    ));
                    stroke.Recreate(tr);
                }
                else
                {
                    GrabWidget widget = o as GrabWidget;
                    var tr = widget!.LocalTransform;
                    tr.translation = new Vector3(
                        axis==0 ? pos : tr.translation.x,
                        axis==1 ? pos : tr.translation.y,
                        axis==2 ? pos : tr.translation.z
                    );
                    widget.LocalTransform = tr;
                    
                }
                pos += inc;
            }
        }
        
        private float GetAnchorPosition(int axis)
        {
            var items = GetPositionList(axis);
            return items.Count > 0 ? items.Average() : 0;
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
            positionList.AddRange(GetValidSelectedWidgets().Select(w=>w.transform.position[axis]));
            return positionList;
        }

        private static IEnumerable<GrabWidget> GetValidSelectedWidgets() => SelectionManager.m_Instance.SelectedWidgets;
    }
}
