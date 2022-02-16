using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush.Layers
{
    public class ToggleButton : BaseButton
    {
        [Header("State")]
        [SerializeField] protected bool activated;

        [Header("Visual")]
        [SerializeField] private Texture2D activatedButtonTexture;

        protected override void OnButtonPressed()
        {
            ToggleButtonActivation();
        }

        public void ToggleButtonActivation()
        {
            activated = !activated;
            SetButtonActivation(activated);
        }

        public void SetButtonActivation(bool active)
        {
            activated = active;
            SetButtonTexture(activated ? activatedButtonTexture : m_ButtonTexture);
        }
    }
}
