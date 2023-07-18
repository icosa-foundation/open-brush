// Copyright 2023 The Open Brush Authors
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

namespace TiltBrush
{
    public class KeyboardPopUpWindow : OptionsPopUpWindow
    {
        private KeyboardUI m_KeyboardUI;
        [NonSerialized] public static string m_InitialText;
        [NonSerialized] public static string m_LastInput;

        public bool m_SanitizeFilename;

        void Awake()
        {
            m_KeyboardUI = GetComponentInChildren<KeyboardUI>();
            m_KeyboardUI.KeyPressed += KeyPressed;
        }

        override public void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            m_KeyboardUI.AddConsoleContent(m_InitialText);
            m_LastInput = m_InitialText;
        }

        private void OnDestroy()
        {
            m_KeyboardUI.KeyPressed -= KeyPressed;
        }

        private void KeyPressed(object sender, KeyboardKeyEventArgs e)
        {
            switch (e.Key.KeyType)
            {
                case KeyboardKeyType.Enter:
                    // Logic will been to be updated if we ever have a multi-line keyboard
                    m_LastInput = m_KeyboardUI.ConsoleContent;
                    if (m_ParentPanel)
                    {
                        m_ParentPanel.ResolveDelayedButtonCommand(true);
                    }
                    RequestClose(bForceClose: true);
                    break;
            }

            if (m_SanitizeFilename)
            {
                m_KeyboardUI.SantizeFilename();
            }
        }
    }
} // namespace TiltBrush
