// Copyright 2020 The Tilt Brush Authors
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
using Unity.Collections;
using UnityEngine;

namespace TiltBrush
{

    /// In this class, "Landscape" is the term used for cameras at 0 and 180 degree rotation.
    /// "Portrait" means 90 or 270 degree camera rotation.
    ///
    /// General Requirements:
    /// - Must have a sibling Camera component.
    ///
    /// Requirements for m_AutoAlignRig:
    /// - parent must start with identity rotation
    /// - it must be OK to modify parent's rotation
    ///
    /// Requirements for m_UseStereoRig:
    /// - this object's local transform must be identity
    ///
    public class ScreenshotManager : MonoBehaviour
    {
        public sealed class DepthCaptureFiles
        {
            public byte[] normalizedDepthPng;
            public byte[] linearDepth16Png;
            public byte[] linearDepthExr;
            public byte[] metadataJson;
        }

        [System.Serializable]
        private sealed class DepthCaptureMetadata
        {
            public int schemaVersion = 1;
            public int width;
            public int height;
            public string distanceConvention = "optical-axis";
            public string units = "metres";
            public int invalidValue = 0;
            public float nearClipMetres;
            public float farClipMetres;
            public float verticalFieldOfViewDegrees;
            public float aspect;
            public string depth16Encoding =
                "uint16 linear; depthMetres = value * depth16ScaleMetres";
            public float depth16ScaleMetres;
            public string exrEncoding = "float32 linear metres";
        }

        class CameraInfo
        {
            // Material is mutated to display renderTexture
            public MeshRenderer renderer;
            // Camera is mutated to write to renderTexture
            public Camera camera;
            public RenderTexture renderTexture;
        }

        const float MM_TO_UNITS = .001f * App.METERS_TO_UNITS;
        const float HYSTERESIS_DEGREES = 10;
        // Depth capture produces several lossless sidecars and requires GPU and CPU copies of
        // the source. Keep the desktop limit below the general 16K snapshot limit so a valid
        // request cannot require several gigabytes of transient memory.
        public const int kMaxDepthCaptureDimension = 8192;

        // Cached off or created on Start(); otherwise read-only
        private CameraInfo m_LeftInfo;
        private CameraInfo m_RightInfo;
        private Vector2[] m_RendererUVs;
        private float m_LandscapeFov;
        private float m_PortraitFov;

        public bool IsPortrait
        {
            get { return m_bIsPortraitMode; }
            set
            {
                if (m_bIsPortraitModeLocked)
                {
                    return;
                }
                m_bIsPortraitMode = value;
                CreateDisplayRenderTextures();
                UpdateCameraAspect();
            }
        }
        private bool m_bIsPortraitMode = false;

        // Enable/disable set-access to IsPortrait
        public bool IsPortraitModeLocked
        {
            get { return m_bIsPortraitModeLocked; }
            set { m_bIsPortraitModeLocked = value; }
        }
        private bool m_bIsPortraitModeLocked = false;

        /// Where the camera's output should be fed.
        public MeshRenderer m_Display;
        /// On startup, use config file for width and set height to match the aspect ratio.
        public bool m_UseDisplayWidthFromConfigFile;
        // Width of the live render target
        public int m_DisplayWidth;
        // Height of the live render target
        public int m_DisplayHeight;

        /// When set, align camera rig with head; automatically switches
        /// orientations to portrait mode.
        public bool m_AutoAlignRig = false;
        /// When set, use a stereo camera rig
        public bool m_UseStereoRig = false;
        /// Distance from centerline to camera, when using stereo rig. In millimeters.
        public float m_InterAxialOffset = 22f;
        /// Convergence distance, as a multiple of IAO.
        public float m_ConvergenceFactor = 10f;

        /// The left-eye camera is also the main camera when m_UseStereoRig=false.
        public Camera LeftEye { get => LeftInfo.camera; }
        public Material LeftEyeMaterial { get => LeftInfo.renderer.material; }
        public bool LeftEyeMaterialRenderTextureExists { get => LeftInfo.renderTexture != null; }

        private CameraInfo LeftInfo
        {
            get
            {
                // Need to lazy-init this; others might try to call our public API before
                // our Awake() and Start() have been called (because object starts inactive)
                if (m_LeftInfo == null)
                {
                    m_LeftInfo = new CameraInfo();
                    m_LeftInfo.camera = GetComponent<Camera>();
                    m_LeftInfo.renderer = m_Display;
                }
                return m_LeftInfo;
            }
        }

