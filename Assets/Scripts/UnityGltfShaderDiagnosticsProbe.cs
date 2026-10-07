// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF;

namespace TiltBrush
{
    // Temporary, release-build diagnostics. Observe does not load reference assets or warm up.
    internal static class UnityGltfShaderDiagnosticsProbe
    {
        internal const string Prefix = "[OB_GLTF_SHADER_20261007]";
        private static ShaderVariantCollection sm_Collection;
        private static Material sm_PbrReference;
        private static Material sm_UnlitReference;
        private static int sm_ImportCount;

        private static string Mode => App.Config != null
            ? App.Config.UnityGltfShaderDiagnostics : "observe";

        internal static bool IsValidMode(string mode) => mode == "off" || mode == "observe" ||
            mode == "collection" || mode == "references" || mode == "warmup";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Startup()
        {
            // Config/Android intent arguments have not been parsed yet. Record lookup only.
            sm_Collection = null;
            sm_PbrReference = null;
            sm_UnlitReference = null;
            sm_ImportCount = 0;
            Debug.Log($"{Prefix} startup unity={Application.unityVersion}, version={Application.version}, platform={Application.platform}, api={SystemInfo.graphicsDeviceType}, gpu={SystemInfo.graphicsDeviceName}, driver={SystemInfo.graphicsDeviceVersion}, graphicsLevel={SystemInfo.graphicsShaderLevel}, thread={Thread.CurrentThread.ManagedThreadId}");
            RunDiagnostic(() => Snapshot("startup"));
        }

        private static void RunDiagnostic(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                Debug.Log($"{Prefix} diagnostic-error={exception}");
            }
        }

        internal static void BeforeImport() => RunDiagnostic(PrepareImport);

        private static void PrepareImport()
        {
            if (Mode == "off") return;
            ++sm_ImportCount;
            Snapshot("before-load");
            // Apply one isolated intervention per launch. Keep references through natural unloads.
            if (Mode == "collection" || Mode == "warmup")
            {
                if (!sm_Collection)
                    sm_Collection = Resources.Load<ShaderVariantCollection>("UnityGLTF Shader Variants");
                Debug.Log($"{Prefix} collection found={sm_Collection != null}, shaders={(sm_Collection ? sm_Collection.shaderCount : -1)}, variants={(sm_Collection ? sm_Collection.variantCount : -1)}, warmed={(sm_Collection && sm_Collection.isWarmedUp)}");
                Snapshot("after-collection-load");
                if (Mode == "warmup" && sm_Collection && !sm_Collection.isWarmedUp)
                {
                    sm_Collection.WarmUp();
                    Snapshot("after-warmup");
                }
            }
            else if (Mode == "references")
            {
                if (!sm_PbrReference)
                    sm_PbrReference = Resources.Load<Material>("UnityGLTF PBRGraph Reference");
                LogReference("PBR", sm_PbrReference);
                Snapshot("after-pbr-reference");
                if (!sm_UnlitReference)
                    sm_UnlitReference = Resources.Load<Material>("UnityGLTF UnlitGraph Reference");
                LogReference("Unlit", sm_UnlitReference);
                Snapshot("after-unlit-reference");
            }
        }

        internal static void ImportResult(bool success) => RunDiagnostic(() => LogImportResult(success));

        private static void LogImportResult(bool success)
        {
            if (Mode == "off") return;
            Snapshot(success ? "unitygltf-load-succeeded" : "unitygltf-import-exception");
            // Probe the same public constructors used by ConstructMaterial, after the real failure.
            // No retry or forced unload: preserve the original exception and legacy fallback.
            if (!success)
            {
                ProbeMapper("PBR", () => new PBRGraphMap().Material);
                ProbeMapper("Unlit", () => new UnlitGraphMap().Material);
            }
        }

        private static void Snapshot(string stage)
        {
            Shader pbr = Shader.Find("UnityGLTF/PBRGraph");
            Shader unlit = Shader.Find("UnityGLTF/UnlitGraph");
            var loaded = Resources.FindObjectsOfTypeAll<Shader>()
                .Where(shader => shader.name.StartsWith("UnityGLTF/", StringComparison.Ordinal))
                .Select(shader => $"{shader.name}#{shader.GetEntityId()}:supported={shader.isSupported}")
                .OrderBy(name => name).ToArray();
            Debug.Log($"{Prefix} stage={stage}, mode={Mode}, import={sm_ImportCount}, thread={Thread.CurrentThread.ManagedThreadId}, frame={Time.frameCount}, pipeline={GraphicsSettings.currentRenderPipeline?.GetType().FullName ?? "none"}, quality={QualitySettings.GetQualityLevel()}, PBR={Describe(pbr)}, Unlit={Describe(unlit)}, loadedCount={loaded.Length}, loaded=[{string.Join(";", loaded.Take(12))}]");
        }

        private static string Describe(Shader shader) => shader
            ? $"{shader.name}#{shader.GetEntityId()}:supported={shader.isSupported},passes={shader.passCount}"
            : "null";

        private static void LogReference(string label, Material material)
        {
            Debug.Log($"{Prefix} reference={label}, material={(material ? material.name : "null")}, shader={Describe(material ? material.shader : null)}");
        }

        private static void ProbeMapper(string label, Func<Material> create)
        {
            try
            {
                var material = create();
                Debug.Log($"{Prefix} mapper={label}, constructed=true, shader={Describe(material.shader)}");
                UnityEngine.Object.Destroy(material);
            }
            catch (Exception exception)
            {
                Debug.Log($"{Prefix} mapper={label}, constructed=false, exception={exception.GetType().FullName}, message={exception.Message}");
            }
        }
    }
}
