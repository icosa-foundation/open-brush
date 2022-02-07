using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class ClearLayerButton : BaseButton
    {
        public delegate void OnClearLayer(GameObject layer);
        public static event OnClearLayer onClearLayer;

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();

            onClearLayer?.Invoke(transform.parent.gameObject);
        }
    }
}
