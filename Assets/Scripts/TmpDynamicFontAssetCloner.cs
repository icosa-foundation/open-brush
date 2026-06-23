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

using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TiltBrush
{
    // TextMeshPro mutates dynamic fallback font assets as glyphs are requested. Use transient
    // clones for serialized dynamic fallbacks so source-controlled assets remain unchanged.
    static class TmpDynamicFontAssetCloner
    {
        private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> m_Clones =
            new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private static readonly HashSet<TMP_FontAsset> m_RuntimeClones =
            new HashSet<TMP_FontAsset>();
        private static readonly FieldInfo m_AtlasTextureField = typeof(TMP_FontAsset).GetField(
            "m_AtlasTexture", BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            m_Clones.Clear();
            m_RuntimeClones.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            ReplaceLoadedFallbacks();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ReplaceLoadedFallbacks();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitializeEditor()
        {
            EditorApplication.delayCall += ReplaceLoadedFallbacks;
            EditorApplication.hierarchyChanged += ReplaceLoadedFallbacks;
            EditorApplication.projectChanged += ReplaceLoadedFallbacks;
        }
#endif

        public static void ReplaceLoadedFallbacks()
        {
            HashSet<TMP_FontAsset> visited = new HashSet<TMP_FontAsset>();
            foreach (TMP_FontAsset fontAsset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                ReplaceFallbacks(fontAsset, visited);
            }

            ReplaceFallbacks(TMP_Settings.fallbackFontAssets, visited);
        }

        private static void ReplaceFallbacks(TMP_FontAsset fontAsset, HashSet<TMP_FontAsset> visited)
        {
            if (fontAsset == null || !visited.Add(fontAsset))
            {
                return;
            }

            ReplaceFallbacks(fontAsset.fallbackFontAssetTable, visited);
        }

        private static void ReplaceFallbacks(
            List<TMP_FontAsset> fallbackFontAssets, HashSet<TMP_FontAsset> visited)
        {
            if (fallbackFontAssets == null)
            {
                return;
            }

            for (int i = 0; i < fallbackFontAssets.Count; i++)
            {
                TMP_FontAsset fallback = fallbackFontAssets[i];
                TMP_FontAsset clone = GetCloneIfNeeded(fallback);
                if (clone != null)
                {
                    fallbackFontAssets[i] = clone;
                    ReplaceFallbacks(clone, visited);
                }
                else
                {
                    ReplaceFallbacks(fallback, visited);
                }
            }
        }

        private static TMP_FontAsset GetCloneIfNeeded(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null ||
                fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                !IsPersistent(fontAsset))
            {
                return null;
            }

            if (!m_Clones.TryGetValue(fontAsset, out TMP_FontAsset clone) || clone == null)
            {
                clone = CloneDynamicFontAsset(fontAsset);
                m_Clones[fontAsset] = clone;
            }

            return clone;
        }

        private static TMP_FontAsset CloneDynamicFontAsset(TMP_FontAsset source)
        {
            TMP_FontAsset clone = Object.Instantiate(source);
            clone.name = source.name + " Runtime";
            clone.hideFlags = HideFlags.DontSave;
            m_RuntimeClones.Add(clone);

            Texture2D[] sourceTextures = source.atlasTextures;
            if (sourceTextures == null)
            {
                return clone;
            }

            Texture2D[] atlasTextures = new Texture2D[sourceTextures.Length];
            for (int i = 0; i < sourceTextures.Length; i++)
            {
                if (sourceTextures[i] == null)
                {
                    continue;
                }

                atlasTextures[i] = Object.Instantiate(sourceTextures[i]);
                atlasTextures[i].name = sourceTextures[i].name + " Runtime";
                atlasTextures[i].hideFlags = HideFlags.DontSave;
            }
            clone.atlasTextures = atlasTextures;
            m_AtlasTextureField?.SetValue(clone, null);

            if (source.material != null)
            {
                clone.material = Object.Instantiate(source.material);
                clone.material.name = source.material.name + " Runtime";
                clone.material.hideFlags = HideFlags.DontSave;
                if (atlasTextures.Length > 0 && atlasTextures[0] != null)
                {
                    clone.material.mainTexture = atlasTextures[0];
                }
            }

            return clone;
        }

        private static bool IsRuntimeClone(TMP_FontAsset fontAsset)
        {
            return m_RuntimeClones.Contains(fontAsset);
        }

        private static bool IsPersistent(Object obj)
        {
#if UNITY_EDITOR
            return EditorUtility.IsPersistent(obj);
#else
            TMP_FontAsset fontAsset = obj as TMP_FontAsset;
            return fontAsset == null || !IsRuntimeClone(fontAsset);
#endif
        }
    }
}
