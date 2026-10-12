using UnityEngine;

namespace TiltBrush
{
    public class WandPanelRotateButton : BaseButton
    {
        public enum Direction
        {
            Left = -1,
            Right = 1
        }

        [SerializeField]
        private Direction m_Direction = Direction.Right;


        protected override void Awake()
        {
            base.Awake();

            // Ensure RELEASE can never execute OnButtonPressed(),
            // regardless of values inherited from a duplicated prefab.
            m_LongPressReleaseButton = false;
            m_ToggleButton = false;

            SetDescriptionUnavailable(true);
        }


        // Use the normal BaseButton pointer/ray interaction path.
        //
        // AndroidXRHandBridge now maps the right-hand pinch to Open Brush's
        // normal Trigger, so laser hover + pinch behaves exactly like a
        // controller pointing at this button.
        //
        // Do not override UpdateStateWithInput(), CalculateReticleCollision()
        // or ButtonPressed() here: the previous hand-touch-only overrides
        // explicitly prevented laser targeting.


        // Called by the independent hand-aim rotation control.
        public void RotateFromHandAim()
        {
            OnButtonPressed();
        }

        protected override void OnButtonPressed()
        {
            if (PanelManager.m_Instance == null)
                return;

            PanelManager.m_Instance.RotateWandPanels(
                (int)m_Direction);
        }
    }
}
