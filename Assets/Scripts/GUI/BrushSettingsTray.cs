// Copyright 2024 The Open Brush Authors
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

using System;
using UnityEngine;
using UnityEngine.XR;

namespace TiltBrush
{
    public class BrushSettingsTray : BaseTray
    {
        [SerializeField] private AdvancedSlider m_BrushSizeSlider;

        protected override void Awake()
        {
            base.Awake();
            App.Switchboard.BrushSizeChanged += UpdateSliderToMatchCurrentSize;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            App.Switchboard.ToolChanged -= UpdateSliderToMatchCurrentSize;
        }

        private void DetectSupportedDevices()
        {
            // Currently only the Logitech stylus needs this panel
            bool needsBrushSizeUI = VrStylusHandler.m_Instance.CurrentState.isActive;

            // DoAnimateIn performs a toggle so we have to also check the state
            if (needsBrushSizeUI)
            {
                if (!m_AnimateIn)
                {
                    DoAnimateIn();
                    UpdateSliderToMatchCurrentSize();
                }
            }
            else
            {
                if (m_AnimateIn)
                {
                    DoAnimateIn();
                }
            }
        }

        private void UpdateSliderToMatchCurrentSize()
        {
            m_BrushSizeSlider.SetInitialValueAndUpdate(
                PointerManager.m_Instance.MainPointer.BrushSize01
            );
        }

        protected override void Start()
        {
            base.Start();

            // Call DetectSupportedDevice every second
            InvokeRepeating(nameof(DetectSupportedDevices), 0.0f, 1);
        }

        protected override void OnToolChanged()
        {
            UpdateSliderToMatchCurrentSize();
        }

        public void OnSliderChanged(Vector3 value)
        {
            PointerManager.m_Instance.SetAllPointersBrushSize01(value.z);
            PointerManager.m_Instance.MarkAllBrushSizeUsed();
        }
    }

} // namespace TiltBrush
