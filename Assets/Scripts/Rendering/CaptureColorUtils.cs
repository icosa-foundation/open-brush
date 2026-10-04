using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{
    /// Colour is rendered with sample coverage, then resolved for display or export.
    public static class CaptureColorUtils
    {
        public static RenderTextureFormat GetFormat(Camera camera)
        {
            bool hdr = UrpPostProcessingController.Instance != null
                ? UrpPostProcessingController.Instance.SessionHdr
                : camera.allowHDR;
            return hdr ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;
        }

        public static RenderTextureDescriptor CreateDescriptor(
            int width, int height, RenderTextureFormat format, int depthBits = 24,
            int requestedSamples = 4)
        {
            var descriptor = new RenderTextureDescriptor(width, height, format, depthBits)
            {
                dimension = TextureDimension.Tex2D,
                volumeDepth = 1,
                useDynamicScale = false,
                vrUsage = VRTextureUsage.None,
                bindMS = false
            };
            // Probe this colour/depth format, rather than the XR display's format.
            for (int samples = 8; samples >= 1; samples /= 2)
            {
                if (samples > requestedSamples) continue;
                descriptor.msaaSamples = samples;
                if (SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor) == samples)
                    return descriptor;
            }
            descriptor.msaaSamples = 1;
            return descriptor;
        }

        public static void Resolve(RenderTexture source, RenderTexture destination,
            bool transparent)
        {
            if (!transparent)
            {
                Graphics.Blit(source, destination);
                return;
            }

            // Sampling resolves MSAA first. Covered samples contain opaque colour;
            // their average is premultiplied, while PNG expects straight RGB/alpha.
            var material = new Material(Resources.Load<Shader>("Shaders/ResolveCaptureAlpha"))
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            try
            {
                Graphics.Blit(source, destination, material);
            }
            finally
            {
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
            }
        }
    }
}
