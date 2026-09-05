// Copyright 2021 The Open Brush Authors
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

using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using System;

namespace TiltBrush
{
    public class UnityXRControllerInfo : ControllerInfo, IDisposable
    {
        private UnityEngine.XR.InputDevice device;
        private readonly UnityXRInputAction actionSet = new();
        private bool m_IsDisposed;

        private Vector2 padAxisPrevious = new Vector2();
        private const float kInputScrollScalar = 0.5f;

        private bool isBrush = false;

        private StylusInputs stylusState => VrStylusHandler.m_Instance?.CurrentState;

        private string actionMap
        {
            get => isBrush ? "Brush" : "Wand";
        }

        public UnityXRControllerInfo(BaseControllerBehavior behavior, bool isLeftHand)
            : base(behavior)
        {
            isBrush = !isLeftHand;
            Init();
        }

        public void SwapLeftRight()
        {
            isBrush = !isBrush;
            Init();
        }

        public void Dispose()
        {
            if (m_IsDisposed)
            {
                return;
            }

            actionSet.Disable();
            actionSet.Dispose();
            m_IsDisposed = true;
        }

        private void Init()
        {
            device = InputDevices.GetDeviceAtXRNode(isBrush ? XRNode.RightHand : XRNode.LeftHand);
            SetActionMask();
            if (isBrush)
            {
                actionSet.Brush.Enable();
                actionSet.Wand.Disable();
            }
            else
            {
                actionSet.Wand.Enable();
                actionSet.Brush.Disable();
                SetActionMask();
            }
        }

        private void SetActionMask()
        {
            string bindingGroup = string.Empty;
            switch (Behavior.ControllerGeometry.Style)
            {
                case ControllerStyle.Vive:
                    bindingGroup = actionSet.HTCViveControllerScheme.bindingGroup;
                    break;
                case ControllerStyle.Knuckles:
                    bindingGroup = actionSet.IndexControllerScheme.bindingGroup;
                    break;
                case ControllerStyle.OculusTouch:
                    bindingGroup = actionSet.OculusTouchControllerScheme.bindingGroup;
                    break;
                case ControllerStyle.Wmr:
                    bindingGroup = actionSet.WMRControllerScheme.bindingGroup;
                    break;
                case ControllerStyle.Neo3:
                case ControllerStyle.Phoenix:
                    bindingGroup = actionSet.PicoControllerScheme.bindingGroup;
                    break;
                case ControllerStyle.Zapbox:
                    bindingGroup = actionSet.ZapboxControllerScheme.bindingGroup;
                    break;
                case ControllerStyle.SteamFrame:
                    bindingGroup = GetSteamFrameBindingGroupForCurrentDevice();
                    break;
                default:
                    break;
            }

            actionSet.bindingMask = InputBinding.MaskByGroup(bindingGroup);
        }

        private string GetSteamFrameBindingGroupForCurrentDevice()
        {
            // When m_ForceControllerStyleForTesting is used, the geometry is Steam Frame but the
            // physical controller can still be Quest/Index/etc. Keep the real hardware bindings.
            string deviceName = device.name ?? string.Empty;
            if (deviceName.Contains("Oculus Touch"))
            {
                return actionSet.OculusTouchControllerScheme.bindingGroup;
            }
            if (deviceName.StartsWith("Index Controller OpenXR"))
            {
                return actionSet.IndexControllerScheme.bindingGroup;
            }
            if (deviceName.StartsWith("HTC Vive Controller OpenXR"))
            {
                return actionSet.HTCViveControllerScheme.bindingGroup;
            }
            if (deviceName.StartsWith("Windows MR Controller") ||
                deviceName.StartsWith("HP Reverb G2 Controller"))
            {
                return actionSet.WMRControllerScheme.bindingGroup;
            }
            if (deviceName.Contains("PICO Controller"))
            {
                return actionSet.PicoControllerScheme.bindingGroup;
            }
            if (deviceName.Contains("Zapbox"))
            {
                return actionSet.ZapboxControllerScheme.bindingGroup;
            }

            return actionSet.SteamFrameControllerScheme.bindingGroup;
        }

        private InputAction FindAction(string actionName)
        {
            InputActionMap map = actionSet.asset.FindActionMap(actionMap);
            return map?.FindAction(actionName);
        }

        private bool IsActionInProgress(string actionName)
        {
            InputAction action = FindAction(actionName);
            return action != null && action.inProgress;
        }

        private bool IsActionPressed(string actionName)
        {
            InputAction action = FindAction(actionName);
            return action != null && action.IsPressed();
        }

        public override bool IsTrackedObjectValid
        {
            get => device.isValid;
            set
            {

            }
        }

