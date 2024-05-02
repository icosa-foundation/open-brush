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
        private ReferencePanel m_ParentReferencePanel;

        // m_DataCount can get called early, so we need to init m_ParentReferencePanel ourselves
        private ReferencePanel GetParentReferencePanel()
        {
            if (m_ParentReferencePanel == null)
            {
                m_ParentReferencePanel = GetComponentInParent<ReferencePanel>();
            }
            return m_ParentReferencePanel;
        }

        protected override int m_DataCount => GetParentReferencePanel().CurrentSubdirectories.Length;

        // Misleadingly named:
        // ImageIcon actually refers to a button gameobject and button script
        protected override void RefreshIcon(ImageIcon icon, int iCatalog)
        {
            var btn = icon.m_IconScript as DirectoryChooserButton;
            var parent = GetParentReferencePanel();
            btn.SetDirectory(parent.CurrentSubdirectories[iCatalog]);
            btn.m_Popup = this;
            btn.m_Panel = parent;
        }

        protected override void InitIcon(ImageIcon icon)
        {
            icon.m_Valid = true;
        }
    }
}
