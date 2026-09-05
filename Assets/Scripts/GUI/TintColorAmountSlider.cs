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

    public class TintColorAmountSlider : AdvancedSlider
    {
        public BaseTool.ToolType m_ShowOnToolType;

        protected override void Awake()
        {
            base.Awake();
            SetSliderPositionToReflectValue();
            App.Switchboard.ToolChanged += OnToolChanged;
            gameObject.SetActive(false);
        }

        protected void OnToolChanged()
        {
            bool isTintTool = SketchSurfacePanel.m_Instance.GetCurrentToolType() == m_ShowOnToolType;
            gameObject.SetActive(isTintTool);
        }

        public void HandleValueChanged(Vector3 val)
        {
            var tintTool = SketchSurfacePanel.m_Instance.GetToolOfType(BaseTool.ToolType.TintColorTool) as TintColorTool;
            if (tintTool != null) tintTool.EffectAmount = val.z;
        }
    }
} // namespace TiltBrush
