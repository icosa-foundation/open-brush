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

using UnityEngine;

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

        private void UpdateSliderToMatchCurrentSize()
        {
            m_BrushSizeSlider.SetInitialValueAndUpdate(
                PointerManager.m_Instance.MainPointer.BrushSize01
            );
        }

        protected override void Start()
        {
            base.Start();

            bool needsBrushSizeUi = false;

#if OCULUS_SUPPORTED
            const string suffix = "mx_ink_stylus_logitech";
            var leftDeviceName = OVRPlugin.GetCurrentInteractionProfileName(OVRPlugin.Hand.HandLeft);
            var rightDeviceName = OVRPlugin.GetCurrentInteractionProfileName(OVRPlugin.Hand.HandRight);
            needsBrushSizeUi = leftDeviceName.EndsWith(suffix) || rightDeviceName.EndsWith(suffix);
#endif

            if (needsBrushSizeUi)
            {
                DoAnimateIn();
            }

            UpdateSliderToMatchCurrentSize();
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
