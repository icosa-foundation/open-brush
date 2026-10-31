// Copyright 2026 The Open Brush Authors
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
    public sealed class VoxelToolSettingsButton : BaseButton
    {
        public enum Setting
        {
            AlignToGrid,
            NewModel,
        }

        [SerializeField] private Setting m_Setting;
        private bool m_Available = true;

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();
            VoxelTool tool = GetTool();
            if (tool == null)
            {
                return;
            }

            bool available = m_Setting != Setting.AlignToGrid || tool.HasCurrentTarget;
            if (available != m_Available)
            {
                m_Available = available;
                SetButtonAvailable(available);
            }

            bool active = m_Setting == Setting.NewModel && tool.CreatingNewModel;
            if (m_ToggleActive != active)
            {
                m_ToggleActive = active;
                SetButtonActivated(active);
            }
        }

        protected override void OnButtonPressed()
        {
            VoxelTool tool = GetTool();
            if (tool == null)
            {
                return;
            }

            switch (m_Setting)
            {
                case Setting.AlignToGrid:
                    if (!tool.AlignCurrentModelToGrid()) AudioManager.m_Instance.DisabledItemSelect(transform.position);
                    break;
                case Setting.NewModel:
                    tool.RequestNewModel();
                    break;
            }
            UpdateVisuals();
        }

        private static VoxelTool GetTool()
            => SketchSurfacePanel.m_Instance?.GetToolOfType(BaseTool.ToolType.VoxelTool) as VoxelTool;
    }
}
