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

using TMPro;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Keeps TextMesh Pro's generated dynamic-atlas data out of source control.
    /// </summary>
    internal class TmpDynamicFontAssetSaveProcessor : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                // Package assets are owned by their package. Static fonts and dynamic fonts
                // which intentionally retain their generated data are also left alone.
                if (path.StartsWith("Assets/", System.StringComparison.Ordinal) &&
                    path.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    ClearGeneratedAtlasData(path);
                }
            }

            return paths;
        }

        private static void ClearGeneratedAtlasData(string path)
        {
            TMP_FontAsset fontAsset =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

            if (fontAsset == null ||
                (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic &&
                 fontAsset.atlasPopulationMode != AtlasPopulationMode.DynamicOS))
            {
                return;
            }

            // TMP exposes this setting only internally in UI 2.6. Read the serialized
            // setting rather than duplicating the policy with a list of font names.
            var serializedFont = new SerializedObject(fontAsset);
            if (!serializedFont.FindProperty("m_ClearDynamicDataOnBuild").boolValue)
            {
                return;
            }

            bool hasGeneratedAtlasData =
                fontAsset.glyphTable.Count > 0 ||
                fontAsset.characterTable.Count > 0 ||
                fontAsset.atlasTextures.Length > 1 ||
                (fontAsset.atlasTexture != null &&
                 (fontAsset.atlasTexture.width != 1 || fontAsset.atlasTexture.height != 1));

            if (hasGeneratedAtlasData)
            {
                // The public reset also clears OpenType features, unlike TMP's build
                // and shutdown reset. Preserve those settings while clearing the cache.
                string fontFeatures = JsonUtility.ToJson(fontAsset.fontFeatureTable);
                fontAsset.ClearFontAssetData(true);
                JsonUtility.FromJsonOverwrite(fontFeatures, fontAsset.fontFeatureTable);
                fontAsset.ReadFontAssetDefinition();
            }
        }
    }
}
