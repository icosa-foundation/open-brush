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

namespace TiltBrush
{
    public class KeyboardPopUpWindow : OptionsPopUpWindow
    {
        private KeyboardUI m_KeyboardUI;
        [NonSerialized] public static string m_LastInput;

        public bool m_SanitizeFilename;

        void Awake()
        {
            m_KeyboardUI = GetComponentInChildren<KeyboardUI>();
            m_KeyboardUI.KeyPressed += KeyPressed;
        }

        override public void SetPopupCommandParameters(int commandParam, int commandParam2)
        {
            if (commandParam2 != (int)SketchSetType.User)
            {
                return;
            }
            var sketchSet = SketchCatalog.m_Instance.GetSet(SketchSetType.User) as FileSketchSet;
            var sceneFileInfo = sketchSet.GetSketchSceneFileInfo(commandParam);
            var currentName = Path.GetFileName(sceneFileInfo.FullPath);
            if (currentName.EndsWith(SaveLoadScript.TILT_SUFFIX))
            {
                currentName = currentName.Substring(0, currentName.Length - SaveLoadScript.TILT_SUFFIX.Length);
            }
            m_KeyboardUI.AddConsoleContent(currentName);
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
