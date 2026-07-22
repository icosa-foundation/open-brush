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

using UnityEngine;

namespace TiltBrush
{
    public class PortalActivationComponent : UIComponent
    {
        private PortalWidgetBase m_Portal;

        protected override void Awake()
        {
            m_Portal = GetComponentInParent<PortalWidgetBase>();
            base.Awake();
        }

        public override void ButtonPressed(RaycastHit rHitInfo)
        {
            m_Portal?.ActivateFromPointing();
        }

        public override void GainFocus()
        {
            m_Portal?.Activate(true);
        }

        public override void LostFocus()
        {
            m_Portal?.Activate(false);
        }

        public override void ManagerLostFocus()
        {
            LostFocus();
        }
    }
} // namespace TiltBrush
