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
        Gaps,
    }

    public class TransformPanel : BasePanel
    {

        public GrabWidget m_LastWidget;

        public EditableLabel m_LabelForTranslationX;
        public EditableLabel m_LabelForTranslationY;
        public EditableLabel m_LabelForTranslationZ;

        public EditableLabel m_LabelForRotationX;
        public EditableLabel m_LabelForRotationY;
        public EditableLabel m_LabelForRotationZ;

        public EditableLabel m_LabelForScale;
        
        private BoundsTypes m_AlignBoundsType = BoundsTypes.Center;
        private BoundsTypes m_DistributeBoundsType = BoundsTypes.Center;
        
        private Bounds m_SelectionBounds;

        void OnSelectionPoseChanged(TrTransform _, TrTransform __)
        {
            OnSelectionPoseChanged();
        }
        
        void OnSelectionPoseChanged()
        {
            
            var translation = CurrentSelectionPos();
            
            var selectionTr = SelectionManager.m_Instance.SelectionTransform;
            var rotation = selectionTr.rotation.eulerAngles;
            var scale = selectionTr.scale;
            m_LabelForTranslationX.SetValue(FormatValue(translation.x));
            m_LabelForTranslationY.SetValue(FormatValue(translation.y));
            m_LabelForTranslationZ.SetValue(FormatValue(translation.z));
            m_LabelForRotationX.SetValue(FormatValue(rotation.x));
            m_LabelForRotationY.SetValue(FormatValue(rotation.y));
            m_LabelForRotationZ.SetValue(FormatValue(rotation.z));
            m_LabelForScale.SetValue(FormatValue(scale));
        }
        
        private Vector3 CurrentSelectionPos()
        {
            var selectionTr = SelectionManager.m_Instance.SelectionTransform;
            var translation = selectionTr.MultiplyPoint(m_SelectionBounds.center);
            return translation;
        }

        void Awake()
        {
            App.Scene.SelectionCanvas.PoseChanged += OnSelectionPoseChanged;
            App.Switchboard.SelectionChanged += OnSelectionChanged;
        }

        void OnDestroy()
        {
            App.Scene.SelectionCanvas.PoseChanged -= OnSelectionPoseChanged;
            App.Switchboard.SelectionChanged -= OnSelectionChanged;
        }
        
        private void OnSelectionChanged()
        {
            m_SelectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox();
            OnSelectionPoseChanged();
        }

        // private TrTransform GetActiveTransform()
        // {
        //     TrTransform activeTr = TrTransform.identity;
        //     if (SketchControlsScript.m_Instance.CurrentGrabWidget != null)
        //     {
        //         m_LastWidget = SketchControlsScript.m_Instance.CurrentGrabWidget;
        //     }
        //
        //     // Prefer to use selection if it exists
        //     if (SelectionManager.m_Instance.HasSelection)
        //     {
        //         m_LastWidget = null;
        //         activeTr = SelectionManager.m_Instance.SelectionTransform;
        //     }
        //     // if no selection then the panel should control the last widget interacted with
        //     else if (m_LastWidget!=null && m_LastWidget.Canvas!=null)
        //     {
        //         activeTr = m_LastWidget.LocalTransform;
        //     }
        //     return activeTr;
        // }
        //
        // private void SetActiveTransform(TrTransform tr)
        // {
        //     if (SketchControlsScript.m_Instance.CurrentGrabWidget != null)
        //     {
        //         m_LastWidget = SketchControlsScript.m_Instance.CurrentGrabWidget;
        //     }
        //
        //     if (SelectionManager.m_Instance.HasSelection)
        //     {
        //         m_LastWidget = null;
        //         SelectionManager.m_Instance.SelectionTransform = tr;
        //     }
        //     else if (m_LastWidget!=null && m_LastWidget.Canvas!=null)
        //     {
        //         m_LastWidget.LocalTransform = tr;
        //     }
        // }

        private string FormatValue(float val)
        {
            // 2 digits after the decimal, 5 digits maximum
            return (Mathf.Round(val*100)/100f).ToString("G5");
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
                    SelectionManager.m_Instance.m_EnableSnapTranslationX = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockSnapTranslateY:
                    SelectionManager.m_Instance.m_EnableSnapTranslationY = btn.ToggleState;
                    break;
                case TransformPanelToggleType.LockSnapTranslateZ:
                    SelectionManager.m_Instance.m_EnableSnapTranslationZ = btn.ToggleState;
                    break;
            }
        }

        public void HandleLabelEdited(EditableLabel label)
        {
            var selectionTr = SelectionManager.m_Instance.SelectionTransform;
            var currentTranslation = selectionTr.MultiplyPoint(m_SelectionBounds.center);
            var newTranslation = currentTranslation;

            var currentRotation = selectionTr.rotation.eulerAngles;
            var newRotation = currentRotation;
            
            var currentScale = selectionTr.scale;
            var newScale = currentScale;

            if (float.TryParse(label.LastTextInput, out float value))
            {
                label.SetError(false);
                switch (label.m_LabelTag)
                {
                    case "TX":
                        newTranslation.x = value;
                        break;
                    case "TY":
                        newTranslation.y = value;
                        break;
                    case "TZ":
                        newTranslation.z = value;
                        break;
                    case "RX":
                        newRotation.x = value;
                        break;
                    case "RY":
                        newRotation.y = value;
                        break;
                    case "RZ":
                        newRotation.z = value;
                        break;
                    case "SX":
                        newScale = value;
                        break;
                    // case "SY":
                    //     activeTr.scale.y = value;
                    //     break;
                    // case "SZ":
                    //     activeTr.scale.z = value;
                    //     break;
                }

                if (label.m_LabelTag.StartsWith("T"))
                {
                    var translationAmount = currentTranslation - newTranslation;
                    selectionTr.translation -= translationAmount;
                }
                else if (label.m_LabelTag.StartsWith("R"))
                {
                    selectionTr.rotation.eulerAngles = newRotation;
                    var offset = TrTransform.R(Quaternion.Euler(newRotation - currentRotation)).MultiplyPoint(m_SelectionBounds.center);
                    selectionTr.translation += m_SelectionBounds.center - offset;
                }
                else if (label.m_LabelTag.StartsWith("S"))
                {
                    selectionTr.scale = newScale;
                    selectionTr.translation -= (m_SelectionBounds.center * (newScale - currentScale));
                }
   
                SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                    new TransformSelectionCommand(selectionTr)
                );
            }
            else
            {
                m_LabelForTranslationX.SetError(true);
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

        public void HandleSnapSelectionToGrid()
        {
            foreach (var widget in SelectionManager.m_Instance.GetValidSelectedWidgets())
            {
                var tr = widget.LocalTransform;
                tr.translation = FreePaintTool.SnapToGrid(widget.LocalTransform.translation);
                widget.LocalTransform = tr;
            }
            foreach (var stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                var pos = stroke.m_BatchSubset.m_Bounds.center;
                var newPos = FreePaintTool.SnapToGrid(pos);
                stroke.Recreate(TrTransform.T(newPos));
            }
        }

        public void HandleSnapSelectedRotationAngles()
        {
            foreach (var widget in SelectionManager.m_Instance.GetValidSelectedWidgets())
            {
                var tr = widget.LocalTransform;
                tr.rotation = SelectionManager.m_Instance.QuantizeAngle(tr.rotation);
                // SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                //     new MoveWidgetCommand();
                // );
                widget.LocalTransform = tr;
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
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new AlignSelectedCommand(axis, m_AlignBoundsType)
            );
        }

        private void Distribute(int axis)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new DistributeSelectedCommand(axis, m_DistributeBoundsType)
            );
        }
    }
}
