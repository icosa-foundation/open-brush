using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Keeps the hand-tracking Wand rotation buttons independent from all
    /// Sketchbook/Admin panel animation.
    ///
    /// Put this component on a parent object containing the two
    /// WandPanelRotateButton objects.
    ///
    /// At runtime it:
    /// - reparents itself out of any BasePanel hierarchy;
    /// - follows Wand.Geometry.MainAxisAttachPoint;
    /// - shows the controls only while BOTH left and right hands are the active
    ///   Android XR hand-tracking sources;
    /// - disables their renderers/colliders otherwise.
    ///
    /// The component itself remains active even when the controls are hidden,
    /// allowing them to become visible again when hand tracking starts.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HandTrackingWandRotateControls : MonoBehaviour
    {
        [Header("Placement relative to Wand MainAxisAttachPoint")]

        [SerializeField]
        private Vector3 m_LocalPositionOffset = Vector3.zero;

        [SerializeField]
        private Vector3 m_LocalRotationOffset = Vector3.zero;


        [Header("Visibility")]

        [Tooltip(
            "Recommended ON. The left hand owns the Wand and the right hand " +
            "points at these buttons, so the controls are useful only when both " +
            "hands are currently routed through Android XR hand tracking.")]
        [SerializeField]
        private bool m_RequireBothHands = true;


        [Header("Hierarchy")]

        [Tooltip(
            "If this control object was originally placed inside AdminPanel or " +
            "another BasePanel, move it under SketchControls at runtime so it " +
            "does not inherit Sketchbook/panel animation.")]
        [SerializeField]
        private bool m_DetachFromPanelOnStart = true;


        private Renderer[] m_Renderers;
        private Collider[] m_Colliders;
        private WandPanelRotateButton[] m_Buttons;

        private bool m_Visible = true;


        private void Awake()
        {
            CacheVisualsAndColliders();

            // Start hidden. The object itself stays active.
            SetControlsVisible(false, force: true);
        }


        private void Start()
        {
            if (m_DetachFromPanelOnStart &&
                GetComponentInParent<BasePanel>() != null)
            {
                Transform stableParent = null;

                if (SketchControlsScript.m_Instance != null)
                {
                    stableParent =
                        SketchControlsScript.m_Instance.transform;
                }

                // Keep this manager alive and independent from panel transforms.
                // Buttons remain registered with the original panel's UIComponentManager;
                // its detached-collider targeting supplies normal focus, input, and reticles.
                transform.SetParent(
                    stableParent,
                    true);
            }
        }


        private void LateUpdate()
        {
            bool handTrackingReady =
                AndroidXRHandBridge.HandTrackingActive &&
                AndroidXRHandBridge.LeftTracked;

            if (m_RequireBothHands)
            {
                handTrackingReady &=
                    AndroidXRHandBridge.RightTracked;
            }

            Transform anchor =
                GetWandAnchor();

            PanelManager panels = PanelManager.m_Instance;

            bool shouldShow =
                handTrackingReady &&
                SketchControlsScript.m_Instance != null &&
                !SketchControlsScript.m_Instance.IsViewOnly &&
                anchor != null &&
                panels != null &&
                panels.StandardActive() &&
                panels.GazePanelsAreVisible();

            SetControlsVisible(shouldShow);

            if (!shouldShow)
                return;

            Quaternion localRotation =
                Quaternion.Euler(
                    m_LocalRotationOffset);

            transform.position =
                anchor.TransformPoint(
                    m_LocalPositionOffset);

            transform.rotation =
                anchor.rotation *
                localRotation;
        }


        private static Transform GetWandAnchor()
        {
            if (InputManager.Wand == null)
                return null;

            ControllerGeometry geometry =
                InputManager.Wand.Geometry;

            if (geometry == null)
                return null;

            return geometry.MainAxisAttachPoint;
        }


        private void CacheVisualsAndColliders()
        {
            m_Buttons = GetComponentsInChildren<WandPanelRotateButton>(true);
            m_Renderers =
                GetComponentsInChildren<Renderer>(
                    true);

            m_Colliders =
                GetComponentsInChildren<Collider>(
                    true);
        }


        private void SetControlsVisible(
            bool visible,
            bool force = false)
        {
            if (m_Renderers == null ||
                m_Colliders == null)
            {
                CacheVisualsAndColliders();
            }

            if (!force && m_Visible == visible)
                return;

            m_Visible =
                visible;

            if (!visible)
            {
                foreach (WandPanelRotateButton button in m_Buttons)
                {
                    button.ResetState();
                }
            }


            if (m_Renderers != null)
            {
                foreach (Renderer renderer in
                         m_Renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled =
                            visible;
                    }
                }
            }


            if (m_Colliders != null)
            {
                foreach (Collider collider in
                         m_Colliders)
                {
                    if (collider != null)
                    {
                        collider.enabled =
                            visible;
                    }
                }
            }
        }
    }
}
