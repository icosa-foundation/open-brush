using UnityEngine;

namespace TiltBrush
{
    public class MarkovPenPanel : BasePanel
    {
        public static MarkovPenPanel Instance { get; private set; }
        public static bool IsOpen { get; private set; }

        [SerializeField] private Collider m_DrawingCollider;

        public Collider DrawingCollider
        {
            get { return m_DrawingCollider; }
        }

        override protected void Awake()
        {
            base.Awake();
            Instance = this;
        }

        override protected void OnEnablePanel()
        {
            base.OnEnablePanel();
            IsOpen = true;

            Debug.Log("[MarkovPenPanel] Opened. 2D drawing mode active.");
        }

        override protected void OnDisablePanel()
        {
            base.OnDisablePanel();
            IsOpen = false;

            Debug.Log("[MarkovPenPanel] Closed. 2D drawing mode inactive.");
        }

        override public void OnUpdatePanel(Vector3 vToPanel, Vector3 vHitPoint)
        {
            base.OnUpdatePanel(vToPanel, vHitPoint);

            // Hier kannst du später Panel-spezifische Drawing-Logik einbauen.
            // vHitPoint ist der Punkt auf dem Panel, auf den der Controller/Reticle zeigt.
        }

        public bool TryGetPanel2DPoint(Ray ray, out Vector2 point2D, out Vector3 worldPoint)
        {
            point2D = Vector2.zero;
            worldPoint = Vector3.zero;

            if (m_DrawingCollider == null)
            {
                Debug.LogError("[MarkovPenPanel] m_DrawingCollider ist nicht gesetzt.");
                return false;
            }

            RaycastHit hit;
            if (!m_DrawingCollider.Raycast(ray, out hit, 100.0f))
            {
                Debug.LogError("[MarkovPenPanel] Raycast auf m_DrawingCollider fehlgeschlagen.");
                return false;
            }
            Debug.Log("[MarkovPenPanel] Hit: " + hit.point);
            worldPoint = hit.point;

            Vector3 localPoint = transform.InverseTransformPoint(hit.point);

            // 2D auf Panel-Ebene.
            point2D = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        public Vector3 Panel2DToWorld(Vector2 point2D)
        {
            // z bleibt 0, dadurch liegt alles flach auf dem Panel.
            Vector3 localPoint = new Vector3(point2D.x, point2D.y, 0.0f);
            return transform.TransformPoint(localPoint);
        }
    }
}
