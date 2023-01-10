// Copyright 2023 The Tilt Brush Authors
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

using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    public class GenerateLuaAutocomplete : Editor
    {
        [MenuItem("Open Brush/API/Generate Lua Autocomplete File")]
        static void Generate()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }

            Script script = new Script();
            LuaManager.AutoCompleteEntries = new List<string>();
            LuaManager.Instance.SetStaticScriptContext(script);
            LuaManager.Instance.SetDynamicScriptContext(script);
            string filePath = Path.Combine("Assets/Resources/ScriptExamples", "__autocomplete.lua");
            File.WriteAllLines(filePath, LuaManager.AutoCompleteEntries);
            LuaManager.AutoCompleteEntries = null;
        }
    }
}
