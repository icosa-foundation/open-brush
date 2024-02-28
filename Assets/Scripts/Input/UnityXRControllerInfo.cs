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

namespace TiltBrush
{
    public class UnityXRControllerInfo : ControllerInfo
    {
        private UnityEngine.XR.InputDevice device;
        private readonly UnityXRInputAction actionSet = new();

        private Vector2 padAxisPrevious = new Vector2();
        private const float kInputScrollScalar = 0.5f;

        private bool isBrush = false;

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
                default:
                    break;
            }

            actionSet.bindingMask = InputBinding.MaskByGroup(bindingGroup);
        }

        private InputAction FindAction(string actionName)
        {
            return actionSet.asset.FindActionMap($"{actionMap}").FindAction($"{actionName}");
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
            return GetThumbStickValue();
        }

        public override Vector2 GetThumbStickValue()
        {
            return FindAction("PadAxis").ReadValue<Vector2>();
        }

        public override void Update()
        {
            base.Update();

            if (!FindAction("PadTouch").inProgress)
            {
                padAxisPrevious = Vector2.zero;
            }
        }

        public override Vector2 GetPadValueDelta()
        {
            var action = FindAction("ThumbAxis");
            if (action.inProgress)
            {
                Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                Vector2 stick = action.ReadValue<Vector2>();
                return new Vector2(Mathf.Clamp(stick.x, range.x, range.y), Mathf.Clamp(stick.y, range.x, range.y));
            }
            else
            {
                action = FindAction("PadAxis");
                if (FindAction("PadTouch").IsPressed())
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
            return FindAction("GripAxis").ReadValue<float>();
        }

        public override float GetTriggerRatio()
        {
            return GetTriggerValue();
        }

        public override float GetTriggerValue()
        {
            return FindAction("TriggerAxis").ReadValue<float>();
        }

        private bool MapVrTouch(VrInput input)
        {
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                    return FindAction("ThumbTouch").inProgress;
                case VrInput.Touchpad:
                    return FindAction("PadTouch").inProgress;
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    return FindAction("PrimaryTouch").inProgress;
                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    return FindAction("SecondaryTouch").inProgress;


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
                    return FindAction("ThumbButton").IsPressed();
                case VrInput.Touchpad:
                    return FindAction("PadButton").IsPressed();
                case VrInput.Trigger:
                    return FindAction("TriggerAxis").IsPressed();
                case VrInput.Grip:
                    return FindAction("GripAxis").IsPressed();
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    return FindAction("PrimaryButton").IsPressed();
                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    return FindAction("SecondaryButton").IsPressed();
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
                return down ? FindAction(selectedAction).WasPressedThisFrame() : FindAction(selectedAction).WasReleasedThisFrame();
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
