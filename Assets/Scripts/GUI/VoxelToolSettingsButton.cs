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
            Mode,
            Grid,
            NewModel,
            Close,
        }

        [SerializeField] private Setting m_Setting;

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();
            VoxelTool tool = GetTool();
            if (tool == null)
            {
                return;
            }

            string title;
            string description;
            switch (m_Setting)
            {
                case Setting.Mode:
                    title = $"Edit Mode: {tool.Mode}";
                    description = tool.Mode switch
                    {
                        VoxelTool.EditMode.Add => "Add or recolor voxels. Press to switch to Erase.",
                        VoxelTool.EditMode.Erase => "Remove voxels. Press to switch to Paint.",
                        _ => "Recolor existing voxels. Press to switch to Add.",
                    };
                    break;
                case Setting.Grid:
                    title = "Voxel Grid";
                    description = tool.ShowGrid
                        ? "Press to hide the local grid. The volume and cell markers remain visible."
                        : "Press to show the local grid. The volume and cell markers remain visible.";
                    break;
                case Setting.NewModel:
                    title = "New Voxel Model";
                    description = "Close settings, then press the trigger to draw a new model aligned to the snap grid. Its voxel size uses the last chosen snap-grid spacing, even when snapping is off.";
                    break;
                default:
                    title = "Close Settings";
                    description = "Return to voxel editing. The grid grows as you draw and tightens on release, with a 3-cell minimum per axis.";
                    break;
            }
            bool active = m_Setting switch
            {
                Setting.Grid => tool.ShowGrid,
                Setting.NewModel => tool.CreatingNewModel,
                _ => false,
            };
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
                case Setting.Mode:
                    tool.CycleMode();
                    break;
                case Setting.Grid:
                    tool.ToggleGrid();
                    break;
                case Setting.NewModel:
                    tool.RequestNewModel();
                    GetComponentInParent<VoxelToolSettingsPopUpWindow>()?.Close();
                    break;
                default:
                    GetComponentInParent<VoxelToolSettingsPopUpWindow>()?.Close();
                    break;
            }
            UpdateVisuals();
        }

        private static VoxelTool GetTool()
            => SketchSurfacePanel.m_Instance?.GetToolOfType(BaseTool.ToolType.VoxelTool) as VoxelTool;
    }
}
