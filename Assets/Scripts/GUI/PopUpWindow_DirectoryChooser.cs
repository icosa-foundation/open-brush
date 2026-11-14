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
    // Shows the current tab's directory tree with collapsible folders and the current
    // directory highlighted. Each row is a DirectoryChooserButton.
    class PopUpWindow_DirectoryChooser : PagingPopUpWindow
    {
        private ReferencePanel m_ParentReferencePanel;
        private ReferenceDirectoryTree m_Tree;

        // m_DataCount can get called early, so we need to init m_ParentReferencePanel ourselves
        private ReferencePanel GetParentReferencePanel()
        {
            if (m_ParentReferencePanel == null)
            {
                m_ParentReferencePanel = GetComponentInParent<ReferencePanel>();
            }
            return m_ParentReferencePanel;
        }

        private ReferenceDirectoryTree GetTree()
        {
            if (m_Tree == null)
            {
                // First access happens while the popup opens: expand down to the current directory.
                m_Tree = GetParentReferencePanel()?.GetDirectoryTree(revealCurrent: true);
            }
            return m_Tree;
        }

        protected override int m_DataCount => GetTree()?.Nodes.Count ?? 0;

        public override void Init(GameObject rParent, string sText)
        {
            m_Tree = null;
            base.Init(rParent, sText);

            // Open on the page that contains the current directory.
            var tree = GetTree();
            if (tree != null && tree.CurrentIndex >= 0 && m_DataCount > m_IconCountFullPage)
            {
                SetPageImmediate(tree.CurrentIndex / m_IconCountNavPage);
                RefreshPage();
            }
        }

        public void ToggleExpanded(string path)
        {
            var tree = GetTree();
            if (tree == null)
            {
                return;
            }
            tree.ToggleExpanded(path);
            m_Tree = GetParentReferencePanel().GetDirectoryTree(revealCurrent: false);

            // Rows above the toggled one are unchanged, so staying on this page keeps it in view.
            CalcNumPages();
            SetPageImmediate(Mathf.Clamp(m_PageIndex, 0, m_NumPages - 1));
            RefreshPage();
        }

        private void SetPageImmediate(int page)
        {
            m_PageIndex = page;
            m_RequestedPageIndex = page;
            m_BaseIndex = page * m_IconCountNavPage;
        }

        // Misleadingly named:
        // ImageIcon actually refers to a button gameobject and button script
        protected override void RefreshIcon(ImageIcon icon, int iCatalog)
        {
            var btn = icon.m_IconScript as DirectoryChooserButton;
            var parent = GetParentReferencePanel();
            btn.m_Popup = this;
            btn.m_Panel = parent;
            btn.SetNode(GetTree().Nodes[iCatalog]);
        }

        protected override void InitIcon(ImageIcon icon)
        {
            icon.m_Valid = true;
        }
    }
}
