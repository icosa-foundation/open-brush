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
        private const int kAndroidDebugLogLimit = 160;
        private static int s_DebugSkipLogCount;
        private static int s_AndroidDebugLogCount;
        private static int s_MobileSelectionVisibleFrame = -1;
        private static string s_LastAndroidSkipStatus;

        [SerializeField]
        private RenderPassEvent m_RenderPassEvent =
            RenderPassEvent.AfterRenderingPostProcessing;

        [SerializeField]
        private DebugOutput m_DebugOutput = DebugOutput.Off;

        [SerializeField]
        private SimpleCompositeMode m_SimpleCompositeMode = SimpleCompositeMode.FullOutline;

        [SerializeField]
        private bool m_UseMobileCompositeMode = true;

        [SerializeField]
        private SimpleCompositeMode m_MobileCompositeMode = SimpleCompositeMode.FullOutline;

        [SerializeField]
        private bool m_UseAdaptiveMobileCompositeMode = true;

        [SerializeField]
        private float m_AdaptiveMobileLowFps = 72.0f;

        private SelectionPass m_Pass;
        private Material m_MaskMaterial;
        private Material m_SimpleCompositeMaterial;
        private SimpleCompositeMode m_CurrentAdaptiveMobileCompositeMode = SimpleCompositeMode.FullOutline;
        private int m_LastSelectionVisibleFrame = -1;
        private int m_NumSelectionFpsTooLow;

        public static bool MobileSelectionQualityOverrideActive =>
            s_MobileSelectionVisibleFrame >= 0 &&
            Time.frameCount - s_MobileSelectionVisibleFrame <= 1;

        private enum DebugOutput
        {
            Off,
            RawMask
        }

        private enum SimpleCompositeMode
        {
            FullOutline,
            CompromiseOutline,
            BasicTint
        }

        [System.Diagnostics.Conditional("UNITY_ANDROID")]
        private static void LogAndroidSelection(string message)
        {
            if (s_AndroidDebugLogCount >= kAndroidDebugLogLimit)
            {
                return;
            }

            Debug.Log($"{kDebugLogPrefix} {message}");
            s_AndroidDebugLogCount++;
        }

        public override void Create()
        {
            Shader maskShader = Shader.Find("Hidden/UrpSelectionMask");
            if (maskShader != null)
            {
                m_MaskMaterial = CoreUtils.CreateEngineMaterial(maskShader);
            }

            Shader simpleCompositeShader = Shader.Find("Hidden/UrpSelectionSimpleComposite");
            if (simpleCompositeShader != null)
            {
                m_SimpleCompositeMaterial = CoreUtils.CreateEngineMaterial(simpleCompositeShader);
            }

            m_Pass = new SelectionPass
            {
                renderPassEvent = m_RenderPassEvent
            };

            LogAndroidSelection(
                $"Create maskShader={(maskShader != null ? maskShader.name : "null")} " +
                $"maskMaterial={(m_MaskMaterial != null ? m_MaskMaterial.name : "null")} " +
                $"simpleShader={(simpleCompositeShader != null ? simpleCompositeShader.name : "null")} " +
                $"simpleMaterial={(m_SimpleCompositeMaterial != null ? m_SimpleCompositeMaterial.name : "null")} " +
                $"renderPassEvent={m_RenderPassEvent}");
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
                UpdateSelectionSession(false);
                string status = selection.UrpSelectionDebugStatus;
                if (s_LastAndroidSkipStatus != status || s_DebugSkipLogCount < 12)
                {
                    LogAndroidSelection(
                        $"AddRenderPasses skip camera={camera.name} " +
                        $"cameraType={camera.cameraType} status={status}");
                    s_LastAndroidSkipStatus = status;
                }
                if (m_DebugOutput == DebugOutput.RawMask && s_DebugSkipLogCount < 12)
                {
                    Debug.Log(
                        $"{kDebugLogPrefix} Selection pass skipped camera={camera.name} " +
                        $"status={status}.");
                }
                s_DebugSkipLogCount++;
                return;
            }

            int simpleCompositeMode = (int)ResolveSimpleCompositeMode();
            UpdateSelectionSession(true);
            LogAndroidSelection(
                $"AddRenderPasses enqueue camera={camera.name} " +
                $"cameraType={camera.cameraType} mode={(SimpleCompositeMode)simpleCompositeMode} " +
                $"maskMaterial={(m_MaskMaterial != null ? m_MaskMaterial.name : "null")} " +
                $"simpleMaterial={(m_SimpleCompositeMaterial != null ? m_SimpleCompositeMaterial.name : "null")} " +
                $"status={selection.UrpSelectionDebugStatus}");
            m_Pass.Setup(
                selection,
                m_MaskMaterial,
                m_SimpleCompositeMaterial,
                simpleCompositeMode,
                m_DebugOutput == DebugOutput.RawMask);
            renderer.EnqueuePass(m_Pass);
        }

        private SimpleCompositeMode ResolveSimpleCompositeMode()
        {
            bool isMobileHardware = App.Config != null && App.Config.IsMobileHardware;
            if (!isMobileHardware || !m_UseMobileCompositeMode)
            {
                return m_SimpleCompositeMode;
            }

            if (!m_UseAdaptiveMobileCompositeMode)
            {
                return m_MobileCompositeMode;
            }

            if (IsNewSelectionSession())
            {
                m_CurrentAdaptiveMobileCompositeMode = m_MobileCompositeMode;
                m_NumSelectionFpsTooLow = 0;
            }

            if (SelectionFpsIsTooLow())
            {
                m_NumSelectionFpsTooLow++;
                int framesForLowerQuality = QualityControls.m_Instance != null
                    ? QualityControls.m_Instance.AppQualityLevels.FramesForLowerQuality
                    : 10;
                if (m_NumSelectionFpsTooLow >= framesForLowerQuality)
                {
                    m_CurrentAdaptiveMobileCompositeMode =
                        Degrade(m_CurrentAdaptiveMobileCompositeMode);
                    m_NumSelectionFpsTooLow = 0;
                }
            }
            else
            {
                m_NumSelectionFpsTooLow = 0;
            }
            return m_CurrentAdaptiveMobileCompositeMode;
        }

        private void UpdateSelectionSession(bool visible)
        {
            if (!visible || App.Config == null || !App.Config.IsMobileHardware)
            {
                return;
            }

            s_MobileSelectionVisibleFrame = Time.frameCount;
            m_LastSelectionVisibleFrame = Time.frameCount;
        }

        private bool IsNewSelectionSession()
        {
            return m_LastSelectionVisibleFrame < 0 ||
                Time.frameCount - m_LastSelectionVisibleFrame > 1;
        }

        private bool SelectionFpsIsTooLow()
        {
            if (QualityControls.m_Instance != null)
            {
                return QualityControls.m_Instance.FramesInLastSecond <=
                    QualityControls.m_Instance.AppQualityLevels.LowerQualityFpsTrigger;
            }

            float smoothDeltaTime = Time.smoothDeltaTime;
            return smoothDeltaTime > 0.0f &&
                1.0f / smoothDeltaTime <= m_AdaptiveMobileLowFps;
        }

        private static SimpleCompositeMode Degrade(SimpleCompositeMode mode)
        {
            switch (mode)
            {
                case SimpleCompositeMode.FullOutline:
                    return SimpleCompositeMode.CompromiseOutline;
                case SimpleCompositeMode.CompromiseOutline:
                    return SimpleCompositeMode.BasicTint;
                default:
                    return SimpleCompositeMode.BasicTint;
            }
        }

        protected override void Dispose(bool disposing)
        {
            m_Pass?.Dispose();
            CoreUtils.Destroy(m_MaskMaterial);
            CoreUtils.Destroy(m_SimpleCompositeMaterial);
        }

        private class SelectionPass : ScriptableRenderPass
        {
            private static readonly int SimpleSelectionColor =
                Shader.PropertyToID("_SimpleSelectionColor");
            private static readonly int SimpleSelectionMask =
                Shader.PropertyToID("_SimpleSelectionMask");
            private static readonly int SimpleSelectionMaskTexelSize =
                Shader.PropertyToID("_SimpleSelectionMask_TexelSize");
            private static readonly int SimpleSelectionColorFlipY =
                Shader.PropertyToID("_SimpleSelectionColorFlipY");
            private static readonly int SimpleSelectionMaskFlipY =
                Shader.PropertyToID("_SimpleSelectionMaskFlipY");
            private static readonly int SimpleSelectionMode =
                Shader.PropertyToID("_SimpleSelectionMode");
            private static readonly int SelectionMask = Shader.PropertyToID("_SelectionMask");

            private SelectionEffect m_Selection;
            private Material m_MaskMaterial;
            private Material m_SimpleCompositeMaterial;
            private int m_SimpleCompositeMode;
            private bool m_DebugRawMask;
            private RTHandle m_ColorCopy;
            private RTHandle m_SelectionMask;

            public void Setup(
                SelectionEffect selection,
                Material maskMaterial,
                Material simpleCompositeMaterial,
                int simpleCompositeMode,
                bool debugRawMask)
            {
                m_Selection = selection;
                m_MaskMaterial = maskMaterial;
                m_SimpleCompositeMaterial = simpleCompositeMaterial;
                m_SimpleCompositeMode = simpleCompositeMode;
                m_DebugRawMask = debugRawMask;
            }

            public void Dispose()
            {
                m_ColorCopy?.Release();
                m_SelectionMask?.Release();
                m_ColorCopy = null;
                m_SelectionMask = null;
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
                        LogAndroidSelection(
                            $"Execute compatibility skip status={m_Selection.UrpSelectionDebugStatus}");
                        return;
                    }

                    if (m_MaskMaterial == null || m_SimpleCompositeMaterial == null)
                    {
                        LogAndroidSelection(
                            $"Execute compatibility missing materials " +
                            $"maskMaterial={(m_MaskMaterial != null ? m_MaskMaterial.name : "null")} " +
                            $"simpleMaterial={(m_SimpleCompositeMaterial != null ? m_SimpleCompositeMaterial.name : "null")}");
                        return;
                    }

                    RenderTextureDescriptor cameraDescriptor =
                        renderingData.cameraData.cameraTargetDescriptor;
                    cameraDescriptor.depthBufferBits = 0;
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
                        LogAndroidSelection(
                            $"Execute compatibility raw-mask camera={renderingData.cameraData.camera.name} " +
                            $"size={cameraDescriptor.width}x{cameraDescriptor.height}");
                        cmd.Blit(m_SelectionMask, cameraColorTarget);
                        context.ExecuteCommandBuffer(cmd);
                        return;
                    }

                    LogAndroidSelection(
                        $"Execute compatibility composite camera={renderingData.cameraData.camera.name} " +
                        $"size={cameraDescriptor.width}x{cameraDescriptor.height} mode={(SimpleCompositeMode)m_SimpleCompositeMode}");
                    cmd.Blit(cameraColorTarget, m_ColorCopy);
                    CoreUtils.SetRenderTarget(
                        cmd,
                        cameraColorTarget,
                        ClearFlag.None);
                    cmd.SetGlobalTexture(SimpleSelectionColor, m_ColorCopy);
                    cmd.SetGlobalTexture(SimpleSelectionMask, m_SelectionMask);
                    cmd.DrawMesh(
                        RenderingUtils.fullscreenMesh,
                        Matrix4x4.identity,
                        m_SimpleCompositeMaterial,
                        0,
                        0,
                        CreateSimpleCompositeProperties(
                            cameraDescriptor.width,
                            cameraDescriptor.height,
                            m_SimpleCompositeMode));

                    context.ExecuteCommandBuffer(cmd);
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                    m_Selection.EndUrpSelectionFrame();
                    m_Selection = null;
                    m_MaskMaterial = null;
                    m_SimpleCompositeMaterial = null;
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
                    LogAndroidSelection(
                        $"RecordRenderGraph skip status={(m_Selection != null ? m_Selection.UrpSelectionDebugStatus : "selection null")}");
                    return;
                }

                if (m_MaskMaterial == null || m_SimpleCompositeMaterial == null)
                {
                    LogAndroidSelection(
                        $"RecordRenderGraph missing materials " +
                        $"maskMaterial={(m_MaskMaterial != null ? m_MaskMaterial.name : "null")} " +
                        $"simpleMaterial={(m_SimpleCompositeMaterial != null ? m_SimpleCompositeMaterial.name : "null")}");
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                {
                    LogAndroidSelection("RecordRenderGraph skip active target is back buffer");
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
                    LogAndroidSelection(
                        $"RecordRenderGraph raw-mask size={maskDescriptor.width}x{maskDescriptor.height}");
                    renderGraph.AddBlitPass(
                        selectionMask,
                        cameraColor,
                        Vector2.one,
                        Vector2.zero,
                        passName: "Open Brush Selection Raw Mask");
                }
                else
                {
                    LogAndroidSelection(
                        $"RecordRenderGraph composite size={maskDescriptor.width}x{maskDescriptor.height} " +
                        $"mode={(SimpleCompositeMode)m_SimpleCompositeMode}");
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
                        compositeData.material = m_SimpleCompositeMaterial;
                        compositeData.properties = CreateSimpleCompositeProperties(
                            maskDescriptor.width,
                            maskDescriptor.height,
                            m_SimpleCompositeMode);
                        compositeData.fullscreenMesh = RenderingUtils.fullscreenMesh;
                        compositeData.shaderPass = 0;

                        compositeBuilder.UseTexture(compositeData.colorCopy, AccessFlags.Read);
                        compositeBuilder.UseTexture(compositeData.destination, AccessFlags.ReadWrite);
                        compositeBuilder.UseTexture(compositeData.selectionMask, AccessFlags.Read);
                        compositeBuilder.AllowGlobalStateModification(true);
                        compositeBuilder.AllowPassCulling(false);
                        compositeBuilder.SetRenderFunc(
                            static (CompositeTexturePassData data, UnsafeGraphContext context) =>
                                ExecuteCompositePass(data, context));
                    }
                }

                m_Selection = null;
                m_MaskMaterial = null;
                m_SimpleCompositeMaterial = null;
                m_DebugRawMask = false;
            }

            private static MaterialPropertyBlock CreateSimpleCompositeProperties(
                int width,
                int height,
                int simpleCompositeMode)
            {
                MaterialPropertyBlock properties = new MaterialPropertyBlock();
                properties.SetVector(
                    SimpleSelectionMaskTexelSize,
                    new Vector4(1.0f / width, 1.0f / height, width, height));
                properties.SetFloat(SimpleSelectionColorFlipY, 1f);
                properties.SetFloat(SimpleSelectionMaskFlipY, 1f);
                properties.SetFloat(SimpleSelectionMode, simpleCompositeMode);
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
                    data.selection.EndUrpSelectionFrame();
                }
            }

            private static void ExecuteCompositePass(
                CompositeTexturePassData data,
                UnsafeGraphContext context)
            {
                context.cmd.SetRenderTarget(data.destination, 0, CubemapFace.Unknown, -1);
                context.cmd.SetGlobalTexture(SimpleSelectionColor, data.colorCopy);
                context.cmd.SetGlobalTexture(SimpleSelectionMask, data.selectionMask);
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
