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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
            ConfigureUrpCaptureCamera(m_LeftInfo.camera);
            if (m_RightInfo != null)
            {
                ConfigureUrpCaptureCamera(m_RightInfo.camera);
            }

            if (!App.Config.PlatformConfig.EnableMulticamPreview)
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
                DisableOptionalBuiltInCapturePostEffects();
            }
            if (m_UseDisplayWidthFromConfigFile)
            {
                SetScreenshotResolution(App.UserConfig.Video.Resolution);
            }
            CreateDisplayRenderTextures();

            CameraConfig.FovChanged += RefreshFovs;
            RefreshFovs();
        }

        void ConfigureUrpCaptureCamera(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            EnsureCaptureVisibleLayers(camera);

            if (UrpPostProcessingController.Instance == null)
            {
                return;
            }

            UrpPostProcessingController.Instance.ConfigureScreenshotCamera(
                camera, enableCaptureEffects: CameraConfig.PostEffects);
        }

        void EnsureCaptureVisibleLayers(Camera camera)
        {
            AddLayerToCullingMask(camera, "Environment");
        }

        void AddLayerToCullingMask(Camera camera, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return;
            }

            camera.cullingMask |= 1 << layer;
        }

        void DisableOptionalBuiltInCapturePostEffects()
        {
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                return;
            }

            MobileBloom mobileBloom = GetComponent<MobileBloom>();
            if (mobileBloom != null)
            {
                mobileBloom.enabled = false;
            }

            SENaturalBloomAndDirtyLens pcBloom =
                GetComponent<SENaturalBloomAndDirtyLens>();
            if (pcBloom != null)
            {
                pcBloom.enabled = false;
            }
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
            return CaptureColorUtils.GetFormat(GetComponent<Camera>());
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

            UrpPostProcessingController.ConfigureOffscreenCaptureCamera(info.camera);
            info.camera.targetTexture = null;
            Destroy(info.renderTexture);

            info.renderTexture = CreatePreviewRenderTexture(width, height, format);
            info.renderTexture.name = "SshotTex" + tag;
            Debug.Assert(info.renderer != null);
            Debug.Assert(info.renderer.material != null);
            info.renderer.material.SetTexture("_MainTex", info.renderTexture);
            info.renderer.material.name = "SshotMat" + tag;
            info.camera.targetTexture = info.renderTexture;
        }

        private static RenderTexture CreatePreviewRenderTexture(
            int width, int height, RenderTextureFormat format)
        {
            var descriptor = CaptureColorUtils.CreateDescriptor(width, height, format);
            return new RenderTexture(descriptor);
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
        public void RenderToTexture(
            RenderTexture rTexture,
            bool removeBackground = false,
            bool includePostProcessing = false)
        {
            bool usePostProcessing = includePostProcessing && !removeBackground;
            RenderTextureFormat format = CameraFormat();

            // Use a temporary rather than rendering to rTexture because we don't know
            // what format rTexture is... it may not be the correct format.
            RenderTexture targetA = RenderTexture.GetTemporary(
                CaptureColorUtils.CreateDescriptor(rTexture.width, rTexture.height, format));

            try
            {
                // Instead of doing a new Render(), it might seem tempting to copy from
                // the camera target.  That would be wrong, because the camera target's
                // resolution might be much lower than rTexture.
                var camera = LeftInfo.camera;
                UrpPostProcessingController.CameraPostProcessingState postProcessingState =
                    BeginCapturePostProcessing(camera, usePostProcessing);
                RenderTexture prev = camera.targetTexture;
                CameraClearFlags prevClearFlags = camera.clearFlags;
                Color prevBackgroundColor = camera.backgroundColor;
                int prevCullingMask = camera.cullingMask;
                bool prevAllowMsaa = camera.allowMSAA;
                StereoTargetEyeMask prevStereoTargetEye = StereoTargetEyeMask.None;
                bool restoreStereoTargetEye = GraphicsSettings.currentRenderPipeline == null;
                if (restoreStereoTargetEye)
                {
                    prevStereoTargetEye = camera.stereoTargetEye;
                }
                try
                {
                    if (restoreStereoTargetEye)
                    {
                        camera.stereoTargetEye = StereoTargetEyeMask.None;
                    }
                    camera.targetTexture = targetA;
                    camera.allowMSAA = targetA.antiAliasing > 1;
                    if (removeBackground)
                    {
                        camera.clearFlags = CameraClearFlags.SolidColor;
                        camera.backgroundColor = new Color(0, 0, 0, 0);
                        camera.cullingMask = LayerMask.GetMask("MainCanvas");
                    }
                    camera.Render();
                }
                finally
                {
                    camera.clearFlags = prevClearFlags;
                    camera.backgroundColor = prevBackgroundColor;
                    camera.cullingMask = prevCullingMask;
                    camera.targetTexture = prev;
                    camera.allowMSAA = prevAllowMsaa;
                    if (restoreStereoTargetEye)
                    {
                        camera.stereoTargetEye = prevStereoTargetEye;
                    }
                    EndCapturePostProcessing(postProcessingState);
                }

                CaptureColorUtils.Resolve(targetA, rTexture, removeBackground);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(targetA);
            }
        }

        UrpPostProcessingController.CameraPostProcessingState BeginCapturePostProcessing(
            Camera camera, bool includePostProcessing)
        {
            if (UrpPostProcessingController.Instance != null)
            {
                UrpPostProcessingController.CameraPostProcessingState controllerState =
                    UrpPostProcessingController.Instance.BeginCapturePostProcessing(
                        camera, includePostProcessing);
                return controllerState;
            }

            UrpPostProcessingController.CameraPostProcessingState state =
                new UrpPostProcessingController.CameraPostProcessingState
                {
                    camera = camera,
                    allowHDR = camera.allowHDR
                };

            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                if (!includePostProcessing) return state;
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            state.cameraData = cameraData;
            state.renderPostProcessing = cameraData.renderPostProcessing;
            state.volumeLayerMask = cameraData.volumeLayerMask;
            state.volumeTrigger = cameraData.volumeTrigger;

            if (!includePostProcessing)
            {
                cameraData.renderPostProcessing = false;
                return state;
            }

            camera.allowHDR = true;
            cameraData.renderPostProcessing = true;
            cameraData.volumeLayerMask = ~0;
            cameraData.volumeTrigger = camera.transform;


            return state;
        }

        void EndCapturePostProcessing(UrpPostProcessingController.CameraPostProcessingState state)
        {
            if (UrpPostProcessingController.Instance != null)
            {
                UrpPostProcessingController.Instance.EndCapturePostProcessing(state);
                return;
            }

            if (state.camera == null)
            {
                return;
            }

            state.camera.allowHDR = state.allowHDR;
            if (state.cameraData == null)
            {
                return;
            }

            state.cameraData.renderPostProcessing = state.renderPostProcessing;
            state.cameraData.volumeLayerMask = state.volumeLayerMask;
            state.cameraData.volumeTrigger = state.volumeTrigger;
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

            try
            {
                var camera = LeftInfo.camera;
                var prev = camera.targetTexture;
                var prevDepthTextureMode = camera.depthTextureMode;
                var prevClearFlags = camera.clearFlags;
                var prevBackgroundColor = camera.backgroundColor;
                StereoTargetEyeMask prevStereoTargetEye = StereoTargetEyeMask.None;
                bool restoreStereoTargetEye = GraphicsSettings.currentRenderPipeline == null;
                if (restoreStereoTargetEye)
                {
                    prevStereoTargetEye = camera.stereoTargetEye;
                }
                try
                {
                    if (restoreStereoTargetEye)
                    {
                        camera.stereoTargetEye = StereoTargetEyeMask.None;
                    }
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
                    if (restoreStereoTargetEye)
                    {
                        camera.stereoTargetEye = prevStereoTargetEye;
                    }
                }

                Graphics.Blit(targetA, rTexture);
            }
            finally
            {
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

        static public void Save(Stream outf, RenderTexture rTextureToSave, bool bSaveAsPng,
            bool preserveAlpha = false)
        {
            var buffer = SaveToMemory(rTextureToSave, bSaveAsPng, preserveAlpha);
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

        static public byte[] SaveToMemory(RenderTexture rTextureToSave, bool bSaveAsPng,
            bool preserveAlpha = false)
        {
            // CPU readback needs resolved pixels, even when the caller renders directly
            // into an MSAA target (rather than through RenderToTexture).
            RenderTexture resolved = null;
            Texture2D texture = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                bool exportAlpha = bSaveAsPng && preserveAlpha;
                if (rTextureToSave.antiAliasing > 1)
                {
                    resolved = RenderTexture.GetTemporary(rTextureToSave.width,
                        rTextureToSave.height, 0, RenderTextureFormat.ARGB32);
                    CaptureColorUtils.Resolve(rTextureToSave, resolved, exportAlpha);
                }
                RenderTexture.active = resolved != null ? resolved : rTextureToSave;
                var format = exportAlpha ? TextureFormat.RGBA32 : TextureFormat.RGB24;
                texture = new Texture2D(rTextureToSave.width, rTextureToSave.height, format, false);
                texture.ReadPixels(new Rect(0, 0, rTextureToSave.width, rTextureToSave.height), 0, 0);
                return bSaveAsPng ? texture.EncodeToPNG() : texture.EncodeToJPG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (texture != null) Destroy(texture);
                if (resolved != null) RenderTexture.ReleaseTemporary(resolved);
            }
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

        // ------------------------------------------------------------------
        // Calibrated-camera support, for matching a real physical camera when
        // compositing CG over live-action footage.
        //
        // The normal path only exposes a single vertical fieldOfView, which
        // can't express a non-centred principal point (cx, cy) or non-square
        // pixels (fx != fy) - real calibrated lenses have both. Unity's
        // physical-camera lensShift could cover the principal point but not
        // fx != fy, and uses its own normalisation convention. Building the
        // projection matrix straight from OpenCV-style intrinsics expresses
        // all four exactly, with no convention to convert.
        // ------------------------------------------------------------------

        private static bool s_UseCalibratedProjection;
        private static float s_Fx, s_Fy, s_Cx, s_Cy, s_RefWidth, s_RefHeight;

        private static bool s_UseCalibratedClip;
        private static float s_NearClip, s_FarClip;

        /// Written on the main thread whenever a calibrated projection is
        /// applied, so query.camera.projection can report it from the HTTP
        /// listener thread without touching Unity objects off-thread.
        /// Same approach as ApiMainThreadObserver.
        public static volatile string s_ProjectionReportJson = "{\"calibrated\":false}";

        /// Where the capture camera actually was for the most recent snapshot, in world space.
        /// A camera is pose AND projection: reporting only intrinsics can't distinguish "camera
        /// set up correctly" from "camera aimed at empty space", which is exactly the failure
        /// that cost a rebuild to diagnose. Null until the first capture.
        public static volatile string s_LastCaptureExtrinsicsJson = "null";

        // Editor Play sessions keep static state (no domain reload); start uncalibrated.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCalibratedCameraState() => ClearCalibratedCamera();

        // Back to Open Brush's own camera projection and clipping (the post-production clip set
        // these; New Sketch ends that session).
        public static void ClearCalibratedCamera()
        {
            s_UseCalibratedProjection = false;
            s_Fx = s_Fy = s_Cx = s_Cy = s_RefWidth = s_RefHeight = 0;
            s_UseCalibratedClip = false;
            s_NearClip = s_FarClip = 0;
            s_ProjectionReportJson = "{\"calibrated\":false}";
            s_LastCaptureExtrinsicsJson = "null";
        }

        public static void SetCalibratedIntrinsics(
            float fx, float fy, float cx, float cy, float refWidth, float refHeight)
        {
            s_Fx = fx; s_Fy = fy; s_Cx = cx; s_Cy = cy;
            s_RefWidth = refWidth; s_RefHeight = refHeight;
            s_UseCalibratedProjection = true;
            // Report straight away, at the calibrated resolution, so a UI can sanity-check
            // the numbers without having to render first. Refreshed with the real render
            // resolution after each snapshot.
            RefreshProjectionReport(
                fx, fy, cx, cy, refWidth, refHeight,
                s_UseCalibratedClip ? s_NearClip : float.NaN,
                s_UseCalibratedClip ? s_FarClip : float.NaN,
                "configured");
        }

        /// Human-readable derivation of what the intrinsics actually mean, for
        /// sanity-checking against a real camera. Field of view and lens shift are
        /// reported in Blender's own conventions so they can be compared directly
        /// against a Blender camera's values.
        /// Clip may be NaN when it hasn't been set yet, in which case the depth row
        /// of the matrix isn't meaningful and is reported as null.
        private static void RefreshProjectionReport(
            float fx, float fy, float cx, float cy, float w, float h,
            float near, float far, string source)
        {
            float vFovDeg = 2f * Mathf.Atan(h / (2f * fy)) * Mathf.Rad2Deg;
            float hFovDeg = 2f * Mathf.Atan(w / (2f * fx)) * Mathf.Rad2Deg;
            // Blender normalises lens shift by the larger image dimension, and flips
            // the sign of x relative to a raw principal-point offset.
            float maxDim = Mathf.Max(w, h);
            float shiftX = -(cx - w / 2f) / maxDim;
            float shiftY = (cy - h / 2f) / maxDim;

            bool clipKnown = !float.IsNaN(near) && !float.IsNaN(far);
            string matrixJson = clipKnown
                ? MatrixToJson(BuildProjectionFromIntrinsics(fx, fy, cx, cy, w, h, near, far))
                : "null";

            s_ProjectionReportJson =
                "{\"calibrated\":true," +
                $"\"source\":\"{source}\"," +
                $"\"width\":{w:F0},\"height\":{h:F0}," +
                $"\"fx\":{fx:F6},\"fy\":{fy:F6},\"cx\":{cx:F6},\"cy\":{cy:F6}," +
                $"\"verticalFovDegrees\":{vFovDeg:F6},\"horizontalFovDegrees\":{hFovDeg:F6}," +
                $"\"blenderShiftX\":{shiftX:F6},\"blenderShiftY\":{shiftY:F6}," +
                (clipKnown
                    ? $"\"nearClipUnits\":{near:F6},\"farClipUnits\":{far:F6}," +
                      $"\"nearClipMetres\":{near * App.UNITS_TO_METERS:F6}," +
                      $"\"farClipMetres\":{far * App.UNITS_TO_METERS:F6},"
                    : "\"nearClipUnits\":null,\"farClipUnits\":null," +
                      "\"nearClipMetres\":null,\"farClipMetres\":null,") +
                $"\"projectionMatrix\":{matrixJson}}}";
        }

        public static void ClearCalibratedIntrinsics()
        {
            s_UseCalibratedProjection = false;
            s_ProjectionReportJson = "{\"calibrated\":false}";
        }

        // Raw getters for the post-production preview's persistence (stage 8, added
        // 2026-09-08) - values are stored exactly as passed to
        // SetCalibratedIntrinsics/SetCalibratedClip (no unit conversion), so these
        // round-trip directly with the camera.intrinsics/camera.clip API commands.
        public static bool UseCalibratedProjection => s_UseCalibratedProjection;
        public static bool UseCalibratedClip => s_UseCalibratedClip;
        public static float CalibratedFx => s_Fx;
        public static float CalibratedFy => s_Fy;
        public static float CalibratedCx => s_Cx;
        public static float CalibratedCy => s_Cy;
        public static float CalibratedRefWidth => s_RefWidth;
        public static float CalibratedRefHeight => s_RefHeight;
        public static float CalibratedNearClip => s_NearClip;
        public static float CalibratedFarClip => s_FarClip;

        /// Clip planes in Open Brush units (1 metre = App.METERS_TO_UNITS = 10 units),
        /// so a real camera's metric near/far need scaling by 10 before being set here.
        public static void SetCalibratedClip(float near, float far)
        {
            s_NearClip = near; s_FarClip = far;
            s_UseCalibratedClip = true;
            // Clip feeds the projection's depth row, so the report is now stale.
            if (s_UseCalibratedProjection)
            {
                RefreshProjectionReport(
                    s_Fx, s_Fy, s_Cx, s_Cy, s_RefWidth, s_RefHeight, near, far, "configured");
            }
        }

        public static void ClearCalibratedClip()
        {
            s_UseCalibratedClip = false;
        }

        /// Applies the currently-configured calibrated intrinsics/clip (see
        /// SetCalibratedIntrinsics/SetCalibratedClip) to an arbitrary camera - for
        /// the post-production live preview (POSTPROD_PREVIEW_PLAN.md, stage 6),
        /// which needs to drive whatever camera is currently painting the desktop
        /// window rather than the snapshot capture rig. Same maths TakeSnapshot
        /// uses internally, factored out here rather than refactoring that
        /// existing, already-proven path, to avoid any risk of regressing it.
        /// A no-op if no calibrated intrinsics are configured.
        public static void ApplyCalibratedProjectionToCamera(Camera cam)
        {
            if (!s_UseCalibratedProjection) { return; }

            int width = cam.pixelWidth;
            int height = cam.pixelHeight;
            float sx = width / s_RefWidth;
            float sy = height / s_RefHeight;
            float fx = s_Fx * sx, fy = s_Fy * sy;
            float cx = s_Cx * sx, cy = s_Cy * sy;

            if (s_UseCalibratedClip)
            {
                cam.nearClipPlane = s_NearClip;
                cam.farClipPlane = s_FarClip;
            }

            float vFovDeg = 2f * Mathf.Atan(height / (2f * fy)) * Mathf.Rad2Deg;
            cam.fieldOfView = vFovDeg;

            // Assign the projection last - setting fieldOfView can invalidate an
            // explicitly-assigned projection matrix.
            cam.projectionMatrix = BuildProjectionFromIntrinsics(
                fx, fy, cx, cy, width, height, cam.nearClipPlane, cam.farClipPlane);

            RefreshProjectionReport(
                fx, fy, cx, cy, width, height, cam.nearClipPlane, cam.farClipPlane, "livePreview");
        }

        /// Restores a camera to Unity's normal FOV-driven projection, undoing
        /// ApplyCalibratedProjectionToCamera.
        public static void ClearCalibratedProjectionFromCamera(Camera cam)
        {
            cam.ResetProjectionMatrix();
        }

        /// OpenCV-style intrinsics -> OpenGL-convention projection matrix.
        /// Unity wants the GL convention here (view space looking down -Z,
        /// NDC y up) and converts to the platform's own convention itself.
        /// The cx/cy terms carry the principal-point offset; the y row also
        /// absorbs the OpenCV y-down to GL y-up flip.
        public static Matrix4x4 BuildProjectionFromIntrinsics(
            float fx, float fy, float cx, float cy, float w, float h, float near, float far)
        {
            var m = new Matrix4x4();
            m[0, 0] = 2f * fx / w;
            m[0, 2] = 1f - 2f * cx / w;
            m[1, 1] = 2f * fy / h;
            m[1, 2] = 2f * cy / h - 1f;
            m[2, 2] = -(far + near) / (far - near);
            m[2, 3] = -2f * far * near / (far - near);
            m[3, 2] = -1f;
            return m;
        }

        /// Row-major, so it reads the same way it's written in the maths above.
        private static string MatrixToJson(Matrix4x4 m)
        {
            var sb = new System.Text.StringBuilder("[");
            for (int row = 0; row < 4; row++)
            {
                if (row > 0) { sb.Append(","); }
                sb.Append("[");
                for (int col = 0; col < 4; col++)
                {
                    if (col > 0) { sb.Append(","); }
                    sb.Append(m[row, col].ToString("F8"));
                }
                sb.Append("]");
            }
            sb.Append("]");
            return sb.ToString();
        }

        public static void TakeSnapshot(
            TrTransform tr,
            string filename,
            int width,
            int height,
            float superSampling = 1f,
            bool removeBackground = false,
            bool renderDepth = false,
            bool renderNormals = false,
            bool includePostProcessing = false)
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
                try
                {
                    var rig = SketchControlsScript.m_Instance.MultiCamCaptureRig;

                    App.Scene.AsScene[rig.gameObject.transform] = tr;
                    var rMgr = rig.ManagerFromStyle(MultiCamStyle.Snapshot);

                    var initialState = rig.gameObject.activeSelf;
                    RenderTexture tmp = null;
                    RenderWrapper wrapper = null;
                    float ssaaRestore = 0;
                    Camera calibratedCam = null;
                    float fovRestore = 0;
                    float nearRestore = 0, farRestore = 0;
                    try
                    {
                        rig.gameObject.SetActive(true);

                        tmp = rMgr.CreateTemporaryTargetForSave(width, height);

                        wrapper = rMgr.gameObject.GetComponent<RenderWrapper>();

                        ssaaRestore = wrapper.SuperSampling;
                        wrapper.SuperSampling = superSampling;

                        // Applied around ALL passes so colour, depth and normals share one
                        // projection - they get composited against each other later, so any
                        // disagreement between them would be baked into the result.
                        if (s_UseCalibratedProjection || s_UseCalibratedClip)
                        {
                            calibratedCam = rMgr.LeftEye;
                            fovRestore = calibratedCam.fieldOfView;
                            nearRestore = calibratedCam.nearClipPlane;
                            farRestore = calibratedCam.farClipPlane;

                            // Clip planes go on first: the projection matrix below is built
                            // from them, and the depth sidecar's near/far metres come from
                            // them too.
                            if (s_UseCalibratedClip)
                            {
                                calibratedCam.nearClipPlane = s_NearClip;
                                calibratedCam.farClipPlane = s_FarClip;

                            }
                        }

                        if (s_UseCalibratedProjection)
                        {
                            // Intrinsics are stored against the resolution they were
                            // calibrated at; rescale to whatever we're actually rendering.
                            float sx = width / s_RefWidth;
                            float sy = height / s_RefHeight;
                            float fx = s_Fx * sx, fy = s_Fy * sy;
                            float cx = s_Cx * sx, cy = s_Cy * sy;

                            // EncodeDepthCapture writes camera.fieldOfView into the depth
                            // sidecar that the compositing step reads, so keep it consistent
                            // with the projection we're about to install rather than leaving
                            // a stale value behind.
                            float vFovDeg = 2f * Mathf.Atan(height / (2f * fy)) * Mathf.Rad2Deg;
                            calibratedCam.fieldOfView = vFovDeg;

                            // Assign the projection last - setting fieldOfView can invalidate
                            // an explicitly-assigned projection matrix.
                            calibratedCam.projectionMatrix = BuildProjectionFromIntrinsics(
                                fx, fy, cx, cy, width, height,
                                calibratedCam.nearClipPlane, calibratedCam.farClipPlane);

                            RefreshProjectionReport(
                                fx, fy, cx, cy, width, height,
                                calibratedCam.nearClipPlane, calibratedCam.farClipPlane,
                                "lastRender");

                        }

                        // Record where the capture camera actually ended up, AFTER everything
                        // that might have moved it. If something else is driving the rig (as
                        // MultiCamTool does while it is the active tool) this is where it shows
                        // up - the requested pose and the real one will disagree.
                        {
                            Transform camXf = rMgr.LeftEye.transform;
                            Vector3 p = camXf.position;
                            Quaternion q = camXf.rotation;
                            Vector3 e = q.eulerAngles;
                            Vector3 f = camXf.forward;
                            s_LastCaptureExtrinsicsJson =
                                "{" +
                                $"\"worldPosition\":[{p.x:F6},{p.y:F6},{p.z:F6}]," +
                                $"\"rotationQuaternion\":[{q.x:F6},{q.y:F6},{q.z:F6},{q.w:F6}]," +
                                $"\"rotationEulerDegrees\":[{e.x:F4},{e.y:F4},{e.z:F4}]," +
                                $"\"forward\":[{f.x:F6},{f.y:F6},{f.z:F6}]," +
                                $"\"requestedPosition\":[{tr.translation.x:F6},{tr.translation.y:F6},{tr.translation.z:F6}]," +
                                $"\"requestedRotation\":[{tr.rotation.x:F6},{tr.rotation.y:F6},{tr.rotation.z:F6},{tr.rotation.w:F6}]" +
                                "}";

                        }

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

                        rMgr.RenderToTexture(tmp, removeBackground: removeBackground,
                            includePostProcessing: includePostProcessing);

                        using (var fs = new FileStream(path, FileMode.Create))
                        {
                            Save(fs, tmp, bSaveAsPng: saveAsPng, preserveAlpha: removeBackground);
                        }


                    }
                    finally
                    {
                        if (wrapper != null)
                        {
                            wrapper.SuperSampling = ssaaRestore;
                        }
                        if (calibratedCam != null)
                        {
                            // ResetProjectionMatrix hands control back to Unity's own
                            // fieldOfView-driven projection, so the normal in-app camera
                            // isn't left with our calibrated one.
                            calibratedCam.fieldOfView = fovRestore;
                            calibratedCam.nearClipPlane = nearRestore;
                            calibratedCam.farClipPlane = farRestore;
                            calibratedCam.ResetProjectionMatrix();
                        }
                        rig.gameObject.SetActive(initialState);
                        if (tmp != null)
                        {
                            RenderTexture.ReleaseTemporary(tmp);
                        }
                    }
                }
                catch (System.Exception)
                {
                    throw;
                }
            }
        }
    }
} // namespace TiltBrush
