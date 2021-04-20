// Copyright 2020 The Tilt Brush Authors
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

    public class ToolAndPanelButton : BaseButton
    {
        [SerializeField] private BaseTool.ToolType m_Tool;
        [SerializeField] private bool m_EatGazeInputOnPress = false;
        public BasePanel.PanelType m_Type;

        override protected void Awake()
        {
            base.Awake();
            // Panel Buttons need to be toggles if they're not alwaysspawn.
            App.Switchboard.PanelDismissed += UpdateVisuals;
            App.Switchboard.ToolChanged += UpdateVisuals;
        }

        override protected void OnDestroy()
        {
            base.OnDestroy();
            App.Switchboard.PanelDismissed -= UpdateVisuals;
            App.Switchboard.ToolChanged -= UpdateVisuals;
        }

        override public void UpdateVisuals()
        {
            base.UpdateVisuals();
            // Poll for status.
            if (IsAvailable() && m_ToggleButton)
            {
                bool bWasToggleActive = m_ToggleActive;
                m_ToggleActive = SketchSurfacePanel.m_Instance.GetCurrentToolType() == m_Tool;
                m_ToggleActive &= PanelManager.m_Instance.IsPanelOpen(m_Type);

                if (bWasToggleActive != m_ToggleActive)
                {
                    SetButtonActivated(m_ToggleActive);
                }
            }
        }

        override protected void OnButtonPressed()
        {
            if (m_ToggleActive)
            {
                //PanelManager.m_Instance.DismissNonCorePanel(m_Type);
                SketchSurfacePanel.m_Instance.DisableSpecificTool(m_Tool);
            }
            else
            {
                if (m_EatGazeInputOnPress)
                {
                    SketchControlsScript.m_Instance.EatGazeObjectInput();
                }

                if (!PanelManager.m_Instance.IsPanelOpen(m_Type))
                {
                    SketchControlsScript.m_Instance.OpenPanelOfType(m_Type, TrTransform.FromTransform(transform));
                }
                SketchSurfacePanel.m_Instance.RequestHideActiveTool(true);
                SketchSurfacePanel.m_Instance.EnableSpecificTool(m_Tool);
            }
        }
    }
} // namespace TiltBrush
