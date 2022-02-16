using UnityEngine;

namespace TiltBrush.Layers
{
    public class FocusLayerButton : ToggleButton
    {
        private void OnEnable()
        {
            LayerUI_Manager.onActiveSceneChanged += SyncButtonStateWithWidget;
        }
        
        protected override void OnDisable()
        {
            LayerUI_Manager.onActiveSceneChanged -= SyncButtonStateWithWidget;
        }

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            GetComponentInParent<LayerUI_Manager>().SetActiveLayer(transform.parent.gameObject);
        }  

        public void SyncButtonStateWithWidget(GameObject activeLayerWidget)
        {
            SetButtonActivation(activeLayerWidget == transform.parent.gameObject);
        }

    }
}
