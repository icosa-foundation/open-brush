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

namespace TiltBrush
{
    /// Tool button that exposes the voxel tool's current state.
    public sealed class VoxelToolButton : ToolButton
    {
        private VoxelTool.EditMode? m_LastMode;
        private bool m_LastActive;
        private string m_LastTargetDescription;
        private bool m_LastSelectingRegion;

        protected override void OnButtonPressed()
        {
            var panel = SketchSurfacePanel.m_Instance;
            var tool = panel.GetToolOfType(BaseTool.ToolType.VoxelTool) as VoxelTool;
            if (tool != null && tool.SelectingRegion)
            {
                tool.UseEditingModes();
                if (panel.ActiveTool == tool)
                {
                    UpdateVisuals();
                    return;
                }
            }
            base.OnButtonPressed();
        }

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();

            VoxelTool activeTool = SketchSurfacePanel.m_Instance.ActiveTool as VoxelTool;
            bool active = activeTool != null;
            VoxelTool.EditMode mode = activeTool?.Mode ?? VoxelTool.EditMode.Add;
            string targetDescription = activeTool?.CurrentTargetDescription;
            bool selectingRegion = activeTool?.SelectingRegion == true;
            if (m_LastMode == mode && m_LastActive == active &&
                m_LastTargetDescription == targetDescription && m_LastSelectingRegion == selectingRegion)
            {
                return;
            }

            m_LastMode = mode;
            m_LastActive = active;
            m_LastTargetDescription = targetDescription;
            m_LastSelectingRegion = selectingRegion;
            SetExtraDescriptionText(active
                ? selectingRegion
                    ? $"Select Region. Target: {targetDescription}. Drag the trigger to select. Double press in empty space to clear."
                    : $"Mode: {mode}. Target: {targetDescription}. Context button cycles modes. Options are in the tray."
                : "Grab or select a voxel model. Editing an import embeds a copy in this sketch. New Model is in the tray.");
        }
    }
}
