using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    public class UrpSelectionRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] private RenderPassEvent m_RenderPassEvent =
            RenderPassEvent.AfterRenderingPostProcessing;

        private SelectionPass m_Pass;
        private Material m_MaskMaterial;

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
                selection == null ||
                !selection.ShouldRenderUrpSelection)
            {
                return;
            }

            m_Pass.Setup(selection, m_MaskMaterial);
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
            private static readonly int BlurSize = Shader.PropertyToID("_BlurSize");

            private SelectionEffect m_Selection;
            private Material m_MaskMaterial;
            private RTHandle m_ColorCopy;
            private RTHandle m_SelectionMask;
            private RTHandle m_DownsampleA;
            private RTHandle m_DownsampleB;

            public void Setup(SelectionEffect selection, Material maskMaterial)
            {
                m_Selection = selection;
                m_MaskMaterial = maskMaterial;
            }

            public void Dispose()
            {
                m_ColorCopy?.Release();
                m_SelectionMask?.Release();
                m_DownsampleA?.Release();
                m_DownsampleB?.Release();
                m_ColorCopy = null;
                m_SelectionMask = null;
                m_DownsampleA = null;
                m_DownsampleB = null;
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

                    RTHandle cameraColorTarget =
                        renderingData.cameraData.renderer.cameraColorTargetHandle;
                    CoreUtils.SetRenderTarget(
                        cmd,
                        m_SelectionMask,
                        ClearFlag.Color,
                        Color.black);
                    m_Selection.DrawUrpHighlightMeshes(cmd, m_MaskMaterial);

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
                        m_DownsampleA,
                        postEffect,
                        (int)SelectionEffectPass.VerticalBlur);
                    cmd.Blit(
                        m_DownsampleA,
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
                }
            }
        }
    }
}
