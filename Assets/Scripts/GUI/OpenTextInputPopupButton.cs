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

using TMPro;
using UnityEngine;
using UnityEngine.Events;
namespace TiltBrush
{
    public class OpenTextInputPopupButton : OptionButton
    {
        public string m_PropertyName;
        [SerializeField] private Transform m_KeyboardPopup;
        [SerializeField] private UnityEvent<OpenTextInputPopupButton> m_BeforePopupAction;
        [SerializeField] private UnityEvent<OpenTextInputPopupButton> m_AfterPopupAction;
        private string m_ButtonLabel;
        public string ButtonLabel
        {
            get { return m_ButtonLabel; }
            set
            {
                GetComponentInChildren<TextMeshPro>().text = value;
                m_ButtonLabel = value;
            }
        }
        protected override void Awake()
        {
            base.Awake();
            SetButtonSelected(false);
        }

        override protected void OnButtonPressed()
        {
            m_BeforePopupAction?.Invoke(this);
            BasePanel panel = m_Manager.GetPanelForPopUps();
            if (panel != null)
            {
                float zOffset = App.Config.m_SdkMode == SdkMode.Monoscopic ? 0.3f : -0.3f;
                panel.CreatePopUp(
                    m_KeyboardPopup.gameObject,
                    transform.position + Vector3.forward * zOffset,
                    true, true
                );
                ResetState();
            }

            var popup = panel.PanelPopUp as KeyboardPopUpWindow;
            if (popup != null)
            {
                KeyboardPopUpWindow.m_InitialText = ButtonLabel;
                popup.m_OnClose += OnKeyboardClose;
            }
        }

        private void OnKeyboardClose()
        {
            ButtonLabel = KeyboardPopUpWindow.m_LastInput;
            m_AfterPopupAction?.Invoke(this);
        }
    }
}
