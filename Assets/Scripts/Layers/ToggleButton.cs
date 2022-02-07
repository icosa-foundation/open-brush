using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Layers
{
    public class ToggleButton : BaseButton
    {
        public bool debug;

        [Header("State")]
        [SerializeField] protected bool activated;

        [Header("Visual")]
        [SerializeField] private Texture2D activatedButtonTexture;

        protected override void OnButtonPressed()
        {
            ToggleActivation();

            if (activated) ToggleButtonTexture(true);
            else ToggleButtonTexture(false);
        }

        protected void ToggleActivation()
        {
            if (activated) activated = false;
            else activated = true;
        }

        protected void ToggleButtonTexture(bool active)
        {
            switch (activated)
            {
                case true:
                    if (activatedButtonTexture) SetButtonTexture(activatedButtonTexture);
                    break;
                case false:
                    if (activatedButtonTexture) SetButtonTexture(m_ButtonTexture);
                    break;
            }
        }
    }
}
