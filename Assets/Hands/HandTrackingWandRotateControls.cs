using UnityEngine;

namespace TiltBrush
{
    // Standalone wand rotation buttons: right-hand aim + pinch, with reliable visual feedback.
    [DefaultExecutionOrder(30000)]
    public class HandTrackingWandRotateControls : MonoBehaviour
    {
        public static HandTrackingWandRotateControls Instance { get; private set; }
        [SerializeField] private Vector3 m_LocalPositionOffset = Vector3.zero;
        [SerializeField] private Vector3 m_LocalRotationOffset = Vector3.zero;
        [SerializeField] private bool m_RequireBothHands = true;
        [SerializeField] private bool m_DetachFromPanelOnStart = true;
        [SerializeField] private float m_MaxAimDistance = 1.5f;
        [SerializeField] private float m_AimPadding = 0.015f;
        [SerializeField] private float m_TargetHoldSeconds = 0.30f;
        [Header("Aim feedback")]
        [SerializeField] private bool m_ShowAimLine = true;
        [SerializeField] private Color m_AimColor = new Color(0.25f, 0.95f, 1f, 1f);
        [SerializeField] private float m_LineWidth = 0.0025f;
        [SerializeField] private float m_ReticleDiameter = 0.013f;
        [SerializeField] private float m_HoverScale = 1.12f;

        private WandPanelRotateButton[] m_Buttons;
        private Renderer[] m_Renderers;
        private Collider[] m_Colliders;
        private WandPanelRotateButton m_Target;
        private Vector3 m_TargetHit;
        private float m_LastHitTime;
        private bool m_Visible;
        private bool m_PressedThisPinch;
        private bool m_WasPinching;
        private Vector3 m_OriginalTargetScale;
        private LineRenderer m_Line;
        private Transform m_Reticle;
        private Material m_FeedbackMaterial;
        public bool IsAiming => m_Visible && m_Target != null;

        private void Awake()
        {
            Instance = this;
            m_Buttons = GetComponentsInChildren<WandPanelRotateButton>(true);
            m_Renderers = GetComponentsInChildren<Renderer>(true);
            m_Colliders = GetComponentsInChildren<Collider>(true);
            SetVisible(false, true);
        }

        // Reuse the same UI reticle GameObject as every native Open Brush panel.
        // SketchControlsScript owns its appearance, size and animation.
        private bool m_OwnsNativeReticle;

 private void ShowNativeReticle(
    Vector3 hit,
    Vector3 forward)
{
    var controls = SketchControlsScript.m_Instance;

    if (controls == null)
        return;

    controls.SetExternalUIReticle(
        true,
        hit,
        forward
    );

    m_OwnsNativeReticle = true;
}

      private void ReleaseNativeReticle()
{
    if (!m_OwnsNativeReticle)
        return;

    m_OwnsNativeReticle = false;

    var controls = SketchControlsScript.m_Instance;

    if (controls != null &&
        !controls.IsUserInteractingWithUI())
    {
        controls.SetExternalUIReticle(
            false,
            Vector3.zero,
            Vector3.forward
        );
    }
}

