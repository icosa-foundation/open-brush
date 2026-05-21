using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    public class UrpWatermarkRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent m_RenderPassEvent =
            RenderPassEvent.AfterRenderingPostProcessing;

        private WatermarkPass m_Pass;

        public override void Create()
        {
            m_Pass = new WatermarkPass
            {
                renderPassEvent = m_RenderPassEvent
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            if (camera == null ||
                !camera.TryGetComponent(out WatermarkEffect watermark) ||
                !watermark.ShouldRender)
            {
                return;
            }

            m_Pass.Setup(watermark);
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
        }

        private class WatermarkPass : ScriptableRenderPass
        {
            private static readonly int MainTex = Shader.PropertyToID("_MainTex");
            private WatermarkEffect m_Watermark;
            private RTHandle m_TemporaryColorTexture;

            public void Setup(WatermarkEffect watermark)
            {
                m_Watermark = watermark;
                requiresIntermediateTexture = true;
            }

            public void Dispose()
            {
                m_TemporaryColorTexture?.Release();
                m_TemporaryColorTexture = null;
            }

            [System.Obsolete]
            public override void Execute(
                ScriptableRenderContext context,
                ref RenderingData renderingData)
            {
                if (m_Watermark == null || !m_Watermark.ShouldRender)
                {
                    return;
                }

                Material material = m_Watermark.Material;
                if (material == null)
                {
                    return;
                }

                RenderTextureDescriptor descriptor =
                    renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;

                RenderingUtils.ReAllocateIfNeeded(
                    ref m_TemporaryColorTexture,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_OpenBrushWatermarkTemp");

                m_Watermark.ConfigureMaterial(descriptor.width, descriptor.height);

                RTHandle cameraColorTarget =
                    renderingData.cameraData.renderer.cameraColorTargetHandle;
                CommandBuffer cmd = CommandBufferPool.Get("Open Brush Watermark");
                try
                {
                    Blitter.BlitCameraTexture(
                        cmd,
                        cameraColorTarget,
                        m_TemporaryColorTexture);
                    cmd.SetGlobalTexture(MainTex, m_TemporaryColorTexture);
                    Blitter.BlitCameraTexture(
                        cmd,
                        m_TemporaryColorTexture,
                        cameraColorTarget,
                        material,
                        0);
                    context.ExecuteCommandBuffer(cmd);
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                    m_Watermark = null;
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_Watermark == null || !m_Watermark.ShouldRender)
                {
                    return;
                }

                Material material = m_Watermark.Material;
                if (material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
                m_Watermark.ConfigureMaterial(descriptor.width, descriptor.height);

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "_OpenBrushWatermarkGraphColor";
                destinationDesc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                RenderGraphUtils.BlitMaterialParameters parameters =
                    new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, passName: "Open Brush Watermark");
                resourceData.cameraColor = destination;
                m_Watermark = null;
            }
        }
    }
}
