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
using System.Linq;
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
        public static DynValue CallFunctionIfExists(Script script, string fnName)
        {
            var fn = script.Globals.Get(fnName).Function;
            if (fn != null)
            {
                try
                {
                    return script.Call(fn);
                }
                catch (InterpreterException e)
                {
                    string msg = e.DecoratedMessage ?? e.Message;
                    msg = LuaManager.ReformatLuaError(script, fnName, msg);
                    Debug.LogError(msg);
                }
                catch (Exception e)
                {
                    string msg = e.Message;
                    msg = LuaManager.ReformatLuaError(script, fnName, msg);
                    Debug.LogError(msg);
                }
            }
            return null;
        }

        [MenuItem("Open Brush/API/Test Example Scripts")]
        static void DoTest()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }

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
                LuaManager.Instance.SetApiProperty(script, $"Tool.{LuaNames.ToolScriptVector}", Vector3.one);
                LuaManager.Instance.SetApiProperty(script, $"Tool.{LuaNames.ToolScriptRotation}", Quaternion.LookRotation(Vector3.one));
                TestAllKnown(script);
            }

            foreach (var example in backgroundScripts)
            {
                var script = example.Value;
                TestAllKnown(script);
            }

            void TestAllKnown(Script script)
            {
                LuaManager.Instance.InitScript(script);
                DynValue result;
                result = CallFunctionIfExists(script, LuaNames.Start);
                result = CallFunctionIfExists(script, LuaNames.Main);
                result = CallFunctionIfExists(script, LuaNames.End);
            }
        }
    }

    public class TestSpecificScript : EditorWindow
    {
        private int selectedIndex = 0;
        private string inputText = "";

        [MenuItem("Open Brush/API/Test Specific Script")]
        static void Init()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return;
            }
            TestSpecificScript window = (TestSpecificScript)GetWindow(typeof(TestSpecificScript));
            window.Show();
        }

        void OnGUI()
        {
            if (!Application.isPlaying) return;

            EditorGUILayout.BeginVertical();

            string[] scriptNames = LuaManager.Instance.GetScriptNames(LuaApiCategory.ToolScript).ToArray();
            selectedIndex = EditorGUILayout.Popup("Dropdown", selectedIndex, scriptNames);

            if (GUILayout.Button("Submit"))
            {
                SubmitDropDownValue(scriptNames[selectedIndex]);
            }

            inputText = EditorGUILayout.TextField("Input Text", inputText);

            if (Event.current.type == EventType.KeyDown
                && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == "TextField")
            {
                SubmitTextValue(inputText);
                Event.current.Use(); // Consume this event, we don't want to trigger other behavior with return key.
            }

            if (GUILayout.Button("Submit Text"))
            {
                SubmitTextValue(inputText);
            }

            EditorGUILayout.EndVertical();
        }

        void SubmitTextValue(string cmd)
        {
            Script script = new Script();
            LuaManager.Instance.InitScriptOnce(script);
            var result = script.DoString($"return {cmd}");
            Debug.Log(result);
        }

        void SubmitDropDownValue(string value)
        {
            var script = LuaManager.Instance.Scripts[LuaApiCategory.ToolScript][value];
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.ScriptedTool);
            LuaManager.Instance.SetActiveScriptByName(LuaApiCategory.ToolScript, value);

            var startPoint = Vector3.up;
            var endPoint = Vector3.up + Vector3.one;
            LuaManager.Instance.SetApiProperty(script, $"Tool.{LuaNames.ToolScriptVector}", endPoint - startPoint);
            LuaManager.Instance.SetApiProperty(script, $"Tool.{LuaNames.ToolScriptRotation}", Quaternion.LookRotation(endPoint - startPoint));
            LuaManager.Instance.DoToolScript(LuaNames.Main, TrTransform.T(startPoint), TrTransform.T(endPoint));
        }
    }

}
