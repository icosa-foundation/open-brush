// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TiltBrush
{
    internal class UnityGltfShaderRegistration : IPreprocessBuildWithReport
    {
        private const string kPrefix = "[UnityGltfShaderRegistration]";
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            string pbrGuid = "478ce3626be7a5f4ea58d6b13f05a2e4";
            var pbrShader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(pbrGuid));
            ValidateAndRegisterShader("PBRGraph", pbrGuid, pbrShader);
            ValidateAndRegisterReference("UnlitGraph", "59541e6caf586ca4f96ccf48a4813a51");
        }

        private static void ValidateAndRegisterReference(string graph, string expectedGuid)
        {
            string path = $"Assets/Resources/UnityGLTF {graph} Reference.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material || !material.shader || material.shader.name != $"UnityGLTF/{graph}")
                throw new BuildFailedException($"{kPrefix} Invalid shader reference material: {path}");
            ValidateAndRegisterShader(graph, expectedGuid, material.shader);
        }

        private static void ValidateAndRegisterShader(string graph, string expectedGuid, Shader shader)
        {
            if (!shader || shader.name != $"UnityGLTF/{graph}")
                throw new BuildFailedException($"{kPrefix} Invalid shader for GUID {expectedGuid}: expected UnityGLTF/{graph}");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long _);
            if (guid != expectedGuid)
                throw new BuildFailedException($"{kPrefix} Unexpected shader GUID for UnityGLTF/{graph}: {guid}");
            // Android CI builds contained both compiled shaders and valid material references,
            // but omitted their names from the player's ScriptMapper shader lookup table.
            // UnityGLTF uses Shader.Find, so imports failed despite the shaders being present.
            // Explicit registration restored the built name entries and imports on the device.
            // Shader Graph normally calls this API after import; why that registration was
            // missing here is still unknown (cached imports are only a suspected cause).
            // Register before building without expanding the audited 392 variants. The CI
            // artifact check verifies the player table, beyond this Editor-only lookup check.
            ShaderUtil.RegisterShader(shader);
            if (Shader.Find(shader.name) != shader)
                throw new BuildFailedException($"{kPrefix} Shader name registration failed for {shader.name}");
        }
    }
}
