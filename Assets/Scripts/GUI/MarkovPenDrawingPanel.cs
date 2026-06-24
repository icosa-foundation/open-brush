using UnityEngine;
using UnityEngine.Serialization;

namespace TiltBrush
{
    /// @brief Provides the drawing panel used by the Markov pen drawing tool.
    /// Handles panel lifetime state, panel alignment, drawing collider raycasts,
    /// button raycasts, and panel button actions.
    public class MarkovPenDrawingPanel : BasePanel
    {
        private const float k_RaycastMaxDistance = 100.0f;
        private const float k_MinHorizontalDirectionSqrMagnitude = 0.001f;

        private static MarkovPenDrawingPanel s_Instance;
        private static bool s_IsOpen;

        [Header("Markov Panel Colliders")]
        [SerializeField] private Collider m_DrawingCollider;
        [SerializeField] private Collider m_SaveButtonCollider;
        [SerializeField] private Collider m_CloseButtonCollider;

        [Header("Button Hover")]
        [SerializeField] private Transform m_SaveButtonHoverTarget;
        [SerializeField] private Transform m_CloseButtonHoverTarget;
        [SerializeField] private float m_HoverScale = 1.1f;

        [Header("Panel Alignment")]
        [FormerlySerializedAs("m_ForceStraightRotation")]
        [SerializeField] private bool m_IsStraightRotationForced = true;
        [SerializeField] private Vector3 m_StraightEulerRotation = Vector3.zero;

        [Header("Panel Placement")]
        [SerializeField] private float m_DistanceFromUser = 1.75f;
        [SerializeField] private float m_HeightOffsetFromHead = -0.1f;
        [SerializeField] private float m_SizeMultiplier = 0.85f;

        private Vector3 m_InitialLocalScale;
        private Collider m_HoveredButton;
        private Vector3 m_SaveButtonBaseScale;
        private Vector3 m_CloseButtonBaseScale;

        /// @brief Gets the active Markov drawing panel instance.
        public static MarkovPenDrawingPanel Instance
        {
            get { return s_Instance; }
        }

        /// @brief Gets whether the Markov drawing panel is currently open.
        public static bool IsOpen
        {
            get { return s_IsOpen; }
        }

        /// @brief Gets the collider used for drawing input.
        public Collider DrawingCollider
        {
            get { return m_DrawingCollider; }
        }

        /// @brief Gets the collider used for the save button.
        public Collider SaveButtonCollider
        {
            get { return m_SaveButtonCollider; }
        }

        /// @brief Gets the collider used for the close button.
        public Collider CloseButtonCollider
        {
            get { return m_CloseButtonCollider; }
        }

        /// @brief Initializes the panel instance and stores the initial visual state.
        protected override void Awake()
        {
            base.Awake();

            s_Instance = this;
            m_InitialLocalScale = transform.localScale;

            if (m_SaveButtonHoverTarget == null && m_SaveButtonCollider != null)
            {
                m_SaveButtonHoverTarget = m_SaveButtonCollider.transform;
            }

            if (m_CloseButtonHoverTarget == null && m_CloseButtonCollider != null)
            {
                m_CloseButtonHoverTarget = m_CloseButtonCollider.transform;
            }

            if (m_SaveButtonHoverTarget != null)
            {
                m_SaveButtonBaseScale = m_SaveButtonHoverTarget.localScale;
            }

            if (m_CloseButtonHoverTarget != null)
            {
                m_CloseButtonBaseScale = m_CloseButtonHoverTarget.localScale;
            }
        }

        /// @brief Activates the panel and prepares the Markov drawing tool.
        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();

            s_IsOpen = true;

            MarkovPenSketchMemoryScript.BeginMarkovStrokeCapture();

            PositionPanelInFrontOfUser();
            ApplyPanelScale();
            FaceUserButStayUpright();

            MarkovPenDrawingFreepaint.OnPanelOpened();
        }

        /// @brief Deactivates the panel and resets the Markov drawing tool state.
        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();

            SetHoveredButton(null);
            s_IsOpen = false;

