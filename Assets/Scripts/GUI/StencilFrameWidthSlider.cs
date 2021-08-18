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
using UnityEngine.Serialization;

namespace TiltBrush
{
    public class StencilFrameWidthSlider : BaseSlider
    {
        [SerializeField] private float m_MinGridWidth = 0.25f;
        [SerializeField] private float m_MaxGridWidth = 3f;

        void OnEnable()
        {
            Shader.SetGlobalFloat(ModifyStencilFrameWidthCommand.GlobalFrameWidthMultiplierHash, 1f);
            OnStencilFrameWidthChanged();
        }

        public override void UpdateValue(float value)
        {
            base.UpdateValue(value);
            SetSliderPositionToReflectValue();

            float gridWidth = Mathf.Lerp(m_MinGridWidth, m_MaxGridWidth, value);
            Shader.SetGlobalFloat(ModifyStencilFrameWidthCommand.GlobalFrameWidthMultiplierHash, gridWidth);

            SetDescriptionText(m_DescriptionText, $"{value * 100:0}%");
        }

        // If some other logic (not the slider) changes the value, we
        // will be notified here so that we can update the slider visuals
        private void OnStencilFrameWidthChanged()
        {
            if (WidgetManager.m_Instance != null)
            {
                float value = Shader.GetGlobalFloat(ModifyStencilFrameWidthCommand.GlobalFrameWidthMultiplierHash);
                float range = m_MaxGridWidth - m_MinGridWidth;
                float newSliderValue = (value - m_MinGridWidth) / range;
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
            float newGridWidth = Mathf.Lerp(m_MinGridWidth, m_MaxGridWidth, percent);

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyStencilFrameWidthCommand(newGridWidth, true));
        }
    }
} // namespace TiltBrush
