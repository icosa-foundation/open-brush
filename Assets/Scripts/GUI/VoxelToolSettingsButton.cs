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

using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public sealed class VoxelToolSettingsButton : BaseButton
    {
        public enum Setting
        {
            Mode,
            VoxelSize,
            ModelSize,
            Grid,
            NewModel,
            Close,
        }

        [SerializeField] private Setting m_Setting;
        private TextMeshPro m_Label;

        protected override void Awake()
        {
            base.Awake();
            GameObject labelObject = new GameObject("Label");
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0, 0, -0.004f);
            labelObject.transform.localRotation = Quaternion.identity;
            labelObject.transform.localScale = Vector3.one * 0.1f;
            m_Label = labelObject.AddComponent<TextMeshPro>();
            m_Label.alignment = TextAlignmentOptions.Center;
            m_Label.enableAutoSizing = true;
            m_Label.fontSizeMin = 1;
            m_Label.fontSizeMax = 4f;
            m_Label.color = Color.black;
            m_Label.rectTransform.sizeDelta = new Vector2(9f, 8f);
            UpdateVisuals();
        }

        public override void UpdateVisuals()
        {
            base.UpdateVisuals();
            VoxelTool tool = GetTool();
            if (tool == null || m_Label == null)
            {
                return;
            }

            string label;
            string description;
            switch (m_Setting)
            {
                case Setting.Mode:
                    label = $"Mode\n{tool.Mode}";
                    description = $"Edit mode: {tool.Mode}";
                    break;
                case Setting.VoxelSize:
                    label = $"Voxel\n{tool.VoxelSize:0.###} m";
                    description = $"Voxel size for new models: {tool.VoxelSize:0.###} metres";
                    break;
                case Setting.ModelSize:
                    label = $"Volume\n{tool.ModelSize}³";
                    description = $"New model dimensions: {tool.ModelSize} x {tool.ModelSize} x {tool.ModelSize}";
                    break;
                case Setting.Grid:
                    label = $"Grid\n{(tool.ShowGrid ? "On" : "Off")}";
                    description = $"Local voxel grid: {(tool.ShowGrid ? "shown" : "hidden")}";
                    break;
                case Setting.NewModel:
                    label = "New\nModel";
                    description = "Start a new voxel model on the next Add action";
                    break;
                default:
                    label = "Close";
                    description = "Close voxel tool settings";
                    break;
            }
            m_Label.text = label;
            SetExtraDescriptionText(description);
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
                case Setting.VoxelSize:
                    tool.CycleVoxelSize();
                    break;
                case Setting.ModelSize:
                    tool.CycleModelSize();
                    break;
                case Setting.Grid:
                    tool.ToggleGrid();
                    break;
                case Setting.NewModel:
                    tool.RequestNewModel();
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
