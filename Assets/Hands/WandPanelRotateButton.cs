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


        private bool CanRaycast => isActiveAndEnabled && GetCollider() != null &&
            GetCollider().enabled && GetCollider().gameObject.activeInHierarchy;

        public override bool RaycastAgainstCustomCollider(Ray ray, out RaycastHit hitInfo, float dist)
        {
            hitInfo = default;
            return CanRaycast && GetCollider().Raycast(ray, out hitInfo, dist);
        }


        public override bool UpdateStateWithInput(
            bool inputValid,
            Ray inputRay,
            GameObject parentActiveObject,
            Collider parentCollider)
        {
            if (!CanRaycast)
            {
                ResetState();
                return false;
            }
            return base.UpdateStateWithInput(inputValid, inputRay, parentActiveObject, parentCollider);
        }


        public override bool CalculateReticleCollision(
            Ray ray,
            ref Vector3 pos,
            ref Vector3 forward)
        {
            if (!RaycastAgainstCustomCollider(ray, out RaycastHit hitInfo, 100.0f))
            {
                return false;
            }
            pos = hitInfo.point;
            forward = hitInfo.normal;
            return true;
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
