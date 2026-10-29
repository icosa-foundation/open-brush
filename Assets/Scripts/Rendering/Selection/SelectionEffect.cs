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

#if UNITY_ANDROID || UNITY_IOS
# define FEATURE_CUSTOM_MESH_RENDER
# define FEATURE_MOBILE_SELECTION
#else
# define FEATURE_CUSTOM_MESH_RENDER
# undef FEATURE_MOBILE_SELECTION
#endif

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{

    public class SelectionEffect : MonoBehaviour
    {
        // Static flag to disable selection effects during save icon capture
        private static bool s_DisableSelectionEffects = false;
        public static bool DisableSelectionEffects
        {
            get { return s_DisableSelectionEffects; }
            set
            {
                s_DisableSelectionEffects = value;
                // Set shader global so mobile shaders can also check this flag
                Shader.SetGlobalFloat("_DisableSelectionEffect", value ? 1.0f : 0.0f);
            }
        }

#if FEATURE_CUSTOM_MESH_RENDER
        private enum SelectionEffectPass
        {
            OutlineComposite, // 0
            Downsample,       // 1
            VerticalBlur,     // 2
            HorizontalBlur    // 3
        }
#endif

        // Only for FEATURE_CUSTOM_MESH_RENDER, but don't #if out serialized data.
        // #pragma is needed because of the default values
        // ReSharper disable NotAccessedField.Local
#pragma warning disable 0414
        [SerializeField] private Material m_GrabHighlightMaskMaterial;
        [SerializeField] private Material m_WidgetSelectionPostEffect;
        [SerializeField] private Material m_StrokeSelectionPostEffect;
        [SerializeField] private float m_BlurWidth = 1.0f;
#pragma warning restore 0414
        // ReSharper restore NotAccessedField.Local

        // Not sure if these are specific to FEATURE_CUSTOM_MESH_RENDER
        [Header("Selection Color Settings")]
        [SerializeField] private float m_SelectionSaturation = 0.5f;
        [SerializeField] private float m_SelectionValue = 1f;
        [SerializeField] private float m_SelectionHueSpeed = 0.5f;
        [SerializeField] private float m_SelectionHueStereoOffset = 0.2f;

        // Only for FEATURE_MOBILE_SELECTION, but don't #if out serialized data.
        // #pragma is needed because of the default values
        // ReSharper disable NotAccessedField.Local
#pragma warning disable 0414
        [Header("Mobile Selection Shader Settings")]
        [SerializeField] private float m_NoiseSparkleSpeed = 2f;
#pragma warning restore 0414
        // ReSharper restore NotAccessedField.Local

#if FEATURE_CUSTOM_MESH_RENDER
        private RenderWrapper m_CmrRenderWrapper;
        private bool m_CmrRenderHighlight;
        private bool m_CmrUseStrokePostEffect;
        private List<MeshFilter> m_CmrRequestedMeshes;
        private List<SkinnedMeshRenderer> m_CmrRequestedSkinnedMeshes;
        private int m_CmrRequestedMeshesFrame = -1;
        private List<MeshFilter> m_UrpDiagnosticMeshes;
        private int m_UrpDiagnosticFramesRemaining;
        private bool m_UrpDiagnosticUseStrokePostEffect;
        private bool m_bCmrPosesApplied;
        private bool m_bCmrPreCulled;

        private sealed class MobileHighlightState
        {
            public Material[] OriginalMaterials;
            public Material[] HighlightMaterials;
            public MaterialPropertyBlock OriginalRendererPropertyBlock;
            public MaterialPropertyBlock[] OriginalPropertyBlocks;
            public bool HadOriginalRendererPropertyBlock;
            public bool[] HadOriginalPropertyBlocks;
            public bool OriginalEnabled;
            public bool OriginalForceRenderingOff;
            public int RegisteredFrame;
            public bool Applied;
        }

        private Dictionary<Renderer, MobileHighlightState> m_MobileHighlightStates;
        private Material m_MobileHighlightMaterial;
        private Shader m_MobileHighlightShader;
#endif

        void Start()
        {
#if FEATURE_CUSTOM_MESH_RENDER
            m_CmrRenderWrapper = GetComponent<RenderWrapper>();
            m_CmrRequestedMeshes = new List<MeshFilter>();
            m_CmrRequestedSkinnedMeshes = new List<SkinnedMeshRenderer>();
            m_UrpDiagnosticMeshes = new List<MeshFilter>();
            m_MobileHighlightStates = new Dictionary<Renderer, MobileHighlightState>();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
#endif
            InputManager.m_Instance.ControllerPosesApplied += OnPosesApplied;
            Shader.SetGlobalColor("_LeftEyeSelectionColor", Color.white);
            Shader.SetGlobalColor("_RightEyeSelectionColor", Color.white);
        }

        void LateUpdate()
        {
#if FEATURE_CUSTOM_MESH_RENDER
            RestoreStaleMobileHighlights();

            if (m_UrpDiagnosticFramesRemaining <= 0 ||
                GraphicsSettings.currentRenderPipeline == null)
            {
                return;
            }

            m_CmrUseStrokePostEffect = m_UrpDiagnosticUseStrokePostEffect;
            m_UrpDiagnosticFramesRemaining--;
            if (m_UrpDiagnosticFramesRemaining <= 0)
            {
                m_UrpDiagnosticMeshes.Clear();
            }
#endif
        }

        void OnPreCull()
        {
#if FEATURE_CUSTOM_MESH_RENDER
            m_bCmrPreCulled = true;
            RenderHighlightsIfReady();
#endif
        }

        void OnPosesApplied()
        {
#if FEATURE_CUSTOM_MESH_RENDER
            m_bCmrPosesApplied = true;
#endif

            // Set the selection colors for each eye
            float leftHue = Mathf.Repeat(Time.realtimeSinceStartup * m_SelectionHueSpeed, 1f);
            float rightHue = Mathf.Repeat(
                Time.realtimeSinceStartup * m_SelectionHueSpeed + m_SelectionHueStereoOffset, 1f);

            Color left = Color.HSVToRGB(leftHue, m_SelectionSaturation, m_SelectionValue);
            Color right = Color.HSVToRGB(rightHue, m_SelectionSaturation, m_SelectionValue);

            Shader.SetGlobalColor("_LeftEyeSelectionColor", left);
            Shader.SetGlobalColor("_RightEyeSelectionColor", right);

#if FEATURE_CUSTOM_MESH_RENDER
            UpdateMobileHighlightColor(Color.Lerp(left, right, 0.5f));
#endif

#if FEATURE_MOBILE_SELECTION
    TrTransform worldToTransform = App.Scene.SelectionCanvas.Pose;
    // Keep the scale within 0.5 and 2
    if (worldToTransform.scale > 2) {
      worldToTransform.scale /= Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(worldToTransform.scale));
    } else if (worldToTransform.scale < 0.5) {
      worldToTransform.scale *=
          Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(1f / worldToTransform.scale));
    }

    Shader.SetGlobalMatrix("_InverseLimitedScaleSceneMatrix", worldToTransform.inverse.ToMatrix4x4());
    Shader.SetGlobalFloat("_PatternSpeed", m_NoiseSparkleSpeed);
#endif

#if FEATURE_CUSTOM_MESH_RENDER
            RenderHighlightsIfReady();
#endif
        }

        // Public API for FEATURE_CUSTOM_MESH_RENDER

        /// Causes selection effect to be rendered for the passed mesh, this frame only
        public void RegisterMesh(MeshFilter meshFilter)
        {
#if FEATURE_CUSTOM_MESH_RENDER
            if (UseMobileMaterialHighlights)
            {
                RegisterMobileHighlight(meshFilter != null
                    ? meshFilter.GetComponent<Renderer>()
                    : null);
                return;
            }

            if (m_CmrRequestedMeshes == null)
            {
                m_CmrRequestedMeshes = new List<MeshFilter>();
            }
            if (m_CmrRequestedSkinnedMeshes == null)
            {
                m_CmrRequestedSkinnedMeshes = new List<SkinnedMeshRenderer>();
            }
            if (m_CmrRequestedMeshesFrame != Time.frameCount)
            {
                m_CmrRequestedMeshes.Clear();
                m_CmrRequestedSkinnedMeshes.Clear();
                m_CmrRequestedMeshesFrame = Time.frameCount;
            }
            m_CmrRequestedMeshes.Add(meshFilter);
#endif
        }

        public void RegisterMesh(SkinnedMeshRenderer renderer)
        {
#if FEATURE_CUSTOM_MESH_RENDER
            if (UseMobileMaterialHighlights)
            {
                RegisterMobileHighlight(renderer);
                return;
            }

            if (m_CmrRequestedMeshes == null)
            {
                m_CmrRequestedMeshes = new List<MeshFilter>();
            }
            if (m_CmrRequestedSkinnedMeshes == null)
            {
                m_CmrRequestedSkinnedMeshes = new List<SkinnedMeshRenderer>();
            }
            if (m_CmrRequestedMeshesFrame != Time.frameCount)
            {
                m_CmrRequestedMeshes.Clear();
                m_CmrRequestedSkinnedMeshes.Clear();
                m_CmrRequestedMeshesFrame = Time.frameCount;
            }
            m_CmrRequestedSkinnedMeshes.Add(renderer);
#endif
        }

        public void UnregisterMesh(MeshFilter meshFilter)
        {
#if FEATURE_CUSTOM_MESH_RENDER
            if (UseMobileMaterialHighlights)
            {
                RemoveMobileHighlight(meshFilter != null
                    ? meshFilter.GetComponent<Renderer>()
                    : null);
                return;
            }

            m_CmrRequestedMeshes?.RemoveAll(x => x == meshFilter);
#endif
        }

        public void UnregisterMesh(SkinnedMeshRenderer renderer)
        {
#if FEATURE_CUSTOM_MESH_RENDER
            if (UseMobileMaterialHighlights)
            {
                RemoveMobileHighlight(renderer);
                return;
            }

            m_CmrRequestedSkinnedMeshes?.RemoveAll(x => x == renderer);
#endif
        }

