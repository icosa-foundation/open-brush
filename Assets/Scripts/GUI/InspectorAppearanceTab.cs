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

using System.Linq;
using TiltBrush;
using TMPro;

public class InspectorAppearanceTab : InspectorBaseTab
{
    private InspectorPanel m_InspectorPanel;
    public TextMeshPro m_SummaryText;

    void Start()
    {
        m_InspectorPanel = GetComponentInParent<InspectorPanel>();
    }

    public override void OnSelectionChanged()
    {
        switch (m_InspectorPanel.CurrentSelectionType)
        {
            case SelectionType.Nothing:
                m_SummaryText.text = "Nothing selected";
                break;

            case SelectionType.Stroke:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = $"Brush: {SelectionManager.m_Instance.SelectedStrokes.First().m_BrushGuid}";
                }
                else
                {
                    m_SummaryText.text = "Multiple brush strokes selected";
                }
                break;

            case SelectionType.Image:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = $"Image: {m_InspectorPanel.SelectedImages[0].FileName}";
                }
                else
                {
                    m_SummaryText.text = "Multiple images selected";
                }
                break;

            case SelectionType.Video:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    m_SummaryText.text = $"Video: {m_InspectorPanel.SelectedVideos[0].Video.HumanName}";
                }
                else
                {
                    m_SummaryText.text = "Multiple videos selected";
                }
                break;

            case SelectionType.Model:
                if (m_InspectorPanel.CurrentSelectionCount == 1)
                {
                    var model = m_InspectorPanel.SelectedModels[0].Model;
                     m_SummaryText.text = $"Model path: {model.RelativePath}";
                }
                else
                {
                    m_SummaryText.text = "Multiple 3d models selected";
                }
                break;

            case SelectionType.Guide:
                m_SummaryText.text = "No appearance settings for guides";
                break;
            case SelectionType.Mixed:
                m_SummaryText.text = "Multiple selection types";
                break;
        }
    }
}
