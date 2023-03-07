using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Animation
{
    public class TogglePanelButton : TiltBrush.Layers.ToggleButton
    {
        protected override void OnButtonPressed()
        {

            base.OnButtonPressed();
            var uiManager = GetComponentInParent<AnimationUI_Manager>();
            uiManager.togglePanel();
            
        }
    }
} // namespace TiltBrush