        void Start()
        {
            // Check that we never get here if we're a clone (because we remove
            // the ScreenshotManager from the clone)
            Debug.Assert(transform.parent != null);

            if (m_AutoAlignRig)
            {
                // Because we need to override parent.localRotation
                Debug.Assert(transform.parent.localRotation == Quaternion.identity);
            }
            if (m_UseStereoRig)
            {
                // Because we need to override position (for stereo offset)
                // and rotation (for convergence)
                Debug.Assert(transform.localRotation == Quaternion.identity);
                Debug.Assert(transform.localPosition == Vector3.zero);
                Debug.Assert(transform.localScale == Vector3.one);
            }

            m_RendererUVs = m_Display.GetComponent<MeshFilter>().mesh.uv;

            // Lazy init.
            m_LeftInfo = LeftInfo;

            // If requested, create a stereo camera rig
            if (m_UseStereoRig && App.VrSdk.GetHmdDof() == VrSdk.DoF.Six)
            {
                Debug.Assert(LayerMask.NameToLayer("SteamVRLeftEye") != 0);

                // Duplicate the camera
                {
                    var src = m_LeftInfo.camera.gameObject;
                    var dst = Instantiate(src);
                    dst.name = src.name + "_Right";
                    DestroyImmediate(dst.GetComponent<ScreenshotManager>());
                    dst.transform.parent = src.transform.parent;
                    dst.transform.localPosition = Vector3.zero;
                    dst.transform.localRotation = Quaternion.identity;
                    m_RightInfo = new CameraInfo();
                    m_RightInfo.camera = dst.GetComponent<Camera>();
                }

                // Duplicate the renderer
                {
                    var src = m_Display.gameObject;
                    var dst = Instantiate(src);
                    dst.name = src.name + "_Right";
                    dst.transform.parent = src.transform.parent;
                    dst.transform.localPosition = src.transform.localPosition;
                    dst.transform.localRotation = src.transform.localRotation;
                    dst.transform.localScale = src.transform.localScale; // ugh
                    dst.layer = LayerMask.NameToLayer("SteamVRRightEye");
                    src.layer = LayerMask.NameToLayer("SteamVRLeftEye");
                    m_RightInfo.renderer = dst.GetComponent<MeshRenderer>();
                }
            }

            SceneSettings.m_Instance.RegisterCamera(m_LeftInfo.camera);
            if (m_RightInfo != null)
            {
                SceneSettings.m_Instance.RegisterCamera(m_RightInfo.camera);
            }

            if (!UserConfig.PerformanceOverrides.EnableMulticamPreview)
            {
                // If we're looking through the viewfinder, we need to make some changes to this camera
                SetScreenshotResolution(App.UserConfig.Flags.SnapshotWidth > 0
                    ? App.UserConfig.Flags.SnapshotWidth : 1920);
                IsPortraitModeLocked = true;
            }
            if (App.Config.IsMobileHardware)
            {
                // Force no HDR on mobile
                if (m_LeftInfo == null)
                {
                    Debug.LogAssertion("ScreenshotManager m_LeftInfo is null in ScreenshotManager.Start.");
                }
                else if (m_LeftInfo.camera == null)
                {
                    Debug.LogAssertion("ScreenshotManager m_LeftInfo.camera  is null in ScreenshotManager.Start.");
                }
                else
                {
                    m_LeftInfo.camera.allowHDR = false;
                }
                var mobileBloom = GetComponent<MobileBloom>();
                if (mobileBloom != null)
                {
                    mobileBloom.enabled = false;
                }
                else
                {
                    Debug.LogAssertion("No MobileBloom on the Screenshot Manager.");
                }
                var pcBloom = GetComponent<SENaturalBloomAndDirtyLens>();
                if (pcBloom != null)
                {
                    pcBloom.enabled = false;
                }
                else
                {
                    Debug.LogAssertion("No SENaturalBloomAndDirtyLens on the Screenshot Manager.");
                }
            }
            if (m_UseDisplayWidthFromConfigFile)
            {
                SetScreenshotResolution(App.UserConfig.Video.Resolution);
            }
            CreateDisplayRenderTextures();

            CameraConfig.FovChanged += RefreshFovs;
            RefreshFovs();
        }

        void RefreshFovs()
        {
            m_LandscapeFov = CameraConfig.Fov;
            // Given:
            //  tan(fovY/2) = h / d;
            //  tan(fovX/2) = w / d;
            // Solve for fovX as a function of fovY:
            //  fovX = 2 atan( w/h * tan(fovY/2) )
            {
                float invAspect = (float)m_DisplayWidth / m_DisplayHeight;
                float fovY = m_LandscapeFov * Mathf.Deg2Rad;
                float fovX = 2 * Mathf.Atan(invAspect * Mathf.Tan(fovY / 2));
                m_PortraitFov = fovX * Mathf.Rad2Deg;
            }
        }

