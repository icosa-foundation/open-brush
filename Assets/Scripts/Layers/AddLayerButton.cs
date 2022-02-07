using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Events;

namespace TiltBrush.Layers
{
    public class AddLayerButton : BaseButton
    {
        public delegate void OnAddLayer();
        public static event OnAddLayer onAddLayer;

        protected override void OnButtonPressed() => onAddLayer?.Invoke();
    } 
}
