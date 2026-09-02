// Copyright 2022 The Tilt Brush Authors
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
    // TODO Why do we need ToggleButton and ActionToggleButton?
    public class ActionToggleButton : ActionButton
    {
        public bool m_InitialToggleState = false;
        public Texture2D m_TextureOn;
        public Texture2D m_TextureOff;
        private bool m_HasPendingToggleState;

        public bool ToggleState
        {
            get
            {
                return m_ToggleActive;
            }
            set
            {
                m_ToggleActive = value;
                if (m_ButtonRenderer == null)
                {
                    m_HasPendingToggleState = true;
                    return;
                }
                SetButtonTexture(m_ToggleActive ? m_TextureOn : m_TextureOff);
            }
        }

        override protected void Awake()
        {
            bool initialState = m_HasPendingToggleState
                ? m_ToggleActive
                : m_InitialToggleState;
            base.Awake();
            m_HasPendingToggleState = false;
            ToggleState = initialState;
        }

        protected override void OnButtonPressed()
        {
            ToggleState = !ToggleState;
            base.OnButtonPressed();
        }
    }
} // namespace TiltBrush
