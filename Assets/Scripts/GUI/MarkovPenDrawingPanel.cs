using UnityEngine;

namespace TiltBrush
{
    /// Represents the drawing panel used by the Markov pen feature.
    /// Stores the currently active panel instance and exposes panel hit detection.
    /// Converts raycast hits on the drawing collider into local 2D panel coordinates.
    public class MarkovPenDrawingPanel : BasePanel
    {
        private const float k_RaycastMaxDistance = 100.0f;

        public static MarkovPenDrawingPanel Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [SerializeField] private Collider m_DrawingCollider;

        /// Represents the collider used as the interactive drawing surface.
        /// Returns the collider that receives raycasts for panel drawing input.
        public Collider DrawingCollider
        {
            get { return m_DrawingCollider; }
        }

        /// Initialises the drawing panel instance.
        /// Calls the base panel setup and stores this panel as the global instance.
        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        /// Opens the drawing panel.
        /// Calls the base enable behaviour and marks the drawing panel as open.
        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();
            IsOpen = true;
        }

        /// Closes the drawing panel.
        /// Calls the base disable behaviour and marks the drawing panel as closed.
        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();
            IsOpen = false;
        }

        /// Tries to convert a ray hit on the drawing panel into a 2D panel point.
        /// Returns false if no drawing collider exists or if the ray does not hit the panel.
        /// Stores both the world hit position and the local 2D panel position.
        /// @param ray Ray used to test against the drawing panel collider.
        /// @param point2D Local 2D point on the panel plane.
        /// @param worldPoint World position where the ray hits the drawing panel.
        /// @returns True if the ray hits the drawing panel, otherwise false.
        public bool TryGetPanel2DPoint(Ray ray, out Vector2 point2D, out Vector3 worldPoint)
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
    }
}
