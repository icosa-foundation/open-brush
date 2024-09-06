using UnityEngine;
using System;

public class VrStylusHandler : StylusHandler
{
    [SerializeField] private GameObject _mxInk_model;
    [SerializeField] private GameObject _tip;
    [SerializeField] private GameObject _cluster_front;
    [SerializeField] private GameObject _cluster_middle;
    [SerializeField] private GameObject _cluster_back;

    [SerializeField] private GameObject _left_touch_controller;
    [SerializeField] private GameObject _right_touch_controller;

    private bool _inUiInteraction = false;

    public bool InUiInteraction
    {
        get { return _inUiInteraction; }
        set { _inUiInteraction = value; }
    }

    private bool _positionIsTracked;
    private bool _positionIsValid;

    public bool positionIsTracked
    {
        get { return _positionIsTracked; }
    }
    public bool positionIsValid
    {
        get { return _positionIsValid; }
    }
    public Color active_color = Color.green;
    public Color double_tap_active_color = Color.cyan;
    public Color default_color = Color.white;

    public override bool CanDraw()
    {
        return _positionIsTracked && _positionIsValid && !_inUiInteraction;
    }

    // Defined action names.
    private const string MX_Ink_Pose_Right = "aim_right";
    private const string MX_Ink_Pose_Left = "aim_left";
    private const string MX_Ink_TipForce = "tip";
    private const string MX_Ink_MiddleForce = "middle";
    private const string MX_Ink_ClusterFront = "front";
    private const string MX_Ink_ClusterBack = "back";
    private const string MX_Ink_ClusterBack_DoubleTap = "back_double_tap";
    private const string MX_Ink_ClusterFront_DoubleTap = "front_double_tap";
    private const string MX_Ink_Dock = "dock";
    private const string MX_Ink_Haptic_Pulse = "haptic_pulse";

    private bool _tipHasVibrated = false;
    private bool _middleHasVibrated = false;
    private bool _doubleTapHasVibrated = false;
    private float _hapticClickDuration = 0.1f;
    private float _hapticClickAmplitude = 0.9f;
    private float _hapticClickMinThreshold = 0.2f;

    private void UpdatePose()
    {
        _positionIsTracked = false;
        _positionIsValid = false;

        // Retrieve the interaction profile names of the right and left controllers
        var leftDevice = OVRPlugin.GetCurrentInteractionProfileName(OVRPlugin.Hand.HandLeft);
        var rightDevice = OVRPlugin.GetCurrentInteractionProfileName(OVRPlugin.Hand.HandRight);

        // The Quest 3 touch controller interaction profile name is: /interaction_profiles/meta/touch_controller_plus 
        // The MX Ink interaction profile is: /interaction_profiles/logitech/mx_ink_stylus_logitech

        // Find whether the Logitech MX Ink is on the left or the right hand
        bool stylusIsOnLeftHand = leftDevice.Contains("logitech");
        bool stylusIsOnRightHand = rightDevice.Contains("logitech");

        // Flag the stylus as active/inactive, on right/left hand
        _stylus.isActive = stylusIsOnLeftHand || stylusIsOnRightHand;
        _stylus.isOnRightHand = stylusIsOnRightHand;
        // Hide the 3D model if not active
        _mxInk_model.SetActive(_stylus.isActive);

        // Select the right/left hand stylus pose to be used
        string MX_Ink_Pose = _stylus.isOnRightHand ? MX_Ink_Pose_Right : MX_Ink_Pose_Left;

        // Hide the touch controller that is currently inactive, depends on stylus handededness (see stylus settings in VR shell UI)
        try
        {
            _right_touch_controller.SetActive(!_stylus.isOnRightHand || !_stylus.isActive);
            _left_touch_controller.SetActive(_stylus.isOnRightHand || !_stylus.isActive);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }

        if (OVRPlugin.GetActionStatePose(MX_Ink_Pose, out OVRPlugin.Posef handPose))
        {
            transform.localPosition = handPose.Position.FromFlippedZVector3f();
            transform.rotation = handPose.Orientation.FromFlippedZQuatf();
            _stylus.inkingPose.position = transform.localPosition;
            _stylus.inkingPose.rotation = transform.rotation;
            _positionIsTracked = true;
            _positionIsValid = true;
        }
        else
        {
            Debug.LogError($"MX_Ink: Error getting Pose action name {MX_Ink_Pose}, check logcat for specifics.");
        }
    }

