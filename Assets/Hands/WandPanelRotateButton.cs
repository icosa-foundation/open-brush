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


        // Reject normal Open Brush pointer/ray interaction.
        public override bool UpdateStateWithInput(
            bool inputValid,
            Ray inputRay,
            GameObject parentActiveObject,
            Collider parentCollider)
        {
            return false;
        }


        public override bool CalculateReticleCollision(
            Ray ray,
            ref Vector3 pos,
            ref Vector3 forward)
        {
            return false;
        }


        public override void ButtonPressed(
            RaycastHit hitInfo)
        {
            // Only the bridge's stabilized rising edge may activate this.
            if (!AndroidXRHandBridge.HandTrackingActive ||
                !AndroidXRHandBridge.MenuTouchDown)
            {
                return;
            }

            base.ButtonPressed(
                hitInfo);
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
