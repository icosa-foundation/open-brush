﻿// Copyright 2020 The Tilt Brush Authors
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

namespace TiltBrush
{

    public class AudioVizButton : OptionButton
    {
        override protected void OnButtonPressed()
        {
            if (m_Manager)
            {
                BasePanel panel = m_Manager.GetPanelForPopUps();
                // If we haven't requested visuals, show the popup.
                if (!App.Instance.RequestingAudioReactiveMode)
                {
                    panel.CreatePopUp(m_Command, m_CommandParam, -1, m_PopupText);
                    ResetState();
                }
                App.Instance.ToggleAudioReactiveBrushesRequest();
            }
        }
    }
} // namespace TiltBrush
