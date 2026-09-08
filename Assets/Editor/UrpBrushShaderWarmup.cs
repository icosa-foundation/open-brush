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

namespace TiltBrush
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEditor;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// Generates URP brush shader variant coverage.
    ///
    /// The runtime ShaderWarmup scene object still renders brush materials during loading so the
    /// graphics driver sees them early. This editor workflow is the build-time complement: it
    /// records real Open Brush material keyword states in a ShaderVariantCollection so URP and
    /// ShaderGraph variants are represented before player stripping.
    /// </summary>
    public static class UrpBrushShaderWarmup
    {
        private const string kLogPrefix = "OB_URP_SHADER_WARMUP_20260706";
        private const string kGeneratedDirectory = "Assets/Generated/ShaderWarmup";
        private const string kInventoryPath = kGeneratedDirectory + "/open-brush-brush-variant-inventory.json";
        private const string kCollectionPath = kGeneratedDirectory + "/OpenBrushBrushVariants.shadervariants";
        private const string kStandardManifestPath = "Assets/Manifest.asset";
        private const string kExperimentalManifestPath = "Assets/Manifest_Experimental.asset";
        private const string kPackageShaderMaterialSearchRoot =
            "Packages/com.icosa.open-brush-unity-tools/Runtime/Shaders";

        // These are runtime states Open Brush actually toggles or uses for brush rendering.
        // They are intentionally not a full Cartesian product; the generator only adds a state
        // when the shader/material already has evidence that the keyword is relevant.
        //
        // ODS_RENDER and ODS_RENDER_CM are deliberately absent. They are only enabled while
        // rendering a 360 capture, which is an offline export rather than interactive
        // rendering, so a first-use compilation hitch there costs nothing worth the extra
        // variants.
        private static readonly string[] kRuntimeKeywordCandidates =
        {
            "SELECTION_ON",
            // Set by GrabWidget on Android and iOS only, but the collection is shared across
            // platforms, so the variants are recorded everywhere.
            "HIGHLIGHT_ON",
            "AUDIO_REACTIVE",
            "SHADER_SCRIPTING_ON",
            "_IS_TILT_MESH",
            "_ISBAKEDEXPORT",
            "_BAKED_VERTEX_SHADER_ON",
            "_BAKED_VERTEX_SHADER",
        };

        private static readonly PassType[] kBrushPassTypes =
        {
            PassType.Normal,
            PassType.ScriptableRenderPipeline,
            PassType.ForwardBase,
            PassType.ForwardAdd,
        };

        [Serializable]
        private class Inventory
        {
            public string generatedAtUtc;
            public string unityVersion;
            public int materialStateCount;
            public MaterialState[] materialStates;
        }

        [Serializable]
        private class MaterialState
        {
            public string source;
            public string brushAssetPath;
            public string brushGuid;
            public string brushDurableName;
            public string materialPath;
            public string materialName;
            public string shaderPath;
            public string shaderName;
            public string[] keywords;
            public int renderQueue;
            public int globalIlluminationFlags;
            public bool doubleSidedGI;
            public string notes;
        }

        private struct SourceMaterial
        {
            public string Source;
            public BrushDescriptor Brush;
            public Material Material;
        }

        private struct VariantGenerationResult
        {
            public int Attempted;
            public int Accepted;
            public int Rejected;
        }

        [MenuItem("Open Brush/Build/URP Brush Shader Warmup/Generate Inventory And Variants")]
        private static void GenerateInventoryAndVariantsMenu()
        {
            GenerateInventoryAndVariants();
        }

        public static void GenerateInventoryAndVariants()
        {
            Directory.CreateDirectory(kGeneratedDirectory);

            List<MaterialState> states = BuildMaterialStates();
            var inventory = new Inventory
            {
                generatedAtUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                materialStateCount = states.Count,
                materialStates = states.ToArray(),
            };
            File.WriteAllText(kInventoryPath, JsonUtility.ToJson(inventory, prettyPrint: true), Encoding.UTF8);

            int missingShaderCount = states.Count(state => string.IsNullOrEmpty(state.shaderPath));
            if (missingShaderCount > 0)
            {
                throw new BuildFailedException(
                    $"{kLogPrefix} cannot generate shader variants because " +
                    $"{missingShaderCount} material states have no shader.");
            }

            ShaderVariantCollection collection = LoadOrCreateCollection();
            if (collection == null)
            {
                throw new BuildFailedException(
                    $"{kLogPrefix} could not create or load {kCollectionPath}.");
            }

            collection.Clear();
            VariantGenerationResult result = AddVariants(collection, states);
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(kCollectionPath);
            if (!EnsurePreloaded(collection))
            {
                throw new BuildFailedException(
                    $"{kLogPrefix} could not preload {kCollectionPath}.");
            }
            AssetDatabase.SaveAssets();

            Debug.Log($"{kLogPrefix} wrote {states.Count} material states to {kInventoryPath}");
            Debug.Log(
                $"{kLogPrefix} wrote {result.Accepted} shader variants to {kCollectionPath}. " +
                $"attempted={result.Attempted}, accepted={result.Accepted}, rejected={result.Rejected}");
            if (result.Rejected > 0)
            {
                Debug.LogWarning(
                    $"{kLogPrefix} rejected {result.Rejected} unsupported or duplicate variants.");
            }
        }

        private static List<MaterialState> BuildMaterialStates()
        {
            var states = new Dictionary<string, MaterialState>(StringComparer.Ordinal);
            foreach (SourceMaterial source in EnumerateSourceMaterials())
            {
                Material material = source.Material;
                if (material == null)
                {
                    continue;
                }

                AddState(states, source, material.shaderKeywords, "base");
                AddRuntimeKeywordStates(states, source);
            }

            return states.Values
                .OrderBy(state => state.shaderPath, StringComparer.Ordinal)
                .ThenBy(state => state.materialPath, StringComparer.Ordinal)
                .ThenBy(state => string.Join(" ", state.keywords), StringComparer.Ordinal)
                .ThenBy(state => state.source, StringComparer.Ordinal)
                .ToList();
        }

        private static IEnumerable<SourceMaterial> EnumerateSourceMaterials()
        {
            foreach (ManifestBrush manifestBrush in EnumerateManifestBrushes())
            {
                BrushDescriptor brush = manifestBrush.Brush;
                if (brush == null)
                {
                    continue;
                }
                yield return new SourceMaterial
                {
                    Source = $"{manifestBrush.Manifest}:{manifestBrush.Role}",
                    Brush = brush,
                    Material = brush.Material,
                };
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { kPackageShaderMaterialSearchRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                yield return new SourceMaterial
                {
                    Source = "UnityToolsPackageMaterial",
                    Brush = null,
                    Material = material,
                };
            }
        }

        private static IEnumerable<ManifestBrush> EnumerateManifestBrushes()
        {
            foreach (ManifestBrush manifestBrush in EnumerateManifestBrushes(kStandardManifestPath, "Manifest"))
            {
                yield return manifestBrush;
            }
            foreach (ManifestBrush manifestBrush in EnumerateManifestBrushes(kExperimentalManifestPath, "Manifest_Experimental"))
            {
                yield return manifestBrush;
            }
        }

        private static IEnumerable<ManifestBrush> EnumerateManifestBrushes(string path, string manifestName)
        {
            TiltBrushManifest manifest = AssetDatabase.LoadAssetAtPath<TiltBrushManifest>(path);
            if (manifest == null)
            {
                yield break;
            }

            foreach (BrushDescriptor brush in manifest.Brushes ?? Array.Empty<BrushDescriptor>())
            {
                yield return new ManifestBrush { Brush = brush, Manifest = manifestName, Role = "Brushes" };
            }

            foreach (BrushDescriptor brush in manifest.CompatibilityBrushes ?? Array.Empty<BrushDescriptor>())
            {
                yield return new ManifestBrush { Brush = brush, Manifest = manifestName, Role = "CompatibilityBrushes" };
            }
        }

        private struct ManifestBrush
        {
            public BrushDescriptor Brush;
            public string Manifest;
            public string Role;
        }

        private static void AddRuntimeKeywordStates(
            Dictionary<string, MaterialState> states, SourceMaterial source)
        {
            Material material = source.Material;
            if (material == null || material.shader == null)
            {
                return;
            }

            HashSet<string> baseKeywords = NormalizeKeywords(material.shaderKeywords);
            string shaderText = ReadShaderEvidenceText(material.shader);
            foreach (string keyword in kRuntimeKeywordCandidates)
            {
                if (!MaterialOrShaderMentionsKeyword(material, shaderText, keyword))
                {
                    continue;
                }

                var keywords = new HashSet<string>(baseKeywords, StringComparer.Ordinal) { keyword };
                AddState(states, source, keywords, $"runtime keyword {keyword}");
            }
        }

        private static bool MaterialOrShaderMentionsKeyword(Material material, string shaderText, string keyword)
        {
            if (NormalizeKeywords(material.shaderKeywords).Contains(keyword))
            {
                return true;
            }

            string materialPath = AssetDatabase.GetAssetPath(material);
            if (!string.IsNullOrEmpty(materialPath) &&
                File.Exists(materialPath) &&
                File.ReadAllText(materialPath).Contains(keyword))
            {
                return true;
            }

            return shaderText.Contains(keyword);
        }

        private static string ReadShaderEvidenceText(Shader shader)
        {
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            if (string.IsNullOrEmpty(shaderPath) || !File.Exists(shaderPath))
            {
                return "";
            }

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { shaderPath };
            string directory = Path.GetDirectoryName(shaderPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                foreach (string path in Directory.GetFiles(directory, "*.hlsl", SearchOption.TopDirectoryOnly))
                {
                    paths.Add(path.Replace('\\', '/'));
                }
            }

            // A Shader Graph's keywords are not always declared in the graph. Custom Function
            // nodes pull their file in with #include_with_pragmas, so a multi_compile living
            // in a shared .hlsl elsewhere in the project (OpenBrushOds.hlsl and
            // OpenBrushSelection.hlsl in the unity-tools package, for instance) is real
            // evidence even though it is not in the graph's own directory.
            foreach (string includePath in EnumerateCustomFunctionSources(shaderPath))
            {
                paths.Add(includePath);
            }

            var builder = new StringBuilder();
            foreach (string path in paths)
            {
                builder.AppendLine(File.ReadAllText(path));
            }
            return builder.ToString();
        }

        private static IEnumerable<string> EnumerateCustomFunctionSources(string shaderPath)
        {
            if (!shaderPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(shaderPath))
            {
                yield break;
            }

            // Custom Function nodes serialise their file as "m_FunctionSource": "<guid>".
            const string kToken = "\"m_FunctionSource\": \"";
            string graphText = File.ReadAllText(shaderPath);
            int index = graphText.IndexOf(kToken, StringComparison.Ordinal);
            while (index >= 0)
            {
                int start = index + kToken.Length;
                int end = graphText.IndexOf('"', start);
                if (end < 0)
                {
                    yield break;
                }

                string guid = graphText.Substring(start, end - start);
                if (guid.Length == 32)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        yield return path;
                    }
                }

                index = graphText.IndexOf(kToken, end, StringComparison.Ordinal);
            }
        }

        private static void AddState(
            Dictionary<string, MaterialState> states,
            SourceMaterial source,
            IEnumerable<string> keywords,
            string notes)
        {
            Material material = source.Material;
            Shader shader = material != null ? material.shader : null;
            string materialPath = material != null ? AssetDatabase.GetAssetPath(material) : "";
            string shaderPath = shader != null ? AssetDatabase.GetAssetPath(shader) : "";
            string[] normalizedKeywords = NormalizeKeywords(keywords).OrderBy(keyword => keyword, StringComparer.Ordinal).ToArray();
            string key = $"{materialPath}|{shaderPath}|{string.Join(" ", normalizedKeywords)}|{source.Source}";
            if (states.ContainsKey(key))
            {
                return;
            }

            BrushDescriptor brush = source.Brush;
            states.Add(key, new MaterialState
            {
                source = source.Source,
                brushAssetPath = brush != null ? AssetDatabase.GetAssetPath(brush) : "",
                brushGuid = brush != null ? ((Guid)brush.m_Guid).ToString("D") : "",
                brushDurableName = brush != null ? brush.m_DurableName : "",
                materialPath = materialPath,
                materialName = material != null ? material.name : "",
                shaderPath = shaderPath,
                shaderName = shader != null ? shader.name : "",
                keywords = normalizedKeywords,
                renderQueue = material != null ? material.renderQueue : 0,
                globalIlluminationFlags = material != null ? (int)material.globalIlluminationFlags : 0,
                doubleSidedGI = material != null && material.doubleSidedGI,
                notes = notes,
            });
        }

        private static HashSet<string> NormalizeKeywords(IEnumerable<string> keywords)
        {
            return new HashSet<string>(
                (keywords ?? Array.Empty<string>()).Where(keyword => !string.IsNullOrWhiteSpace(keyword)),
                StringComparer.Ordinal);
        }

        private static ShaderVariantCollection LoadOrCreateCollection()
        {
            ShaderVariantCollection collection =
                AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(kCollectionPath);
            if (collection != null)
            {
                return collection;
            }

            collection = new ShaderVariantCollection();
            AssetDatabase.CreateAsset(collection, kCollectionPath);
            return collection;
        }

        private static VariantGenerationResult AddVariants(
            ShaderVariantCollection collection, IEnumerable<MaterialState> states)
        {
            var result = new VariantGenerationResult();
            var attempted = new HashSet<string>(StringComparer.Ordinal);
            foreach (MaterialState state in states)
            {
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(state.shaderPath);
                if (shader == null)
                {
                    continue;
                }

                string keywordKey = string.Join(" ", state.keywords ?? Array.Empty<string>());
                foreach (PassType passType in kBrushPassTypes)
                {
                    string key = $"{state.shaderPath}|{passType}|{keywordKey}";
                    if (!attempted.Add(key))
                    {
                        continue;
                    }

                    result.Attempted++;
                    if (TryAddVariant(collection, shader, passType, state.keywords))
                    {
                        result.Accepted++;
                    }
                    else
                    {
                        result.Rejected++;
                    }
                }
            }
            return result;
        }

        private static bool TryAddVariant(
            ShaderVariantCollection collection, Shader shader, PassType passType, string[] keywords)
        {
            try
            {
                var variant = new ShaderVariantCollection.ShaderVariant(shader, passType, keywords);
                return collection.Add(variant);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool EnsurePreloaded(ShaderVariantCollection collection)
        {
            UnityEngine.Object graphicsSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")
                .FirstOrDefault();
            if (graphicsSettings == null)
            {
                Debug.LogError($"{kLogPrefix} could not load ProjectSettings/GraphicsSettings.asset");
                return false;
            }

            var serializedObject = new SerializedObject(graphicsSettings);
            SerializedProperty preloadedShaders = serializedObject.FindProperty("m_PreloadedShaders");
            if (preloadedShaders == null || !preloadedShaders.isArray)
            {
                Debug.LogError($"{kLogPrefix} could not find GraphicsSettings.m_PreloadedShaders");
                return false;
            }

            for (int i = 0; i < preloadedShaders.arraySize; ++i)
            {
                if (preloadedShaders.GetArrayElementAtIndex(i).objectReferenceValue == collection)
                {
                    return true;
                }
            }

            preloadedShaders.InsertArrayElementAtIndex(preloadedShaders.arraySize);
            preloadedShaders.GetArrayElementAtIndex(preloadedShaders.arraySize - 1).objectReferenceValue = collection;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(graphicsSettings);
            Debug.Log($"{kLogPrefix} added {kCollectionPath} to GraphicsSettings preloaded shaders");
            return true;
        }

        private class BuildPreprocessor : IPreprocessBuildWithReport
        {
            public int callbackOrder => 0;

            public void OnPreprocessBuild(BuildReport report)
            {
                GenerateInventoryAndVariants();
            }
        }
    }
}
