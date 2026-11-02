// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// Uses compact HDR targets where framebuffer alpha is not needed.
    public class UrpHdrRendererFeature : ScriptableRendererFeature
    {
        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var controller = UrpPostProcessingController.Instance;
            var scene = SceneSettings.m_Instance;
            // Keep alpha during passthrough, including transitions into and out of it.
            bool opaqueEnvironment = scene != null && scene.CurrentEnvironment != null &&
                scene.GetDesiredPreset() != null && !scene.CurrentEnvironment.isPassthrough &&
                !scene.GetDesiredPreset().isPassthrough;
            if (opaqueEnvironment && controller != null &&
                controller.SessionHdr && renderingData.cameraData.cameraType == CameraType.Game &&
                renderingData.cameraData.camera.targetTexture == null)
            {
                // Configure the public camera descriptor before URP allocates RenderGraph targets.
                // Framebuffer-alpha preservation otherwise forces HDR32 requests into RGBA16F.
                var descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.graphicsFormat = GraphicsFormat.B10G11R11_UFloatPack32;
                if (SystemInfo.IsFormatSupported(descriptor.graphicsFormat, GraphicsFormatUsage.Blend) &&
                    SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) == descriptor.msaaSamples)
                {
                    renderingData.cameraData.cameraTargetDescriptor = descriptor;
                    renderingData.cameraData.isAlphaOutputEnabled = false;
                }
            }
        }
    }
}
