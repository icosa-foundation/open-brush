// Copyright 2020 The Tilt Brush Authors
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

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public class ShaderWarmup : MonoBehaviour
    {
        private static readonly string[] kAlwaysWarmupKeywords =
        {
            "SELECTION_ON",
            "SHADER_SCRIPTING_ON",
        };

        private const string kRuntimeLogPrefix = "OB_URP_SHADER_WARMUP_RUNTIME_20260706";

        [SerializeField] private int m_FramesBeforeWarmup;
        [SerializeField] private int m_FramesAfterWarmup;
        [SerializeField] private int m_ShadersPerFrame = 10;

        [SerializeField] private GameObject m_RootObject;

        public static ShaderWarmup Instance { get; private set; }

        public float Progress { get; private set; }

        private IEnumerator Start()
        {
            Instance = this;
            Progress = 0;
            for (int i = 0; i < m_FramesBeforeWarmup; ++i)
            {
                yield return null;
            }
            Progress = 0.05f;
            yield return StartCoroutine(WarmupShaders());
            Progress = 0.95f;
            for (int i = 0; i < m_FramesAfterWarmup; ++i)
            {
                yield return null;
            }
            m_RootObject.SetActive(false);
        }

        // Enumerates the materials we need and creates a quad with each one.
        // Build-time URP/ShaderGraph variant preservation is handled by the editor
        // generator in UrpBrushShaderWarmup. This runtime step is still useful as a
        // driver warmup during loading after the variants have survived stripping.
        private IEnumerator WarmupShaders()
        {
            bool logDiagnostics = IsDiagnosticLoggingEnabled();
            List<Material> materials = new List<Material>();
            foreach (BrushDescriptor brush in BrushCatalog.m_Instance.AllBrushes)
            {
                AddBrushWarmupMaterials(materials, brush);
            }

            Renderer[] renderers = Resources.FindObjectsOfTypeAll<Renderer>();
            materials.AddRange(renderers.SelectMany(x => x.sharedMaterials));

            var distinctShaders = materials.Distinct(new MaterialComparer()).ToArray();
            if (logDiagnostics)
            {
                Debug.Log(
                    $"{kRuntimeLogPrefix} starting runtime shader warmup. " +
                    $"inputMaterials={materials.Count}, distinctStates={distinctShaders.Length}");
            }

            int size = Mathf.CeilToInt(Mathf.Sqrt(distinctShaders.Length));
            Vector3 offset = new Vector3(-size / 2f, -size / 2f, 0);
            int index = 0;
            foreach (Material material in distinctShaders)
            {
                if (material == null)
                {
                    continue;
                }
                GameObject gobj = GameObject.CreatePrimitive(PrimitiveType.Quad);
                gobj.name = material.name;
                gobj.GetComponent<Renderer>().material = material;
                gobj.transform.parent = transform;
                gobj.transform.localPosition = new Vector3(index % size, index / size, 0) + offset;
                index++;
                Progress = 0.05f + (index / (float)distinctShaders.Length) * 0.9f;
                if (logDiagnostics)
                {
                    string shaderName = material.shader == null ? "<missing shader>" : material.shader.name;
                    string keywords = string.Join(",", material.shaderKeywords.OrderBy(keyword => keyword));
                    Debug.Log(
                        $"{kRuntimeLogPrefix} warmed index={index}/{distinctShaders.Length}, " +
                        $"material='{material.name}', shader='{shaderName}', keywords='{keywords}'");
                }
                if (index % m_ShadersPerFrame == 0)
                {
                    yield return null;
                }
            }

            if (logDiagnostics)
            {
                Debug.Log($"{kRuntimeLogPrefix} completed runtime shader warmup.");
            }
        }

        private static void AddBrushWarmupMaterials(List<Material> materials, BrushDescriptor brush)
        {
            Material material = brush.Material;
            if (material == null || !material)
            {
                return;
            }

            materials.Add(material);
            foreach (string keyword in kAlwaysWarmupKeywords)
            {
                AddKeywordMaterial(materials, material, keyword);
            }

            if (brush.m_AudioReactive)
            {
                AddKeywordMaterial(materials, material, "AUDIO_REACTIVE");
            }

            if (material.HasProperty("_ISBAKEDEXPORT"))
            {
                AddKeywordMaterial(materials, material, "_ISBAKEDEXPORT");
            }

            if (material.HasProperty("_BAKED_VERTEX_SHADER_ON") ||
                material.HasProperty("_BAKED_VERTEX_SHADER"))
            {
                AddKeywordMaterial(materials, material, "_BAKED_VERTEX_SHADER_ON");
                AddKeywordMaterial(materials, material, "_BAKED_VERTEX_SHADER");
            }
        }

        private static void AddKeywordMaterial(List<Material> materials, Material source, string keyword)
        {
            Material keywordMaterial = new Material(source);
            keywordMaterial.EnableKeyword(keyword);
            materials.Add(keywordMaterial);
        }

        private static bool IsDiagnosticLoggingEnabled()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string envValue = System.Environment.GetEnvironmentVariable("OPENBRUSH_LOG_SHADER_WARMUP");
            if (envValue == "1" ||
                string.Equals(envValue, "true", System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return System.Environment.GetCommandLineArgs()
                .Any(arg => string.Equals(
                    arg, "--openbrush-log-shader-warmup", System.StringComparison.OrdinalIgnoreCase));
#else
            return false;
#endif
        }

        // Comparator for materials compares them on shader, shader keywords and global illumination flags
        // This is used to help work out which shaders are distinct.
        private class MaterialComparer : IEqualityComparer<Material>
        {
            public bool Equals(Material x, Material y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
                {
                    return false;
                }

                return x.shader == y.shader &&
                    x.shaderKeywords.SequenceEqual(y.shaderKeywords) &&
                    x.globalIlluminationFlags == y.globalIlluminationFlags;
            }

            public int GetHashCode(Material material)
            {
                if (ReferenceEquals(material, null))
                {
                    return 0;
                }

                int hashCode = (material.shader == null ? 0 : material.shader.GetHashCode()) ^
                    material.globalIlluminationFlags.GetHashCode();
                foreach (string keyword in material.shaderKeywords)
                {
                    hashCode ^= keyword.GetHashCode();
                }

                return hashCode;
            }
        }
    }
} // namespace TiltBrush
