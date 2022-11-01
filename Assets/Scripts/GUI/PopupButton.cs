// Copyright 2022 The Tilt Brush Authors
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
using UnityEngine.Events;

namespace TiltBrush
{

    public class PopupButton : BaseButton
    {
        [SerializeField] protected bool m_CenterPopupOnButton;
        [SerializeField] protected Vector3 m_PopupOffset;
        [SerializeField] protected string m_PopupText = "";
        public SketchControlsScript.GlobalCommands command;
        public UnityEvent m_OnConfirm;

        protected override void OnButtonPressed()
        {
            if (!m_Manager) return;

            BasePanel panel = m_Manager.GetPanelForPopUps();
            if (panel != null)
            {
                panel.CreatePopUp(command, -1, -1, m_PopupOffset, m_PopupText, HandleConfirm);
                if (m_CenterPopupOnButton)
                {
                    panel.PositionPopUp(transform.position +
                        transform.forward * panel.PopUpOffset +
                        panel.transform.TransformVector(m_PopupOffset));
                }
                ResetState();
            }
        }

        private void HandleConfirm()
        {
            m_OnConfirm.Invoke();
        }
    }
} // namespace TiltBrush
