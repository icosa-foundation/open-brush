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

            string title;
            string description;
            switch (m_Setting)
            {
                case Setting.AlignToGrid:
                    title = "Align Voxel Model to Grid";
                    description = "Align voxel axes and snap the model into place. Cell size matches the active snap grid, or the default grid size when snapping is off.";
                    break;
                case Setting.NewModel:
                    title = "New Voxel Model";
                    description = "Press the trigger to draw a new model. Its voxel size matches the snap grid when snapping is on, or the last voxel model you interacted with when snapping is off.";
                    break;
                default:
                    return;
            }
            bool active = m_Setting == Setting.NewModel && tool.CreatingNewModel;
            if (m_ToggleActive != active)
            {
                m_ToggleActive = active;
                SetButtonActivated(active);
            }
            if (m_DescriptionText != title || m_DescriptionTextExtra != description)
            {
                SetDescriptionText(title, description);
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
