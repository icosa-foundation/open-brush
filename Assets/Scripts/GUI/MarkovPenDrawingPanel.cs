using UnityEngine;

namespace TiltBrush
{
    /// @brief Provides the drawing panel used by the Markov pen drawing tool.
    /// Handles panel lifetime state, panel alignment, drawing collider raycasts, button raycasts,
    /// and the final panel button action.
    public class MarkovPenDrawingPanel : BasePanel
    {
        private const float k_RaycastMaxDistance = 100.0f;

        private static MarkovPenDrawingPanel s_Instance;
        private static bool s_IsOpen;

        [Header("Markov Panel Colliders")]
        [SerializeField] private Collider m_DrawingCollider;
        [SerializeField] private Collider m_SaveButtonCollider;
        [SerializeField] private Collider m_CloseButtonCollider;

        [Header("Panel Alignment")]
        [SerializeField] private bool m_ForceStraightRotation = true;
        [SerializeField] private Vector3 m_StraightEulerRotation = Vector3.zero;

        [Header("Panel Placement")]
        [SerializeField] private float m_DistanceFromUser = 1.75f;
        [SerializeField] private float m_HeightOffsetFromHead = -0.1f;
        [SerializeField] private float m_SizeMultiplier = 0.85f;

        private Vector3 m_InitialLocalScale;

        public static MarkovPenDrawingPanel Instance
        {
            get { return s_Instance; }
        }

        public static bool IsOpen
        {
            get { return s_IsOpen; }
        }

        public Collider DrawingCollider
        {
            get { return m_DrawingCollider; }
        }

        public Collider SaveButtonCollider
        {
            get { return m_SaveButtonCollider; }
        }


        public Collider CloseeButtonCollider
        {
            get { return m_CloseButtonCollider; }
        }


        /// @brief Initializes the Markov drawing panel instance reference.
        protected override void Awake()
        {
            base.Awake();

            s_Instance = this;
            m_InitialLocalScale = transform.localScale;
        }

        /// @brief Handles panel activation.
        /// Marks the panel as open, starts stroke capture, positions the panel in front of the user,
        /// and resets the Markov drawing tool state.
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

        /// @brief Places the panel in front of the user at a fixed distance.
        private void PositionPanelInFrontOfUser()
        {
            Transform head = ViewpointScript.Head;

            if (head == null)
            {
                return;
            }

            Vector3 forward = head.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 0.001f)
            {
                return;
            }

            forward.Normalize();

            Vector3 targetPosition = head.position + forward * m_DistanceFromUser;
            targetPosition.y = head.position.y + m_HeightOffsetFromHead;

            transform.position = targetPosition;
        }

        /// @brief Applies the configured panel size without accumulating scale changes.
        private void ApplyPanelScale()
        {
            transform.localScale = m_InitialLocalScale * m_SizeMultiplier;
        }

        /// @brief Faces the user horizontally while keeping the panel upright.
        private void FaceUserButStayUpright()
        {
            Transform head = ViewpointScript.Head;

            if (head == null)
            {
                return;
            }

            Vector3 direction = transform.position - head.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            if (m_ForceStraightRotation)
            {
                transform.rotation = Quaternion.Euler(m_StraightEulerRotation);
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// @brief Handles panel deactivation.
        /// Marks the panel as closed, ends stroke capture, and resets the Markov drawing tool state.
        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();

            s_IsOpen = false;

            MarkovPenSketchMemoryScript.EndMarkovStrokeCapture();
            MarkovPenDrawingFreepaint.OnPanelClosed();
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

            if (m_DrawingCollider == null)
            {
                return false;
            }

            RaycastHit raycastHit;
            if (!m_DrawingCollider.Raycast(ray, out raycastHit, k_RaycastMaxDistance))
            {
                return false;
            }

            worldPoint = raycastHit.point;

            Vector3 localPoint = transform.InverseTransformPoint(raycastHit.point);
            point2D = new Vector2(localPoint.x, localPoint.y);

            return true;
        }

        /// @brief Tries to get a button point from the button collider using a ray.
        /// @param ray The ray used to test the button collider.
        /// @param worldPoint The resulting world-space point on the button collider.
        /// @return True if the ray hit the button collider.
        public Collider TryGetButtonPoint(Ray ray, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            RaycastHit raycastHit;



            if (m_SaveButtonCollider.Raycast(ray, out raycastHit, k_RaycastMaxDistance))
                return m_SaveButtonCollider;

            if (m_CloseButtonCollider.Raycast(ray, out raycastHit, k_RaycastMaxDistance))
                return m_CloseButtonCollider;

            return null;
        }

        /// @brief Handles the Markov drawing panel button press.
        /// Stops active drawing, deletes the visible strokes captured since panel opening,
        /// and hides the drawing panel while keeping saved point lists available.
        public void OnButtonPressed(Collider buttonCollider)
        {
            if (buttonCollider == m_CloseButtonCollider)
            {
                MarkovPenDrawingFreepaint.clearList();
            }
            if (PointerManager.m_Instance != null)
            {
                PointerManager.m_Instance.EnableLine(false);
                PointerManager.m_Instance.PointerPressure = 0f;
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
