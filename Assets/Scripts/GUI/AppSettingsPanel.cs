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

using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{

    public class AppSettingsPanel : BasePanel
    {
        public ToggleButton m_ToggleExperimentalModeToggle;
        public ToggleButton m_ToggleWandOnRight;

        public override void InitPanel()
        {
            base.InitPanel();
            m_ToggleExperimentalModeToggle.m_IsToggledOn = App.Config.GetIsExperimental();
            m_ToggleWandOnRight.m_IsToggledOn = InputManager.m_Instance.WandOnRight;
        }

        public void HandleToggleHandedness(ToggleButton btn)
        {
            InputManager.m_Instance.WandOnRight = btn.m_IsToggledOn;
        }

        public void HandleResetFirstUse()
        {
            App.Config.SetIsExperimental(false);
            PlayerPrefs.SetInt(PanelManager.kPlayerPrefAdvancedMode, 0);
            PlayerPrefs.DeleteKey(App.kPlayerPrefHasPlayedBefore);
            RestartNotification();
        }

        public void HandleToggleExperimentalMode(ToggleButton btn)
        {
            App.Config.SetIsExperimental(btn.m_IsToggledOn);
            RestartNotification();
        }

        private void RestartNotification()
        {
            OutputWindowScript.m_Instance.CreateInfoCardAtController(
                InputManager.ControllerName.Brush,
                $"Please restart Open Brush",
                fPopScalar: 0.5f, false);
        }
    }
} // namespace TiltBrush
