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

namespace TiltBrush
{
    public class UnityXRControllerInfo : ControllerInfo
    {
        public UnityXRControllerInfo(BaseControllerBehavior behavior)
            : base(behavior)
        {

        }

        public override bool IsTrackedObjectValid
        {
            get
            {
                return false;
            }
            set
            {

            }
        }

        public override float GetTriggerRatio()
        {
            return 0.0f;
        }

        public override Vector2 GetPadValue()
        {
            return new Vector2();
        }

        public override Vector2 GetThumbStickValue()
        {
            return new Vector2();
        }

        public override Vector2 GetPadValueDelta()
        {
            return new Vector2();
        }

        public override float GetGripValue()
        {
            return 0.0f;
        }

        public override float GetTriggerValue()
        {
            return 0.0f;
        }

        public override float GetScrollXDelta()
        {
            return 0.0f;
        }

        public override float GetScrollYDelta()
        {
            return 0.0f;
        }

        public override bool GetVrInputTouch(VrInput input)
        {
            return false;
        }

        /// Returns the value of the specified button (level trigger).
        public override bool GetVrInput(VrInput input)
        {
            return false;
        }

        /// Returns true if the specified button was just pressed (rising-edge trigger).
        public override bool GetVrInputDown(VrInput input)
        {
            return false;
        }

        /// Returns true if the specified input has just been deactivated (falling-edge trigger).
        public override bool GetVrInputUp(VrInput input)
        {
            return false;
        }
        public override void TriggerControllerHaptics(float seconds)
        {

        }
    }

} // namespace TiltBrush
