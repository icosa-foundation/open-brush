// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// Restores the optional camera effects using the legacy components as serialized settings.
    /// Those components stay disabled because their OnRenderImage callbacks are Built-in only.
    public class UrpCaptureEffectsRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader m_VignetteShader;
        private Material m_VignetteMaterial;
        [SerializeField] private Shader m_TiltShiftShader;
        private Material m_TiltShiftMaterial;

        public override void Create()
        {
            CoreUtils.Destroy(m_VignetteMaterial);
            CoreUtils.Destroy(m_TiltShiftMaterial);
            m_VignetteMaterial = m_VignetteShader != null
                ? CoreUtils.CreateEngineMaterial(m_VignetteShader) : null;
            m_TiltShiftMaterial = m_TiltShiftShader != null
                ? CoreUtils.CreateEngineMaterial(m_TiltShiftShader) : null;
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_VignetteMaterial);
            CoreUtils.Destroy(m_TiltShiftMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null || renderingData.cameraData.cameraType != CameraType.Game ||
                !renderingData.cameraData.postProcessEnabled ||
                (camera.TryGetComponent(out RenderWrapper wrapper) && wrapper.SuppressPostEffects))
            {
                return;
            }

            if (m_TiltShiftMaterial != null && camera.TryGetComponent(out TiltShift tiltShift) &&
                !tiltShift.SuppressForCapture)
            {
                var properties = new MaterialPropertyBlock();
                if (tiltShift.mat != null)
                {
                    properties.SetFloat("_BlurAmount", tiltShift.mat.GetFloat("_BlurAmount"));
                    properties.SetFloat("_Center", tiltShift.mat.GetFloat("_Center"));
                    properties.SetFloat("_StepSize", tiltShift.mat.GetFloat("_StepSize"));
                    properties.SetFloat("_Steps", tiltShift.mat.GetFloat("_Steps"));
                }
                renderer.EnqueuePass(new CaptureEffectPass(
                    m_TiltShiftMaterial, properties, "Open Brush Capture Tilt Shift"));
            }

            if (m_VignetteMaterial != null && camera.TryGetComponent(out Kino.Vignette vignette))
            {
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_Falloff", vignette.intensity);
                properties.SetFloat("_ChromaticAberration", vignette.chromaticAberration);
                renderer.EnqueuePass(new CaptureEffectPass(
                    m_VignetteMaterial, properties, "Open Brush Capture Vignette"));
            }
        }

        private sealed class CaptureEffectPass : ScriptableRenderPass
        {
            private readonly Material m_Material;
            private readonly MaterialPropertyBlock m_Properties;
            private readonly string m_Name;

            public CaptureEffectPass(Material material, MaterialPropertyBlock properties, string name)
            {
                m_Material = material;
                m_Properties = properties;
                m_Name = name;
                // The renderer asset orders this feature before the watermark at the same event.
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;

                TextureHandle source = resources.activeColorTexture;
                TextureDesc descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = m_Name;
                descriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(descriptor);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(
                    source, destination, m_Material, 0, m_Properties,
                    geometry: RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
                renderGraph.AddBlitPass(parameters, passName: m_Name);
                resources.cameraColor = destination;
            }
        }
    }
}
