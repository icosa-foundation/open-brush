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

using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Management;
using InputDevice = UnityEngine.XR.InputDevice;

namespace TiltBrush
{
    /// <summary>
    /// Queries for hand tracking input.
    ///
    /// Hands replace the controllers, so hand-driven sessions have none of the sticks, triggers
    /// or touchpads the rest of the input code relies on. Anything that has to stay usable
    /// without those inputs (currently the brush size tray) asks this class rather than looking
    /// for a controller.
    /// </summary>
    public static class HandTrackingUtils
    {
        /// <summary>
        /// True while a hand is tracked.
        ///
        /// Runtimes stop tracking the hands when the controllers are picked up, so this reads as
        /// "hands are being used instead of controllers".
        /// </summary>
        public static bool IsActive
        {
            get
            {
                XRHandSubsystem hands = XRGeneralSettings.Instance?.Manager?.activeLoader?
                    .GetLoadedSubsystem<XRHandSubsystem>();

                if (hands != null && hands.running &&
                    (hands.leftHand.isTracked || hands.rightHand.isTracked))
                {
                    return true;
                }

                // Runtimes that don't run XR Hands can still expose the hands as tracked devices.
                return HandIsTrackedAtNode(XRNode.LeftHand) || HandIsTrackedAtNode(XRNode.RightHand);
            }
        }

        private static bool HandIsTrackedAtNode(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            return device.isValid &&
                (device.characteristics & InputDeviceCharacteristics.HandTracking) != 0;
        }
    }

} // namespace TiltBrush
