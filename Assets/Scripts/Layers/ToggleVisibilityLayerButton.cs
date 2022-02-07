using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class ToggleVisibilityLayerButton : ToggleButton
    {
        public delegate void OnVisiblityToggle(GameObject layerUi);
        public static event OnVisiblityToggle onVisiblityToggle;

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();

            onVisiblityToggle?.Invoke(transform.parent.gameObject);
        }
    }
}
