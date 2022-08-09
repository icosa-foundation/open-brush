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

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;
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

    public class TransformPanel : BasePanel
    {

        public GrabWidget m_LastWidget;

        public TextMeshPro m_LabelForTranslationX;
        public TextMeshPro m_LabelForTranslationY;
        public TextMeshPro m_LabelForTranslationZ;

        public TextMeshPro m_LabelForRotationX;
        public TextMeshPro m_LabelForRotationY;
        public TextMeshPro m_LabelForRotationZ;

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

        public static void HandleToggle(TransformPanelToggleButton btn)
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
            }
        }

        public static void HandleAction(TransformPanelActionButton btn)
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


        private static void Distribute(int axis)
        {
        }

        private static void Align(int axis)
        {
            var center = GetAveragePosition();
            foreach (var stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                var offset = stroke.StrokeTransform.position - center;
                var tr = TrTransform.T(new Vector3(
                    axis==0 ? offset.x : 0,
                    axis==1 ? offset.y : 0,
                    axis==2 ? offset.z : 0
                ));
                Debug.Log($"Moving stroke by {offset}");
                stroke.Recreate(tr);
            }
            foreach (var widget in GetValidSelectedWidgets())
            {
                var tr = widget.LocalTransform;
                tr.translation = new Vector3(
                    axis==0 ? center.x : tr.translation.x,
                    axis==1 ? center.y : tr.translation.y,
                    axis==2 ? center.z : tr.translation.z
                );
                widget.LocalTransform = tr;
            }
        }

        private static Vector3 GetAveragePosition()
        {
            var positionList = new List<Vector3>();
            positionList.AddRange(SelectionManager.m_Instance.SelectedStrokes.Select(s => s.StrokeTransform.position));
            positionList.AddRange(GetValidSelectedWidgets().Select(w=>w.transform.position));
            return positionList.Aggregate(new Vector3(0,0,0), (s,v) => s + v) / (float)positionList.Count;
        }

        private static IEnumerable<GrabWidget> GetValidSelectedWidgets() => SelectionManager.m_Instance.SelectedWidgets;
    }
}