            MarkovPenSketchMemoryScript.EndMarkovStrokeCapture();
            MarkovPenDrawingFreepaint.OnPanelClosed();
        }

        /// @brief Positions the panel in front of the user's head at the configured distance.
        private void PositionPanelInFrontOfUser()
        {
            Transform headTransform = ViewpointScript.Head;

            if (headTransform == null)
            {
                return;
            }

            Vector3 horizontalForward = headTransform.forward;
            horizontalForward.y = 0.0f;

            if (horizontalForward.sqrMagnitude < k_MinHorizontalDirectionSqrMagnitude)
            {
                horizontalForward = transform.forward;
                horizontalForward.y = 0.0f;
            }

            if (horizontalForward.sqrMagnitude < k_MinHorizontalDirectionSqrMagnitude)
            {
                return;
            }

            horizontalForward.Normalize();

            Vector3 targetPosition =
                headTransform.position + horizontalForward * m_DistanceFromUser;
            targetPosition.y = headTransform.position.y + m_HeightOffsetFromHead;

            transform.position = targetPosition;
        }

        /// @brief Sets the currently hovered panel button and updates its visual state.
        /// @param buttonCollider The collider of the button that is currently hovered.
        public void SetHoveredButton(Collider buttonCollider)
        {
            if (m_HoveredButton == buttonCollider)
            {
                return;
            }

            SetButtonHoverVisual(m_HoveredButton, false);

            m_HoveredButton = buttonCollider;

            SetButtonHoverVisual(m_HoveredButton, true);
        }

        /// @brief Tries to get the closest point on the drawing collider using a ray.
        /// @param ray The ray used to test the drawing collider.
        /// @param worldPoint The resulting world-space point on the drawing collider.
        /// @return True if the ray hit the drawing collider.
        public bool TryGetClosestPanelPoint(Ray ray, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            float closestDistance = float.MaxValue;

            return TryUpdateClosestColliderHit(
                m_DrawingCollider,
                ray,
                ref closestDistance,
                ref worldPoint);
        }

        /// @brief Tries to get a drawing point from the drawing collider using a ray.
        /// @param ray The ray used to test the drawing collider.
        /// @param point2D The resulting local two-dimensional point on the drawing panel.
        /// @param worldPoint The resulting world-space point on the drawing panel.
        /// @return True if the ray hit the drawing collider.
        public bool TryGetDrawingPoint(
            Ray ray,
            out Vector2 point2D,
            out Vector3 worldPoint)
        {
            point2D = Vector2.zero;
            worldPoint = Vector3.zero;

            if (m_DrawingCollider == null ||
                !m_DrawingCollider.Raycast(ray, out RaycastHit raycastHit, k_RaycastMaxDistance))
            {
                return false;
            }

            worldPoint = raycastHit.point;

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            point2D = new Vector2(localPoint.x, localPoint.y);

            return true;
        }

        /// @brief Tries to get the closest button collider hit by a ray.
        /// @param ray The ray used to test the button colliders.
        /// @param worldPoint The resulting world-space point on the button collider.
        /// @return The closest hit button collider, or null if no button was hit.
        public Collider TryGetButtonPoint(Ray ray, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            Collider closestButtonCollider = null;
            float closestDistance = float.MaxValue;

            if (TryUpdateClosestColliderHit(
                m_SaveButtonCollider,
                ray,
                ref closestDistance,
                ref worldPoint))
            {
                closestButtonCollider = m_SaveButtonCollider;
            }

            if (TryUpdateClosestColliderHit(
                m_CloseButtonCollider,
                ray,
                ref closestDistance,
                ref worldPoint))
            {
                closestButtonCollider = m_CloseButtonCollider;
            }

            return closestButtonCollider;
        }

        /// @brief Tries to update the closest raycast hit for a collider.
        /// @param collider The collider to test.
        /// @param ray The ray used for the collider test.
        /// @param closestDistance The current closest hit distance.
        /// @param worldPoint The current closest world-space hit point.
        /// @return True if the collider produced a closer hit.
        private bool TryUpdateClosestColliderHit(
            Collider collider,
            Ray ray,
            ref float closestDistance,
            ref Vector3 worldPoint)
        {
            if (collider == null ||
                !collider.Raycast(ray, out RaycastHit raycastHit, k_RaycastMaxDistance))
            {
                return false;
            }

            if (raycastHit.distance >= closestDistance)
            {
                return false;
            }

            closestDistance = raycastHit.distance;
            worldPoint = raycastHit.point;

            return true;
        }

        /// @brief Updates the hover scale for a panel button.
        /// @param buttonCollider The collider of the button to update.
        /// @param isHovered True when the button is hovered.
        private void SetButtonHoverVisual(Collider buttonCollider, bool isHovered)
        {
            if (buttonCollider == m_SaveButtonCollider && m_SaveButtonHoverTarget != null)
            {
                m_SaveButtonHoverTarget.localScale = isHovered
                    ? m_SaveButtonBaseScale * m_HoverScale
                    : m_SaveButtonBaseScale;
            }
            else if (buttonCollider == m_CloseButtonCollider && m_CloseButtonHoverTarget != null)
            {
                m_CloseButtonHoverTarget.localScale = isHovered
                    ? m_CloseButtonBaseScale * m_HoverScale
                    : m_CloseButtonBaseScale;
            }
        }

        /// @brief Applies the configured panel size without accumulating scale changes.
        private void ApplyPanelScale()
        {
            transform.localScale = m_InitialLocalScale * m_SizeMultiplier;
        }

        /// @brief Faces the user horizontally while keeping the panel upright.
        private void FaceUserButStayUpright()
        {
            Transform headTransform = ViewpointScript.Head;

            if (headTransform == null)
            {
                return;
            }

            Vector3 horizontalDirection = transform.position - headTransform.position;
            horizontalDirection.y = 0.0f;

            if (horizontalDirection.sqrMagnitude < k_MinHorizontalDirectionSqrMagnitude)
            {
                return;
            }

            if (m_IsStraightRotationForced)
            {
                transform.rotation = Quaternion.Euler(m_StraightEulerRotation);
                return;
            }

            transform.rotation = Quaternion.LookRotation(
                horizontalDirection.normalized,
                Vector3.up);
        }

        /// @brief Handles a press on a Markov drawing panel button.
        /// @param buttonCollider The collider of the pressed button.
        public void OnButtonPressed(Collider buttonCollider)
        {
            if (buttonCollider != m_SaveButtonCollider &&
                buttonCollider != m_CloseButtonCollider)
            {
                return;
            }

            if (buttonCollider == m_CloseButtonCollider)
            {
                MarkovPenDrawingFreepaint.RestorePaintPointListsFromBackup();

            }

            if (PointerManager.m_Instance != null)
            {
                PointerManager.m_Instance.EnableLine(false);
                PointerManager.m_Instance.PointerPressure = 0.0f;
                PointerManager.m_Instance.EatLineEnabledInput();
            }

            MarkovPenSketchMemoryScript.DeleteNewMarkovStrokes();

            if (PanelManager.m_Instance != null)
            {
                PanelManager.m_Instance.HidePanel(Type);
            }
        }
    }
}
