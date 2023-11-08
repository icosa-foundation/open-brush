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
using UnityEngine.Events;

namespace TiltBrush
{
    public class IcosaLoginKeyboardController : MonoBehaviour
    {
        private KeyboardUI m_KeyboardUI;
        [NonSerialized] public static string m_InitialText;
        public UnityEvent<string> OnSubmit;

        void Awake()
        {
            m_KeyboardUI = GetComponentInChildren<KeyboardUI>(includeInactive: true);
            m_KeyboardUI.KeyPressed += KeyPressed;
            m_KeyboardUI.AddConsoleContent(m_InitialText);
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
                    OnSubmit.Invoke(m_KeyboardUI.ConsoleContent);
                    break;
            }
        }

        public void Clear()
        {
            m_KeyboardUI.Clear();
        }
    }
} // namespace TiltBrush
