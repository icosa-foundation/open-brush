using UnityEngine;

namespace TiltBrush.Layers
{
    public class SquashLayerButton : BaseButton
    {
        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            GetComponentInParent<LayerUI_Manager>().SquashLayer(transform.parent.gameObject);
        }
    }
}