        public void SetScreenshotResolution(int width)
        {
            int oldWidth = m_DisplayWidth;
            int oldHeight = m_DisplayHeight;
            m_DisplayWidth = width;
            // Preserve the aspect ratio using exact math (most likely 16 x 9)
            m_DisplayHeight = (oldHeight * width) / oldWidth;
            // Don't allow odd widths and heights.
            if ((m_DisplayWidth % 2 == 1) || (m_DisplayHeight % 2 == 1))
            {
                m_DisplayHeight = Mathf.FloorToInt(m_DisplayHeight / 2) * 2;
                m_DisplayWidth = Mathf.FloorToInt(m_DisplayWidth / 2) * 2;
                OutputWindowScript.Error("Odd-numbered capture dimensions not supported.",
                    string.Format("Capture dimensions capped to {0}x{1}.", m_DisplayWidth, m_DisplayHeight));
            }
            CreateDisplayRenderTextures();
        }

        void Update()
        {
            if (m_RightInfo != null)
            {
                Transform tL = m_LeftInfo.camera.transform;
                Transform tR = m_RightInfo.camera.transform;
                Vector3 offset = new Vector3(m_InterAxialOffset * MM_TO_UNITS, 0, 0);
                tL.localPosition = -offset;
                tR.localPosition = offset;

                float theta = Mathf.Atan2(1, m_ConvergenceFactor) * Mathf.Rad2Deg;
                tL.localRotation = Quaternion.AngleAxis(theta, Vector3.up);
                tR.localRotation = Quaternion.AngleAxis(-theta, Vector3.up);
            }

            if (m_AutoAlignRig)
            {
                var headUp = ViewpointScript.Head.up;
                AlignRigTo(headUp);
            }
        }

        // Helper for AlignRigTo()
        // Set rotation of camera rig relative to its parent.
        // Behavior is undefined if degrees is not a multiple of 90.
        void SetRigRotation(float degrees)
        {
            Debug.Assert(degrees % 90 == 0);

            Transform root = m_LeftInfo.camera.transform.parent;
            Quaternion desiredRotation = Quaternion.AngleAxis(degrees, Vector3.forward);

            root.localRotation = desiredRotation;
            IsPortrait = ((degrees % 180) != 0);

            // Counter-rotate the UVs to compensate
            Vector2[] altUVs = new Vector2[m_RendererUVs.Length];
            Vector2 centerOfRotation = new Vector2(0.5f, 0.5f);
            float compensation = -degrees * Mathf.Deg2Rad;
            for (int i = 0; i < m_RendererUVs.Length; ++i)
            {
                Vector2 uv = m_RendererUVs[i];
                uv = (uv - centerOfRotation).Rotate(compensation) + centerOfRotation;
                altUVs[i] = uv;
            }
            m_LeftInfo.renderer.GetComponent<MeshFilter>().mesh.uv = altUVs;
            if (m_RightInfo != null)
            {
                m_RightInfo.renderer.GetComponent<MeshFilter>().mesh.uv = altUVs;
            }
        }

        float GetRigRotation()
        {
            Transform root = m_LeftInfo.camera.transform.parent;
            Vector3 ea = root.localRotation.eulerAngles;
            Debug.Assert(ea.x == 0 && ea.y == 0);
            return ea.z;
        }

        // Rotate camera rig about its z axis until its up direction
        // aligns as closely as possible to desiredUp
        void AlignRigTo(Vector3 desiredUp)
        {
            if (IsPortraitModeLocked)
            {
                return;
            }
            Transform rig = m_LeftInfo.camera.transform.parent;
            Transform parent = rig.parent;
            float stability;
            float desiredAngle = MathUtils.GetAngleBetween(
                parent.up, desiredUp, parent.forward, out stability);
            // Could alternatively use head-forward to infer the desired orientation
            if (stability < .1f) { return; }

            // Add hysteresis
            float delta = MathUtils.PeriodicDifference(GetRigRotation(), desiredAngle, 360);
            if (Mathf.Abs(delta) < (90 / 2) + HYSTERESIS_DEGREES)
            {
                return;
            }

            // Make multiple of 90
            desiredAngle = 90 * (int)Mathf.Round(desiredAngle / 90);
            SetRigRotation(desiredAngle);
        }

        RenderTextureFormat CameraFormat()
        {
            return GetComponent<Camera>().allowHDR
                ? RenderTextureFormat.ARGBFloat
                : RenderTextureFormat.ARGB32;
        }

        void UpdateCameraAspect()
        {
            float fieldOfView = IsPortrait ? m_PortraitFov : m_LandscapeFov;
            m_LeftInfo.camera.fieldOfView = fieldOfView;
            if (m_RightInfo != null)
            {
                m_RightInfo.camera.fieldOfView = fieldOfView;
            }
        }

