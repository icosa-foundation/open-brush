using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;

// Draws the guardian bounds. 
public class GuardianBoundaryDisplay : MonoBehaviour
{
    // Depending on the demo manager only for reorient notifications.
    public GuardianBoundaryEnforcer m_enforcer;

    // So that we can easily render the rectangular play area and
    // the more exact outer bounds.
    public OVRBoundary.BoundaryType m_boundaryType;

    // Something to tell the user their guardian bounds aren't configured.
    // This isn't a solution a shipping app would use-- it's just because
    // the demo makes no sense without bounds.
    public GameObject m_errorDisplay;

    void Start()
    {
        m_enforcer.TrackingChanged += RefreshDisplay;
        RefreshDisplay();
    }

    void RefreshDisplay()
    {
		bool configured = OVRManager.boundary.GetConfigured();
        if(configured)
        {
            LineRenderer lr = GetComponent<LineRenderer>();
            lr.positionCount = 0;

            // Note that these points are returned in (the newly reoriented) tracking space.
            // So rendering them correctly aligned with your guardian bounds in VR is
            // straightforward, and does not require additional transformations as long
            // as this is parented to the TrackingSpace node.
		    Vector3[] boundaryPoints = OVRManager.boundary.GetGeometry(m_boundaryType);
            lr.positionCount = boundaryPoints.Length + 1;
            Vector3 v;
            for(int i=0; i<boundaryPoints.Length; ++i)
            {
                v = boundaryPoints[i];
                v.y = 0.0f;
                lr.SetPosition(i, v);
            }
            v = boundaryPoints[0];
            v.y = 0.0f;
            lr.SetPosition(boundaryPoints.Length, v);
        }
        if(m_errorDisplay)
        {
            m_errorDisplay.SetActive(!configured);
        }
    }
}
