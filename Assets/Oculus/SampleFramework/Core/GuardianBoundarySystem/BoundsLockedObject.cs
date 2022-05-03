using UnityEngine;
using System.Collections;

// Simple script to keep an object within the user's play area. 
// If the play area is configured, the object attempts to stay within its initial distance,
// but clamped to be within the play area.
// If the play area is not configured, the object is left alone.
// A shipping title might use similar logic to keep critical interactive objects within
// the player's playable area.
public class BoundsLockedObject : MonoBehaviour
{
    Vector3 m_initialOffset;
    public OVRCameraRig m_playerOrigin;
    public GuardianBoundaryEnforcer m_enforcer;
    private Bounds? m_bounds = null;

    void Start()
    {
        m_enforcer.TrackingChanged += RefreshDisplay;
        m_initialOffset = gameObject.transform.position - m_playerOrigin.transform.position;
        Renderer renderer = gameObject.GetComponent<Renderer>();
        if(renderer != null)
        {
            m_bounds = renderer.bounds;
        }
        RefreshDisplay();
    }

	void RefreshDisplay()
    {
		bool configured = OVRManager.boundary.GetConfigured();
        if (configured)
        {
            Vector3[] boundaryPoints = OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.PlayArea);
            float xMin = 10000.0f; float zMin = 10000.0f;
            float xMax = -10000.0f; float zMax = -10000.0f;

            for (int i = 0; i < boundaryPoints.Length; ++i)
            {
                // Transforming the points to deal with the case where GuardianBoundaryDemoSettings.AllowRecenterYaw = false.
                // The boundary points will be returned in the new tracking space, but we want to ignore the new orientation
                // and instead use our nicely axis-aligned original play area.
                // If AllowRecenterYaw = true, trackingSpace will simply be the identity, so this is fine.
                boundaryPoints[i] = m_enforcer.OrientToOriginalForward * boundaryPoints[i];

                xMin = Mathf.Min(xMin, boundaryPoints[i].x);
                zMin = Mathf.Min(zMin, boundaryPoints[i].z);
                xMax = Mathf.Max(xMax, boundaryPoints[i].x);
                zMax = Mathf.Max(zMax, boundaryPoints[i].z);
            }

            if(m_bounds != null)
            {
                float halfWidth = ((Bounds)m_bounds).size.x * 0.5f;
                float halfLength = ((Bounds)m_bounds).size.z * 0.5f;
                xMin += halfWidth;
                xMax -= halfWidth;
                zMin += halfLength;
                zMax -= halfLength;
            }

            // Now we can easily constrain the object's position to be within the play area.
            Vector3 newPos = m_initialOffset;
            newPos.x = Mathf.Max(Mathf.Min(xMax, m_initialOffset.x), xMin);
            newPos.z = Mathf.Max(Mathf.Min(zMax, m_initialOffset.z), zMin);
            newPos.y = gameObject.transform.position.y;

            if (m_enforcer.m_AllowRecenter)
            {
                newPos = Quaternion.Inverse(m_enforcer.OrientToOriginalForward) * newPos;
            }

            gameObject.transform.position = newPos;
        }
	}
}
