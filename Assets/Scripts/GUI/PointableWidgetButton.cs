// Copyright 2024 The Open Brush Authors
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
    /// Example UIComponent child for use inside a PointableGrabWidget.
    /// Place this on a child GameObject that has a Collider.
    /// It will self-register into the parent UIComponentManager on Awake.
    public class PointableWidgetButton : BaseButton
    {
        override protected void Awake()
        {
            base.Awake(); // Walks up hierarchy, finds UIComponentManager, registers self.
        }

        override public void ButtonPressed(RaycastHit rHitInfo)
        {
            base.ButtonPressed(rHitInfo);
            // React to a point-and-activate press here.
        }

        override public void GainFocus()
        {
            base.GainFocus();
            // Ray entered this button's collider.
        }

        override public void LostFocus()
        {
            base.LostFocus();
            // Ray left this button's collider.
        }
    }
} // namespace TiltBrush
