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

using System;
using System.Collections;
using UnityEngine;

namespace TiltBrush
{
    public class RepaintTray : BaseTray
    {

        enum ButtonType
        {
            Recolor,
            Rebrush,
            Resize,
            Jitter,
        }

        private void SetRepaintFlags(ButtonType buttonType, bool state)
        {
            var pm = PointerManager.m_Instance;
            switch (buttonType)
            {
                case ButtonType.Recolor:
                    pm.RecolorOn = state;
                    break;
                case ButtonType.Rebrush:
                    pm.RebrushOn = state;
                    break;
                case ButtonType.Resize:
                    pm.ResizeOn = state;
                    break;
                case ButtonType.Jitter:
                    pm.JitterOn = state;
                    break;
            }
        }

        public void RecolorToggled(ToggleButton button)
        {
            SetRepaintFlags(ButtonType.Recolor, button.m_IsToggledOn);
        }

        public void RebrushToggled(ToggleButton button)
        {
            SetRepaintFlags(ButtonType.Rebrush, button.m_IsToggledOn);
        }

        public void ResizeToggled(ToggleButton button)
        {
            SetRepaintFlags(ButtonType.Resize, button.m_IsToggledOn);
        }

        public void JitterToggled(ToggleButton button)
        {
            SetRepaintFlags(ButtonType.Jitter, button.m_IsToggledOn);
        }
    }

} // namespace TiltBrush
