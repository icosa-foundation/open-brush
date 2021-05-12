namespace TiltBrush
{
    class SnapAngleButton : MultistateButton
    {
        
        protected override float OptionAngleDeltaDegrees
        {
            get
            {
                return 60; // Looks weird if the angle is too shallow so fake it
            }
        }
        
        protected virtual void OnStart()
        {
            CreateOptionSides();
            ForceSelectedOption(SelectionManager.m_Instance.CurrentSnapIndex);
        }
        
        override protected void OnButtonPressed()
        {
            SetSelectedOption((m_CurrentOptionIdx + 1) % NumOptions);
            SelectionManager.m_Instance.IncrementSnappingAngle();
        }
        
    }
}
