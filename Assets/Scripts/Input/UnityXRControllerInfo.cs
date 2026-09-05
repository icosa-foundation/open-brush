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

using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace TiltBrush
{
    /// <summary>
    /// Unity XR controller implementation used by Open Brush.
    ///
    /// Android XR hand mode:
    ///   - Left tracked hand  -> Open Brush Wand
    ///   - Right tracked hand -> Open Brush Brush
    ///   - AndroidXRHandBridge supplies Brush trigger state
    ///   - Controller-only inputs are suppressed while hand mode is active
    /// </summary>
    public class UnityXRControllerInfo : ControllerInfo, IDisposable
    {
        private UnityEngine.XR.InputDevice device;
        private readonly UnityXRInputAction actionSet = new();
        private bool m_IsDisposed;

        private Vector2 padAxisPrevious = Vector2.zero;
        private const float kInputScrollScalar = 0.5f;

        // In Open Brush's UnityXR setup the right-hand ControllerInfo is the Brush,
        // and the left-hand ControllerInfo is the Wand.
        private bool isBrush;

        private StylusInputs stylusState => VrStylusHandler.m_Instance?.CurrentState;

        private string actionMap => isBrush ? "Brush" : "Wand";

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
            device = InputDevices.GetDeviceAtXRNode(
                isBrush ? XRNode.RightHand : XRNode.LeftHand);

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

        // ---------------------------------------------------------------------
        // Tracking
        // ---------------------------------------------------------------------

        public override bool IsTrackedObjectValid
        {
            get
            {
                if (AndroidXRHandBridge.Active)
                {
                    // AndroidXRHandBridge owns hand assignment:
                    // false = Wand/left hand, true = Brush/right hand.
                    return AndroidXRHandBridge.IsTracked(isBrush);
                }

                return device.isValid;
            }

            set
            {
                // Required by ControllerInfo API; tracking validity is read-only here.
            }
        }

        // ---------------------------------------------------------------------
        // Analog inputs
        // ---------------------------------------------------------------------

        public override Vector2 GetPadValue()
        {
            if (AndroidXRHandBridge.Active)
                return Vector2.zero;

            InputAction action = FindAction("PadAxis");
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        public override Vector2 GetThumbStickValue()
        {
            if (AndroidXRHandBridge.Active)
                return Vector2.zero;

            InputAction action = FindAction("ThumbAxis");
            return action != null ? action.ReadValue<Vector2>() : Vector2.zero;
        }

        public override void Update()
        {
            base.Update();

            // Hands have no controller touchpad; don't query controller actions.
            if (AndroidXRHandBridge.Active)
            {
                padAxisPrevious = Vector2.zero;
                return;
            }

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
            if (AndroidXRHandBridge.Active)
                return Vector2.zero;

            InputAction thumbAction = FindAction("ThumbAxis");

            if (thumbAction != null && thumbAction.inProgress)
            {
                Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                Vector2 stick = thumbAction.ReadValue<Vector2>();

                return new Vector2(
                    Mathf.Clamp(stick.x, range.x, range.y),
                    Mathf.Clamp(stick.y, range.x, range.y));
            }

            InputAction padAction = FindAction("PadAxis");
            InputAction padTouch = FindAction("PadTouch");

            if (padAction != null &&
                padTouch != null &&
                padTouch.IsPressed())
            {
                Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                Vector2 padAxisCurrent = padAction.ReadValue<Vector2>();

                if (padAxisPrevious == Vector2.zero)
                {
                    padAxisPrevious = padAxisCurrent;
                }

                Vector2 delta = padAxisCurrent - padAxisPrevious;
                padAxisPrevious = padAxisCurrent;

                delta.x = Mathf.Clamp(delta.x, range.x, range.y);
                delta.y = Mathf.Clamp(delta.y, range.x, range.y);

                return delta * kInputScrollScalar;
            }

            return Vector2.zero;
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
            // Grip is currently unused in Android XR hand mode.
            if (AndroidXRHandBridge.Active)
                return 0.0f;

#if OCULUS_SUPPORTED
            if (IsStylusActive())
            {
                return stylusState.cluster_front_value ? 1.0f : 0.0f;
            }
#endif

            InputAction action = FindAction("GripAxis");
            return action != null ? action.ReadValue<float>() : 0.0f;
        }

        // ---------------------------------------------------------------------
        // Trigger / drawing
        // ---------------------------------------------------------------------

        public override float GetTriggerRatio()
        {
            return GetTriggerValue();
        }

        public override float GetTriggerValue()
        {
            if (AndroidXRHandBridge.Active)
            {
                // Only the Brush/right hand has a draw trigger in hand mode.
                return AndroidXRHandBridge.Trigger(isBrush) ? 1.0f : 0.0f;
            }

#if OCULUS_SUPPORTED
            if (IsStylusActive())
            {
                return Math.Max(
                    stylusState.tip_value,
                    stylusState.cluster_middle_value);
            }
#endif

            InputAction action = FindAction("TriggerAxis");
            return action != null ? action.ReadValue<float>() : 0.0f;
        }

        // ---------------------------------------------------------------------
        // Touch state
        // ---------------------------------------------------------------------

        private bool MapVrTouch(VrInput input)
        {
            // There are no controller capacitive touch controls in custom hand mode yet.
            if (AndroidXRHandBridge.Active)
                return false;

            InputAction action;

            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                    action = FindAction("ThumbTouch");
                    return action != null && action.inProgress;

                case VrInput.Touchpad:
                    action = FindAction("PadTouch");
                    return action != null && action.inProgress;

                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    action = FindAction("PrimaryTouch");
                    return action != null && action.inProgress;

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    action = FindAction("SecondaryTouch");
                    return action != null && action.inProgress;
            }

            return false;
        }

        public override bool GetVrInputTouch(VrInput input)
        {
            return MapVrTouch(input);
        }

        // ---------------------------------------------------------------------
        // Button state
        // ---------------------------------------------------------------------

        private bool MapVrInput(VrInput input)
        {
            // Android XR hand mode currently exposes only the Brush trigger.
            if (AndroidXRHandBridge.Active)
            {
                if (input == VrInput.Trigger)
                {
                    return AndroidXRHandBridge.Trigger(isBrush);
                }

                return false;
            }

            // This logic is inferred from OculusControllerInfo.
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                {
                    InputAction action = FindAction("ThumbButton");
                    return action != null && action.IsPressed();
                }

                case VrInput.Touchpad:
                {
                    InputAction action = FindAction("PadButton");
                    return action != null && action.IsPressed();
                }

                case VrInput.Trigger:
#if OCULUS_SUPPORTED
                    if (IsStylusActive())
                    {
                        return stylusState.cluster_middle_value > 0.2f ||
                               stylusState.tip_value > 0.2f;
                    }
#endif
                    {
                        InputAction action = FindAction("TriggerAxis");
                        return action != null && action.IsPressed();
                    }

                case VrInput.Grip:
#if OCULUS_SUPPORTED
                    if (IsStylusActive())
                    {
                        return stylusState.cluster_front_value;
                    }
#endif
                    {
                        InputAction action = FindAction("GripAxis");
                        return action != null && action.IsPressed();
                    }

                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
#if OCULUS_SUPPORTED
                    if (IsStylusActive())
                    {
                        return stylusState.cluster_back_value;
                    }
#endif
                    {
                        InputAction action = FindAction("PrimaryButton");
                        return action != null && action.IsPressed();
                    }

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                {
                    InputAction action = FindAction("SecondaryButton");
                    return action != null && action.IsPressed();
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the current value of a VR input.
        /// </summary>
        public override bool GetVrInput(VrInput input)
        {
            return MapVrInput(input);
        }

        private bool MapVrInputPerFrame(VrInput input, bool down)
        {
            // In Android XR hand mode the custom right-hand gesture is the only
            // per-frame virtual controller button we currently expose.
            if (AndroidXRHandBridge.Active)
            {
                if (input == VrInput.Trigger)
                {
                    return down
                        ? AndroidXRHandBridge.TriggerDown(isBrush)
                        : AndroidXRHandBridge.TriggerUp(isBrush);
                }

                return false;
            }

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

            if (string.IsNullOrEmpty(selectedAction))
                return false;

            InputAction action = FindAction(selectedAction);

            if (action == null)
                return false;

            return down
                ? action.WasPressedThisFrame()
                : action.WasReleasedThisFrame();
        }

        /// <summary>
        /// Returns true if the specified input was activated this frame.
        /// </summary>
        public override bool GetVrInputDown(VrInput input)
        {
            return MapVrInputPerFrame(input, true);
        }

        /// <summary>
        /// Returns true if the specified input was released this frame.
        /// </summary>
        public override bool GetVrInputUp(VrInput input)
        {
            return MapVrInputPerFrame(input, false);
        }

        // ---------------------------------------------------------------------
        // Haptics
        // ---------------------------------------------------------------------

        public override void TriggerControllerHaptics(float seconds)
        {
            // Tracked hands don't provide controller haptics through this path.
            if (AndroidXRHandBridge.Active)
                return;

            if (!device.isValid)
                return;

            float durationSeconds =
                seconds * App.VrSdk.VrControls.HapticsDurationScale;

            device.SendHapticImpulse(
                0,
                App.VrSdk.VrControls.HapticsAmplitudeScale,
                durationSeconds);
        }
    }
}