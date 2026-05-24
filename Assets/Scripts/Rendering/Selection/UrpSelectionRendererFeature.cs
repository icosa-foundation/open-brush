using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    public class UrpSelectionRendererFeature : ScriptableRendererFeature
    {
        private const string kDebugLogPrefix = "[OB_URP_SELECTION_DIAG]";
        private static int s_DebugSkipLogCount;

        [SerializeField]
        private RenderPassEvent m_RenderPassEvent =
            RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField]
        private DebugOutput m_DebugOutput = DebugOutput.Off;

        private SelectionPass m_Pass;
        private Material m_MaskMaterial;

        private enum DebugOutput
        {
            Off,
            RawMask
        }

        public override void Create()
        {
            Shader maskShader = Shader.Find("Hidden/UrpSelectionMask");
            if (maskShader != null)
            {
                m_MaskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            }

            m_Pass = new SelectionPass
            {
                renderPassEvent = m_RenderPassEvent
            };
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            SelectionEffect selection = null;
            if (camera != null)
            {
                camera.TryGetComponent(out selection);
            }
            if (selection == null && App.Instance != null)
            {
                selection = App.Instance.SelectionEffect;
            }
            if (camera == null ||
                selection == null)
            {
                return;
            }
            if (renderingData.cameraData.isSceneViewCamera ||
                camera.cameraType == CameraType.SceneView ||
                camera.cameraType == CameraType.Preview)
            {
                return;
            }
            if (!selection.ShouldRenderUrpSelection)
            {
                if (m_DebugOutput == DebugOutput.RawMask && s_DebugSkipLogCount < 12)
                {
                    Debug.Log(
                        $"{kDebugLogPrefix} Selection pass skipped camera={camera.name} " +
                        $"status={selection.UrpSelectionDebugStatus}.");
                    s_DebugSkipLogCount++;
                }
                return;
            }

            m_Pass.Setup(selection, m_MaskMaterial, m_DebugOutput == DebugOutput.RawMask);
            renderer.EnqueuePass(m_Pass);
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            CoreUtils.Destroy(m_MaskMaterial);
        }

        private class SelectionPass : ScriptableRenderPass
        {
            private enum SelectionEffectPass
            {
                OutlineComposite,
                Downsample,
                VerticalBlur,
                HorizontalBlur
            }

            private static readonly int MainTex = Shader.PropertyToID("_MainTex");
            private static readonly int SelectionMask = Shader.PropertyToID("_SelectionMask");
            private static readonly int BlurredSelectionMask =
                Shader.PropertyToID("_BlurredSelectionMask");
            private static readonly int SelectionColorSource =
                Shader.PropertyToID("_SelectionColorSource");
            private static readonly int UrpSelectionMask =
                Shader.PropertyToID("_UrpSelectionMask");
            private static readonly int UrpBlurredSelectionMask =
                Shader.PropertyToID("_UrpBlurredSelectionMask");
            private static readonly int BlurSize = Shader.PropertyToID("_BlurSize");
            private static readonly int UseBlitTexture = Shader.PropertyToID("_UseBlitTexture");
            private static readonly int SelectionUrpBlit =
                Shader.PropertyToID("_SelectionUrpBlit");
            private static readonly int MainTexTexelSize = Shader.PropertyToID("_MainTex_TexelSize");

            private SelectionEffect m_Selection;
            private Material m_MaskMaterial;
            private bool m_DebugRawMask;
            private RTHandle m_ColorCopy;
            private RTHandle m_SelectionMask;
            private RTHandle m_DownsampleA;
            private RTHandle m_DownsampleB;
            private RTHandle m_BlurA;

            public void Setup(SelectionEffect selection, Material maskMaterial, bool debugRawMask)
            {
                m_Selection = selection;
                m_MaskMaterial = maskMaterial;
                m_DebugRawMask = debugRawMask;
            }

            public void Dispose()
            {
                m_ColorCopy?.Release();
                m_SelectionMask?.Release();
                m_DownsampleA?.Release();
                m_DownsampleB?.Release();
                m_BlurA?.Release();
                m_ColorCopy = null;
                m_SelectionMask = null;
                m_DownsampleA = null;
                m_DownsampleB = null;
                m_BlurA = null;
            }

            [System.Obsolete]
            public override void Execute(
                ScriptableRenderContext context,
                ref RenderingData renderingData)
            {
                if (m_Selection == null)
                {
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get("Open Brush Selection");
                try
                {
                    if (!m_Selection.HasPreparedUrpSelectionFrame)
                    {
                        return;
                    }

                    Material postEffect = m_Selection.UrpPostEffectMaterial;
                    if (m_MaskMaterial == null || postEffect == null)
                    {
                        return;
                    }

                    RenderTextureDescriptor cameraDescriptor =
                        renderingData.cameraData.cameraTargetDescriptor;
                    cameraDescriptor.depthBufferBits = 0;
                    cmd.SetGlobalFloat(UseBlitTexture, 0f);

                    RenderingUtils.ReAllocateIfNeeded(
                        ref m_ColorCopy,
                        cameraDescriptor,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_OpenBrushSelectionColorCopy");

                    RenderTextureDescriptor maskDescriptor = cameraDescriptor;
                    maskDescriptor.msaaSamples = 1;
                    maskDescriptor.colorFormat = RenderTextureFormat.RFloat;

                    RenderingUtils.ReAllocateIfNeeded(
                        ref m_SelectionMask,
                        maskDescriptor,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_OpenBrushSelectionMask");

                    RenderTextureDescriptor blurDescriptor = maskDescriptor;
                    blurDescriptor.width = 1024;
                    blurDescriptor.height = 1024;

                    RenderingUtils.ReAllocateIfNeeded(
                        ref m_DownsampleA,
                        blurDescriptor,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_OpenBrushSelectionDownsampleA");

                    blurDescriptor.width = 512;
                    blurDescriptor.height = 512;

                    RenderingUtils.ReAllocateIfNeeded(
                        ref m_DownsampleB,
                        blurDescriptor,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_OpenBrushSelectionDownsampleB");

                    RenderingUtils.ReAllocateIfNeeded(
                        ref m_BlurA,
                        blurDescriptor,
                        FilterMode.Bilinear,
                        TextureWrapMode.Clamp,
                        name: "_OpenBrushSelectionBlurA");

                    RTHandle cameraColorTarget =
                        renderingData.cameraData.renderer.cameraColorTargetHandle;
                    CoreUtils.SetRenderTarget(
                        cmd,
                        m_SelectionMask,
                        ClearFlag.Color,
                        Color.black);
                    m_Selection.DrawUrpHighlightMeshes(cmd, m_MaskMaterial);
                    if (m_DebugRawMask)
                    {
                        cmd.Blit(m_SelectionMask, cameraColorTarget);
                        context.ExecuteCommandBuffer(cmd);
                        return;
                    }

                    cmd.Blit(
                        m_SelectionMask,
                        m_DownsampleA,
                        postEffect,
                        (int)SelectionEffectPass.Downsample);
                    cmd.Blit(
                        m_DownsampleA,
                        m_DownsampleB,
                        postEffect,
                        (int)SelectionEffectPass.Downsample);

                    postEffect.SetFloat(BlurSize, m_Selection.UrpBlurWidth);
                    cmd.Blit(
                        m_DownsampleB,
                        m_BlurA,
                        postEffect,
                        (int)SelectionEffectPass.VerticalBlur);
                    cmd.Blit(
                        m_BlurA,
                        m_DownsampleB,
                        postEffect,
                        (int)SelectionEffectPass.HorizontalBlur);

                    cmd.Blit(cameraColorTarget, m_ColorCopy);
                    cmd.SetGlobalTexture(MainTex, m_ColorCopy);
                    cmd.SetGlobalTexture(SelectionMask, m_SelectionMask);
                    cmd.SetGlobalTexture(BlurredSelectionMask, m_DownsampleB);
                    cmd.Blit(
                        m_ColorCopy,
                        cameraColorTarget,
                        postEffect,
                        (int)SelectionEffectPass.OutlineComposite);

                    context.ExecuteCommandBuffer(cmd);
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                    m_Selection.EndUrpSelectionFrame();
                    m_Selection = null;
                    m_MaskMaterial = null;
                    m_DebugRawMask = false;
                }
            }

            private class PassData
            {
                internal SelectionEffect selection;
                internal Material maskMaterial;
                internal TextureHandle selectionMask;
            }

            private class CompositeTexturePassData
            {
                internal TextureHandle colorCopy;
                internal TextureHandle destination;
                internal TextureHandle selectionMask;
                internal TextureHandle blurredSelectionMask;
                internal Material material;
                internal MaterialPropertyBlock properties;
                internal Mesh fullscreenMesh;
                internal int shaderPass;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                if (m_Selection == null ||
                    !m_Selection.HasPreparedUrpSelectionFrame)
                {
                    return;
                }

                Material postEffect = m_Selection.UrpPostEffectMaterial;
                if (m_MaskMaterial == null || postEffect == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                RenderTextureDescriptor cameraDescriptor = cameraData.cameraTargetDescriptor;
                cameraDescriptor.depthBufferBits = 0;

                TextureHandle cameraColor = resourceData.activeColorTexture;
                TextureDesc colorCopyDesc = renderGraph.GetTextureDesc(cameraColor);
                colorCopyDesc.name = "_OpenBrushSelectionGraphColorCopy";
                colorCopyDesc.clearBuffer = false;
                colorCopyDesc.msaaSamples = MSAASamples.None;
                TextureHandle colorCopy = renderGraph.CreateTexture(colorCopyDesc);

                RenderTextureDescriptor maskDescriptor = cameraDescriptor;
                maskDescriptor.msaaSamples = 1;
                maskDescriptor.colorFormat = RenderTextureFormat.RFloat;
                TextureHandle selectionMask = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    maskDescriptor,
                    "_OpenBrushSelectionGraphMask",
                    true,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp);

                RenderTextureDescriptor blurDescriptor = maskDescriptor;
                blurDescriptor.width = 1024;
                blurDescriptor.height = 1024;
                TextureHandle downsampleA = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    blurDescriptor,
                    "_OpenBrushSelectionGraphDownsampleA",
                    false,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp);

                blurDescriptor.width = 512;
                blurDescriptor.height = 512;
                TextureHandle downsampleB = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    blurDescriptor,
                    "_OpenBrushSelectionGraphDownsampleB",
                    false,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp);
                TextureHandle blurA = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    blurDescriptor,
                    "_OpenBrushSelectionGraphBlurA",
                    false,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp);

                using (var builder = renderGraph.AddUnsafePass<PassData>(
                           "Open Brush Selection Mask",
                           out var passData))
                {
                    passData.selection = m_Selection;
                    passData.maskMaterial = m_MaskMaterial;
                    passData.selectionMask = selectionMask;

                    builder.UseTexture(passData.selectionMask, AccessFlags.ReadWrite);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                        ExecuteRenderGraphMaskPass(data, context));
                    builder.SetGlobalTextureAfterPass(selectionMask, SelectionMask);
                }

                if (m_DebugRawMask)
                {
                    renderGraph.AddBlitPass(
                        selectionMask,
                        cameraColor,
                        Vector2.one,
                        Vector2.zero,
                        passName: "Open Brush Selection Raw Mask");
                }
                else
                {
                    renderGraph.AddBlitPass(
                        CreateSelectionBlitParameters(
                            selectionMask,
                            downsampleA,
                            postEffect,
                            (int)SelectionEffectPass.Downsample,
                            CreateBlitProperties(maskDescriptor.width, maskDescriptor.height)),
                        "Open Brush Selection Downsample A");

                    renderGraph.AddBlitPass(
                        CreateSelectionBlitParameters(
                            downsampleA,
                            downsampleB,
                            postEffect,
                            (int)SelectionEffectPass.Downsample,
                            CreateBlitProperties(1024, 1024)),
                        "Open Brush Selection Downsample B");

                    renderGraph.AddBlitPass(
                        CreateSelectionBlitParameters(
                            downsampleB,
                            blurA,
                            postEffect,
                            (int)SelectionEffectPass.VerticalBlur,
                            CreateBlitProperties(
                                512,
                                512,
                                m_Selection.UrpBlurWidth)),
                        "Open Brush Selection Vertical Blur");

                    using (var horizontalBuilder = renderGraph.AddBlitPass(
                               CreateSelectionBlitParameters(
                                   blurA,
                                   downsampleB,
                                   postEffect,
                                   (int)SelectionEffectPass.HorizontalBlur,
                                   CreateBlitProperties(
                                       512,
                                       512,
                                       m_Selection.UrpBlurWidth)),
                               "Open Brush Selection Horizontal Blur",
                               true))
                    {
                        horizontalBuilder.SetGlobalTextureAfterPass(
                            downsampleB,
                            BlurredSelectionMask);
                    }

                    renderGraph.AddBlitPass(
                        cameraColor,
                        colorCopy,
                        Vector2.one,
                        Vector2.zero,
                        passName: "Open Brush Selection Color Copy");

                    using (var compositeBuilder = renderGraph.AddUnsafePass<CompositeTexturePassData>(
                               "Open Brush Selection Composite",
                               out var compositeData))
                    {
                        compositeData.colorCopy = colorCopy;
                        compositeData.destination = cameraColor;
                        compositeData.selectionMask = selectionMask;
                        compositeData.blurredSelectionMask = downsampleB;
                        compositeData.material = postEffect;
                        compositeData.properties = CreateBlitProperties(
                            cameraDescriptor.width,
                            cameraDescriptor.height,
                            urpBlitMode: 2f);
                        compositeData.fullscreenMesh = RenderingUtils.fullscreenMesh;
                        compositeData.shaderPass = (int)SelectionEffectPass.OutlineComposite;

                        compositeBuilder.UseTexture(compositeData.colorCopy, AccessFlags.Read);
                        compositeBuilder.UseTexture(compositeData.destination, AccessFlags.ReadWrite);
                        compositeBuilder.UseTexture(compositeData.selectionMask, AccessFlags.Read);
                        compositeBuilder.UseTexture(
                            compositeData.blurredSelectionMask,
                            AccessFlags.Read);
                        compositeBuilder.AllowGlobalStateModification(true);
                        compositeBuilder.AllowPassCulling(false);
                        compositeBuilder.SetRenderFunc(
                            static (CompositeTexturePassData data, UnsafeGraphContext context) =>
                                ExecuteCompositePass(data, context));
                    }
                }

                m_Selection = null;
                m_MaskMaterial = null;
                m_DebugRawMask = false;
            }

            private static RenderGraphUtils.BlitMaterialParameters CreateSelectionBlitParameters(
                TextureHandle source,
                TextureHandle destination,
                Material material,
                int pass,
                MaterialPropertyBlock properties)
            {
                return new RenderGraphUtils.BlitMaterialParameters(
                    source,
                    destination,
                    material,
                    pass,
                    properties,
                    RenderGraphUtils.FullScreenGeometryType.Mesh,
                    MainTex);
            }

            private static MaterialPropertyBlock CreateBlitProperties(
                int width,
                int height,
                float? blurSize = null,
                float urpBlitMode = 1f)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                properties.SetFloat(UseBlitTexture, 0f);
                properties.SetFloat(SelectionUrpBlit, urpBlitMode);
                properties.SetVector(
                    MainTexTexelSize,
                    new Vector4(1.0f / width, 1.0f / height, width, height));
                if (blurSize.HasValue)
                {
                    properties.SetFloat(BlurSize, blurSize.Value);
                }
                return properties;
            }

            private static void ExecuteRenderGraphMaskPass(
                PassData data,
                UnsafeGraphContext context)
            {
                CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                try
                {
                    context.cmd.SetRenderTarget(data.selectionMask, 0, CubemapFace.Unknown, -1);
                    context.cmd.ClearRenderTarget(false, true, Color.black);
                    data.selection.DrawUrpHighlightMeshes(unsafeCmd, data.maskMaterial);
                }
                finally
                {
                    context.cmd.SetGlobalFloat(UseBlitTexture, 0f);
                    data.selection.EndUrpSelectionFrame();
                }
            }

            private static void ExecuteCompositePass(
                CompositeTexturePassData data,
                UnsafeGraphContext context)
            {
                context.cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                context.cmd.SetGlobalFloat(UseBlitTexture, 0f);
                context.cmd.SetGlobalFloat(SelectionUrpBlit, 2f);
                context.cmd.SetGlobalTexture(MainTex, data.colorCopy);
                context.cmd.SetGlobalTexture(SelectionColorSource, data.colorCopy);
                context.cmd.SetGlobalTexture(SelectionMask, data.selectionMask);
                context.cmd.SetGlobalTexture(UrpSelectionMask, data.selectionMask);
                context.cmd.SetGlobalTexture(BlurredSelectionMask, data.blurredSelectionMask);
                context.cmd.SetGlobalTexture(
                    UrpBlurredSelectionMask,
                    data.blurredSelectionMask);
                context.cmd.DrawMesh(
                    data.fullscreenMesh,
                    Matrix4x4.identity,
                    data.material,
                    0,
                    data.shaderPass,
                    data.properties);
            }
        }
    }
}
