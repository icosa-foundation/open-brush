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

using System.Collections;
using System.Collections.Generic;
using TiltBrush;
using TMPro;
using UnityEngine;

public class ScriptUiNav : MonoBehaviour
{

    private TextMeshPro textMesh;
    public LuaManager.ApiCategories ApiCategory;
    public List<string> names;

    public void Start()
    {
        textMesh = GetComponentInChildren<TextMeshPro>();
        textMesh.text = LuaManager.Instance.GetScriptName(ApiCategory, 0);
        names = LuaManager.Instance.GetScriptNames(ApiCategory);
    }

    public void ChangeScript(int increment)
    {
        var text = LuaManager.Instance.ChangeCurrentScript(ApiCategory, increment);
        textMesh.text = text;
    }
}
