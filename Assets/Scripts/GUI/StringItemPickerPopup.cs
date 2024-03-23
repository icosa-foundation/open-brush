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

using System;
using UnityEngine;

namespace TiltBrush
{

    public class StringItemPickerPopup : PagingPopUpWindow
    {
        [NonSerialized] public int ActiveItemIndex;
        [NonSerialized] public OpenListPickerPopupButton m_OpenerButton;
        protected override int m_DataCount => m_OpenerButton != null ? m_OpenerButton.m_Items.Count : 0;

        protected override void InitIcon(ImageIcon icon)
        {
            icon.m_Valid = true;
        }

        protected override void RefreshIcon(ImageIcon icon, int iconIndex)
        {
            var itemButton = icon.m_IconScript as StringPickerItemButton;
            string text = m_OpenerButton?.m_Items[iconIndex];
            itemButton.SetPreset(null, text, iconIndex);
            itemButton.SetButtonSelected(false);
            itemButton.m_OnItemSelected = OnItemSelected;
        }

        private void OnItemSelected(int itemIndex)
        {
            int iconIndexInPage = itemIndex % m_IconCountNavPage;
            ActiveItemIndex = itemIndex;
            var iconButton = m_Icons[iconIndexInPage].m_IconScript;
            iconButton.SetButtonSelected(true);
            m_OpenerButton.OnItemSelected(itemIndex);
            RequestClose();
        }

        public override void Init(GameObject rParent, string sText)
        {
            // Set this early so that Init can access parent to get m_DataCount
            m_ParentPanel = rParent.GetComponent<BasePanel>();

            if (ActiveItemIndex >= 0)
            {
                m_RequestedPageIndex = ActiveItemIndex / m_IconCountNavPage;
            }

            base.Init(rParent, sText);

            // Set active icon
            int activeIconIndex = ActiveItemIndex % m_IconCountNavPage;
            var iconButton = m_Icons[activeIconIndex].m_IconScript;
            iconButton.SetButtonSelected(true);
        }
    }
} // namespace TiltBrush
