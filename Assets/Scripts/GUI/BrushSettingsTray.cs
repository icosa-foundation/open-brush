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

using System.Collections.Generic;
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

        private void ListenForConnectedDevices()
        {
            bool needsBrushSizeUI = false;
            InputDevice tryGetUnityXRController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (tryGetUnityXRController.isValid && tryGetUnityXRController.name.Contains("Logitech MX Ink"))
            {
                needsBrushSizeUI = true;
            }
            else
            {
                tryGetUnityXRController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (tryGetUnityXRController.isValid && tryGetUnityXRController.name.Contains("Logitech MX Ink"))
                {
                    needsBrushSizeUI = true;
                }
            }

            if (needsBrushSizeUI)
            {
                DoAnimateIn();
                UpdateSliderToMatchCurrentSize();
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
            ListenForConnectedDevices();
        }

        protected override void OnToolChanged()
        {
            UpdateSliderToMatchCurrentSize();
            ListenForConnectedDevices();
        }

        public void OnSliderChanged(Vector3 value)
        {
            PointerManager.m_Instance.SetAllPointersBrushSize01(value.z);
            PointerManager.m_Instance.MarkAllBrushSizeUsed();
        }
    }

} // namespace TiltBrush