#if FEATURE_CUSTOM_MESH_RENDER
        private bool UseMobileMaterialHighlights =>
            Application.isMobilePlatform &&
            GraphicsSettings.currentRenderPipeline != null;

        private void RegisterMobileHighlight(Renderer renderer)
        {
            if (renderer == null || !renderer.gameObject.activeInHierarchy || DisableSelectionEffects)
            {
                return;
            }

            m_MobileHighlightStates ??= new Dictionary<Renderer, MobileHighlightState>();
            if (m_MobileHighlightStates.TryGetValue(renderer, out MobileHighlightState state))
            {
                state.RegisteredFrame = Time.frameCount;
                return;
            }

            m_MobileHighlightStates.Add(renderer, new MobileHighlightState
            {
                RegisteredFrame = Time.frameCount
            });
        }

        private bool EnsureMobileHighlightMaterial()
        {
            if (m_MobileHighlightMaterial != null)
            {
                return true;
            }
            m_MobileHighlightShader ??= Shader.Find("Universal Render Pipeline/Lit");
            if (m_MobileHighlightShader == null)
            {
                Debug.LogError("The URP Lit shader is unavailable for mobile selection highlighting.");
                return false;
            }

            m_MobileHighlightMaterial = new Material(m_MobileHighlightShader)
            {
                name = "Mobile Selection Highlight",
                hideFlags = HideFlags.HideAndDontSave
            };
            m_MobileHighlightMaterial.SetFloat("_Metallic", 0.0f);
            m_MobileHighlightMaterial.SetFloat("_Smoothness", 0.3f);
            m_MobileHighlightMaterial.SetFloat("_Cull", 0.0f);
            m_MobileHighlightMaterial.SetFloat("_Surface", 0.0f);
            m_MobileHighlightMaterial.SetFloat("_ZWrite", 1.0f);
            m_MobileHighlightMaterial.SetColor("_BaseColor", new Color(0.55f, 0.55f, 0.55f, 1.0f));
            m_MobileHighlightMaterial.EnableKeyword("_EMISSION");
            m_MobileHighlightMaterial.SetColor(
                "_EmissionColor",
                Shader.GetGlobalColor("_LeftEyeSelectionColor") * 0.55f);
            m_MobileHighlightMaterial.renderQueue = (int)RenderQueue.Geometry;
            return true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!UseMobileMaterialHighlights)
            {
                return;
            }

            RestoreAppliedMobileHighlights();
            Camera vrCamera = App.VrSdk != null ? App.VrSdk.GetVrCamera() : null;
            if (DisableSelectionEffects || camera == null || camera != vrCamera ||
                m_MobileHighlightStates == null || m_MobileHighlightStates.Count == 0 ||
                !EnsureMobileHighlightMaterial())
            {
                return;
            }

            foreach (var pair in m_MobileHighlightStates)
            {
                ApplyMobileHighlight(pair.Key, pair.Value);
            }
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            RestoreAppliedMobileHighlights();
        }

        private void ApplyMobileHighlight(Renderer renderer, MobileHighlightState state)
        {
            if (renderer == null || state.Applied || !renderer.gameObject.activeInHierarchy)
            {
                return;
            }

            state.OriginalMaterials = renderer.sharedMaterials;
            int originalMaterialCount = state.OriginalMaterials.Length;
            int highlightMaterialCount = Mathf.Max(1, originalMaterialCount);
            EnsureMobileHighlightStateCapacity(state, highlightMaterialCount);

            state.OriginalEnabled = renderer.enabled;
            state.OriginalForceRenderingOff = renderer.forceRenderingOff;
            state.OriginalRendererPropertyBlock ??= new MaterialPropertyBlock();
            state.OriginalRendererPropertyBlock.Clear();
            renderer.GetPropertyBlock(state.OriginalRendererPropertyBlock);
            state.HadOriginalRendererPropertyBlock =
                !state.OriginalRendererPropertyBlock.isEmpty;
            for (int i = 0; i < highlightMaterialCount; ++i)
            {
                state.HighlightMaterials[i] = m_MobileHighlightMaterial;

                if (i < originalMaterialCount)
                {
                    MaterialPropertyBlock originalProperties = state.OriginalPropertyBlocks[i];
                    originalProperties.Clear();
                    renderer.GetPropertyBlock(originalProperties, i);
                    state.HadOriginalPropertyBlocks[i] = !originalProperties.isEmpty;
                }
            }

            renderer.sharedMaterials = state.HighlightMaterials;
            renderer.SetPropertyBlock(null);
            for (int i = 0; i < highlightMaterialCount; ++i)
            {
                renderer.SetPropertyBlock(null, i);
            }
            renderer.forceRenderingOff = false;
            renderer.enabled = true;
            state.Applied = true;
        }

        private static void EnsureMobileHighlightStateCapacity(
            MobileHighlightState state, int materialCount)
        {
            if (state.HighlightMaterials != null && state.HighlightMaterials.Length == materialCount)
            {
                return;
            }

            state.HighlightMaterials = new Material[materialCount];
            state.OriginalPropertyBlocks = new MaterialPropertyBlock[materialCount];
            state.HadOriginalPropertyBlocks = new bool[materialCount];
            for (int i = 0; i < materialCount; ++i)
            {
                state.OriginalPropertyBlocks[i] = new MaterialPropertyBlock();
            }
        }

        private void UpdateMobileHighlightColor(Color color)
        {
            if (!UseMobileMaterialHighlights ||
                m_MobileHighlightStates == null || m_MobileHighlightStates.Count == 0 ||
                !EnsureMobileHighlightMaterial())
            {
                return;
            }

            float pulse = 0.45f + 0.2f * Mathf.Sin(Time.realtimeSinceStartup * 5.0f);
            m_MobileHighlightMaterial.SetColor(
                "_BaseColor", Color.Lerp(new Color(0.55f, 0.55f, 0.55f), color, 0.2f));
            m_MobileHighlightMaterial.SetColor("_EmissionColor", color * pulse);
        }

        private void RestoreStaleMobileHighlights()
        {
            if (m_MobileHighlightStates == null || m_MobileHighlightStates.Count == 0)
            {
                return;
            }

            List<Renderer> stale = null;
            foreach (var pair in m_MobileHighlightStates)
            {
                if (pair.Key == null || DisableSelectionEffects ||
                    pair.Value.RegisteredFrame != Time.frameCount)
                {
                    stale ??= new List<Renderer>();
                    stale.Add(pair.Key);
                }
            }
            if (stale == null)
            {
                return;
            }
            foreach (Renderer renderer in stale)
            {
                RemoveMobileHighlight(renderer);
            }
        }

        private void RestoreAppliedMobileHighlights()
        {
            if (m_MobileHighlightStates == null)
            {
                return;
            }
            foreach (var pair in m_MobileHighlightStates)
            {
                RestoreAppliedMobileHighlight(pair.Key, pair.Value);
            }
        }

        private static void RestoreAppliedMobileHighlight(
            Renderer renderer, MobileHighlightState state)
        {
            if (!state.Applied)
            {
                return;
            }

            state.Applied = false;
            if (renderer == null)
            {
                state.OriginalMaterials = null;
                return;
            }

            for (int i = 0; i < state.HighlightMaterials.Length; ++i)
            {
                renderer.SetPropertyBlock(null, i);
            }
            renderer.sharedMaterials = state.OriginalMaterials;
            renderer.SetPropertyBlock(
                state.HadOriginalRendererPropertyBlock
                    ? state.OriginalRendererPropertyBlock
                    : null);
            for (int i = 0; i < state.OriginalMaterials.Length; ++i)
            {
                renderer.SetPropertyBlock(
                    state.HadOriginalPropertyBlocks[i] ? state.OriginalPropertyBlocks[i] : null,
                    i);
            }
            renderer.forceRenderingOff = state.OriginalForceRenderingOff;
            renderer.enabled = state.OriginalEnabled;
            state.OriginalMaterials = null;
        }

        private void RemoveMobileHighlight(Renderer renderer)
        {
            if (ReferenceEquals(renderer, null) ||
                m_MobileHighlightStates == null ||
                !m_MobileHighlightStates.TryGetValue(renderer, out MobileHighlightState state))
            {
                return;
            }

            RestoreAppliedMobileHighlight(renderer, state);
            m_MobileHighlightStates.Remove(renderer);
        }

        private void OnDestroy()
        {
            if (InputManager.m_Instance != null)
            {
                InputManager.m_Instance.ControllerPosesApplied -= OnPosesApplied;
            }
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

            if (m_MobileHighlightStates != null)
            {
                var renderers = new List<Renderer>(m_MobileHighlightStates.Keys);
                foreach (Renderer renderer in renderers)
                {
                    RemoveMobileHighlight(renderer);
                }
            }
            Destroy(m_MobileHighlightMaterial);
        }
