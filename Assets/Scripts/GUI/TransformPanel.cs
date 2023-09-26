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

        protected override void Awake()
        {
            base.Awake();
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

        private TrTransform GetPreferredTransform()
        {
            TrTransform activeTr = TrTransform.identity;

            // Prefer the selection if one exists
            if (SelectionManager.m_Instance.HasSelection)
            {
                m_LastWidget = null;
                activeTr = SelectionManager.m_Instance.SelectionTransform;
            }
            // otherwise use the last widget that was interacted with
            else if (m_LastWidget != null && m_LastWidget.Canvas != null)
            {
                m_LastWidget = SketchControlsScript.m_Instance.CurrentGrabWidget ?? m_LastWidget;
                activeTr = m_LastWidget.LocalTransform;
            }
            return activeTr;
        }

        private string FormatValue(float val)
        {
            // 2 digits after the decimal, 5 digits maximum
            return (Mathf.Round(val * 100) / 100f).ToString("G5");
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
            }
        }

        public void HandleLabelEdited(EditableLabel label)
        {

            var newTr = GetPreferredTransform();

            if (float.TryParse(label.LastTextInput, out float value))
            {
                label.SetError(false);
                switch (label.m_LabelTag)
                {
                    case "TX":
                        newTr.translation.x = value;
                        break;
                    case "TY":
                        newTr.translation.y = value;
                        break;
                    case "TZ":
                        newTr.translation.z = value;
                        break;
                    case "RX":
                        newTr.rotation.eulerAngles = new Vector3(value, 0, 0);
                        break;
                    case "RY":
                        newTr.rotation.eulerAngles = new Vector3(0, value, 0);
                        break;
                    case "RZ":
                        newTr.rotation.eulerAngles = new Vector3(0, 0, value);
                        break;
                    case "SX":
                        newTr.scale = value;
                        break;
                }

                if (SelectionManager.m_Instance.HasSelection)
                {
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                        new TransformSelectionCommand(newTr, m_SelectionBounds.center)
                    );
                }
                else
                {
                    var pivot = m_LastWidget.LocalTransform.translation;
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                        new TransformItemsCommand(null, new List<GrabWidget> { m_LastWidget }, newTr, pivot)
                    );
                }
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
