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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using TMPro;

namespace TiltBrush
{
    public class KeyboardPanel : BasePanel
    {
        private KeyboardUI m_KeyboardUI;
        [NonSerialized] public static string m_InitialText;
        [NonSerialized] public static string m_LastInput;
        [NonSerialized] public Action<KeyboardPanel> m_OnClose;

        public bool m_SanitizeFilename;

        protected override void Awake()
        {
            base.Awake();
            m_KeyboardUI = GetComponentInChildren<KeyboardUI>();
            m_KeyboardUI.KeyPressed += KeyPressed;
            BeginSession(m_InitialText);
        }

        public void BeginSession(string initialText)
        {
            m_KeyboardUI.Clear();
            m_KeyboardUI.AddConsoleContent(initialText);
            m_LastInput = initialText ?? string.Empty;
            m_OnClose = null;
        }

        private void OnDestroy()
        {
            CloseSession();
            m_KeyboardUI.KeyPressed -= KeyPressed;
        }

        private void CloseSession()
        {
            Action<KeyboardPanel> onClose = m_OnClose;
            m_OnClose = null;
            onClose?.Invoke(this);
        }

        private void KeyPressed(object sender, KeyboardKeyEventArgs e)
        {
            switch (e.Key.KeyType)
            {
                case KeyboardKeyType.Enter:
                    // Logic will been to be updated if we ever have a multi-line keyboard
                    m_LastInput = m_KeyboardUI.ConsoleContent;
                    CloseSession();
                    PanelManager.m_Instance.DismissNonCorePanel(PanelType.Keyboard);
                    SketchControlsScript.m_Instance.EatGazeObjectInput();
                    break;
            }

            if (m_SanitizeFilename)
            {
                m_KeyboardUI.SanitizeFilename();
            }
        }

    }
} // namespace TiltBrush
