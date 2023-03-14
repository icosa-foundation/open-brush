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

using System.IO;
using TiltBrush;
using TMPro;
using UnityEngine;

public class ScriptUiNav : MonoBehaviour
{

    private TextMeshPro textMesh;
    public LuaManager.ApiCategory ApiCategory;
    public ActionButton m_CopyToUserScriptsFolder;

    public void Init()
    {
        textMesh = GetComponentInChildren<TextMeshPro>();
        RefreshNavUi();
    }

    public void ChangeScript(int increment)
    {
        LuaManager.Instance.ChangeCurrentScript(ApiCategory, increment);
        RefreshNavUi();
    }

    private void RefreshNavUi()
    {
        var index = LuaManager.Instance.ActiveScripts[ApiCategory];
        var scriptName = LuaManager.Instance.GetScriptNames(ApiCategory)[index];
        var script = LuaManager.Instance.GetActiveScript(ApiCategory);
        textMesh.text = scriptName;
        m_CopyToUserScriptsFolder.gameObject.SetActive(script.Globals.Get(LuaNames.IsExampleScriptBool).Boolean);
    }

    public void CopyScriptToUserScriptFolder()
    {
        var index = LuaManager.Instance.ActiveScripts[ApiCategory];
        var scriptName = LuaManager.Instance.GetScriptNames(ApiCategory)[index];
        var originalFilename = $"{ApiCategory}.{scriptName}";
        var newFilename = Path.Join(ApiManager.Instance.UserScriptsPath(), $"{originalFilename}.lua");
        if (!File.Exists(newFilename))
        {
            FileUtils.WriteTextFromResources($"LuaScriptExamples/{originalFilename}", newFilename);
            m_CopyToUserScriptsFolder.gameObject.SetActive(false);
        }
    }
}
