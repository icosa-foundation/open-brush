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

using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class EditableLabel : MonoBehaviour
{
    public TextMeshPro m_Label;
    public UnityEvent<EditableLabel> m_OnEditConfirmed;
    public string m_LabelTag;
    private string m_LastTextInput;
    public GameObject m_ErrorIcon;

    public string LastTextInput
    {
        get { return m_LastTextInput; }
        set
        {
            m_LastTextInput = value;
        }
    }

    public void SetValue(string value)
    {
        LastTextInput = value;
        GetComponentInChildren<TextMeshPro>().text = value;
    }

    public void HandleEditFinished()
    {
        m_OnEditConfirmed.Invoke(this);
    }

    public void SetError(bool hasError)
    {
        m_ErrorIcon.SetActive(hasError);
    }
}
