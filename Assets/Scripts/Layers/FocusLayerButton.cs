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
            LayerUI_Manager.onActiveSceneChanged += ToggleIfActive;
        }
        protected override void OnDisable()
        {
            LayerUI_Manager.onActiveSceneChanged -= ToggleIfActive;
        }

        protected override void OnButtonPressed()
        {
            if (activated) return;
            if (!activated) onFocusedLayer?.Invoke(transform.parent.gameObject);
        }  

        public void ToggleIfActive(GameObject activeLayerWidget)
        {
            if (activeLayerWidget == transform.parent.gameObject)
            {
                activated = true;
            }
            else
            {
                activated = false;
            }
            SetVisualState();
        }
        
        public void SetAsActive(bool active)
        {
            activated = active;
            SetVisualState();
        }
    }
}
