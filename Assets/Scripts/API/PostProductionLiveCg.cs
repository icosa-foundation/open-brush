// Copyright 2026 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IO;
using UnityEngine;

namespace TiltBrush
{
    // Live CG for the fast preview (POSTPROD_FAST_PREVIEW_DESIGN.md section 4.2): renders
    // the calibrated view's colour and encoded depth into low-res RenderTextures every
    // frame, for the compositor to sample directly - no GPU readback, no files, no reload.
    //
    // Mirrors ScreenshotManager.TakeSnapshot so the two paths produce the same CG: camera
    // settings copied from the snapshot rig's camera, pose from the stored calibrated
    // target pose (scene space), intrinsics rescaled to the render size, and depth from the
    // same Hidden/Internal-DepthNormalsTexture replacement pass (decoded on the GPU here
    // instead of on the CPU).
    public static class PostProductionLiveCg
    {
        // On by default: the snapshot path it replaced is no longer offered in the preview.
        public static bool Enabled { get; set; } = true;

        // Off by default: the snapshot colour pass has bloom (its post-effects toggle only
        // covers the depth pass), but a flatter image is clearer for judging alignment.
        // When on, the snapshot camera's own bloom component and settings are used.
        public static bool Bloom { get; set; }
        public static int Width { get; private set; } = 1280;
        public static int Height { get; private set; } = 676;

        // By default the live CG renders at the displayed camera's own pixel size: at a
        // fixed lower size its colour and depth are upscaled with point sampling and
        // stroke edges stair-step (seen 2026-10-20 at 1280x676 in a 1920x1080 Game view).
        private static bool s_MatchDisplay = true;

        public static RenderTexture ColorRT { get; private set; }
        // Unity DepthNormals encoding: rg = view normal, ba = EncodeFloatRG(linear 0-1
        // depth, relative to the far plane). b == 1 && a == 0 means "nothing rendered".
        public static RenderTexture EncodedDepthRT { get; private set; }
        public static float FarClipMetres { get; private set; }
        public static bool HasFrame { get; private set; }

        public static volatile string LastStatus = "live CG off";
        // Kept separate from LastStatus, which is overwritten every rendered frame.
        public static volatile string LastDumpStatus = "no dump yet";

        private static Camera s_ColorCamera;
        private static Camera s_DepthCamera;
        private static Shader s_DepthNormalsShader;
        private static readonly Color kNoDepthBackground = new Color(0.5f, 0.5f, 1.0f, 0.0f);

        // Editor Play sessions keep static state (no domain reload). The previous session's
        // cameras were destroyed with its scene; its render textures are released here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Enabled = true;
            Bloom = false;
            Width = 1280;
            Height = 676;
            s_MatchDisplay = true;
            if (ColorRT != null) { ColorRT.Release(); }
            if (EncodedDepthRT != null) { EncodedDepthRT.Release(); }
            ColorRT = null;
            EncodedDepthRT = null;
            FarClipMetres = 0;
            HasFrame = false;
            LastStatus = "live CG off";
            LastDumpStatus = "no dump yet";
            s_ColorCamera = null;
            s_DepthCamera = null;
            s_DepthNormalsShader = null;
        }

        // width or height <= 0 returns to matching the display.
        public static void SetResolution(int width, int height)
        {
            s_MatchDisplay = width <= 0 || height <= 0;
            if (!s_MatchDisplay)
            {
                Width = Mathf.Max(16, width);
                Height = Mathf.Max(16, height);
            }
            ReleaseTargets();
        }

        public static void Render(Camera targetCamera, bool hasTargetPose, Vector3 targetPosition,
            Quaternion targetRotation)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Camera template = GetSnapshotCamera();
            if (template == null)
            {
                HasFrame = false;
                LastStatus = "live CG: snapshot rig camera not found";
                return;
            }

            if (s_MatchDisplay && targetCamera.pixelWidth > 0 && targetCamera.pixelHeight > 0)
            {
                Width = targetCamera.pixelWidth;
                Height = targetCamera.pixelHeight;
            }
            EnsureTargets();

