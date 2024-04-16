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
    class PopUpWindow_DirectoryChooser : PopUpWindow
    {
        public Transform m_DirectoryChooserButtonPrefab;
        public Transform m_DirectoryChooserButtonParent;
        private float m_ButtonSpacing = -0.15f;
        private float m_ButtonYLimit = -1.4f;

        public override void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            var parentPanel = rParent.GetComponent<ReferencePanel>();
            if (parentPanel != null)
            {
                var directories = parentPanel.CurrentSubdirectories;
                var currentPos = new Vector3(0, 0, 0);
                foreach (var directory in directories)
                {
                    var btnTransform = Instantiate(m_DirectoryChooserButtonPrefab, m_DirectoryChooserButtonParent);
                    btnTransform.gameObject.SetActive(true);
                    btnTransform.localPosition = currentPos;
                    btnTransform.localRotation = Quaternion.identity;
                    btnTransform.localScale = Vector3.one * 0.6f;
                    var btn = btnTransform.GetComponent<DirectoryChooserButton>();
                    btn.SetDirectory(directory);
                    btn.m_Popup = this;
                    btn.m_Panel = parentPanel;
                    currentPos.y += m_ButtonSpacing;
                    if (currentPos.y < m_ButtonYLimit) break;
                }
            }
        }
    }
}
