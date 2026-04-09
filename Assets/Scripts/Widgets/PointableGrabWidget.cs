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

        override protected void OnUpdate()
        {
            // Animate scale with the show/hide ratio, matching PanelWidget behaviour.
            transform.localScale = m_BaseScale * GetShowRatio();

            if (m_PointingEnabled)
            {
                UpdatePointing();
            }
        }

        private void UpdatePointing()
        {
            // Mirrors how BasePanel.UpdatePanel() constructs its ray and reads input.
            Vector3 reticlePos = SketchControlsScript.m_Instance.GetUIReticlePos();
            Ray selectionRay = new Ray(reticlePos - transform.forward, transform.forward);
            bool inputValid = InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate);

            m_UIComponentManager.UpdateUIComponents(selectionRay, inputValid, GrabCollider);
        }

        override protected void OnUserBeginInteracting()
        {
            base.OnUserBeginInteracting();
            // Suppress pointing while the user is physically grabbing the widget,
            // matching WidgetSiblingBeginInteraction() in the PanelWidget pattern.
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
