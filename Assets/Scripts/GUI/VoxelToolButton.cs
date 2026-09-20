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
    /// Tool button that exposes the voxel tool's current mode and new-model action.
    public sealed class VoxelToolButton : ToolButton
    {
        private VoxelTool.EditMode? m_LastMode;
        private bool m_LastActive;
        private string m_LastTargetDescription;

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();

            VoxelTool activeTool = SketchSurfacePanel.m_Instance.ActiveTool as VoxelTool;
            bool active = activeTool != null;
            VoxelTool.EditMode mode = activeTool?.Mode ?? VoxelTool.EditMode.Add;
            string targetDescription = activeTool?.CurrentTargetDescription;
            if (m_LastMode == mode && m_LastActive == active &&
                m_LastTargetDescription == targetDescription)
            {
                return;
            }

            m_LastMode = mode;
            m_LastActive = active;
            m_LastTargetDescription = targetDescription;
            SetExtraDescriptionText(active
                ? $"Mode: {mode}. Target: {targetDescription}. Press again to start a new model"
                : "Activate in Add mode");
        }

        protected override void OnButtonPressed()
        {
            if (SketchSurfacePanel.m_Instance.ActiveTool is VoxelTool tool)
            {
                tool.RequestNewModel();
                UpdateVisuals();
                return;
            }

            base.OnButtonPressed();
        }
    }
}
