// Copyright 2021 The Tilt Brush Authors
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

namespace TiltBrush
{

    using UnityEngine;
    using UnityEditor;

    public class SetIcosaToken : EditorWindow
    {
        private static string key = "IcosaToken";
        private static string value = "";

        [MenuItem("Open Brush/Icosa/Set Login Token")]
        public static void ShowWindow()
        {
            value = PlayerPrefs.GetString(key);
            GetWindow<SetIcosaToken>("Set Icosa Login Token");
        }

        void OnGUI()
        {
            GUILayout.Label("Enter Token", EditorStyles.boldLabel);
            value = EditorGUILayout.TextField("Value", value);

            if (GUILayout.Button("Save"))
            {
                PlayerPrefs.SetString(key, value);
                PlayerPrefs.Save();
                Debug.Log($"Saved: {key} = {value}");
            }

            if (GUILayout.Button("Load"))
            {
                value = PlayerPrefs.GetString(key, "");
                Debug.Log($"Loaded: {key} = {value}");
            }

            if (GUILayout.Button("Delete"))
            {
                PlayerPrefs.DeleteKey(key);
                Debug.Log($"Deleted: {key}");
            }
        }
    }
}
