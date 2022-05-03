using UnityEngine;
using System.Collections;

public class GuardianBoundaryEnforcer : MonoBehaviour
{
    // This is a bit of a hack:
    // We listen to OVRDisplay.RecenteredPose, to update the tracking space to be within
    // our desired constraints. However, other components may also wish to listen to that
    // event-- but we need our adjustments to happen first. So instead, they listen to
    // this event. 
    // This does mean this script is incompatible with OVRPlayerController. If you want
    // to use them together, you'll need to modify OVRPlayerController to listen to this
    // event instead.
	public event System.Action TrackingChanged;

    public bool m_AllowRecenter = true;
    public OVRCameraRig m_mainCamera;

    // Stores one tracker's initial orientation on launch, so we can track how orientation changes later
    // due to user-initiated reorient. Which tracker we use is not actually important, we just need a
    // stationary point of reference.
    Quaternion m_originalTrackerOrientation;

    // Used in a recenter hack; see Update.
    int m_framecount = 0;

    // Parts of this demo want to keep the original forward vector, parallel to the sides
    // of the user's play area. This can be accomplished using the below rotation.
    public Quaternion OrientToOriginalForward
    {
        get { return m_orientToOriginalForward; }
    }
    Quaternion m_orientToOriginalForward;


	void Start ()
    {
        OVRManager.display.RecenteredPose += Recentered;

        m_originalTrackerOrientation = OVRPlugin.GetNodePose(OVRPlugin.Node.TrackerZero, OVRPlugin.Step.Render).ToOVRPose().orientation;
	}

    private void Update()
    {
        // Hack: the recenter doesn't kick in for 1-2 frames, so stall.
        // We can remove this hack once the bug is resolved.
        if (m_framecount >= 0)
        {
            m_framecount++;
            if(m_framecount > 2)
            {
                // Implementation of AllowRecenterYaw is a bit complicated. There's no way in our Unity integration to prevent the yaw
                // recenter. So we transform the trackingSpace node, and hence all of its child cameras, to "undo" the rotation done
                // to the tracking space.
                Quaternion newTrackerOri = OVRPlugin.GetNodePose(OVRPlugin.Node.TrackerZero, OVRPlugin.Step.Render).ToOVRPose().orientation;
                float diff = newTrackerOri.eulerAngles.y - m_originalTrackerOrientation.eulerAngles.y;
                m_orientToOriginalForward = Quaternion.Euler(0.0f, -diff, 0.0f);
                if (!m_AllowRecenter)
                {
                    m_mainCamera.trackingSpace.transform.rotation = m_orientToOriginalForward;
                }

                m_framecount = -1;
                if (TrackingChanged != null)
                {
                    TrackingChanged();
                }
            }
        }
    }

    void Recentered()
    {
        // Kicks off the Update loop count down until the Recenter has kicked in, and we do the real work.
        m_framecount = 0;
    }
}
