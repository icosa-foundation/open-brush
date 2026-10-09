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
    // UnityGLTF resolves its generated shaders using Shader.Find. In Android builds made
    // with Unity 6000.6 / URP / Vulkan, both compiled shaders were present, but their
    // names were missing from the player's ScriptMapper lookup table, so imports failed.
    // Explicitly registering them before the build restored the entries and imports on
    // the device. Shader Graph normally registers shaders after import; why registration
    // was missing here is unknown (cached imports are only a suspected cause).
    // Keep this workaround in the Editor: it fixes build metadata without changing
    // UnityGLTF's runtime lookup or expanding the existing audited 392 shader variants.
    // Commit a63723deff contains the original diagnostics and APK/AAB metadata verifier.
    internal class UnityGltfShaderRegistration : IPreprocessBuildWithReport
    {
        private const string kPrefix = "[UnityGltfShaderRegistration]";
        public int callbackOrder => int.MaxValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            ValidateAndRegisterShader("PBRGraph", "478ce3626be7a5f4ea58d6b13f05a2e4");
            ValidateAndRegisterShader("UnlitGraph", "59541e6caf586ca4f96ccf48a4813a51");
        }

        private static void ValidateAndRegisterShader(string graph, string expectedGuid)
        {
            // Load by asset GUID because name lookup is the mechanism being repaired.
            // Validate the name so a package change fails the build instead of silently
            // registering a different shader. No runtime reference materials are needed.
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(expectedGuid));
            if (!shader || shader.name != $"UnityGLTF/{graph}")
                throw new BuildFailedException($"{kPrefix} Invalid shader for GUID {expectedGuid}: expected UnityGLTF/{graph}");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long _);
            if (guid != expectedGuid)
                throw new BuildFailedException($"{kPrefix} Unexpected shader GUID for UnityGLTF/{graph}: {guid}");
            ShaderUtil.RegisterShader(shader);
            // This validates the Editor registry; it does not inspect the built player.
            if (Shader.Find(shader.name) != shader)
                throw new BuildFailedException($"{kPrefix} Shader name registration failed for {shader.name}");
        }
    }
}
