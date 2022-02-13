using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class DeleteLayerButton : OptionButton
    {
        public delegate void OnDeleteLayer(GameObject layer);
        public static event OnDeleteLayer onDeleteLayer;

        override public void ButtonPressed(RaycastHit rHitInfo)
        {
            // Button is only activated on release because m_LongPressReleaseButton
            // But we still want some feedback when pressed
            AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);
            base.ButtonPressed(rHitInfo);
        }

        override public void ButtonReleased()
        {
            AudioManager.m_Instance.ItemSelect(transform.position);
            base.ButtonReleased();
        }

        protected override void OnButtonPressed() 
        { 
            base.OnButtonPressed();
            onDeleteLayer?.Invoke(transform.parent.gameObject);
        }
        
    }
}
