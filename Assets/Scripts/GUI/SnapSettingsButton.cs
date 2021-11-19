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
