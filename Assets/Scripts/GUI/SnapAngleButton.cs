using UnityEngine;
namespace TiltBrush
{
    class SnapAngleButton : MultistateButton
    {
        
        protected override float OptionAngleDeltaDegrees
        {
            get
            {
                return 120; // Button looks weird if the angle is too shallow so fake it
            }
        }

        protected override void OnStart()
        {
            CreateOptionSides();
            ForceSelectedOption(SelectionManager.m_Instance.CurrentSnapIndex);
        }
        
        protected override void OnButtonPressed()
        {
            SetSelectedOption((m_CurrentOptionIdx + 1) % NumOptions);
            SelectionManager.m_Instance.SetSnappingAngle(m_CurrentOptionIdx);
        }
        
    }
}
