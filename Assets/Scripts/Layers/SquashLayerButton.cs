using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace TiltBrush.Layers
{
    public class SquashLayerButton : BaseButton
    {
        public delegate void OnSquashLayer(GameObject layer);
        public static event OnSquashLayer onSquashLayer;

        override public void ButtonPressed(RaycastHit rHitInfo)
        {
            // Button is only activated on release because m_LongPressReleaseButton
            // But we still want some feedback when pressed
            AdjustButtonPositionAndScale(m_ZAdjustClick, m_HoverScale, m_HoverBoxColliderGrow);
            base.ButtonPressed(rHitInfo);
        }
        
        protected override void OnButtonPressed()
        {
            base.OnButtonPressed();
            onSquashLayer?.Invoke(transform.parent.gameObject);
        }
    }
}
