using System.Collections.Generic;
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

        public static int RuntimeLevels { get; set; } = 3;
        public static int RuntimeDownsample { get; set; } = 2;
        public static bool AlternateEyes { get; set; }
        public static bool ReprojectHistory { get; set; } = true;
        public static int HistoryRevision { get; set; }
        private static readonly Dictionary<Camera, string> s_Reuse = new Dictionary<Camera, string>();
        public static string GetReuse(Camera camera) =>
            camera != null && s_Reuse.TryGetValue(camera, out var reuse) ? reuse : "not rendered";
        public static void ResetDiagnostics() { s_Reuse.Clear(); LastPassCount = 0; }
        public static int LastPassCount { get; private set; }

        private Material m_Material;
        private EncodedBloomPass m_Pass;
        private readonly DiagnosticPass m_Diagnostics = new DiagnosticPass();

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
            m_Pass?.Dispose();
            CoreUtils.Destroy(m_Material);
            m_Material = m_Shader != null ? CoreUtils.CreateEngineMaterial(m_Shader) : null;
            m_Pass = new EncodedBloomPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            CoreUtils.Destroy(m_Material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var controller = UrpPostProcessingController.Instance;
            if (BloomBenchmark.Enabled && controller != null && !renderingData.cameraData.isSceneViewCamera)
                renderer.EnqueuePass(m_Diagnostics);
            if (controller == null || !controller.UsesEncodedBloom || m_Material == null ||
                !renderingData.cameraData.postProcessEnabled ||
                renderingData.cameraData.isSceneViewCamera ||
                renderingData.cameraData.cameraType == CameraType.Preview)
                return;

            float amount = controller.EncodedBloomAmount;
            if (renderingData.cameraData.camera.TryGetComponent(out MobileBloom legacyBloom))
                amount *= legacyBloom.EvaluateBloomAmount(1f);
            if (amount <= 0f || m_Intensity <= 0f) return;

            m_Pass.Setup(m_Material, BloomBenchmark.Enabled ? RuntimeLevels : m_BlurLevels,
                BloomBenchmark.Enabled ? RuntimeDownsample : m_Downsample,
                amount * m_Intensity, controller.EncodedBloomThreshold);
            renderer.EnqueuePass(m_Pass);
        }

        private class DiagnosticPass : ScriptableRenderPass
        {
            public DiagnosticPass() { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>().camera;
                if (resources.isActiveTargetBackBuffer || BloomBenchmark.Instance == null) return;
                if (!BloomBenchmark.Instance.WantsTarget(camera)) return;
                var desc = graph.GetTextureDesc(resources.activeColorTexture);
                BloomBenchmark.Instance.RecordTarget(camera, new BloomBenchmark.TextureDescInfo {
                    format = desc.format.ToString(), width = desc.width, height = desc.height,
                    slices = desc.slices, msaa = (int)desc.msaaSamples, vrUsage = desc.vrUsage.ToString()
                });
            }
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
            private int m_UpdateEye = -1;
            private Matrix4x4 m_LeftWarp = Matrix4x4.identity;
            private Matrix4x4 m_RightWarp = Matrix4x4.identity;
            private readonly Dictionary<Camera, History> m_Histories = new Dictionary<Camera, History>();

            private class History
            {
                public RTHandle texture;
                public int width, height, slices, levels, downsample, revision, frame = -100;
                public float threshold;
                public readonly Matrix4x4[] rotationView = new Matrix4x4[2];
                public readonly Matrix4x4[] projection = new Matrix4x4[2];
                public Quaternion rotation;
            }

            public void Dispose()
            {
                foreach (var history in m_Histories.Values) history.texture?.Release();
                m_Histories.Clear();
            }

            private static Matrix4x4 RotationOnly(Matrix4x4 view)
            {
                view.SetColumn(3, new Vector4(0, 0, 0, 1));
                return view;
            }

            private class AddPassData
            {
                public TextureHandle source;
                public Material material;
                public int shaderPass;
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
                var cameraData = frameData.Get<UniversalCameraData>();
                m_UpdateEye = -1;
                m_LeftWarp = m_RightWarp = Matrix4x4.identity;
                LastPassCount = 0;
                bool reuse = AlternateEyes && cameraData.xr.enabled && cameraData.xr.singlePassEnabled &&
                    sourceDesc.dimension == TextureDimension.Tex2DArray && sourceDesc.slices == 2;
                s_Reuse[cameraData.camera] = reuse ? "alternating array slices" : AlternateEyes ? "unsupported layout: both eyes" : "both eyes";
                History history = null;
                if (!AlternateEyes && m_Histories.Count > 0) Dispose();
                if (reuse)
                {
                    Camera camera = cameraData.camera;
                    if (!m_Histories.TryGetValue(camera, out history))
                        m_Histories.Add(camera, history = new History());
                    int width = Mathf.Max(1, sourceDesc.width / m_Downsample);
                    int height = Mathf.Max(1, sourceDesc.height / m_Downsample);
                    bool resized = history.texture == null || history.width != width || history.height != height ||
                        history.slices != sourceDesc.slices;
                    bool reset = resized || history.frame != Time.frameCount - 1 || history.levels != m_Levels ||
                        history.downsample != m_Downsample || history.threshold != m_Threshold ||
                        history.revision != HistoryRevision || Quaternion.Angle(history.rotation, camera.transform.rotation) > 20f;
                    if (resized)
                    {
                        history.texture?.Release();
                        var descriptor = new RenderTextureDescriptor(width, height)
                        {
                            graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm,
                            dimension = TextureDimension.Tex2DArray, volumeDepth = 2,
                            // Preserve the source's XR usage so Vulkan multiview writes both slices.
                            vrUsage = sourceDesc.vrUsage,
                            msaaSamples = 1, depthBufferBits = 0
                        };
                        history.texture = RTHandles.Alloc(descriptor, filterMode: FilterMode.Bilinear,
                            wrapMode: TextureWrapMode.Clamp, name: "Open Brush encoded bloom eye history");
                        history.width = width; history.height = height; history.slices = 2;
                    }
                    m_UpdateEye = reset ? -1 : Time.frameCount & 1;
                    for (int eye = 0; eye < 2; ++eye)
                    {
                        Matrix4x4 rotation = RotationOnly(cameraData.GetViewMatrix(eye));
                        Matrix4x4 projection = GL.GetGPUProjectionMatrix(cameraData.GetProjectionMatrix(eye), true);
                        if (m_UpdateEye == -1 || m_UpdateEye == eye)
                        {
                            history.rotationView[eye] = rotation;
                            history.projection[eye] = projection;
                        }
                        else if (ReprojectHistory)
                        {
                            Matrix4x4 warp = history.projection[eye] * history.rotationView[eye] *
                                rotation.inverse * projection.inverse;
                            if (eye == 0) m_LeftWarp = warp; else m_RightWarp = warp;
                        }
                    }
                    history.frame = Time.frameCount; history.rotation = camera.transform.rotation;
                    history.levels = m_Levels; history.downsample = m_Downsample;
                    history.threshold = m_Threshold; history.revision = HistoryRevision;
                }
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
                    levels[i] = i == 0 && history != null
                        ? graph.ImportTexture(history.texture) : graph.CreateTexture(desc);
                    DrawGlow(graph, previous, levels[i], 1f, i == 0 ? 0 : 1,
                        i == 0 ? "Open Brush encoded bloom extraction" : $"Open Brush encoded bloom downsample {i}",
                        i == 0 && history != null ? AccessFlags.ReadWrite : AccessFlags.Write);
                    previous = levels[i];
                    previousDesc = desc;
                    levelCount++;
                    if (desc.width == 1 && desc.height == 1) break;
                }

                for (int i = levelCount - 1; i > 0; --i)
                    DrawGlow(graph, levels[i], levels[i - 1], 1f, 2, $"Open Brush encoded bloom upsample {i}");
                // Blend directly into the existing camera attachment: no copy of scene colour.
                m_UpdateEye = -1;
                DrawGlow(graph, levels[0], cameraColor, m_Amount, 3, "Open Brush encoded bloom composite");
            }

            private MaterialPropertyBlock CreateProperties(int width, int height, float amount)
            {
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_EncodedBloomUpdateEye", m_UpdateEye);
                properties.SetMatrix("_EncodedBloomLeftWarp", m_LeftWarp);
                properties.SetMatrix("_EncodedBloomRightWarp", m_RightWarp);
                properties.SetFloat(kAmount, amount);
                properties.SetFloat(kThreshold, m_Threshold);
                properties.SetVector(kTexelSize, new Vector4(1f / width, 1f / height, width, height));
                return properties;
            }

            private void DrawGlow(RenderGraph graph, TextureHandle source, TextureHandle target,
                float amount, int shaderPass, string name, AccessFlags access = AccessFlags.ReadWrite)
            {
                TextureDesc desc = graph.GetTextureDesc(source);
                using var builder = graph.AddRasterRenderPass<AddPassData>(name, out var data);
                data.source = source;
                data.material = m_Material;
                data.shaderPass = shaderPass;
                LastPassCount++;
                data.properties = CreateProperties(desc.width, desc.height, amount);
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(target, 0, access);
                builder.SetRenderFunc((AddPassData pass, RasterGraphContext context) =>
                {
                    // A per-pass property block keeps cameras and pyramid levels independent.
                    pass.properties.SetTexture("_BlitTexture", (RTHandle)pass.source);
                    pass.properties.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                    context.cmd.DrawProcedural(Matrix4x4.identity, pass.material, pass.shaderPass,
                        MeshTopology.Triangles, 3, 1, pass.properties);
                });
            }
        }
    }
}
