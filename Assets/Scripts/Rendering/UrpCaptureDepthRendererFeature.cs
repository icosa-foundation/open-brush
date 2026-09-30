using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// Copies capture depth while URP still owns the camera's actual depth attachment.
    public class UrpCaptureDepthRendererFeature : ScriptableRendererFeature
    {
        // Retain both depth shader variants in player builds; requests supply the material.
        [SerializeField] private Shader m_DepthShader;

        private sealed class CaptureRequest
        {
            public RTHandle target;
            public Material material;
            public bool copied;
        }

        private static readonly Dictionary<Camera, CaptureRequest> s_Requests = new();

        public static void BeginCapture(Camera camera, RenderTexture target, Material material)
        {
            s_Requests.Add(camera, new CaptureRequest
            {
                target = RTHandles.Alloc(target), material = material
            });
        }

        public static void EndCapture(Camera camera)
        {
            if (!s_Requests.Remove(camera, out var request)) return;
            request.target.Release();
        }

        public static bool HasCapturedDepth(Camera camera) =>
            s_Requests.TryGetValue(camera, out var request) && request.copied;

        public override void Create() { }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_DepthShader != null &&
                s_Requests.TryGetValue(renderingData.cameraData.camera, out var request))
                renderer.EnqueuePass(new CaptureDepthPass(request));
        }

        private sealed class CaptureDepthPass : ScriptableRenderPass
        {
            private readonly CaptureRequest m_Request;
            private sealed class PassData
            {
                public TextureHandle depth;
                public TextureHandle target;
                public Material material;
                public CaptureRequest request;
            }

            public CaptureDepthPass(CaptureRequest request)
            {
                m_Request = request;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var depth = resources.activeDepthTexture;
                var target = graph.ImportTexture(m_Request.target);
                var depthDescriptor = graph.GetTextureDesc(depth);
                // Match URP's CopyDepthPass: only sample individual samples when the
                // attachment is bound as MSAA and the platform supports that binding.
                // A resolved binding retains the attachment's original sample count.
                bool sampleMultisampledDepth =
                    depthDescriptor.msaaSamples != MSAASamples.None &&
                    depthDescriptor.bindTextureMS && SystemInfo.supportsMultisampledTextures != 0;
                if (sampleMultisampledDepth) m_Request.material.EnableKeyword("CAPTURE_DEPTH_MSAA");
                else m_Request.material.DisableKeyword("CAPTURE_DEPTH_MSAA");

                using var builder = graph.AddUnsafePass<PassData>("Open Brush capture depth", out var data);
                data.depth = depth;
                data.target = target;
                data.material = m_Request.material;
                data.request = m_Request;
                builder.UseTexture(depth, AccessFlags.Read);
                builder.UseTexture(target, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData pass, UnsafeGraphContext context) =>
                {
                    var command = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    command.SetGlobalTexture("_CaptureNativeDepth", pass.depth, RenderTextureSubElement.Depth);
                    command.Blit(BuiltinRenderTextureType.None, pass.target, pass.material);
                    pass.request.copied = true;
                });
            }
        }
    }
}
