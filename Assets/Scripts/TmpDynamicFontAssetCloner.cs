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
        private const string k_DynamicFallbackFontName = "NotoSansCJK-Light SDF";
#if UNITY_EDITOR
        private const string k_DynamicFallbackFontGuid = "8383e2a53dc64bf4eb3ce5ce0a3b765f";
#endif

        private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> m_Clones =
            new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private static readonly Dictionary<TMP_FontAsset, TMP_FontAsset> m_FallbackOwnerClones =
            new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        private static readonly HashSet<TMP_FontAsset> m_RuntimeClones =
            new HashSet<TMP_FontAsset>();
        private static readonly FieldInfo m_AtlasTextureField = typeof(TMP_FontAsset).GetField(
            "m_AtlasTexture", BindingFlags.Instance | BindingFlags.NonPublic);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            m_Clones.Clear();
            m_FallbackOwnerClones.Clear();
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
            EditorApplication.delayCall += ReplaceLoadedFallbacksInPlayMode;
            EditorApplication.hierarchyChanged += ReplaceLoadedFallbacksInPlayMode;
        }

        private static void ReplaceLoadedFallbacksInPlayMode()
        {
            if (Application.isPlaying)
            {
                ReplaceLoadedFallbacks();
            }
        }
#endif

        public static void ReplaceLoadedFallbacks()
        {
            HashSet<TMP_FontAsset> visited = new HashSet<TMP_FontAsset>();
#if UNITY_EDITOR
            foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
            {
                ReplaceTextFallbacks(text, visited);
            }
#endif

            foreach (TMP_FontAsset fontAsset in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                ReplaceFallbacks(fontAsset, visited);
            }

            if (CanModifyTmpSettings())
            {
                ReplaceFallbacks(TMP_Settings.fallbackFontAssets, visited);
            }
        }

        private static void ReplaceTextFallbacks(TMP_Text text, HashSet<TMP_FontAsset> visited)
        {
            if (text == null || text.font == null || !CanModifyTextOwner(text))
            {
                return;
            }

            TMP_FontAsset clone = GetFallbackOwnerCloneIfNeeded(text.font, visited);
            if (clone != null)
            {
                text.font = clone;
            }
        }

        private static void ReplaceFallbacks(TMP_FontAsset fontAsset, HashSet<TMP_FontAsset> visited)
        {
            if (fontAsset == null || !visited.Add(fontAsset) || !CanModifyFallbackOwner(fontAsset))
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

        private static TMP_FontAsset GetFallbackOwnerCloneIfNeeded(
            TMP_FontAsset fontAsset, HashSet<TMP_FontAsset> visited)
        {
            if (fontAsset == null || !IsPersistent(fontAsset) ||
                !ContainsNotoDynamicFallback(fontAsset.fallbackFontAssetTable))
            {
                return null;
            }

            if (!m_FallbackOwnerClones.TryGetValue(fontAsset, out TMP_FontAsset clone) ||
                clone == null)
            {
                clone = Object.Instantiate(fontAsset);
                clone.name = fontAsset.name + " Runtime";
                clone.hideFlags = HideFlags.DontSave;
                m_FallbackOwnerClones[fontAsset] = clone;
                m_RuntimeClones.Add(clone);
            }

            ReplaceFallbacks(clone, visited);
            return clone;
        }

        private static TMP_FontAsset GetCloneIfNeeded(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null ||
                !IsNotoDynamicFallback(fontAsset) ||
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

        private static bool ContainsNotoDynamicFallback(List<TMP_FontAsset> fallbackFontAssets)
        {
            if (fallbackFontAssets == null)
            {
                return false;
            }

            for (int i = 0; i < fallbackFontAssets.Count; i++)
            {
                if (IsNotoDynamicFallback(fallbackFontAssets[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsNotoDynamicFallback(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                return false;
            }

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic ||
                fontAsset.name != k_DynamicFallbackFontName)
            {
                return false;
            }

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(fontAsset);
            return AssetDatabase.AssetPathToGUID(path) == k_DynamicFallbackFontGuid;
#else
            return true;
#endif
        }

        private static bool CanModifyTextOwner(TMP_Text text)
        {
#if UNITY_EDITOR
            return Application.isPlaying && !EditorUtility.IsPersistent(text);
#else
            return true;
#endif
        }

        private static bool CanModifyFallbackOwner(TMP_FontAsset fontAsset)
        {
#if UNITY_EDITOR
            return Application.isPlaying && !EditorUtility.IsPersistent(fontAsset);
#else
            return !IsRuntimeClone(fontAsset);
#endif
        }

        private static bool CanModifyTmpSettings()
        {
#if UNITY_EDITOR
            return false;
#else
            return true;
#endif
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
