using UnityEngine;

public struct StylusInputs
{
    public float tip_value;
    public bool cluster_front_value;
    public float cluster_middle_value;
    public bool cluster_back_value;
    public bool cluster_back_double_tap_value;
    public bool any;
    public Pose inkingPose;
    public bool positionIsTracked;
    public bool positionIsValid;
    public float batteryLevel;
    public bool isActive;
    public bool isOnRightHand;
    public bool docked;
}

public abstract class StylusHandler : MonoBehaviour
{
    protected StylusInputs _stylus;

    public StylusInputs CurrentState
    {
        get { return _stylus; }
    }

    public virtual bool CanDraw()
    {
        return true;
    }
}
