// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{
    // Log the result after the project's and package's shader preprocessors, without changing it.
    internal class UnityGltfShaderBuildDiagnostics : IPreprocessBuildWithReport, IPreprocessShaders
    {
        private const string kPrefix = "[OB_GLTF_SHADER_BUILD_20261007]";
        private const string kCollectionPath = "Assets/Resources/UnityGLTF Shader Variants.shadervariants";
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(kCollectionPath);
            if (!collection || collection.shaderCount != 2 || collection.variantCount != 392)
                throw new BuildFailedException($"{kPrefix} Expected audited UnityGLTF collection with 2 shaders and 392 variants.");
            string collectionGuid = AssetDatabase.AssetPathToGUID(kCollectionPath);
            Debug.Log($"{kPrefix} target={report.summary.platform}, options={report.summary.options}, unity={Application.unityVersion}, apis={string.Join(",", PlayerSettings.GetGraphicsAPIs(report.summary.platform))}, pipeline={GraphicsSettings.defaultRenderPipeline?.name ?? "none"}, collectionGuid={collectionGuid}, preloadedSettingContainsGuid={File.ReadAllText("ProjectSettings/GraphicsSettings.asset").Contains(collectionGuid)}, shaders={collection.shaderCount}, variants={collection.variantCount}");
            ValidateAndRegisterReference("PBRGraph", "478ce3626be7a5f4ea58d6b13f05a2e4");
            ValidateAndRegisterReference("UnlitGraph", "59541e6caf586ca4f96ccf48a4813a51");
        }

        private static void ValidateAndRegisterReference(string graph, string expectedGuid)
        {
            string path = $"Assets/Resources/UnityGLTF {graph} Reference.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material || !material.shader || material.shader.name != $"UnityGLTF/{graph}")
                throw new BuildFailedException($"{kPrefix} Invalid shader reference material: {path}");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(material.shader, out string guid, out long localId);
            if (guid != expectedGuid)
                throw new BuildFailedException($"{kPrefix} Unexpected shader GUID for {path}: {guid}");
            // Android CI builds contained both compiled shaders and valid material references,
            // but omitted their names from the player's ScriptMapper shader lookup table.
            // UnityGLTF uses Shader.Find, so imports failed despite the shaders being present.
            // Explicit registration restored the built name entries and imports on the device.
            // Shader Graph normally calls this API after import; why that registration was
            // missing here is still unknown (cached imports are only a suspected cause).
            // Register before building without expanding the audited 392 variants. The CI
            // artifact check verifies the player table, beyond this Editor-only lookup check.
            bool foundBeforeRegistration = Shader.Find(material.shader.name) == material.shader;
            ShaderUtil.RegisterShader(material.shader);
            if (Shader.Find(material.shader.name) != material.shader)
                throw new BuildFailedException($"{kPrefix} Shader name registration failed for {material.shader.name}");
            Debug.Log($"{kPrefix} registered={material.shader.name}, foundBeforeRegistration={foundBeforeRegistration}, foundAfterRegistration=True");
            Debug.Log($"{kPrefix} reference={path}, shader={material.shader.name}, guid={guid}, localId={localId}, source={AssetDatabase.GetAssetPath(material.shader)}, supportedInEditor={material.shader.isSupported}, passesInEditor={material.shader.passCount}");
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android ||
                !shader.name.StartsWith("UnityGLTF/", System.StringComparison.Ordinal)) return;
            var platforms = data.GroupBy(variant => variant.shaderCompilerPlatform)
                .Select(group => $"{group.Key}:{group.Count()}");
            Debug.Log($"{kPrefix} shader={shader.name}, pass={snippet.passName}, passType={snippet.passType}, stage={snippet.shaderType}, remainingVariants={data.Count}, platforms=[{string.Join(",", platforms)}]");
        }
    }
}
