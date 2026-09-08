// Copyright 2025 The Open Brush Authors
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
    public class OpenBrowserButton : BaseButton
    {
        public string m_Url;
        public string m_ButtonLabel;

        protected override void Awake()
        {
            base.Awake();
            SetTextLabel();
        }

        private void SetTextLabel()
        {
            GetComponentInChildren<TextMeshPro>().text = m_ButtonLabel;
        }

        protected override void OnButtonPressed()
        {
            // Non-mobile hardware should get an info card reminding them they need to remove their headset.
            if (!App.Config.IsMobileHardware)
            {
                OutputWindowScript.m_Instance.CreateInfoCardAtController(
                    InputManager.ControllerName.Brush,
                    SketchControlsScript.kRemoveHeadsetFyi,
                    fPopScalar: 0.5f
                );
            }
            App.OpenURL(m_Url);
        }
    }
} // namespace TiltBrush