    void Update()
    {
        OVRInput.Update();
        UpdatePose();

        if (!OVRPlugin.GetActionStateFloat(MX_Ink_TipForce, out _stylus.tip_value))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_TipForce}");
        }

        if (!OVRPlugin.GetActionStateFloat(MX_Ink_MiddleForce, out _stylus.cluster_middle_value))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_TipForce}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterFront, out _stylus.cluster_front_value))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterFront}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterBack, out _stylus.cluster_back_value))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterBack}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterFront_DoubleTap, out _stylus.cluster_back_double_tap_value))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterFront_DoubleTap}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_ClusterBack_DoubleTap, out _stylus.cluster_back_double_tap_value))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_ClusterBack_DoubleTap}");
        }

        if (!OVRPlugin.GetActionStateBoolean(MX_Ink_Dock, out _stylus.docked))
        {
            Debug.LogError($"MX_Ink: Error getting action name: {MX_Ink_Dock}");
        }

        _stylus.any = _stylus.tip_value > 0 || _stylus.cluster_front_value ||
                        _stylus.cluster_middle_value > 0 || _stylus.cluster_back_value ||
                        _stylus.cluster_back_double_tap_value;

        _tip.GetComponent<MeshRenderer>().material.color = _stylus.tip_value > 0 ? active_color : default_color;
        _cluster_front.GetComponent<MeshRenderer>().material.color = _stylus.cluster_front_value ? active_color : default_color;
        _cluster_middle.GetComponent<MeshRenderer>().material.color = _stylus.cluster_middle_value > 0 ? active_color : default_color;
        if (_stylus.cluster_back_value)
        {
            _cluster_back.GetComponent<MeshRenderer>().material.color = _stylus.cluster_back_value ? active_color : default_color;
        }
        else
        {
            _cluster_back.GetComponent<MeshRenderer>().material.color = _stylus.cluster_back_double_tap_value ? double_tap_active_color : default_color;
        }

        GenerateHapticClicks();
    }

    private void PlayHapticClick(float analogValue, ref bool hasVibrated, OVRPlugin.Hand hand)
    {
        if (analogValue >= _hapticClickMinThreshold)
        {
            if (!hasVibrated)
            {
                OVRPlugin.TriggerVibrationAction(MX_Ink_Haptic_Pulse, hand,
                _hapticClickDuration, _hapticClickAmplitude);
                hasVibrated = true;
            }
        }
        if (analogValue < _hapticClickMinThreshold)
        {
            hasVibrated = false;
        }
    }

    private void PlayHapticClick(bool inputValue, ref bool hasVibrated, OVRPlugin.Hand hand)
    {
        if (inputValue)
        {
            if (!hasVibrated)
            {
                OVRPlugin.TriggerVibrationAction(MX_Ink_Haptic_Pulse, hand,
                _hapticClickDuration, _hapticClickAmplitude);
                hasVibrated = true;
            }
        }
        else
        {
            hasVibrated = false;
        }
    }

    private void GenerateHapticClicks()
    {
        try
        {
            OVRPlugin.Hand holdingHand = _stylus.isOnRightHand ? OVRPlugin.Hand.HandRight : OVRPlugin.Hand.HandLeft;
            PlayHapticClick(_stylus.tip_value, ref _tipHasVibrated, holdingHand);
            PlayHapticClick(_stylus.cluster_middle_value, ref _middleHasVibrated, holdingHand);
            PlayHapticClick(_stylus.cluster_back_double_tap_value, ref _doubleTapHasVibrated, holdingHand);
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }
}
