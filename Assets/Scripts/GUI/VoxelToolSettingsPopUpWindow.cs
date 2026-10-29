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
    public sealed class VoxelToolSettingsPopUpWindow : OptionsPopUpWindow
    {
        private TextMeshPro m_TargetStatus;

        public override void Init(GameObject parent, string text)
        {
            base.Init(parent, text);
            var labelObject = new GameObject("ActiveVoxelModel");
            labelObject.layer = gameObject.layer;
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(0, 0.49f, -0.03f);
            labelObject.transform.localScale = Vector3.one * 0.1f;
            m_TargetStatus = labelObject.AddComponent<TextMeshPro>();
            m_TargetStatus.alignment = TextAlignmentOptions.Center;
            m_TargetStatus.enableAutoSizing = true;
            m_TargetStatus.fontSizeMin = 1;
            m_TargetStatus.fontSizeMax = 2;
            m_TargetStatus.richText = false;
            m_TargetStatus.rectTransform.sizeDelta = new Vector2(12, 1.3f);
            UpdateTargetStatus();
        }

        protected override void BaseUpdate()
        {
            base.BaseUpdate();
            UpdateTargetStatus();
        }

        private void UpdateTargetStatus()
        {
            var tool = SketchSurfacePanel.m_Instance?.GetToolOfType(BaseTool.ToolType.VoxelTool)
                as VoxelTool;
            if (m_TargetStatus != null && tool != null)
            {
                m_TargetStatus.text = tool.CurrentTargetDescription;
            }
        }

        public void Close()
        {
            RequestClose(true);
        }
    }
}