        void CreateDisplayRenderTextures()
        {
            RenderTextureFormat format = CameraFormat();
            CreateDisplayRenderTexture(m_LeftInfo, format, "L");
            if (m_RightInfo != null)
            {
                CreateDisplayRenderTexture(m_RightInfo, format, "R");
            }
        }

        void CreateDisplayRenderTexture(CameraInfo info, RenderTextureFormat format, string tag)
        {
            int width, height;
            width = IsPortrait ? m_DisplayHeight : m_DisplayWidth;
            height = IsPortrait ? m_DisplayWidth : m_DisplayHeight;
            if (info.renderTexture != null
                && info.renderTexture.format == format
                && info.renderTexture.width == width
                && info.renderTexture.height == height)
            {
                return;
            }

            info.camera.targetTexture = null;
            Destroy(info.renderTexture);

            info.renderTexture = new RenderTexture(width, height, 0, format);
            info.renderTexture.name = "SshotTex" + tag;
            info.renderTexture.depth = 24;
            Debug.Assert(info.renderer != null);
            Debug.Assert(info.renderer.material != null);
            info.renderer.material.SetTexture("_MainTex", info.renderTexture);
            info.renderer.material.name = "SshotMat" + tag;
            info.camera.targetTexture = info.renderTexture;
        }

        /// Creates an ARGB32 save target. May transpose width and height if camera
        /// is in portrait orientation.
        /// Caller should release with RenderTexture.ReleaseTemporary() when done.
        public RenderTexture CreateTemporaryTargetForSave(int width, int height)
        {
            if (IsPortrait)
            {
                int tmp = width;
                width = height;
                height = tmp;
            }
            return RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
        }

        /// If m_AutoAlignRig is set, you should pass in a RenderTexture created
        /// with CreateTemporaryTargetForSave().
        public void RenderToTexture(RenderTexture rTexture, bool removeBackground = false)
        {
            RenderTextureFormat format = CameraFormat();
            int depth = 24;

            // Use a temporary rather than rendering to rTexture because we don't know
            // what format rTexture is... it may not be the correct format.
            RenderTexture targetA = RenderTexture.GetTemporary(
                rTexture.width, rTexture.height, depthBuffer: depth, format: format);

            {
                // Instead of doing a new Render(), it might seem tempting to copy from
                // the camera target.  That would be wrong, because the camera target's
                // resolution might be much lower than rTexture.
                var camera = LeftInfo.camera;
                var prev = camera.targetTexture;
                camera.targetTexture = targetA;
                if (removeBackground)
                {
                    var prevClearFlags = camera.clearFlags;
                    var prevBackgroundColor = camera.backgroundColor;
                    var prevCullingMask = camera.cullingMask;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0, 0, 0, 0);
                    camera.cullingMask = LayerMask.GetMask("MainCanvas");
                    camera.Render();
                    camera.clearFlags = prevClearFlags;
                    camera.backgroundColor = prevBackgroundColor;
                    camera.cullingMask = prevCullingMask;
                }
                else
                {
                    camera.Render();
                }
                camera.targetTexture = prev;
            }

            if (targetA != rTexture)
            {
                Graphics.Blit(targetA, rTexture);
                RenderTexture.ReleaseTemporary(targetA);
            }
        }

        /// Renders depth-normal data to a texture for use with SaveNormals method.
        /// If m_AutoAlignRig is set, you should pass in a RenderTexture created
        /// with CreateTemporaryTargetForSave().
        public void RenderDepthNormalToTexture(RenderTexture rTexture)
        {
            RenderEncodedDepthNormalsToTexture(rTexture);
        }

        private void RenderEncodedDepthNormalsToTexture(RenderTexture rTexture)
        {
            RenderTextureFormat format = CameraFormat();
            int depth = 24;

            // Use a temporary rather than rendering to rTexture because we don't know
            // what format rTexture is... it may not be the correct format.
            RenderTexture targetA = RenderTexture.GetTemporary(
                rTexture.width, rTexture.height, depthBuffer: depth, format: format);

            {
                var camera = LeftInfo.camera;
                var prev = camera.targetTexture;
                var prevDepthTextureMode = camera.depthTextureMode;
                var prevClearFlags = camera.clearFlags;
                var prevBackgroundColor = camera.backgroundColor;
                try
                {
                    camera.targetTexture = targetA;
                    camera.depthTextureMode = DepthTextureMode.Depth;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    // Neutral view-space normal and maximum encoded depth. This makes pixels
                    // untouched by the replacement shader decode as far rather than near.
                    camera.backgroundColor = new Color(0.5f, 0.5f, 1.0f, 0.0f);
                    camera.RenderWithShader(
                        Shader.Find("Hidden/Internal-DepthNormalsTexture"), "RenderType");
                }
                finally
                {
                    camera.backgroundColor = prevBackgroundColor;
                    camera.clearFlags = prevClearFlags;
                    camera.depthTextureMode = prevDepthTextureMode;
                    camera.targetTexture = prev;
                }
            }

            if (targetA != rTexture)
            {
                Graphics.Blit(targetA, rTexture);
                RenderTexture.ReleaseTemporary(targetA);
            }
        }

