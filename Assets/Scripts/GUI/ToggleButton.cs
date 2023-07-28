// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine.Events;

namespace TiltBrush
{

    // TODO Refactor ToggleButton and OptionButton so that ToggleButton
    // carries less baggage that it doesn't need from OptionButton.
    public class ToggleButton : OptionButton
    {
        public UnityEvent m_OnToggle;

        private bool m_IsToggledOn;
        public bool IsToggledOn
        {
            get => m_IsToggledOn;
            set
            {
                m_IsToggledOn = value;
                UpdateToggleStateVisuals();
            }
        }

        // I expected UpdateVisuals to handle this but it's hard to untangle everything it does
        // so it's simpler to duplicate the important bits in a method specific to ToggleButton
        private void UpdateToggleStateVisuals()
        {
            // m_ToggleActive = m_IsToggledOn;
            if (m_IsToggledOn)
            {
                SetButtonActivated(true);

                if (m_ToggleOnDescription != "")
                {
                    SetDescriptionText(m_ToggleOnDescription);
                }
                if (m_ToggleOnTexture != null)
                {
                    SetButtonTexture(m_ToggleOnTexture);
                }
            }
            else
            {
                SetButtonActivated(false);

                if (m_ToggleOnDescription != "")
                {
                    SetDescriptionText(m_DefaultDescription);
                }
                if (m_ToggleOnTexture != null)
                {
                    SetButtonTexture(m_DefaultTexture);
                }
            }

        }

        public override bool IsButtonActive()
        {
            return IsToggledOn;
        }

        override protected void OnButtonPressed()
        {
            IsToggledOn = !IsToggledOn;
            m_OnToggle.Invoke();
        }
    }
} // namespace TiltBrush
