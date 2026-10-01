using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// Extracts the legacy HDR_SIMPLE brush signal before URP post-processing.
    public class UrpEncodedBloomRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader m_Shader;
        [SerializeField, Range(1, 5)] private int m_BlurLevels = 3;
        [SerializeField, Range(2, 4)] private int m_Downsample = 2;
        [SerializeField, Min(0)] private float m_Intensity = 1f;

        private Material m_Material;
        private EncodedBloomPass m_Pass;

        public static bool IsConfigured(UniversalRenderPipelineAsset pipeline)
        {
            foreach (ScriptableRendererData data in pipeline.rendererDataList)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature is UrpEncodedBloomRendererFeature bloom &&
                        bloom.isActive && bloom.m_Shader != null)
                        return true;
                }
            }
            return false;
        }

        public override void Create()
        {
            CoreUtils.Destroy(m_Material);
            m_Material = m_Shader != null ? CoreUtils.CreateEngineMaterial(m_Shader) : null;
            m_Pass = new EncodedBloomPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(m_Material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var controller = UrpPostProcessingController.Instance;
            if (controller == null || !controller.UsesEncodedBloom || m_Material == null ||
                !renderingData.cameraData.postProcessEnabled ||
                renderingData.cameraData.isSceneViewCamera ||
                renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            float amount = controller.EncodedBloomAmount;
            if (renderingData.cameraData.camera.TryGetComponent(out MobileBloom legacyBloom))
                amount *= legacyBloom.EvaluateBloomAmount(1f);
            if (amount <= 0f || m_Intensity <= 0f) return;

            m_Pass.Setup(m_Material, m_BlurLevels, m_Downsample,
                amount * m_Intensity, controller.EncodedBloomThreshold);
            renderer.EnqueuePass(m_Pass);
        }

        private class EncodedBloomPass : ScriptableRenderPass
        {
            private static readonly int kAmount = Shader.PropertyToID("_EncodedBloomAmount");
            private static readonly int kThreshold = Shader.PropertyToID("_EncodedBloomThreshold");
            private static readonly int kTexelSize = Shader.PropertyToID("_EncodedBloomTexelSize");
            private Material m_Material;
            private int m_Levels;
            private int m_Downsample;
            private float m_Amount;
            private float m_Threshold;
            private bool m_LoggedFormat;

            private class AddPassData
            {
                public TextureHandle source;
                public Material material;
                public MaterialPropertyBlock properties;
            }

            public void Setup(Material material, int levels, int downsample, float amount, float threshold)
            {
                m_Material = material;
                m_Levels = Mathf.Clamp(levels, 1, 5);
                m_Downsample = Mathf.Clamp(downsample, 2, 4);
                m_Amount = amount;
                m_Threshold = threshold;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                TextureHandle cameraColor = resources.activeColorTexture;
                TextureDesc sourceDesc = graph.GetTextureDesc(cameraColor);
                if (!m_LoggedFormat)
                {
                    Debug.Log($"[OB_ENCODED_BLOOM] Camera target={sourceDesc.format}, " +
                        $"size={sourceDesc.width}x{sourceDesc.height}, slices={sourceDesc.slices}, " +
                        $"MSAA={sourceDesc.msaaSamples}. Native bloom disabled; no scene-colour copy.");
                    m_LoggedFormat = true;
                }
                if (!GraphicsFormatUtility.HasAlphaChannel(sourceDesc.format))
                {
                    Debug.LogError("[OB_ENCODED_BLOOM] Camera colour must retain the HDR_SIMPLE alpha signal.");
                    return;
                }

                var levels = new TextureHandle[m_Levels];
                TextureHandle previous = cameraColor;
                TextureDesc previousDesc = sourceDesc;
                int levelCount = 0;
                for (int i = 0; i < m_Levels; ++i)
                {
                    TextureDesc desc = sourceDesc;
                    desc.width = Mathf.Max(1, previousDesc.width / m_Downsample);
                    desc.height = Mathf.Max(1, previousDesc.height / m_Downsample);
                    desc.msaaSamples = MSAASamples.None;
                    desc.bindTextureMS = false;
                    desc.depthBufferBits = DepthBits.None;
                    // The old mobile pyramid was LDR; keep its storage and saturation behaviour.
                    desc.format = GraphicsFormat.R8G8B8A8_UNorm;
                    desc.filterMode = FilterMode.Bilinear;
                    desc.name = $"Open Brush encoded bloom level {i}";
                    desc.clearBuffer = false;
                    levels[i] = graph.CreateTexture(desc);
                    var properties = CreateProperties(previousDesc.width, previousDesc.height, 1f);
                    var parameters = new RenderGraphUtils.BlitMaterialParameters(
                        previous, levels[i], m_Material, i == 0 ? 0 : 1,
                        properties, destinationSlice: 0, destinationMip: 0,
                        geometry: RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);
                    graph.AddBlitPass(parameters, passName: i == 0
                        ? "Open Brush encoded bloom extraction" : $"Open Brush encoded bloom downsample {i}");
                    previous = levels[i];
                    previousDesc = desc;
                    levelCount++;
                    if (desc.width == 1 && desc.height == 1) break;
                }

                for (int i = levelCount - 1; i > 0; --i)
                    AddGlow(graph, levels[i], levels[i - 1], 1f, $"Open Brush encoded bloom upsample {i}");
                // Blend directly into the existing camera attachment: no copy of scene colour.
                AddGlow(graph, levels[0], cameraColor, m_Amount, "Open Brush encoded bloom composite");
            }

            private MaterialPropertyBlock CreateProperties(int width, int height, float amount)
            {
                var properties = new MaterialPropertyBlock();
                properties.SetFloat(kAmount, amount);
                properties.SetFloat(kThreshold, m_Threshold);
                properties.SetVector(kTexelSize, new Vector4(1f / width, 1f / height, width, height));
                return properties;
            }

            private void AddGlow(RenderGraph graph, TextureHandle source, TextureHandle target,
                float amount, string name)
            {
                TextureDesc desc = graph.GetTextureDesc(source);
                using var builder = graph.AddRasterRenderPass<AddPassData>(name, out var data);
                data.source = source;
                data.material = m_Material;
                data.properties = CreateProperties(desc.width, desc.height, amount);
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(target, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((AddPassData pass, RasterGraphContext context) =>
                {
                    // A per-pass property block keeps cameras and pyramid levels independent.
                    pass.properties.SetTexture("_BlitTexture", (RTHandle)pass.source);
                    pass.properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, 2,
                        MeshTopology.Triangles, 3, 1, pass.properties);
                });
            }
        }
    }
}