        public override Vector2 GetPadValue()
        {
            InputAction action = FindAction("PadAxis");
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        public override Vector2 GetThumbStickValue()
        {
            InputAction action = FindAction("ThumbAxis");
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        public override void Update()
        {
            base.Update();

            InputAction padTouch = FindAction("PadTouch");
            if (padTouch == null || !padTouch.inProgress)
            {
                padAxisPrevious = Vector2.zero;
            }
        }

        private bool IsStylusActive()
        {
            return stylusState != null &&
                stylusState.isActive &&
                stylusState.isOnRightHand == isBrush;
        }

        public override Vector2 GetPadValueDelta()
        {
            InputAction action = FindAction("ThumbAxis");
            if (action != null && action.inProgress)
            {
                Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                Vector2 stick = action.ReadValue<Vector2>();
                return new Vector2(Mathf.Clamp(stick.x, range.x, range.y), Mathf.Clamp(stick.y, range.x, range.y));
            }
            else
            {
                action = FindAction("PadAxis");
                InputAction padTouch = FindAction("PadTouch");
                if (action != null && padTouch != null && padTouch.IsPressed())
                {
                    Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                    Vector2 padAxisCurrent = action.ReadValue<Vector2>();

                    if (padAxisPrevious == Vector2.zero)
                    {
                        padAxisPrevious = padAxisCurrent;
                    }

                    var delta = padAxisCurrent - padAxisPrevious;
                    padAxisPrevious = padAxisCurrent;

                    delta.x = Mathf.Clamp(delta.x, range.x, range.y);
                    delta.y = Mathf.Clamp(delta.y, range.x, range.y);
                    return delta * kInputScrollScalar;
                }

                //padAxisPrevious = Vector2.zero;
                return Vector2.zero;
            }
        }

        public override float GetScrollXDelta()
        {
            return GetPadValueDelta().x;
        }

        public override float GetScrollYDelta()
        {
            return GetPadValueDelta().y;
        }

        public override float GetGripValue()
        {
            if (IsStylusActive())
            {
                return stylusState.cluster_front_value ? 1.0f : 0;
            }
            InputAction action = FindAction("GripAxis");
            return action != null ? action.ReadValue<float>() : 0f;
        }

        public override float GetTriggerRatio()
        {
            return GetTriggerValue();
        }

        public override float GetTriggerValue()
        {
            if (IsStylusActive())
            {
                return Math.Max(stylusState.tip_value, stylusState.cluster_middle_value);
            }
            InputAction action = FindAction("TriggerAxis");
            return action != null ? action.ReadValue<float>() : 0f;
        }

        private bool MapVrTouch(VrInput input)
        {
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                    return IsActionInProgress("ThumbTouch");
                case VrInput.Touchpad:
                    return IsActionInProgress("PadTouch");
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    return IsActionInProgress("PrimaryTouch");
                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    return IsActionInProgress("SecondaryTouch");


            }
            return false;
        }

        public override bool GetVrInputTouch(VrInput input)
        {
            return MapVrTouch(input);
        }

        private bool MapVrInput(VrInput input)
        {
            // This logic is inferred from OculusControllerInfo
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                    return IsActionPressed("ThumbButton");
                case VrInput.Touchpad:
                    return IsActionPressed("PadButton");
                case VrInput.Trigger:
                    if (IsStylusActive())
                        return stylusState.cluster_middle_value > 0.2 || stylusState.tip_value > 0.2;
                    return IsActionPressed("TriggerAxis");
                case VrInput.Grip:
                    if (IsStylusActive())
                        return stylusState.cluster_front_value;
                    return IsActionPressed("GripAxis");
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    if (IsStylusActive())
                        return stylusState.cluster_back_value;
                    return IsActionPressed("PrimaryButton");
                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    return IsActionPressed("SecondaryButton");
            }
            return false;
        }

        /// Returns the value of the specified button (level trigger).
        public override bool GetVrInput(VrInput input)
        {
            //Debug.Log("Get Input");
            return MapVrInput(input);
        }

        private bool MapVrInputPerFrame(VrInput input, bool down)
        {
            string selectedAction = string.Empty;
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                    selectedAction = "ThumbButton";
                    break;
                case VrInput.Touchpad:
                    selectedAction = "PadButton";
                    break;
                case VrInput.Trigger:
                    selectedAction = "TriggerAxis";
                    break;
                case VrInput.Grip:
                    selectedAction = "GripAxis";
                    break;
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    selectedAction = "PrimaryButton";
                    break;
                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    selectedAction = "SecondaryButton";
                    break;
            }

            if (!string.IsNullOrEmpty(selectedAction))
            {
                InputAction action = FindAction(selectedAction);
                if (action == null)
                {
                    return false;
                }

                return down ? action.WasPressedThisFrame() : action.WasReleasedThisFrame();
            }
            return false;
        }

        /// Returns true if the specified button was just pressed (rising-edge trigger).
        public override bool GetVrInputDown(VrInput input)
        {
            return MapVrInputPerFrame(input, true);
        }

        /// Returns true if the specified input has just been deactivated (falling-edge trigger).
        public override bool GetVrInputUp(VrInput input)
        {
            return MapVrInputPerFrame(input, false);
        }
        public override void TriggerControllerHaptics(float seconds)
        {
            float durationSeconds = seconds * App.VrSdk.VrControls.HapticsDurationScale;
            device.SendHapticImpulse(0, App.VrSdk.VrControls.HapticsAmplitudeScale, durationSeconds);
        }
    }

} // namespace TiltBrush
