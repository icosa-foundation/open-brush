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

namespace TiltBrush
{
    public class UnityXRControllerInfo : ControllerInfo
    {
        private InputDevice device;
        private readonly UnityXRInputAction actions;

        private Vector2 padAxisPrevious = new Vector2();
        private const float kInputScrollScalar = 0.5f;

        public UnityXRControllerInfo(BaseControllerBehavior behavior, bool isLeftHand)
            : base(behavior)
        {
            // XRController controller = isLeftHand ? XRController.leftHand : XRController.rightHand;
            // foreach ( var d in controller.allControls)
            // {
            //     Debug.Log(d.name);
            // }
            //asdasd

            device = InputDevices.GetDeviceAtXRNode(isLeftHand ? XRNode.LeftHand : XRNode.RightHand);
            actions = new UnityXRInputAction();
            if (true)
            {
                actions.Brush.Enable();
            }
        }

        public override bool IsTrackedObjectValid
        {
            get
            {
                return device.isValid;
            }
            set
            {

            }
        }

        public override Vector2 GetPadValue()
        {
            //return actions.Brush.PadAxis.ReadValue<Vector2>();
            return GetThumbStickValue();
        }

        public override Vector2 GetThumbStickValue()
        {
            return actions.Brush.PadAxis.ReadValue<Vector2>();
            // if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumbStickValue))
            // {
            //     return thumbStickValue;
            // }

            // return new Vector2();
        }

        public override Vector2 GetPadValueDelta()
        {
            if (false)
            {
                if (actions.Brush.PadAxis.inProgress)
                {
                    Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                    Vector2 stick = actions.Brush.PadAxis.ReadValue<Vector2>();
                    return new Vector2(Mathf.Clamp(stick.x, range.x, range.y), Mathf.Clamp(stick.y, range.x, range.y));
                }
            }
            else
            {
                if (!actions.Brush.PadAxis.inProgress)
                {
                    padAxisPrevious = Vector2.zero;
                    return padAxisPrevious;
                }

                Vector2 range = App.VrSdk.VrControls.TouchpadActivationRange;
                Vector2 padAxisCurrent = actions.Brush.PadAxis.ReadValue<Vector2>();

                if (padAxisPrevious == Vector2.zero)
                {
                    padAxisPrevious = padAxisCurrent;
                }

                var delta = padAxisCurrent - padAxisPrevious;
                padAxisPrevious = padAxisCurrent;

                delta.x = Mathf.Clamp(delta.x, range.x, range.y);
                delta.y = Mathf.Clamp(delta.y, range.x, range.y);
                return delta * kInputScrollScalar;

                // var newState = actions.Brush.PadAxis.ReadValue<Vector2>();
                // padAxisState = newState;
                // return delta;
            }
            return Vector2.zero;
        }

        public override float GetScrollXDelta()
        {
            return GetPadValueDelta().x;
            // if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumbStickValue))
            // {
            //     return thumbStickValue.x;
            // }
            // return 0.0f;
        }

        public override float GetScrollYDelta()
        {
            return GetPadValueDelta().y;
            // if (device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumbStickValue))
            // {
            //     return thumbStickValue.y;
            // }
            // return 0.0f;
        }

        public override float GetGripValue()
        {
            return actions.Brush.GripAxis.ReadValue<float>();
            // if (device.TryGetFeatureValue(CommonUsages.grip, out var gripValue))
            // {
            //     return gripValue;
            // }
            // return 0.0f;
        }

        public override float GetTriggerRatio()
        {
            return GetTriggerValue();
        }

        public override float GetTriggerValue()
        {
            return actions.Brush.TriggerAxis.ReadValue<float>();
            // if (device.TryGetFeatureValue(CommonUsages.trigger, out var triggerValue))
            // {
            //     return triggerValue;
            // }
            // return 0.0f;
        }

        private bool MapVrTouch(VrInput input)
        {
            switch (input)
            {
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    return actions.Brush.PrimaryTouch.inProgress;
                // if (device.TryGetFeatureValue(CommonUsages.primaryTouch, out var primaryTouch))
                // {
                //     return primaryTouch;
                // }
                // break;

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    return actions.Brush.SecondaryTouch.inProgress;
                // if (device.TryGetFeatureValue(CommonUsages.secondaryTouch, out var secondaryTouch))
                // {
                //     return secondaryTouch;
                // }
                // break;
                case VrInput.Directional:
                case VrInput.Thumbstick:
                case VrInput.Touchpad:
                    return actions.Brush.PadTouch.inProgress;
                    // if (device.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out var primary2DAxisTouch))
                    // {
                    //     return primary2DAxisTouch;
                    // }
                    // break;
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
                case VrInput.Touchpad:
                    return actions.Brush.PadButton.IsPressed();
                // if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var primaryThumbstick))
                // {
                //     return primaryThumbstick;
                // }
                // break;

                case VrInput.Trigger:
                    return actions.Brush.TriggerButton.IsPressed();
                // if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton))
                // {
                //     return triggerButton;
                // }
                // break;

                case VrInput.Grip:
                    return actions.Brush.GripButton.IsPressed();
                // if (device.TryGetFeatureValue(CommonUsages.gripButton, out var gripButton))
                // {
                //     return gripButton;
                // }
                // break;

                case VrInput.Button01:
                case VrInput.Button06:
                    return actions.Brush.PrimaryButton.IsPressed();
                // if (device.TryGetFeatureValue(CommonUsages.primaryButton, out var primaryButton))
                // {
                //     return primaryButton;
                // }
                // break;

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button04:
                case VrInput.Button05:
                    return actions.Brush.SecondaryButton.IsPressed();
                    // if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondaryButton))
                    // {
                    //     return secondaryButton;
                    // }
                    // break;
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
            UnityEngine.InputSystem.InputAction selectedAction = null;
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                case VrInput.Touchpad:
                    selectedAction = actions.Brush.PadButton;
                    break;
                // if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var primaryThumbstick))
                // {
                //     return primaryThumbstick;
                // }
                // break;

                case VrInput.Trigger:
                    selectedAction = actions.Brush.TriggerButton;
                    break;
                // if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton))
                // {
                //     return triggerButton;
                // }
                // break;

                case VrInput.Grip:
                    selectedAction = actions.Brush.GripButton;
                    break;
                // if (device.TryGetFeatureValue(CommonUsages.gripButton, out var gripButton))
                // {
                //     return gripButton;
                // }
                // break;

                case VrInput.Button01:
                case VrInput.Button06:
                    selectedAction = actions.Brush.PrimaryButton;
                    break;
                // if (device.TryGetFeatureValue(CommonUsages.primaryButton, out var primaryButton))
                // {
                //     return primaryButton;
                // }
                // break;

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button04:
                case VrInput.Button05:
                    selectedAction = actions.Brush.SecondaryButton;
                    break;
                    // if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondaryButton))
                    // {
                    //     return secondaryButton;
                    // }
                    // break;
            }

            if (selectedAction != null)
            {
                return down ? selectedAction.WasPressedThisFrame() : selectedAction.WasReleasedThisFrame();
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
            device.SendHapticImpulse(0, App.VrSdk.VrControls.HapticsAmplitudeScale, seconds);
        }


    }

} // namespace TiltBrush
