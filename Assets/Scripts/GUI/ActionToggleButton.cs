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
    public class ActionToggleButton : ActionButton
    {
        public bool m_InitialToggleState = false;
        public Texture2D m_TextureOn;
        public Texture2D m_TextureOff;

        public bool ToggleState
        {
            get
            {
                return m_ToggleActive;
            }
            set
            {
                m_ToggleActive = value;
                SetButtonTexture(m_ToggleActive ? m_TextureOn : m_TextureOff);
            }
        }

        override protected void Awake()
        {
            base.Awake();
            m_ToggleActive = m_InitialToggleState;
            ToggleState = m_ToggleActive;
        }

        protected override void OnButtonPressed()
        {
            ToggleState = !ToggleState;
            base.OnButtonPressed();
        }
    }
} // namespace TiltBrush
