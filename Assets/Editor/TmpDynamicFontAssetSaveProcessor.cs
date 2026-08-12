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

namespace TiltBrush
{
    /// <summary>
    /// Keeps TextMesh Pro's generated dynamic-atlas data out of source control.
    /// </summary>
    internal class TmpDynamicFontAssetSaveProcessor : AssetModificationProcessor
    {
        private const string kDynamicFallbackPath =
            "Assets/Fonts/NotoSansCJK-Light SDF.asset";

        private static string[] OnWillSaveAssets(string[] paths)
        {
            foreach (string path in paths)
            {
                if (path == kDynamicFallbackPath)
                {
                    ClearGeneratedAtlasData();
                    break;
                }
            }

            return paths;
        }

        private static void ClearGeneratedAtlasData()
        {
            TMP_FontAsset fontAsset =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(kDynamicFallbackPath);

            if (fontAsset == null ||
                fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                return;
            }

            bool hasGeneratedAtlasData =
                fontAsset.glyphTable.Count > 0 ||
                fontAsset.characterTable.Count > 0 ||
                (fontAsset.atlasTexture != null && fontAsset.atlasTexture.width > 1);

            if (hasGeneratedAtlasData)
            {
                fontAsset.ClearFontAssetData(true);
            }
        }
    }
}
