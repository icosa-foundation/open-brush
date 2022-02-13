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
        
        public UnityXRControllerInfo(BaseControllerBehavior behavior, bool isLeftHand)
            : base(behavior)
        {
            device = InputDevices.GetDeviceAtXRNode(isLeftHand ? XRNode.LeftHand : XRNode.RightHand);
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
            return GetThumbStickValue();
        }

        public override Vector2 GetThumbStickValue()
        {
            if(device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumbStickValue))
            {
                return thumbStickValue;
            }

            return new Vector2();
        }

        public override Vector2 GetPadValueDelta()
        {
            return new Vector2(GetScrollXDelta(), GetScrollYDelta());
        }

        public override float GetScrollXDelta()
        {
            if(device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumbStickValue))
            {
                return thumbStickValue.x;
            }
            return 0.0f;
        }

        public override float GetScrollYDelta()
        {
            if(device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var thumbStickValue))
            {
                return thumbStickValue.y;
            }
            return 0.0f;
        }

        public override float GetGripValue()
        {
            if(device.TryGetFeatureValue(CommonUsages.grip, out var gripValue))
            {
                return gripValue;
            }
            return 0.0f;
        }

        public override float GetTriggerRatio()
        {
            return GetTriggerValue();
        }

        public override float GetTriggerValue()
        {
            if(device.TryGetFeatureValue(CommonUsages.trigger, out var triggerValue))
            {
                return triggerValue;
            }
            return 0.0f;
        }

        private bool MapVrTouch(VrInput input)
        {
            switch (input)
            {
                case VrInput.Button01:
                case VrInput.Button04:
                case VrInput.Button06:
                    if (device.TryGetFeatureValue(CommonUsages.primaryTouch, out var primaryTouch))
                    {
                        return primaryTouch;
                    }
                    break;

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button05:
                    if (device.TryGetFeatureValue(CommonUsages.secondaryTouch, out var secondaryTouch))
                    {
                        return secondaryTouch;
                    }
                    break;
                case VrInput.Directional:
                case VrInput.Thumbstick:
                case VrInput.Touchpad:
                    if (device.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out var primary2DAxisTouch))
                    {
                        return primary2DAxisTouch;
                    }
                    break;
            }
            return false;
        }

        public override bool GetVrInputTouch(VrInput input)
        {
            return MapVrTouch(input);
        }

        private bool MapVrInput (VrInput input)
        {
            // This logic is inferred from OculusControllerInfo
            switch (input)
            {
                case VrInput.Directional:
                case VrInput.Thumbstick:
                case VrInput.Touchpad:
                    if (device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out var primaryThumbstick))
                    {
                        return primaryThumbstick;
                    }
                    break;

                case VrInput.Trigger:
                    if (device.TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton))
                    {
                        return triggerButton;
                    }
                    break;

                case VrInput.Grip:
                    if (device.TryGetFeatureValue(CommonUsages.gripButton, out var gripButton))
                    {
                        return gripButton;
                    }
                    break;

                case VrInput.Button01:
                case VrInput.Button06:
                    if (device.TryGetFeatureValue(CommonUsages.primaryButton, out var primaryButton))
                    {
                        return primaryButton;
                    }
                    break;

                case VrInput.Button02:
                case VrInput.Button03:
                case VrInput.Button04:
                case VrInput.Button05:
                    if (device.TryGetFeatureValue(CommonUsages.secondaryButton, out var secondaryButton))
                    {
                        return secondaryButton;
                    }
                    break;
            }
            return false;
        }

        /// Returns the value of the specified button (level trigger).
        public override bool GetVrInput(VrInput input)
        {
            return MapVrInput(input);
        }

        /// Returns true if the specified button was just pressed (rising-edge trigger).
        public override bool GetVrInputDown(VrInput input)
        {
            // TODO:Mike - how to detect Down?
            return false; // MapVrInput(input);
        }

        /// Returns true if the specified input has just been deactivated (falling-edge trigger).
        public override bool GetVrInputUp(VrInput input)
        {
            // TODO:Mike - how to detect Up?
            return false; // MapVrInput(input);
        }
        public override void TriggerControllerHaptics(float seconds)
        {
            device.SendHapticImpulse(0, App.VrSdk.VrControls.HapticsAmplitudeScale, seconds);
        }
    }

} // namespace TiltBrush