#endif

        public bool RenderHighlight()
        {
#if FEATURE_CUSTOM_MESH_RENDER
            return m_CmrRenderHighlight;
#else
    throw new InvalidOperationException();  // Don't know what to return
#endif
        }

        public void HighlightForGrab(bool grabAvailable)
        {
#if FEATURE_CUSTOM_MESH_RENDER
            m_CmrUseStrokePostEffect = !grabAvailable;
#endif
        }

        public void QueueUrpDiagnosticHighlight(
            MeshFilter meshFilter,
            int frameCount,
            bool useStrokePostEffect)
        {
#if FEATURE_CUSTOM_MESH_RENDER
            if (meshFilter == null || frameCount <= 0)
            {
                return;
            }

            m_UrpDiagnosticMeshes.Clear();
            m_UrpDiagnosticMeshes.Add(meshFilter);
            m_UrpDiagnosticFramesRemaining = frameCount;
            m_UrpDiagnosticUseStrokePostEffect = useStrokePostEffect;
            m_CmrUseStrokePostEffect = useStrokePostEffect;
            Shader.SetGlobalFloat("_GrabHighlightIntensity", 1.0f);
            Shader.SetGlobalFloat("_NoGrabHighlightIntensity", useStrokePostEffect ? 0.0f : 1.0f);
            Shader.SetGlobalColor("_GrabHighlightActiveColor", Color.white);
#endif
        }

