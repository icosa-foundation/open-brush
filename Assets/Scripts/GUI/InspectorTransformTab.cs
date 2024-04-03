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

    private InspectorPanel m_InspectorPanel;

    void Start()
    {
        m_InspectorPanel = GetComponentInParent<InspectorPanel>();
    }

    public override void OnSelectionPoseChanged()
    {
        var tr = m_InspectorPanel.CurrentSelection;
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
        SelectionBounds = App.Scene.SelectionCanvas.GetCanvasBoundingBox();
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
        var newTr = TrTransform.identity;

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

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new TransformSelectionCommand(newTr, SelectionBounds.center)
            );
        }
        else
        {
            m_LabelForTranslationX.SetError(true);
        }
    }
}
