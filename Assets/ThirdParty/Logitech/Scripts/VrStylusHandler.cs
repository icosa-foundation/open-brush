// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Linq;
using TiltBrush;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

using MxInkController =
    UnityEngine.XR.OpenXR.Features.Interactions.LogitechMxInkControllerProfile.LogitechMxInkController;

#if USE_INPUT_SYSTEM_POSE_CONTROL
using OpenXRPoseControl = UnityEngine.InputSystem.XR.PoseControl;
#else
using OpenXRPoseControl = UnityEngine.XR.OpenXR.Input.PoseControl;
#endif

public class VrStylusHandler : StylusHandler
{
    private const float kHapticClickDuration = 0.01f;
    private const float kHapticClickAmplitude = 0.9f;
    private const float kHapticClickMinThreshold = 0.2f;

    [SerializeField] private GameObject _mxInk_model;
    [SerializeField] private GameObject _tip;
    [SerializeField] private GameObject _cluster_front;
    [SerializeField] private GameObject _cluster_middle;
    [SerializeField] private GameObject _cluster_back;

    public static VrStylusHandler m_Instance;

    public Color active_color = Color.green;
    public Color double_tap_active_color = Color.cyan;
    public Color default_color = Color.white;

    private MxInkController m_Device;
    private bool m_InUiInteraction;
    private bool m_TipHasVibrated;
    private bool m_MiddleHasVibrated;
    private bool m_DoubleTapHasVibrated;
    private bool m_BrushHandednessDirty;
    private bool m_ControllerVisibilityDirty;

    public bool InUiInteraction
    {
        get { return m_InUiInteraction; }
        set { m_InUiInteraction = value; }
    }

    public bool positionIsTracked => _stylus.positionIsTracked;
    public bool positionIsValid => _stylus.positionIsValid;

    private void Awake()
    {
        _stylus = new StylusInputs();
        m_Instance = this;
        SetStylusActive(false);
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnInputDeviceChange;
        RefreshDevice();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnInputDeviceChange;
        m_Device = null;
        SetStylusActive(false);
        UpdateControllerVisibility();
    }

    private void OnDestroy()
    {
        if (m_Instance == this)
        {
            m_Instance = null;
        }
    }

    public override bool CanDraw()
    {
        return _stylus.positionIsTracked &&
            _stylus.positionIsValid &&
            !m_InUiInteraction;
    }

    private void OnInputDeviceChange(
        UnityEngine.InputSystem.InputDevice device,
        InputDeviceChange change)
    {
        if (device is MxInkController || device == m_Device)
        {
            RefreshDevice();
        }
    }

    private void RefreshDevice()
    {
        MxInkController previousDevice = m_Device;
        m_Device = InputSystem.devices
            .OfType<MxInkController>()
            .FirstOrDefault(device => device.added);

        SetStylusActive(m_Device != null);
        if (m_Device != null)
        {
            UpdateHandedness();
        }

        if (previousDevice != m_Device)
        {
            ResetHapticState();
            string deviceName = m_Device != null ? m_Device.displayName : "none";
            string hand = m_Device == null
                ? "none"
                : _stylus.isOnRightHand ? "right" : "left";
            Debug.Log($"[MXINK-OPENXR] Active device: {deviceName}; hand: {hand}");
        }
    }

    private void LateUpdate()
    {
        if (m_Device != null && !m_Device.added)
        {
            RefreshDevice();
        }

        if (m_Device == null)
        {
            ClearInputState();
            UpdateControllerVisibility();
            return;
        }

        SetStylusActive(true);
        UpdateHandedness();
        UpdateBrushHandedness();

        UpdatePose();
        UpdateInputs();
        UpdateModelMaterials();
        GenerateHapticClicks();
        UpdateControllerVisibility();
    }

