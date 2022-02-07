using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Events;

namespace TiltBrush.Layers
{
    public class LayerButton : BaseButton
    {
        public UnityEvent onPress; 

        public void Update()
        {
            if (m_CurrentButtonState == ButtonState.Pressed)
            {
                onPress?.Invoke();
            }
        }
    }
}
