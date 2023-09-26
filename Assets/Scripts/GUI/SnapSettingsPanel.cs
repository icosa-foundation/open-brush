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


using System.Collections.Generic;
using System.Linq;
namespace TiltBrush
{

    public enum SnapSettingsPanelToggleType
    {
        LockSnapTranslateX,
        LockSnapTranslateY,
        LockSnapTranslateZ,
        SnapToGuides
    }

    public class SnapSettingsPanel : BasePanel
    {

        public void HandleToggle(SnapSettingsPanelToggleButton btn)
        {
            switch (btn.m_ButtonType)
            {
                case SnapSettingsPanelToggleType.LockSnapTranslateX:
                    SelectionManager.m_Instance.m_EnableSnapTranslationX = btn.ToggleState;
                    break;
                case SnapSettingsPanelToggleType.LockSnapTranslateY:
                    SelectionManager.m_Instance.m_EnableSnapTranslationY = btn.ToggleState;
                    break;
                case SnapSettingsPanelToggleType.LockSnapTranslateZ:
                    SelectionManager.m_Instance.m_EnableSnapTranslationZ = btn.ToggleState;
                    break;
            }
        }

        public void HandleSnapSelectionToGrid()
        {
            TransformItems.SnapSelectionToGrid();
        }

        public void HandleToggleSnapToGuides()
        {
            WidgetManager.m_Instance.m_EnableSnapToGuides = !WidgetManager.m_Instance.m_EnableSnapToGuides;
        }

        public void HandleSnapSelectedRotationAngles()
        {
            TransformItems.SnapSelectedRotationAngles();
        }

    }

} // namespace TiltBrush