    private void UpdateHandedness()
    {
        bool isOnRightHand =
            m_Device.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand);
        if (_stylus.isOnRightHand != isOnRightHand)
        {
            _stylus.isOnRightHand = isOnRightHand;
            m_BrushHandednessDirty = true;
            m_ControllerVisibilityDirty = true;
        }
    }

    private void UpdateBrushHandedness()
    {
        if (!m_BrushHandednessDirty ||
            InputManager.m_Instance == null ||
            App.VrSdk.IsInitializingUnityXR)
        {
            return;
        }

        // MX Ink is always the brush. WandOnRight is therefore the inverse of
        // the physical hand holding the stylus.
        InputManager.m_Instance.WandOnRight = !_stylus.isOnRightHand;
        m_BrushHandednessDirty = false;
        m_ControllerVisibilityDirty = true;
    }

    private void UpdatePose()
    {
        OpenXRPoseControl poseControl = m_Device.tipPose;
        if (!poseControl.isTracked.isPressed)
        {
            poseControl = m_Device.pointer;
        }

        var pose = poseControl.ReadValue();
        _stylus.positionIsTracked = pose.isTracked;
        _stylus.positionIsValid =
            (pose.trackingState & InputTrackingState.Position) != 0 &&
            (pose.trackingState & InputTrackingState.Rotation) != 0;

        if (!_stylus.positionIsValid)
        {
            return;
        }

        transform.localPosition = pose.position;
        transform.localRotation = pose.rotation;
        _stylus.inkingPose = new Pose(pose.position, pose.rotation);
    }

    private void UpdateInputs()
    {
        _stylus.tip_value = m_Device.tip.ReadValue();
        _stylus.cluster_middle_value = m_Device.clusterMiddleButton.ReadValue();
        _stylus.cluster_front_value = m_Device.clusterFrontButton.isPressed;
        _stylus.cluster_back_value = m_Device.clusterBackButton.isPressed;
        _stylus.cluster_back_double_tap_value =
            m_Device.clusterBackDoubleTap.isPressed ||
            m_Device.clusterFrontDoubleTap.isPressed;
        _stylus.docked = m_Device.docked.isPressed;
        _stylus.any = _stylus.tip_value > 0 ||
            _stylus.cluster_front_value ||
            _stylus.cluster_middle_value > 0 ||
            _stylus.cluster_back_value ||
            _stylus.cluster_back_double_tap_value;
    }

    private void ClearInputState()
    {
        _stylus.positionIsTracked = false;
        _stylus.positionIsValid = false;
        _stylus.tip_value = 0;
        _stylus.cluster_middle_value = 0;
        _stylus.cluster_front_value = false;
        _stylus.cluster_back_value = false;
        _stylus.cluster_back_double_tap_value = false;
        _stylus.docked = false;
        _stylus.any = false;
    }

    private void ResetHapticState()
    {
        m_TipHasVibrated = false;
        m_MiddleHasVibrated = false;
        m_DoubleTapHasVibrated = false;
    }

    private void SetStylusActive(bool active)
    {
        if (_stylus.isActive != active)
        {
            _stylus.isActive = active;
            m_BrushHandednessDirty = active;
            m_ControllerVisibilityDirty = true;
        }

        if (_mxInk_model != null && _mxInk_model.activeSelf != active)
        {
            _mxInk_model.SetActive(active);
        }
    }

    private void UpdateControllerVisibility()
    {
        if (!m_ControllerVisibilityDirty || InputManager.m_Instance == null)
        {
            return;
        }

        if (!_stylus.isActive)
        {
            InputManager.m_Instance.ShowController(true, 0);
            InputManager.m_Instance.ShowController(true, 1);
        }
        else
        {
            InputManager.m_Instance.ShowController(
                false,
                (int)InputManager.ControllerName.Brush);
            InputManager.m_Instance.ShowController(
                true,
                (int)InputManager.ControllerName.Wand);
        }

        m_ControllerVisibilityDirty = false;
    }

    private void UpdateModelMaterials()
    {
        SetMaterialColor(_tip, _stylus.tip_value > 0 ? active_color : default_color);
        SetMaterialColor(
            _cluster_front,
            _stylus.cluster_front_value ? active_color : default_color);
        SetMaterialColor(
            _cluster_middle,
            _stylus.cluster_middle_value > 0 ? active_color : default_color);

        Color backColor = _stylus.cluster_back_value
            ? active_color
            : _stylus.cluster_back_double_tap_value
                ? double_tap_active_color
                : default_color;
        SetMaterialColor(_cluster_back, backColor);
    }

    private static void SetMaterialColor(GameObject target, Color color)
    {
        if (target != null && target.TryGetComponent(out MeshRenderer renderer))
        {
            renderer.material.color = color;
        }
    }

    private void GenerateHapticClicks()
    {
        PlayHapticClick(_stylus.tip_value, ref m_TipHasVibrated);
        PlayHapticClick(_stylus.cluster_middle_value, ref m_MiddleHasVibrated);
        PlayHapticClick(
            _stylus.cluster_back_double_tap_value,
            ref m_DoubleTapHasVibrated);
    }

    private void PlayHapticClick(float value, ref bool hasVibrated)
    {
        PlayHapticClick(value >= kHapticClickMinThreshold, ref hasVibrated);
    }

    private void PlayHapticClick(bool pressed, ref bool hasVibrated)
    {
        if (pressed && !hasVibrated)
        {
            m_Device.SendImpulse(kHapticClickAmplitude, kHapticClickDuration);
        }

        hasVibrated = pressed;
    }
}
