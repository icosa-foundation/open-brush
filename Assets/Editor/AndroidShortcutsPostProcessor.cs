// Copyright 2024 The Open Brush Authors
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
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace TiltBrush
{
    public class AndroidShortcutsPostProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            // Unity puts Assets/Plugins/Android/res/ into the launcher module, sibling of unityLibrary.
            string launcherPath = Path.Combine(Path.GetDirectoryName(unityLibraryPath), "launcher");
            string shortcutsPath = Path.Combine(launcherPath, "src", "main", "res", "xml", "shortcuts.xml");

            if (!File.Exists(shortcutsPath))
            {
                Debug.LogWarning($"AndroidShortcutsPostProcessor: shortcuts.xml not found at {shortcutsPath}");
                return;
            }

            string packageName = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            string content = File.ReadAllText(shortcutsPath);
            string patched = content.Replace("{{PACKAGE_NAME}}", packageName);

            if (patched == content)
            {
                Debug.LogWarning("AndroidShortcutsPostProcessor: {{PACKAGE_NAME}} placeholder not found in shortcuts.xml");
                return;
            }

            File.WriteAllText(shortcutsPath, patched);
            Debug.Log($"AndroidShortcutsPostProcessor: patched shortcuts.xml with package '{packageName}'");
        }
    }
}
