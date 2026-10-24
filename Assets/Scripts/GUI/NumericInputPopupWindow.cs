// Copyright 2022 The Tilt Brush Authors
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
using TMPro;

namespace TiltBrush
{

    public class NumericInputPopupWindow : PopUpWindow
    {
        public TMP_InputField m_InputField;

        private Action<string> m_OnGenericConfirm;

        public void Initialize(string initialValue, Action<string> onConfirm)
        {
            m_InputField.text = initialValue;
            m_OnGenericConfirm = onConfirm;
        }

        public void HandleKeypress(string input)
        {
            m_InputField.text += input;
        }

        public void HandleCursorPress(int offset)
        {
            // TODO
        }

        public void HandleConfirmCancel(bool confirm)
        {
            if (confirm)
            {
                if (m_OnGenericConfirm != null)
                {
                    m_OnGenericConfirm(m_InputField.text);
                }
                else
                {
                    var popupButton = m_OnClose?.Target as PopupButton;
                    var label = popupButton?.GetComponentInParent<EditableLabel>();
                    if (label != null)
                    {
                        label.LastTextInput = m_InputField.text;
                    }
                }
            }
            RequestClose();
        }

    }
} // namespace TiltBrush
