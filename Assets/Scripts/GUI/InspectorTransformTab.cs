// Copyright 2024 The Tilt Brush Authors
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
using TiltBrush;
using UnityEngine;

public class InspectorTransformTab : InspectorBaseTab
{
    public Bounds SelectionBounds { get; set; }

    public EditableLabel m_LabelForTranslationX;
    public EditableLabel m_LabelForTranslationY;
    public EditableLabel m_LabelForTranslationZ;

    public EditableLabel m_LabelForRotationX;
    public EditableLabel m_LabelForRotationY;
    public EditableLabel m_LabelForRotationZ;

    public EditableLabel m_LabelForScale;

    public override void OnSelectionPoseChanged()
    {
        if (ParentPanel.CurrentSelectionType == SelectionType.Nothing)
        {
            return;
        }
        SelectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox();
        TrTransform tr = ParentPanel.CurrentSelectionTr;
        var translation = tr.MultiplyPoint(SelectionBounds.center);
        var rotation = tr.rotation.eulerAngles;
        var scale = tr.scale;
        m_LabelForTranslationX.SetValue(FormatValue(translation.x));
        m_LabelForTranslationY.SetValue(FormatValue(translation.y));
        m_LabelForTranslationZ.SetValue(FormatValue(translation.z));
        m_LabelForRotationX.SetValue(FormatValue(rotation.x));
        m_LabelForRotationY.SetValue(FormatValue(rotation.y));
        m_LabelForRotationZ.SetValue(FormatValue(rotation.z));
        m_LabelForScale.SetValue(FormatValue(scale));
    }

    public override void OnSelectionChanged()
    {
        OnSelectionPoseChanged();
    }

    private string FormatValue(float val)
    {
        // 2 digits after the decimal, 5 digits maximum
        return (Mathf.Round(val * 100) / 100f).ToString("G5");
    }

    public void HandleLabelEdited(EditableLabel label)
    {
        if (ParentPanel.CurrentSelectionType == SelectionType.Nothing) return;
        if (!float.TryParse(label.LastTextInput, out float value) ||
            float.IsNaN(value) || float.IsInfinity(value) ||
            (label.m_LabelTag == "SX" && value == 0f))
        {
            label.SetError(true);
            return;
        }

        // The fields describe the absolute pose at the selection center. The command
        // applies a relative transform about that center, so convert between the two.
        TrTransform currentCenter = ParentPanel.CurrentSelectionTr * TrTransform.T(SelectionBounds.center);
        TrTransform editedCenter = currentCenter;
        switch (label.m_LabelTag)
        {
            case "TX": editedCenter.translation.x = value; break;
            case "TY": editedCenter.translation.y = value; break;
            case "TZ": editedCenter.translation.z = value; break;
            case "RX":
            case "RY":
            case "RZ":
                Vector3 rotation = editedCenter.rotation.eulerAngles;
                if (label.m_LabelTag == "RX") rotation.x = value;
                else if (label.m_LabelTag == "RY") rotation.y = value;
                else rotation.z = value;
                editedCenter.rotation = Quaternion.Euler(rotation);
                break;
            case "SX": editedCenter.scale = value; break;
            default:
                label.SetError(true);
                return;
        }

        label.SetError(false);
        TrTransform delta = currentCenter.inverse * editedCenter;
        SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            new TransformSelectionCommand(delta, SelectionBounds.center));
    }
}
