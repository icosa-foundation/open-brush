using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class FocusLayerButton : ToggleButton
    {
        public delegate void OnFocusedLayer(GameObject layerUi);
        public static event OnFocusedLayer onFocusedLayer;

        private void OnEnable()
        {
            LayerUI_Manager.onActiveSceneChanged += ParentIsActiveLayerToggleActivation;
        }
        protected override void OnDisable()
        {
            LayerUI_Manager.onActiveSceneChanged -= ParentIsActiveLayerToggleActivation;
        }

        protected override void OnButtonPressed()
        {
            if (activated) return;

            if (!activated)
                onFocusedLayer?.Invoke(transform.parent.gameObject);
        }  

        private void ParentIsActiveLayerToggleActivation(GameObject activeLayer)
        {
            if (activeLayer == transform.parent.gameObject)
            {
                activated = true;
                ToggleButtonTexture(activated);
            }
            else
            {
                activated = false;
                ToggleButtonTexture(activated);
            }
        }
    }
}