        private void Start()
        {
            if (m_DetachFromPanelOnStart && GetComponentInParent<BasePanel>() != null)
            {
                Transform parent = SketchControlsScript.m_Instance != null
                    ? SketchControlsScript.m_Instance.transform : null;
                transform.SetParent(parent, true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ReleaseNativeReticle();
        }

        private void LateUpdate()
        {
            Transform anchor = InputManager.Wand != null && InputManager.Wand.Geometry != null
                ? InputManager.Wand.Geometry.MainAxisAttachPoint : null;
            bool hands = AndroidXRHandBridge.HandTrackingActive && AndroidXRHandBridge.LeftTracked &&
                (!m_RequireBothHands || AndroidXRHandBridge.RightTracked);
            PanelManager panels = PanelManager.m_Instance;
            bool show = hands && anchor != null && panels != null &&
                panels.StandardActive() && panels.GazePanelsAreVisible();
            SetVisible(show, false);
            if (!show) { HideFeedback(); return; }
            transform.SetPositionAndRotation(anchor.TransformPoint(m_LocalPositionOffset),
                anchor.rotation * Quaternion.Euler(m_LocalRotationOffset));

            if (InputManager.Brush == null || InputManager.Brush.Behavior == null ||
                InputManager.Brush.Behavior.PointerAttachPoint == null)
            {
                HideFeedback(); return;
            }
            Transform pointer = InputManager.Brush.Behavior.PointerAttachPoint;
            Ray ray = new Ray(pointer.position, pointer.forward);
            bool pinch = AndroidXRHandBridge.HandPinchValue(true) >= 0.75f;
            bool pinchDown = pinch && !m_WasPinching;
            m_WasPinching = pinch;
            if (UpdateUI(ray, pinch, pinchDown, out Vector3 hit, out Vector3 forward))
                ShowNativeReticle(hit, forward);
            else
                HideFeedback();
        }

        private void HideFeedback()
        {
            ReleaseNativeReticle();
        }

        private void SetVisible(bool show, bool force)
        {
            if (!force && show == m_Visible) return;
            m_Visible = show;
            if (!show) { ClearTarget(); m_WasPinching = false; HideFeedback(); }
            foreach (Renderer r in m_Renderers) if (r != null) r.enabled = show;
            foreach (Collider c in m_Colliders) if (c != null) c.enabled = show;
        }

        private void ClearTarget()
        {
            if (m_Target != null)
            {
                m_Target.transform.localScale = m_OriginalTargetScale;
                m_Target.LostFocus();
            }
            m_Target = null;
        }
        public void ClearUIFocus() { ClearTarget(); HideFeedback(); }

        public bool UpdateUI(Ray pointerRay, bool triggerHeld, bool triggerDown,
            out Vector3 hitPosition, out Vector3 hitForward)
        {
            hitPosition = Vector3.zero;
            hitForward = pointerRay.direction;
            if (!triggerHeld) m_PressedThisPinch = false;
            if (!m_Visible || !AndroidXRHandBridge.RightTracked)
            {
                ClearTarget(); return false;
            }

            WandPanelRotateButton best = null;
            Vector3 bestHit = Vector3.zero;
            float bestDist = m_MaxAimDistance;
            foreach (var button in m_Buttons)
            {
                if (button == null || !button.isActiveAndEnabled) continue;
                Collider col = button.GetCollider();
                if (col == null || !col.enabled) continue;
                if (col.Raycast(pointerRay, out RaycastHit hit, m_MaxAimDistance))
                {
                    if (hit.distance < bestDist)
                    {
                        best = button; bestDist = hit.distance; bestHit = hit.point;
                    }
                }
                else
                {
                    Vector3 center = col.bounds.center;
                    float along = Vector3.Dot(center - pointerRay.origin, pointerRay.direction);
                    if (along <= 0 || along >= bestDist) continue;
                    Vector3 onRay = pointerRay.GetPoint(along);
                    if (Vector3.Distance(col.ClosestPoint(onRay), onRay) <= m_AimPadding)
                    {
                        best = button; bestDist = along; bestHit = col.ClosestPoint(onRay);
                    }
                }
            }

            if (best != null)
            {
                m_LastHitTime = Time.unscaledTime;
                m_TargetHit = bestHit;
            }
            else if (triggerHeld && m_Target != null &&
                     Time.unscaledTime - m_LastHitTime < m_TargetHoldSeconds)
            {
                best = m_Target; bestHit = m_TargetHit;
            }

            if (best != m_Target)
            {
                ClearTarget();
                m_Target = best;
                if (m_Target != null)
                {
                    m_OriginalTargetScale = m_Target.transform.localScale;
                    m_Target.transform.localScale = m_OriginalTargetScale * m_HoverScale;
                    m_Target.GainFocus();
                }
            }
            if (m_Target == null) return false;
            hitPosition = bestHit;
            if (triggerDown && !m_PressedThisPinch)
            {
                m_PressedThisPinch = true;
                m_Target.RotateFromHandAim();
            }
            return true;
        }
    }
}