        /// Renders depth data to a texture for use with SaveDepth method.
        /// If m_AutoAlignRig is set, you should pass in a RenderTexture created
        /// with CreateTemporaryTargetForSave().
        public void RenderDepthToTexture(RenderTexture rTexture)
        {
            RenderEncodedDepthNormalsToTexture(rTexture);
        }

        static public void Save(Stream outf, RenderTexture rTextureToSave, bool bSaveAsPng)
        {
            var buffer = SaveToMemory(rTextureToSave, bSaveAsPng);
            outf.Write(buffer, 0, buffer.Length);
        }

        static public void SaveDepth(Stream outf, RenderTexture depthNormalTexture)
        {
            var buffer = SaveDepthToMemory(depthNormalTexture);
            outf.Write(buffer, 0, buffer.Length);
        }

        public static void SaveDepthCaptureFiles(string imagePath, DepthCaptureFiles files)
        {
            string captureBasePath = GetCaptureBasePath(imagePath);
            File.WriteAllBytes($"{captureBasePath}_depth.png", files.normalizedDepthPng);
            File.WriteAllBytes($"{captureBasePath}_depth16.png", files.linearDepth16Png);
            File.WriteAllBytes($"{captureBasePath}_depth.exr", files.linearDepthExr);
            File.WriteAllBytes($"{captureBasePath}_depth.json", files.metadataJson);
        }

        private static string GetCaptureBasePath(string imagePath)
        {
            string fullImagePath = Path.GetFullPath(imagePath);
            return Path.Combine(
                Path.GetDirectoryName(fullImagePath),
                Path.GetFileNameWithoutExtension(fullImagePath));
        }

        public DepthCaptureFiles EncodeDepthCapture(RenderTexture depthNormalTexture)
        {
            Debug.Assert(depthNormalTexture.format == RenderTextureFormat.ARGB32);

            Texture2D encodedDepthTexture;
            {
                RenderTexture prev = RenderTexture.active;
                try
                {
                    RenderTexture.active = depthNormalTexture;
                    encodedDepthTexture = new Texture2D(
                        depthNormalTexture.width, depthNormalTexture.height,
                        TextureFormat.RGBA32, false, true);
                    encodedDepthTexture.ReadPixels(
                        new Rect(0, 0, depthNormalTexture.width, depthNormalTexture.height),
                        0, 0);
                }
                finally
                {
                    RenderTexture.active = prev;
                }
            }

            Camera camera = LeftInfo.camera;
            float farClipMetres = camera.farClipPlane * App.UNITS_TO_METERS;
            try
            {
                var files = EncodeDepthCapture(encodedDepthTexture, farClipMetres);
                var metadata = new DepthCaptureMetadata
                {
                    width = depthNormalTexture.width,
                    height = depthNormalTexture.height,
                    nearClipMetres = camera.nearClipPlane * App.UNITS_TO_METERS,
                    farClipMetres = farClipMetres,
                    verticalFieldOfViewDegrees = camera.fieldOfView,
                    aspect = depthNormalTexture.width / (float)depthNormalTexture.height,
                    depth16ScaleMetres = farClipMetres / ushort.MaxValue,
                };
                files.metadataJson = System.Text.Encoding.UTF8.GetBytes(
                    JsonUtility.ToJson(metadata, true));
                return files;
            }
            finally
            {
                Destroy(encodedDepthTexture);
            }
        }

        static public void SaveNormals(Stream outf, RenderTexture depthNormalTexture)
        {
            var buffer = SaveNormalsToMemory(depthNormalTexture);
            outf.Write(buffer, 0, buffer.Length);
        }

        static public byte[] SaveToMemory(RenderTexture rTextureToSave, bool bSaveAsPng)
        {
            Debug.Assert(rTextureToSave.format == RenderTextureFormat.ARGB32);

            // Copy out of the RenderTexture
            Texture2D rNoAlphaTexture;
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rTextureToSave;
                rNoAlphaTexture = new Texture2D(rTextureToSave.width, rTextureToSave.height, TextureFormat.RGB24, false);
                rNoAlphaTexture.ReadPixels(new Rect(0, 0, rTextureToSave.width, rTextureToSave.height), 0, 0);
                RenderTexture.active = prev;
            }

            byte[] bytes = null;
            if (bSaveAsPng)
            {
                bytes = rNoAlphaTexture.EncodeToPNG();
            }
            else
            {
                bytes = rNoAlphaTexture.EncodeToJPG();
            }
            Destroy(rNoAlphaTexture);

