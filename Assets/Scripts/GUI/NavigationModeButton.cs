// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

namespace TiltBrush
{
    // A popup choice selects a mode; selecting the current mode simply closes the popup.
    public class NavigationModeButton : ToolButton
    {
        protected override void OnButtonPressed()
        {
            UpdateVisuals();
            if (!m_ToggleActive)
            {
                base.OnButtonPressed();
            }

            SketchSurfacePanel.m_Instance.ActiveTool.EatInput();
            GetComponentInParent<PopUpWindow>().RequestClose();
        }
    }
}
