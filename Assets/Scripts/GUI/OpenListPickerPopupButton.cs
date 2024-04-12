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

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace TiltBrush
{
    public class OpenListPickerPopupButton : BaseButton
    {
        public Transform m_ListPickerPopup;
        public string m_PropertyName;
        public List<string> m_Items;
        public int ItemIndex { get; set; }
        [SerializeField] private UnityEvent<OpenListPickerPopupButton> m_AfterPopupAction;

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

        protected override void OnButtonPressed()
        {
            BasePanel panel = m_Manager.GetPanelForPopUps();
            if (panel != null)
            {
                float zOffset = App.Config.m_SdkMode == SdkMode.Monoscopic ? 0.3f : -0.3f;
                panel.CreatePopUp(
                    m_ListPickerPopup.gameObject,
                    transform.position + (transform.rotation * Vector3.forward * zOffset),
                    true, true
                );
                ResetState();
            }

            var popup = panel.PanelPopUp as StringItemPickerPopup;
            if (popup != null)
            {
                popup.ActiveItemIndex = ItemIndex;
                popup.m_OpenerButton = this;
                popup.RefreshPage();
            }
        }

        public void OnItemSelected(int itemIndex)
        {
            ItemIndex = itemIndex;
            ButtonLabel = m_Items[itemIndex];
            m_AfterPopupAction?.Invoke(this);
        }
    }
}
