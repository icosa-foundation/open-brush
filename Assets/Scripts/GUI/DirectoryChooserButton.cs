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
using System.IO;
using UnityEngine;
using TMPro;

namespace TiltBrush
{
    class DirectoryChooserButton : BaseButton
    {
        public TextMeshPro m_TextLabel;
        [NonSerialized] public PopUpWindow_DirectoryChooser m_Popup;
        [NonSerialized] public ReferencePanel m_Panel;

        private DirectoryInfo m_DirectoryInfo;
        private string m_Label;

        public void SetDirectory(string directory)
        {
            m_DirectoryInfo = new DirectoryInfo(directory);
            m_Label = m_DirectoryInfo.Name;
            m_TextLabel.text = m_Label;
        }

        public override void ButtonPressed(RaycastHit rHitInfo)
        {
            base.ButtonPressed(rHitInfo);
            m_Panel.CloseActivePopUp(false);
            m_Panel.ChangeDirectoryForCurrentTab(m_DirectoryInfo.FullName);
        }

        protected override void SetMaterialColor(Color rColor)
        {
            // m_TextLabel.color = rColor;
        }
    }
}
