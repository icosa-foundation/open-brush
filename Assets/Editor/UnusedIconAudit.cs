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

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Finds general icon textures with no serialized project dependency.
    /// Atlas catalog references are ignored because the atlas is a generated
    /// consumer of the icons rather than evidence that an icon is used.
    /// </summary>
    public static class UnusedIconAudit
    {
        private const string kIconFolder = "Assets/Resources/Icons";

        [MenuItem("Open Brush/Info/Find Unused General Icons")]
        public static void FindUnusedIcons()
        {
            string[] iconPaths = AssetDatabase.FindAssets("t:Texture2D", new[] { kIconFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var iconPathSet = new HashSet<string>(iconPaths, StringComparer.OrdinalIgnoreCase);
            var references = iconPaths.ToDictionary(
                path => path,
                _ => new List<string>(),
                StringComparer.OrdinalIgnoreCase);

            string[] projectPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            try
            {
                for (int i = 0; i < projectPaths.Length; ++i)
                {
                    string projectPath = projectPaths[i];
                    if (AssetDatabase.LoadAssetAtPath<IconTextureAtlasCatalog>(projectPath) != null)
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "Finding Unused General Icons",
                        projectPath,
                        (float)i / projectPaths.Length);

                    foreach (string dependency in AssetDatabase.GetDependencies(projectPath, false))
                    {
                        if (iconPathSet.Contains(dependency) &&
                            !string.Equals(projectPath, dependency, StringComparison.OrdinalIgnoreCase))
                        {
                            references[dependency].Add(projectPath);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var unused = references
                .Where(pair => pair.Value.Count == 0)
                .Select(pair => pair.Key)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Debug.LogFormat(
                "UNUSED_GENERAL_ICON_AUDIT: scanned {0} icons; {1} have no serialized references outside atlas catalogs.",
                iconPaths.Length,
                unused.Length);

            foreach (string path in unused)
            {
                Debug.LogFormat("UNUSED_GENERAL_ICON_AUDIT: {0}", path);
            }

            Debug.Log(
                "UNUSED_GENERAL_ICON_AUDIT: review candidates for Resources.Load-by-name or code-driven use before deleting them.");
        }
    }
}
#endif
