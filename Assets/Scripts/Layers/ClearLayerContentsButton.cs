using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class ClearLayerContentsButton : BaseButton
    {
        public delegate void OnClearLayer(GameObject layer);
        public static event OnClearLayer onClearLayerContents;

        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();

            onClearLayerContents?.Invoke(transform.parent.gameObject);
        }
    }
}
