using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class DeleteLayerButton : OptionButton
    {
        public delegate void OnDeleteLayer(GameObject layer);
        public static event OnDeleteLayer onDeleteLayer;

        protected override void OnButtonPressed() 
        { 
            base.OnButtonPressed();

            onDeleteLayer?.Invoke(transform.parent.gameObject); 
        }
    }
}
