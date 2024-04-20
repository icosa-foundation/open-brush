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
    class PopUpWindow_DirectoryChooser : PagingPopUpWindow
    {
        private float m_ButtonSpacing = -0.15f;
        private float m_ButtonYLimit = -1.4f;

        protected override int m_DataCount
        {
            get
            {
                // This gets called early, so we need to init m_ParentPanel ourselves
                if (m_ParentPanel == null)
                {
                    m_ParentPanel = GetComponentInParent<BasePanel>();
                }
                var parentPanel = m_ParentPanel as ReferencePanel;
                return parentPanel.CurrentSubdirectories.Length;
            }
        }

        protected override void RefreshIcon(ImageIcon icon, int iCatalog)
        {
            // Misleadingly named:
            // ImageIcon actually refers to a button gameobject and button script
            var parentPanel = m_ParentPanel as ReferencePanel;
            var btn = icon.m_IconScript as DirectoryChooserButton;
            btn.SetDirectory(parentPanel.CurrentSubdirectories[iCatalog]);
            btn.m_Popup = this;
            btn.m_Panel = parentPanel;
        }

        protected override void InitIcon(ImageIcon icon)
        {
            icon.m_Valid = true;
        }
    }
}