            var bloom = RenderTo(ColorRT, EncodedDepthRT, PostProductionLens.Active, template, targetCamera,
                hasTargetPose, targetPosition, targetRotation, out var colorCam, out float farMetres);
            FarClipMetres = farMetres;
            HasFrame = true;
            string bloomState = !Bloom ? "off" : bloom != null ? "on" : "unavailable (no bloom on snapshot camera)";
            LastStatus = $"live CG {Width}x{Height} ({(s_MatchDisplay ? "matching display" : "fixed")}), bloom {bloomState}: " +
                $"CPU-side render submit {timer.Elapsed.TotalMilliseconds:F1}ms | " +
                $"colour cam path={colorCam.actualRenderingPath} hdr={colorCam.allowHDR} " +
                $"allowMSAA={colorCam.allowMSAA} rtAA={ColorRT.antiAliasing} " +
                $"qualityAA={QualitySettings.antiAliasing} template path={template.renderingPath}/" +
                $"{template.actualRenderingPath} template hdr={template.allowHDR}";
        }

        // The export's render (PostProductionExport): the same CG as the preview, into the
        // caller's targets at their own size, always in the undistorted working frame when
        // workingFrame is set (the export distorts it back into the original lens itself).
        // Returns false if the snapshot rig camera isn't available.
        public static bool RenderForExport(RenderTexture color, RenderTexture encodedDepth, bool workingFrame,
            Vector3 targetPosition, Quaternion targetRotation, out float farClipMetres)
        {
            farClipMetres = 0f;
            Camera template = GetSnapshotCamera();
            if (template == null) { return false; }
            RenderTo(color, encodedDepth, workingFrame, template, template, true, targetPosition, targetRotation,
                out _, out farClipMetres);
            return true;
        }

        private static SENaturalBloomAndDirtyLens RenderTo(RenderTexture color, RenderTexture encodedDepth,
            bool workingFrame, Camera template, Camera targetCamera, bool hasTargetPose, Vector3 targetPosition,
            Quaternion targetRotation, out Camera colorCam, out float farClipMetres)
        {
            // Two cameras so the colour camera can carry the bloom effect without it ever
            // touching depth (toggling the effect per pass would rebuild its material every
            // frame, since it creates it in OnEnable and destroys it in OnDisable).
            colorCam = GetCamera(ref s_ColorCamera, "PostProdLiveCgColorCamera");
            Camera depthCam = GetCamera(ref s_DepthCamera, "PostProdLiveCgDepthCamera");
            SetupCamera(colorCam, template, targetCamera, hasTargetPose, targetPosition, targetRotation);
            SetupCamera(depthCam, template, targetCamera, hasTargetPose, targetPosition, targetRotation);
            farClipMetres = depthCam.farClipPlane * App.UNITS_TO_METERS;

            // Per-layer spatial offsets apply only while these two renders run.
            using var layerOffsets = new PostProductionLayerOffsetScope();

            var bloom = SyncBloom(colorCam, template);
            colorCam.targetTexture = color;
            // MSAA only applies to forward rendering, and an HDR intermediate may not be
            // multisampled; 4x AA was measured as having no effect with the copied settings
            // (2026-10-20), so pin both. ScreenshotManager.Start sets allowHDR = false on
            // the snapshot camera anyway.
            colorCam.renderingPath = RenderingPath.Forward;
            colorCam.allowHDR = false;
            colorCam.allowMSAA = true;
            ApplyProjection(colorCam, color.width, color.height, workingFrame);
            colorCam.Render();
            colorCam.targetTexture = null;

            // Depth must not be multisampled: averaging encoded depth corrupts it.
            depthCam.allowMSAA = false;
            depthCam.targetTexture = encodedDepth;
            depthCam.depthTextureMode = DepthTextureMode.Depth;
            depthCam.clearFlags = CameraClearFlags.SolidColor;
            depthCam.backgroundColor = kNoDepthBackground;
            ApplyProjection(depthCam, encodedDepth.width, encodedDepth.height, workingFrame);
            depthCam.RenderWithShader(GetDepthNormalsShader(), "RenderType");
            depthCam.targetTexture = null;
            return bloom;
        }

        // Keeps a copy of the snapshot camera's bloom effect on the live camera, enabled
        // only when Bloom is on. Settings are copied from the template each frame (cheap:
        // a handful of public fields) so they always match the snapshot path.
        private static SENaturalBloomAndDirtyLens SyncBloom(Camera cam, Camera template)
        {
            var source = template.GetComponent<SENaturalBloomAndDirtyLens>();
            var bloom = cam.GetComponent<SENaturalBloomAndDirtyLens>();
            if (source == null)
            {
                if (bloom != null) { bloom.enabled = false; }
                return null;
            }
            if (bloom == null)
            {
                if (!Bloom) { return null; }
                // Its OnEnable builds a material from the shader field, so the settings must
                // be copied before it is enabled: add it on an inactive object.
                cam.gameObject.SetActive(false);
                bloom = cam.gameObject.AddComponent<SENaturalBloomAndDirtyLens>();
                CopyBloomSettings(source, bloom);
                cam.gameObject.SetActive(true);
            }
            else
            {
                CopyBloomSettings(source, bloom);
            }
            if (bloom.enabled != Bloom) { bloom.enabled = Bloom; }
            return bloom;
        }

        private static void CopyBloomSettings(SENaturalBloomAndDirtyLens from, SENaturalBloomAndDirtyLens to)
        {
            foreach (var field in typeof(SENaturalBloomAndDirtyLens).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                field.SetValue(to, field.GetValue(from));
            }
        }

        private static void SetupCamera(Camera cam, Camera template, Camera targetCamera,
            bool hasTargetPose, Vector3 targetPosition, Quaternion targetRotation)
        {
            cam.CopyFrom(template);
            cam.enabled = false;
            cam.stereoTargetEye = StereoTargetEyeMask.None;
            cam.depthTextureMode = DepthTextureMode.None;

            // Same pose source as TakeSnapshot (scene-space calibrated pose); fall back to
            // the displayed camera if no calibrated pose has been set.
            if (hasTargetPose)
            {
                // The global post-production offset moves the sketch (scene space); from a
                // fixed camera that is exactly the camera moving by the inverse, so it is
                // applied here and the sketch itself is never touched.
                App.Scene.AsScene[cam.transform] = PostProductionSettings.GlobalSpatialOffset.inverse *
                    TrTransform.TR(targetPosition, targetRotation);
            }
            else
            {
                cam.transform.SetPositionAndRotation(
                    targetCamera.transform.position, targetCamera.transform.rotation);
            }

            if (ScreenshotManager.UseCalibratedClip)
            {
                cam.nearClipPlane = ScreenshotManager.CalibratedNearClip;
                cam.farClipPlane = ScreenshotManager.CalibratedFarClip;
            }
        }

        // Intrinsics are stored against the resolution they were calibrated at; rescaled to
        // the render size exactly as TakeSnapshot does.
        private static void ApplyProjection(Camera cam, int width, int height, bool work)
        {
            if (!ScreenshotManager.UseCalibratedProjection) { return; }
            // Undistorted: render the overscanned working frame (same focal length, larger
            // frame, shifted principal point), so the composite covers every pixel of the
            // real frame once undistorted.
            float refW = work ? PostProductionLens.WorkWidth : ScreenshotManager.CalibratedRefWidth;
            float refH = work ? PostProductionLens.WorkHeight : ScreenshotManager.CalibratedRefHeight;
            float sx = width / refW;
            float sy = height / refH;
            float fx = (work ? PostProductionLens.WorkFx : ScreenshotManager.CalibratedFx) * sx;
            float fy = (work ? PostProductionLens.WorkFy : ScreenshotManager.CalibratedFy) * sy;
            float cx = (work ? PostProductionLens.WorkCx : ScreenshotManager.CalibratedCx) * sx;
            float cy = (work ? PostProductionLens.WorkCy : ScreenshotManager.CalibratedCy) * sy;
            cam.fieldOfView = 2f * Mathf.Atan(height / (2f * fy)) * Mathf.Rad2Deg;
            cam.projectionMatrix = ScreenshotManager.BuildProjectionFromIntrinsics(
                fx, fy, cx, cy, width, height, cam.nearClipPlane, cam.farClipPlane);
        }

        // Parity test (design doc section 10, step 3): writes the current live frame as a
        // colour PNG and a metric-depth EXR using the same decode and EXR settings as the
        // snapshot path, so the two can be compared pixel for pixel.
        public static string Dump(string basePath)
        {
            if (!HasFrame) { return "error: no live CG frame rendered yet (enable postprod.preview and postprod.preview.livecg)"; }
            var prevActive = RenderTexture.active;
            try
            {
                // Blit to a plain texture first: that resolves the MSAA samples, which
                // reading the multisampled texture directly may not.
                var resolved = RenderTexture.GetTemporary(Width, Height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(ColorRT, resolved);
                RenderTexture.active = resolved;
                var color = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                color.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                color.Apply();
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(resolved);
                File.WriteAllBytes(basePath + ".png", color.EncodeToPNG());
                Object.Destroy(color);

                RenderTexture.active = EncodedDepthRT;
                var encoded = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
                encoded.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                encoded.Apply();
                Color32[] px = encoded.GetPixels32();
                Object.Destroy(encoded);

                var exr = new Texture2D(Width, Height, TextureFormat.RFloat, false, true);
                var metres = exr.GetPixelData<float>(0);
                for (int i = 0; i < px.Length; i++)
                {
                    bool invalid = px[i].b == byte.MaxValue && px[i].a == 0;
                    float linear01 = px[i].b * (1.0f / byte.MaxValue) +
                        px[i].a * (1.0f / (byte.MaxValue * byte.MaxValue));
                    metres[i] = invalid ? 0f : linear01 * FarClipMetres;
                }
                exr.Apply(false, false);
                File.WriteAllBytes(basePath + "_depth.exr", exr.EncodeToEXR(
                    Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP));
                Object.Destroy(exr);
                return $"wrote {basePath}.png + {basePath}_depth.exr ({Width}x{Height})";
            }
            finally
            {
                RenderTexture.active = prevActive;
            }
        }

        private static Camera GetSnapshotCamera()
        {
            var rig = SketchControlsScript.m_Instance != null
                ? SketchControlsScript.m_Instance.MultiCamCaptureRig
                : null;
            if (rig == null) { return null; }
            var mgr = rig.ManagerFromStyle(MultiCamStyle.Snapshot);
            return mgr != null ? mgr.LeftEye : null;
        }

        private static Camera GetCamera(ref Camera field, string name)
        {
            if (field == null)
            {
                var go = new GameObject(name);
                Object.DontDestroyOnLoad(go);
                field = go.AddComponent<Camera>();
                field.enabled = false;
            }
            return field;
        }

        private static Shader GetDepthNormalsShader()
        {
            if (s_DepthNormalsShader == null)
            {
                s_DepthNormalsShader = Shader.Find("Hidden/Internal-DepthNormalsTexture");
            }
            return s_DepthNormalsShader;
        }

        private static void EnsureTargets()
        {
            if (ColorRT != null && ColorRT.width == Width && ColorRT.height == Height) { return; }
            ReleaseTargets();
            ColorRT = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 4,
                name = "PostProdLiveCgColor",
            };
            EncodedDepthRT = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PostProdLiveCgEncodedDepth",
            };
        }

        private static void ReleaseTargets()
        {
            HasFrame = false;
            if (ColorRT != null) { ColorRT.Release(); Object.Destroy(ColorRT); ColorRT = null; }
            if (EncodedDepthRT != null) { EncodedDepthRT.Release(); Object.Destroy(EncodedDepthRT); EncodedDepthRT = null; }
        }
    }
}
