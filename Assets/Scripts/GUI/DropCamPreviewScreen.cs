// Copyright 2023 The Open Brush Authors
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

using UnityEngine;

namespace TiltBrush
{
    public class DropCamPreviewScreen : UIComponent
    {
        [SerializeField] private GameObject m_Mesh;
        [SerializeField] private Material m_UninitializedCameraMaterial;

        public bool m_PreviewOn;

        private Camera m_Camera;

        // Cached off or created on Start(); otherwise read-only
        private CameraInfo m_CameraInfo;

        /// Where the camera's output should be fed.
        public MeshRenderer m_Display;
        // Width of the live render target
        public int m_DisplayWidth;
        // Height of the live render target
        public int m_DisplayHeight;


        protected override void Awake()
        {
            base.Awake();
            m_Mesh.GetComponent<Renderer>().material = m_UninitializedCameraMaterial;
        }

        void UpdateScreenVisuals()
        {
            bool previewShouldBeOn = LeftEyeMaterialRenderTextureExists;
            if (previewShouldBeOn != m_PreviewOn)
            {
                m_PreviewOn = previewShouldBeOn;
                if (m_PreviewOn)
                {
                    m_Mesh.GetComponent<Renderer>().material = LeftEyeMaterial;
                }
                else
                {
                    m_Mesh.GetComponent<Renderer>().material = m_UninitializedCameraMaterial;
                }
            }
        }

        void Update()
        {
            UpdateVisuals();
        }

        override public void UpdateVisuals()
        {
            base.UpdateVisuals();
            UpdateScreenVisuals();
        }

        class CameraInfo
        {
            // Material is mutated to display renderTexture
            public MeshRenderer renderer;
            // Camera is mutated to write to renderTexture
            public Camera camera;
            public RenderTexture renderTexture;
        }

        public Material LeftEyeMaterial { get => CamInfo.renderer.material; }
        public bool LeftEyeMaterialRenderTextureExists { get => CamInfo.renderTexture != null; }

        private CameraInfo CamInfo
        {
            get
            {
                // Need to lazy-init this; others might try to call our public API before
                // our Awake() and Start() have been called (because object starts inactive)
                if (m_CameraInfo == null)
                {
                    m_CameraInfo = new CameraInfo();
                    m_CameraInfo.camera = m_Camera;
                    m_CameraInfo.renderer = m_Display;
                }
                return m_CameraInfo;
            }
        }

        protected override void Start()
        {
            base.Start();

            m_Camera = SketchControlsScript.m_Instance.GetDropCampWidget().GetComponentInChildren<Camera>();

            // Lazy init.
            m_CameraInfo = CamInfo;

            SceneSettings.m_Instance.RegisterCamera(m_CameraInfo.camera);

            if (App.Config.IsMobileHardware)
            {
                // Force no HDR on mobile
                if (m_CameraInfo == null)
                {
                    Debug.LogAssertion("ScreenshotManager m_LeftInfo is null in ScreenshotManager.Start.");
                }
                else if (m_CameraInfo.camera == null)
                {
                    Debug.LogAssertion("ScreenshotManager m_LeftInfo.camera  is null in ScreenshotManager.Start.");
                }
                else
                {
                    m_CameraInfo.camera.allowHDR = false;
                }
                var mobileBloom = GetComponent<MobileBloom>();
                if (mobileBloom != null)
                {
                    mobileBloom.enabled = true;
                }
                else
                {
                    Debug.LogAssertion("No MobileBloom on the Screenshot Manager.");
                }
                var pcBloom = m_Camera.GetComponent<SENaturalBloomAndDirtyLens>();
                if (pcBloom != null)
                {
                    pcBloom.enabled = false;
                }
                else
                {
                    Debug.LogAssertion("No SENaturalBloomAndDirtyLens on the Screenshot Manager.");
                }
            }
            CreateDisplayRenderTextures();
        }

        RenderTextureFormat CameraFormat()
        {
            return m_Camera.allowHDR
                ? RenderTextureFormat.ARGBFloat
                : RenderTextureFormat.ARGB32;
        }

        void CreateDisplayRenderTextures()
        {
            RenderTextureFormat format = CameraFormat();
            CreateDisplayRenderTexture(m_CameraInfo, format, "L");
        }

        void CreateDisplayRenderTexture(CameraInfo info, RenderTextureFormat format, string tag)
        {
            int width, height;
            width = m_DisplayWidth;
            height = m_DisplayHeight;
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
            Material material;
            (material = info.renderer.material).SetTexture("_MainTex", info.renderTexture);
            material.name = "SshotMat" + tag;
            info.camera.targetTexture = info.renderTexture;
        }
    }
} // namespace TiltBrush
