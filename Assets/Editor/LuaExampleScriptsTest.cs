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

using System;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{

    // Attempts to call all known functions on all scripts.
    // Currently lots of false positives because we aren't calling the script as it's usually called.
    // But still a useful smoke test for syntax errors and name changes.
    public class LuaExampleScriptsTest : Editor
    {
        private static DynValue CallFunctionIfExists(Script script, string name)
        {
            var fn = script.Globals.Get(name).Function;
            if (fn != null)
            {
                try
                {
                    return script.Call(fn);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error calling {script.Globals.Get(LuaNames.ScriptNameString).String}.{name} :: {e.Message}");
                }
            }
            return null;
        }

        [MenuItem("Open Brush/API/Test Example Scripts")]
        static void DoTest()
        {
            var pointerScripts = LuaManager.Instance.Scripts[LuaApiCategory.PointerScript];
            var symmetryScripts = LuaManager.Instance.Scripts[LuaApiCategory.SymmetryScript];
            var toolScripts = LuaManager.Instance.Scripts[LuaApiCategory.ToolScript];
            var backgroundScripts = LuaManager.Instance.Scripts[LuaApiCategory.BackgroundScript];

            foreach (var example in pointerScripts)
            {
                var script = example.Value;
                TestAllKnown(script);
            }

            foreach (var example in symmetryScripts)
            {
                var script = example.Value;
                TestAllKnown(script);
            }

            foreach (var example in toolScripts)
            {
                var script = example.Value;
                TestAllKnown(script);
            }

            foreach (var example in backgroundScripts)
            {
                var script = example.Value;
                TestAllKnown(script);
            }

            void TestAllKnown(Script script)
            {
                DynValue result;
                result = CallFunctionIfExists(script, LuaNames.Start);
                result = CallFunctionIfExists(script, LuaNames.Main);
                result = CallFunctionIfExists(script, LuaNames.OnTriggerPressed);
                result = CallFunctionIfExists(script, LuaNames.WhileTriggerPressed);
                result = CallFunctionIfExists(script, LuaNames.OnTriggerReleased);
                result = CallFunctionIfExists(script, LuaNames.End);
            }
        }
    }
}
