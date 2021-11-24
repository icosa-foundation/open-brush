// Copyright 2021 The Open Brush Authors
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
using System;

namespace TiltBrush
{
    public class StencilGridSizeSlider : BaseSlider
    {
        [SerializeField] private float m_MinGridSize = 0.25f;
        [SerializeField] private float m_MaxGridSize = 3f;

        void OnEnable()
        {
            Shader.SetGlobalFloat(ModifyStencilGridSizeCommand.GlobalGridSizeMultiplierHash, 1f);
            OnStencilGridSizeChanged();
        }

        public override void UpdateValue(float value)
        {
            base.UpdateValue(value);
            SetSliderPositionToReflectValue();

            float gridSize = Mathf.Lerp(m_MinGridSize, m_MaxGridSize, value);
            Shader.SetGlobalFloat(ModifyStencilGridSizeCommand.GlobalGridSizeMultiplierHash, gridSize);

            SetDescriptionText(m_DescriptionText, $"{value * 100:0}%");
        }

        // If some other logic (not the slider) changes the value, we
        // will be notified here so that we can update the slider visuals
        private void OnStencilGridSizeChanged()
        {
            if (WidgetManager.m_Instance != null)
            {
                float value = Shader.GetGlobalFloat(ModifyStencilGridSizeCommand.GlobalGridSizeMultiplierHash);
                float range = m_MaxGridSize - m_MinGridSize;
                float newSliderValue = (value - m_MinGridSize) / range;
                UpdateValue(newSliderValue);
            }
        }

        public override void ButtonReleased()
        {
            base.ButtonReleased();
            EndModifyCommand();
        }

        public override void ResetState()
        {
            if (m_HadButtonPress)
            {
                EndModifyCommand();
            }
            base.ResetState();
        }

        void EndModifyCommand()
        {
            float percent = GetCurrentValue();
            float newGridSize = Mathf.Lerp(m_MinGridSize, m_MaxGridSize, percent);

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyStencilGridSizeCommand(newGridSize, true));
        }
    }
} // namespace TiltBrush
