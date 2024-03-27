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
using TiltBrush;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class ScriptUiNav : MonoBehaviour
{

    [SerializeField] private TextMeshPro m_TextMesh;
    [SerializeField] private LuaApiCategory m_ApiCategory;
    [SerializeField] private ActionButton m_CopyToUserScriptsFolder;

    public void Init()
    {
        m_TextMesh = GetComponentInChildren<TextMeshPro>();
        RefreshNavUi();
    }

    public void HandleChangeScript(int increment)
    {
        LuaManager.Instance.ChangeCurrentScript(m_ApiCategory, increment);
        RefreshNavUi();
    }

    private void RefreshNavUi()
    {
        var index = LuaManager.Instance.ActiveScripts[m_ApiCategory];
        var scriptName = LuaManager.Instance.GetScriptNames(m_ApiCategory)[index];
        var script = LuaManager.Instance.GetActiveScript(m_ApiCategory);
        m_TextMesh.text = scriptName;
        m_CopyToUserScriptsFolder.gameObject.SetActive(script.Globals.Get(LuaNames.IsExampleScriptBool).Boolean);
    }

    public void HandleCopyScriptToUserScriptFolder()
    {
        var copied = LuaManager.Instance.CopyActiveScriptToUserScriptFolder(m_ApiCategory);
        m_CopyToUserScriptsFolder.gameObject.SetActive(copied);
    }
}
