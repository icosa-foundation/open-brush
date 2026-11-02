// Copyright 2026 The Open Brush Authors
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

using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Prevents Addressables' transient cached hash from causing source-control churn.
    /// </summary>
    internal class AddressableAssetSettingsSaveProcessor : AssetModificationProcessor
    {
        private const string kSettingsPath =
            "Assets/AddressableAssetsData/AddressableAssetSettings.asset";

        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path == kSettingsPath)
                {
                    ClearCachedHash();
                    break;
                }
            }

            return paths;
        }

        private static void ClearCachedHash()
        {
            Object settings = AssetDatabase.LoadMainAssetAtPath(kSettingsPath);
            if (settings == null)
            {
                return;
            }

            var serializedSettings = new SerializedObject(settings);
            SerializedProperty cachedHash = serializedSettings.FindProperty("m_currentHash");
            if (cachedHash != null && cachedHash.hash128Value.isValid)
            {
                cachedHash.hash128Value = default;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
