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
    public class StencilAttractDistanceSlider : BaseSlider
    {
        [SerializeField] private float m_MinAttractDistance = 0.25f;
        [SerializeField] private float m_MaxAttractDistance = 2f;

        void OnEnable()
        {
            App.Switchboard.StencilAttractDistChanged += OnStencilAttractDistChanged;
            OnStencilAttractDistChanged();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            App.Switchboard.StencilAttractDistChanged -= OnStencilAttractDistChanged;
        }

        public override void UpdateValue(float value)
        {
            base.UpdateValue(value);
            SetSliderPositionToReflectValue();

            SetDescriptionText(m_DescriptionText, $"{value * 100:0}%");
        }

        // If some other logic (not the slider) changes the value, we
        // will be notified here so that we can update the slider visuals
        private void OnStencilAttractDistChanged()
        {
            if (WidgetManager.m_Instance != null)
            {
                float value = WidgetManager.m_Instance.StencilAttractDist;
                float range = m_MaxAttractDistance - m_MinAttractDistance;
                float newSliderValue = (value - m_MinAttractDistance) / range;
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
            float displacement = m_MaxAttractDistance - m_MinAttractDistance;
            float newDistance = m_MinAttractDistance + percent * displacement;

            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyStencilAttractDistanceCommand(newDistance, true));
        }
    }
} // namespace TiltBrush
