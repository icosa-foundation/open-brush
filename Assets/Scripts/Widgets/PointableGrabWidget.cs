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
    /// A GrabWidget that also supports distance pointing interaction via UIComponentManager,
    /// without requiring a BasePanel sibling. Child UIComponents self-register on Awake via
    /// GetComponentInParent<UIComponentManager>(), so no manual wiring is needed.
    ///
    /// Minimal prefab setup:
    ///   - UIComponentManager on the same GameObject (enforced by RequireComponent)
    ///   - A Collider on the same GameObject (serves both grab detection and pointing)
    ///   - Set m_ShowDuration to a non-zero value (e.g. 0.2) — GetShowRatio() divides by it
    ///   - Child GameObjects with UIComponent subclasses (e.g. BaseButton) and their own Colliders
    [RequireComponent(typeof(UIComponentManager))]
    public class PointableGrabWidget : GrabWidget
    {
        private UIComponentManager m_UIComponentManager;
        private Vector3 m_BaseScale;
        private bool m_PointingEnabled = true;

        override protected void Awake()
        {
            base.Awake();
            m_UIComponentManager = GetComponent<UIComponentManager>();
            m_BaseScale = transform.localScale;
        }

        // Subclasses that override OnUpdate MUST call base.OnUpdate() — unlike plain GrabWidget,
        // this method drives scale animation and pointing in addition to widget movement.
        override protected void OnUpdate()
        {
            base.OnUpdate();
            transform.localScale = m_BaseScale * GetShowRatio();

            if (m_PointingEnabled)
            {
                UpdatePointing();
            }
        }

        private void UpdatePointing()
        {
            // Ray origin offset by one unit behind the widget face so the ray travels forward
            // through the surface — mirrors BasePanel.UpdatePanel().
            Vector3 reticlePos = SketchControlsScript.m_Instance.GetUIReticlePos();
            Ray selectionRay = new Ray(reticlePos - transform.forward, transform.forward);
            bool inputValid = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);
            m_UIComponentManager.UpdateUIComponents(selectionRay, inputValid, GrabCollider);
        }

        override protected void OnUserBeginInteracting()
        {
            base.OnUserBeginInteracting();
            // Suppress pointing while grabbed so both systems don't fire simultaneously.
            m_PointingEnabled = false;
            m_UIComponentManager.ManagerLostFocus();
        }

        override protected void OnUserEndInteracting()
        {
            base.OnUserEndInteracting();
            m_PointingEnabled = true;
        }
    }
} // namespace TiltBrush