            return bytes;
        }

        static public byte[] SaveDepthToMemory(RenderTexture depthTexture)
        {
            Debug.Assert(depthTexture.format == RenderTextureFormat.ARGB32);

            // Copy out of the RenderTexture
            Texture2D texture;
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = depthTexture;
                texture = new Texture2D(
                    depthTexture.width, depthTexture.height, TextureFormat.RGBA32, false);
                texture.ReadPixels(new Rect(0, 0, depthTexture.width, depthTexture.height), 0, 0);
                RenderTexture.active = prev;
            }

            // Unity's EncodeDepthNormal puts linear 0-1 depth in blue/alpha channels using
            // EncodeFloatRG. Build a histogram from that 16-bit source so the useful depth
            // range in this particular view can use the full 8-bit output range.
            NativeArray<Color32> pixels = texture.GetPixelData<Color32>(0);
            const int kDepthHistogramSize = 65536;
            int[] depthHistogram = new int[kDepthHistogramSize];
            int validPixelCount = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                // The replacement render clears untouched pixels to encoded depth 1.0.
                if (pixel.b == 255 && pixel.a == 0)
                {
                    continue;
                }

                // DecodeFloatRG: dot(enc, float2(1.0, 1/255.0))
                // Blue=enc.x, Alpha=enc.y
                float depth = pixel.b * (1.0f / 255.0f) +
                    pixel.a * (1.0f / (255.0f * 255.0f));
                int histogramIndex = Mathf.Clamp(
                    Mathf.RoundToInt(depth * (kDepthHistogramSize - 1)),
                    0, kDepthHistogramSize - 1);
                depthHistogram[histogramIndex]++;
                validPixelCount++;
            }

            int nearDepthIndex = 0;
            int farDepthIndex = kDepthHistogramSize - 1;
            if (validPixelCount > 0)
            {
                int nearRank = Mathf.FloorToInt((validPixelCount - 1) * 0.01f);
                int farRank = Mathf.CeilToInt((validPixelCount - 1) * 0.99f);
                int cumulativeCount = 0;
                bool foundNearDepth = false;
                for (int i = 0; i < depthHistogram.Length; i++)
                {
                    cumulativeCount += depthHistogram[i];
                    if (!foundNearDepth && cumulativeCount > nearRank)
                    {
                        nearDepthIndex = i;
                        foundNearDepth = true;
                    }
                    if (cumulativeCount > farRank)
                    {
                        farDepthIndex = i;
                        break;
                    }
                }
            }

            float nearDepth = nearDepthIndex * (1.0f / (kDepthHistogramSize - 1));
            float farDepth = farDepthIndex * (1.0f / (kDepthHistogramSize - 1));
            float depthRange = farDepth - nearDepth;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                byte value = 0;
                if (validPixelCount > 0 && !(pixel.b == 255 && pixel.a == 0))
                {
                    float depth = pixel.b * (1.0f / 255.0f) +
                        pixel.a * (1.0f / (255.0f * 255.0f));
                    float normalizedDepth = depthRange > 0.0f
                        ? Mathf.Clamp01((depth - nearDepth) / depthRange)
                        : 0.0f;

                    // Export disparity-style grayscale: white is near and black is far.
                    value = (byte)Mathf.RoundToInt((1.0f - normalizedDepth) * 255.0f);
                }
                pixels[i] = new Color32(value, value, value, 255);
            }

            byte[] bytes = texture.EncodeToPNG();
            Destroy(texture);

            return bytes;
        }

        private static DepthCaptureFiles EncodeDepthCapture(
            Texture2D encodedDepthTexture, float farClipMetres)
        {
            int width = encodedDepthTexture.width;
            int height = encodedDepthTexture.height;
            NativeArray<Color32> encodedPixels = encodedDepthTexture.GetPixelData<Color32>(0);

            const int kDepthHistogramSize = 65536;
            int[] depthHistogram = new int[kDepthHistogramSize];
            int validPixelCount = 0;
            for (int i = 0; i < encodedPixels.Length; i++)
            {
                Color32 pixel = encodedPixels[i];
                if (IsInvalidEncodedDepth(pixel))
                {
                    continue;
                }

                float linear01Depth = DecodeLinear01Depth(pixel);
                int histogramIndex = Mathf.Clamp(
                    Mathf.RoundToInt(linear01Depth * (kDepthHistogramSize - 1)),
                    0, kDepthHistogramSize - 1);
                depthHistogram[histogramIndex]++;
                validPixelCount++;
            }

            int nearDepthIndex = 0;
            int farDepthIndex = kDepthHistogramSize - 1;
            if (validPixelCount > 0)
            {
                int nearRank = Mathf.FloorToInt((validPixelCount - 1) * 0.01f);
                int farRank = Mathf.CeilToInt((validPixelCount - 1) * 0.99f);
                int cumulativeCount = 0;
                bool foundNearDepth = false;
                for (int i = 0; i < depthHistogram.Length; i++)
                {
                    cumulativeCount += depthHistogram[i];
                    if (!foundNearDepth && cumulativeCount > nearRank)
                    {
                        nearDepthIndex = i;
                        foundNearDepth = true;
                    }
                    if (cumulativeCount > farRank)
                    {
                        farDepthIndex = i;
                        break;
                    }
                }
            }

            float nearDepth = nearDepthIndex * (1.0f / (kDepthHistogramSize - 1));
            float farDepth = farDepthIndex * (1.0f / (kDepthHistogramSize - 1));
            float depthRange = farDepth - nearDepth;
            var files = new DepthCaptureFiles();

            Texture2D depth16Texture = new Texture2D(
                width, height, TextureFormat.R16, false, true);
            try
            {
                NativeArray<ushort> depth16Pixels = depth16Texture.GetPixelData<ushort>(0);
                for (int i = 0; i < encodedPixels.Length; i++)
                {
                    Color32 pixel = encodedPixels[i];
                    if (validPixelCount == 0 || IsInvalidEncodedDepth(pixel))
                    {
                        depth16Pixels[i] = 0;
                        continue;
                    }

                    float linear01Depth = DecodeLinear01Depth(pixel);
                    // Zero is reserved for pixels where the replacement shader rendered nothing.
                    depth16Pixels[i] = (ushort)Mathf.Clamp(
                        Mathf.RoundToInt(linear01Depth * ushort.MaxValue),
                        1, ushort.MaxValue);
                }

                depth16Texture.Apply(false, false);
                files.linearDepth16Png = ImageConversion.EncodeToPNG(depth16Texture);
            }
            finally
            {
                // These transient textures can be hundreds of megabytes. Release their native
                // storage before allocating the next representation rather than at frame end.
                DestroyImmediate(depth16Texture);
            }

            Texture2D exrTexture = new Texture2D(
                width, height, TextureFormat.RFloat, false, true);
            try
            {
                NativeArray<float> exrPixels = exrTexture.GetPixelData<float>(0);
                for (int i = 0; i < encodedPixels.Length; i++)
                {
                    Color32 pixel = encodedPixels[i];
                    exrPixels[i] = validPixelCount == 0 || IsInvalidEncodedDepth(pixel)
                        ? 0.0f
                        : DecodeLinear01Depth(pixel) * farClipMetres;
                }

                exrTexture.Apply(false, false);
                files.linearDepthExr = ImageConversion.EncodeToEXR(
                    exrTexture,
                    Texture2D.EXRFlags.OutputAsFloat | Texture2D.EXRFlags.CompressZIP);
            }
            finally
            {
                DestroyImmediate(exrTexture);
            }

            // The source texture is no longer needed in its packed form. Reuse its RGBA32
            // storage for the normalized compatibility image instead of allocating another copy.
            for (int i = 0; i < encodedPixels.Length; i++)
            {
                Color32 pixel = encodedPixels[i];
                byte value = 0;
                if (validPixelCount > 0 && !IsInvalidEncodedDepth(pixel))
                {
                    float linear01Depth = DecodeLinear01Depth(pixel);
                    float normalizedDepth = depthRange > 0.0f
                        ? Mathf.Clamp01((linear01Depth - nearDepth) / depthRange)
                        : 0.0f;
                    value = (byte)Mathf.RoundToInt(
                        (1.0f - normalizedDepth) * byte.MaxValue);
                }
                encodedPixels[i] = new Color32(value, value, value, 255);
            }
            encodedDepthTexture.Apply(false, false);
            files.normalizedDepthPng = ImageConversion.EncodeToPNG(encodedDepthTexture);
            return files;
        }

        private static bool IsInvalidEncodedDepth(Color32 pixel)
        {
            return pixel.b == byte.MaxValue && pixel.a == 0;
        }

        private static float DecodeLinear01Depth(Color32 pixel)
        {
            // Unity's DecodeFloatRG: dot(enc, float2(1.0, 1/255.0)).
            return pixel.b * (1.0f / byte.MaxValue) +
                pixel.a * (1.0f / (byte.MaxValue * byte.MaxValue));
        }

        static public byte[] SaveNormalsToMemory(RenderTexture depthNormalTexture)
        {
            Debug.Assert(depthNormalTexture.format == RenderTextureFormat.ARGB32);

            // Copy out of the RenderTexture
            Texture2D normalTexture;
            {
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = depthNormalTexture;
                normalTexture = new Texture2D(
                    depthNormalTexture.width, depthNormalTexture.height, TextureFormat.RGBA32, false);
                normalTexture.ReadPixels(new Rect(0, 0, depthNormalTexture.width, depthNormalTexture.height), 0, 0);
                RenderTexture.active = prev;
            }

            // Decode the stereographically encoded view-space normals from red and green.
            NativeArray<Color32> pixels = normalTexture.GetPixelData<Color32>(0);
            for (int i = 0; i < pixels.Length; i++)
            {
                const float kStereoScale = 1.7777f;
                float encodedX = pixels[i].r * (1.0f / 255.0f) *
                    (2.0f * kStereoScale) - kStereoScale;
                float encodedY = pixels[i].g * (1.0f / 255.0f) *
                    (2.0f * kStereoScale) - kStereoScale;
                float scale = 2.0f / (encodedX * encodedX + encodedY * encodedY + 1.0f);
                float nx = encodedX * scale;
                float ny = encodedY * scale;
                float nz = scale - 1.0f;

                // Convert back to 0-1 range for storage
                pixels[i] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(nx * 0.5f + 0.5f) * 255.0f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(ny * 0.5f + 0.5f) * 255.0f),
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(nz * 0.5f + 0.5f) * 255.0f),
                    255);
            }

            byte[] bytes = normalTexture.EncodeToPNG();
            Destroy(normalTexture);

            return bytes;
        }

        public static void TakeSnapshot(TrTransform tr, string filename, int width, int height, float superSampling = 1f, bool removeBackground = false, bool renderDepth = false, bool renderNormals = false)
        {
            bool saveAsPng;
            if (filename.ToLower().EndsWith(".jpg") || filename.ToLower().EndsWith(".jpeg"))
            {
                saveAsPng = false;
            }
            else if (filename.ToLower().EndsWith(".png"))
            {
                saveAsPng = true;
            }
            else
            {
                saveAsPng = false;
                filename += ".jpg";
            }
            string path = Path.Join(App.SnapshotPath(), filename);
            MultiCamTool cam = SketchSurfacePanel.m_Instance.GetToolOfType(BaseTool.ToolType.MultiCamTool) as MultiCamTool;

            if (cam != null)
            {
                var rig = SketchControlsScript.m_Instance.MultiCamCaptureRig;
                App.Scene.AsScene[rig.gameObject.transform] = tr;
                var rMgr = rig.ManagerFromStyle(MultiCamStyle.Snapshot);
                var initialState = rig.gameObject.activeSelf;
                RenderTexture tmp = null;
                RenderWrapper wrapper = null;
                float ssaaRestore = 0;
                try
                {
                    rig.gameObject.SetActive(true);
                    tmp = rMgr.CreateTemporaryTargetForSave(width, height);
                    wrapper = rMgr.gameObject.GetComponent<RenderWrapper>();
                    ssaaRestore = wrapper.SuperSampling;
                    wrapper.SuperSampling = superSampling;

                    bool watermarkEnabled = CameraConfig.Watermark;
                    bool postEffectsEnabled = CameraConfig.PostEffects;
                    bool suppressPostEffectsRestore = wrapper.SuppressPostEffects;
                    try
                    {
                        CameraConfig.Watermark = false;
                        CameraConfig.PostEffects = false;
                        wrapper.SuppressPostEffects = true;

                        if (renderDepth)
                        {
                            rMgr.RenderDepthToTexture(tmp);
                            var depthFiles = rMgr.EncodeDepthCapture(tmp);
                            SaveDepthCaptureFiles(path, depthFiles);
                        }

                        if (renderNormals)
                        {
                            rMgr.RenderDepthNormalToTexture(tmp);
                            var normalPath = $"{GetCaptureBasePath(path)}_normals.png";
                            using (var fs = new FileStream(normalPath, FileMode.Create))
                            {
                                SaveNormals(fs, tmp);
                            }
                        }
                    }
                    finally
                    {
                        wrapper.SuppressPostEffects = suppressPostEffectsRestore;
                        CameraConfig.Watermark = watermarkEnabled;
                        CameraConfig.PostEffects = postEffectsEnabled;
                    }

                    rMgr.RenderToTexture(tmp, removeBackground: removeBackground);
                    using (var fs = new FileStream(path, FileMode.Create))
                    {
                        Save(fs, tmp, bSaveAsPng: saveAsPng);
                    }

                }
                finally
                {
                    if (wrapper != null)
                    {
                        wrapper.SuperSampling = ssaaRestore;
                    }
                    rig.gameObject.SetActive(initialState);
                    if (tmp != null)
                    {
                        RenderTexture.ReleaseTemporary(tmp);
                    }
                }
            }
        }
    }
} // namespace TiltBrush
