// Copyright 2021 The Tilt Brush Authors
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
namespace TiltBrush
{
    class SnapSettingsButton : MultistateButton
    {

        [Serializable]
        public enum SettingTypes
        {
            Angle,
            GridSize
        }

        public SettingTypes SettingType;

        protected override void OnStart()
        {
            CreateOptionSides();
            int optionIndex = -1;
            switch (SettingType)
            {
                case SettingTypes.Angle:
                    optionIndex = SelectionManager.m_Instance.CurrentSnapAngleIndex;
                    break;
                case SettingTypes.GridSize:
                    optionIndex = SelectionManager.m_Instance.CurrentSnapGridIndex;
                    break;
            }
            ForceSelectedOption(optionIndex);
        }

        protected override void OnButtonPressed()
        {
            SetSelectedOption((m_CurrentOptionIdx + 1) % NumOptions);
            switch (SettingType)
            {
                case SettingTypes.Angle:
                    SelectionManager.m_Instance.SetSnappingAngle(m_CurrentOptionIdx);
                    break;
                case SettingTypes.GridSize:
                    SelectionManager.m_Instance.SetSnappingGridSize(m_CurrentOptionIdx);
                    break;
            }
        }

    }
}