#if FEATURE_CUSTOM_MESH_RENDER
        public bool ShouldRenderUrpSelection
        {
            get
            {
                return GetUrpSelectionBlockReason() == null;
            }
        }

        public string UrpSelectionDebugStatus =>
            GetUrpSelectionBlockReason() ??
            $"ready meshes={m_CmrRequestedMeshes.Count} " +
            $"skinned={m_CmrRequestedSkinnedMeshes.Count} frame={m_CmrRequestedMeshesFrame}";

        private string GetUrpSelectionBlockReason()
        {
            if (!Application.isPlaying) { return "not playing"; }
            if (GraphicsSettings.currentRenderPipeline == null) { return "no SRP"; }
            if (VideoRecorderUtils.ActiveVideoRecording?.IsCapturing == true) { return "video recording"; }
            if (DisableSelectionEffects) { return "selection effects disabled"; }
            if (OverlayManager.m_Instance == null) { return "overlay manager missing"; }
            if (OverlayManager.m_Instance.OverlayEnabled) { return "overlay enabled"; }
            // URP mask/composite uses materials owned by UrpSelectionRendererFeature.
            // Legacy post-effect materials are only required by the non-SRP OnRenderImage path.
            if (m_CmrRequestedMeshes == null) { return "requested mesh list missing"; }
            if (m_CmrRequestedSkinnedMeshes == null) { return "requested skinned mesh list missing"; }
            if (!HasActiveUrpRequestedMeshes && !HasActiveUrpDiagnosticMeshes)
            {
                return "no requested meshes";
            }
            return null;
        }

        private bool HasActiveUrpRequestedMeshes =>
            m_CmrRequestedMeshesFrame == Time.frameCount &&
            ((m_CmrRequestedMeshes != null &&
                m_CmrRequestedMeshes.Count > 0) ||
                (m_CmrRequestedSkinnedMeshes != null &&
                m_CmrRequestedSkinnedMeshes.Count > 0));

        private bool HasActiveUrpDiagnosticMeshes =>
            m_UrpDiagnosticFramesRemaining > 0 &&
            m_UrpDiagnosticMeshes != null &&
            m_UrpDiagnosticMeshes.Count > 0;

        public Material UrpStencilToMaskMaterial =>
            m_CmrRenderWrapper != null ? m_CmrRenderWrapper.m_StencilToMaskMaterial : null;

        public Material UrpPostEffectMaterial => ActivePostEffect();

        public float UrpBlurWidth => m_BlurWidth;

        public bool HasPreparedUrpSelectionFrame =>
            ShouldRenderUrpSelection;

        public void DrawUrpHighlightMeshes(CommandBuffer cmd, Material maskMaterial)
        {
            if (!ShouldRenderUrpSelection || maskMaterial == null)
            {
                return;
            }

            m_CmrRenderHighlight = true;
            int meshDraws = DrawMeshFilters(cmd, maskMaterial, m_CmrRequestedMeshes);
            int skinnedDraws = DrawSkinnedMeshRenderers(cmd, maskMaterial, m_CmrRequestedSkinnedMeshes);
            int diagnosticDraws = 0;
            if (HasActiveUrpDiagnosticMeshes)
            {
                diagnosticDraws = DrawMeshFilters(cmd, maskMaterial, m_UrpDiagnosticMeshes);
            }
        }

        private static int DrawMeshFilters(
            CommandBuffer cmd,
            Material maskMaterial,
            List<MeshFilter> meshFilters)
        {
            int draws = 0;
            for (int i = 0; i < meshFilters.Count; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null)
                {
                    continue;
                }

                Mesh mesh = meshFilter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Transform xf = meshFilter.transform;
                for (int iSubMesh = 0; iSubMesh < mesh.subMeshCount; iSubMesh++)
                {
                    cmd.DrawMesh(
                        mesh,
                        xf.localToWorldMatrix,
                        maskMaterial,
                        iSubMesh);
                    draws++;
                }
            }

            return draws;
        }

        private static int DrawSkinnedMeshRenderers(
            CommandBuffer cmd,
            Material maskMaterial,
            List<SkinnedMeshRenderer> renderers)
        {
            int draws = 0;
            for (int i = 0; i < renderers.Count; i++)
            {
                SkinnedMeshRenderer renderer = renderers[i];
                if (renderer == null ||
                    renderer.sharedMesh == null)
                {
                    continue;
                }
                if (!renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Mesh mesh = renderer.sharedMesh;
                for (int iSubMesh = 0; iSubMesh < mesh.subMeshCount; iSubMesh++)
                {
                    cmd.DrawRenderer(renderer, maskMaterial, iSubMesh);
                    draws++;
                }
            }

            return draws;
        }

        public void EndUrpSelectionFrame()
        {
            m_bCmrPosesApplied = false;
            m_bCmrPreCulled = false;
            m_CmrRenderHighlight = false;
        }
#else
        public bool ShouldRenderUrpSelection => false;

        public string UrpSelectionDebugStatus => "custom mesh selection disabled";

        public Material UrpPostEffectMaterial => null;

        public float UrpBlurWidth => 0.0f;

        public bool HasPreparedUrpSelectionFrame => false;

        public void DrawUrpHighlightMeshes(CommandBuffer cmd, Material maskMaterial) {}

        public void EndUrpSelectionFrame() {}
#endif

        // Internal API for FEATURE_CUSTOM_MESH_RENDER

        // If highlight meshes have been populated, render them using our special grab mask material
        // This material renders objects into the stencil buffer for outline generation
#if FEATURE_CUSTOM_MESH_RENDER
        private Material ActivePostEffect()
        {
            return m_CmrUseStrokePostEffect ? m_StrokeSelectionPostEffect : m_WidgetSelectionPostEffect;
        }

        private void RenderHighlightsIfReady()
        {
            // Deliberately Application.isMobilePlatform rather than Config.IsMobileHardware:
            // this gates on the actual graphics device, which spoofing mobile in the Editor
            // does not change.
            if (Application.isMobilePlatform &&
                GraphicsSettings.currentRenderPipeline == null)
            {
                return;
            }
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                if (ShouldRenderUrpSelection)
                {
                    m_CmrRenderHighlight = true;
                }
                return;
            }
            if (m_bCmrPreCulled && m_bCmrPosesApplied)
            {
                m_CmrRenderHighlight = false;

                try
                {
                    if (OverlayManager.m_Instance.OverlayEnabled) { return; }
                    for (int i = 0; i < m_CmrRequestedMeshes.Count; i++)
                    {
                        InternalDrawMesh(m_CmrRequestedMeshes[i]);
                    }
                }
                finally
                {
                    // Highlights are populated each frame, so we don't need to keep these lists around.
                    // Also, we clear the m_bPosesApplied and m_bPreCulled flags later on OnRenderImage()
                    // to guarantee that poses and cull have been done before clearing those flags.
                    m_CmrRequestedMeshes.Clear();
                    m_CmrRequestedSkinnedMeshes.Clear();
                }
            }
        }

        void InternalDrawMesh(MeshFilter meshFilter)
        {
            // Object might have been destroyed after the request was made.
            // This happens and is expected, eg with batches auto-destroying themselves.
            if (meshFilter == null)
            {
                return;
            }
            // This is not known to happen, but is theoretically possible.
            var mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                return;
            }
            var xf = meshFilter.transform;
            m_CmrRenderHighlight = true;

            Material selectMaterial = m_GrabHighlightMaskMaterial;

            for (int iSubMesh = 0; iSubMesh < mesh.subMeshCount; iSubMesh++)
            {
                Graphics.DrawMesh(
                    mesh, xf.localToWorldMatrix,
                    selectMaterial,
                    xf.gameObject.layer,
                    null, iSubMesh);
            }
        }

        // This must be excluded if unused; even a no-op post effect negatively impacts performance.
        // Postprocess the image
        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            // Clear flags for the next frame.
            m_bCmrPosesApplied = false;
            m_bCmrPreCulled = false;

            if (GraphicsSettings.currentRenderPipeline != null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Early out, if possible
            Material postEffect = ActivePostEffect();
            bool isRecording = (VideoRecorderUtils.ActiveVideoRecording != null) &&
                VideoRecorderUtils.ActiveVideoRecording.IsCapturing;
            if (isRecording || DisableSelectionEffects || !m_CmrRenderHighlight || !postEffect)
            {
                Graphics.Blit(source, destination);
                return;
            }

            RenderTextureFormat fmt = RenderTextureFormat.RFloat;
            int rtWidth = 1024;
            int rtHeight = 1024;

            // Downsample the mask

            // TODO:
            // We could steal another bit of alpha/exponent to squirrel away the stencil/mask.
            // Then we could do a single down sample pass along with bloom.

            RenderTexture maskTarget = m_CmrRenderWrapper.m_MaskTarget;

            RenderTexture rt = RenderTexture.GetTemporary(rtWidth, rtHeight, 0, fmt);
            rt.filterMode = FilterMode.Bilinear;
            // 4 tap downsample
            Graphics.Blit(maskTarget, rt, postEffect, (int)SelectionEffectPass.Downsample);

            RenderTexture rt2 = RenderTexture.GetTemporary(rtWidth / 2, rtWidth / 2, 0, fmt);
            rt2.filterMode = FilterMode.Bilinear;
            // 4 tap downsample
            Graphics.Blit(rt, rt2, postEffect, (int)SelectionEffectPass.Downsample);
            RenderTexture.ReleaseTemporary(rt);
            rt = rt2;

            //  Vertical / Horizontal Blur

            postEffect.SetFloat("_BlurSize", m_BlurWidth);

            // vertical blur
            rt2 = RenderTexture.GetTemporary(rtWidth / 2, rtWidth / 2, 0, fmt);
            rt2.filterMode = FilterMode.Bilinear;
            Graphics.Blit(rt, rt2, postEffect, (int)SelectionEffectPass.VerticalBlur);
            RenderTexture.ReleaseTemporary(rt);
            rt = rt2;

            // horizontal blur
            rt2 = RenderTexture.GetTemporary(rtWidth / 2, rtWidth / 2, 0, fmt);
            rt2.filterMode = FilterMode.Bilinear;
            Graphics.Blit(rt, rt2, postEffect, (int)SelectionEffectPass.HorizontalBlur);
            RenderTexture.ReleaseTemporary(rt);
            rt = rt2;

            // Using the previously created mask, composite it over the scene,
            // but only where the newly inflated outline is not drawing on top of
            // stencil ref 1 (highlight object).

            // Note that stencil ref 2 is reserved for ui objects.

            postEffect.SetTexture("_BlurredSelectionMask", rt2);
            postEffect.SetTexture("_SelectionMask", maskTarget);
            Graphics.Blit(source, destination, postEffect, (int)SelectionEffectPass.OutlineComposite);
            RenderTexture.ReleaseTemporary(rt);
        }
#endif // FEATURE_CUSTOM_MESH_RENDER
    }
} // namespace TiltBrush
