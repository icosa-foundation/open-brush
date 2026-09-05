using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    public class UrpWatermarkRendererFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private RenderPassEvent m_RenderPassEvent =
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

        private class WatermarkPass : ScriptableRenderPass
        {
            private WatermarkEffect m_Watermark;

            public void Setup(WatermarkEffect watermark)
            {
                m_Watermark = watermark;
                requiresIntermediateTexture = true;
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
