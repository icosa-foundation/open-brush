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

using TiltBrush;
using TMPro;
using UnityEngine;

public class ScriptUiNavMultiple : MonoBehaviour
{

    [SerializeField] private TextMeshPro m_TextMesh;
    public ActionButton m_CopyToUserScriptsFolder;
    public ToggleButton m_ToggleBackgroundScript;
    private int m_CurrentBackgroundScriptIndex;
    private string m_CurrentBackgroundScriptName;

    public void Init()
    {
        UpdateUi();
    }

    private void UpdateUi()
    {
        m_CurrentBackgroundScriptName = LuaManager.Instance.GetScriptNames(LuaApiCategory.BackgroundScript)[m_CurrentBackgroundScriptIndex];
        var script = LuaManager.Instance.Scripts[LuaApiCategory.BackgroundScript][m_CurrentBackgroundScriptName];
        m_TextMesh.text = m_CurrentBackgroundScriptName;
        m_CopyToUserScriptsFolder.gameObject.SetActive(script.Globals.Get(LuaNames.IsExampleScriptBool).Boolean);
        bool isActive = LuaManager.Instance.IsBackgroundScriptActive(m_CurrentBackgroundScriptName);
        m_ToggleBackgroundScript.IsToggledOn = isActive;
        m_TextMesh.text = m_CurrentBackgroundScriptName;
        LuaManager.Instance.ActiveScripts[LuaApiCategory.BackgroundScript] = m_CurrentBackgroundScriptIndex;
    }

    public void HandleChangeScript(int increment)
    {
        int backgroundScriptCount = LuaManager.Instance.Scripts[LuaApiCategory.BackgroundScript].Count;
        if (backgroundScriptCount == 0) return;
        int index = (int)Mathf.Repeat(m_CurrentBackgroundScriptIndex + increment, backgroundScriptCount);
        m_CurrentBackgroundScriptIndex = index;
        UpdateUi();
    }

    public void HandleCopyScriptToUserScriptFolder()
    {
        var copied = LuaManager.Instance.CopyActiveScriptToUserScriptFolder(LuaApiCategory.BackgroundScript);
        m_CopyToUserScriptsFolder.gameObject.SetActive(copied);
    }

    public void HandleToggleScript()
    {
        LuaManager.Instance.ToggleBackgroundScript(m_CurrentBackgroundScriptName);
    }
}
