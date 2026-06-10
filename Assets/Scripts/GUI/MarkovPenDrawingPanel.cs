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

        [SerializeField] private Collider m_ButtonCollider;

        [Header("Panel Alignment")]
        [SerializeField] private bool m_ForceStraightRotation = true;

        [SerializeField] private Vector3 m_StraightEulerRotation = Vector3.zero;

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

        public Collider ButtonCollider
        {
            get { return m_ButtonCollider; }
        }

        /// @brief Initialize the Markov drawing panel instance reference.
        protected override void Awake()
        {
            base.Awake();

            s_Instance = this;
        }

        /// @brief Handle panel activation.
        /// Marks the panel as open, starts stroke capture, aligns the panel to the user,
        /// and resets the Markov drawing tool state.
        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();

            s_IsOpen = true;

            MarkovPenSketchMemoryScript.BeginMarkovStrokeCapture();

            FaceUserButStayUpright();
            MarkovPenDrawingFreepaint.OnPanelOpened();
        }

        /// @brief Face the user horizontally while keeping the panel upright.
        private void FaceUserButStayUpright()
        {
            Transform head = ViewpointScript.Head;

            Vector3 direction = transform.position - head.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        /// @brief Handle panel deactivation.
        /// Marks the panel as closed, ends stroke capture, and resets the Markov drawing tool state.
        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();

            s_IsOpen = false;

            MarkovPenSketchMemoryScript.EndMarkovStrokeCapture();
            MarkovPenDrawingFreepaint.OnPanelClosed();
        }

        /// @brief Try to get a drawing point from the drawing collider using a ray.
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

        /// @brief Try to get a button point from the button collider using a ray.
        /// @param ray The ray used to test the button collider.
        /// @param worldPoint The resulting world-space point on the button collider.
        /// @return True if the ray hit the button collider.
        public bool TryGetButtonPoint(Ray ray, out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;

            if (m_ButtonCollider == null)
            {
                return false;
            }

            RaycastHit raycastHit;
            if (!m_ButtonCollider.Raycast(ray, out raycastHit, k_RaycastMaxDistance))
            {
                return false;
            }

            worldPoint = raycastHit.point;

            return true;
        }

        /// @brief Handle the Markov drawing panel button press.
        /// Stops active drawing, deletes the visible strokes captured since panel opening,
        /// and hides the drawing panel while keeping saved point lists available.
        public void OnButtonPressed()
        {
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
