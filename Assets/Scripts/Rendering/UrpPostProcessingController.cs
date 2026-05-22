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

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    /// <summary>
    /// Establishes the URP post-processing baseline while the legacy Built-in post effects
    /// are migrated. Later phases should expand this into the single owner for URP quality
    /// and capture post-processing state.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class UrpPostProcessingController : MonoBehaviour
    {
        private const string kLogPrefix = "[OB_URP_POST]";
        private const string kRuntimeVolumeName = "OpenBrush URP Runtime Global Volume";
        private const float kDisabledBloomIntensity = 0f;
        private const float kFastBloomIntensity = 0.14f;
        private const float kFullBloomIntensity = 0.2f;
        private const float kMobileBloomIntensity = 0.1f;
        private const float kFastBloomScatter = 0.45f;
        private const float kFullBloomScatter = 0.55f;
        private const float kMobileBloomScatter = 0.35f;

        public static UrpPostProcessingController Instance { get; private set; }

        [SerializeField] private VolumeProfile m_MainProfile;
        [SerializeField] private VolumeProfile m_CaptureProfile;
        [SerializeField] private bool m_CreateRuntimeGlobalVolume = true;
        [SerializeField] private bool m_EnablePostProcessingOnMainCameras = true;
        [SerializeField] private LayerMask m_VolumeLayerMask = ~0;

        private Volume m_RuntimeVolume;
        private VolumeProfile m_RuntimeMainProfile;
        private VolumeProfile m_RuntimeCaptureProfile;
        private Bloom m_Bloom;
        private Vignette m_Vignette;
        private Bloom m_CaptureBloom;
        private Vignette m_CaptureVignette;
        private bool m_CurrentHdr = true;
        private bool m_CurrentFxaa;
        private AppQualitySettingLevels.BloomMode m_CurrentBloomMode =
            AppQualitySettingLevels.BloomMode.None;
        private float m_MobileBloomAmount = 1f;
        private readonly HashSet<Camera> m_ExplicitCaptureCameras = new HashSet<Camera>();

        public VolumeProfile MainProfile => m_RuntimeMainProfile;
        public VolumeProfile CaptureProfile => m_RuntimeCaptureProfile;

        public struct CameraPostProcessingState
        {
            public Camera camera;
            public UniversalAdditionalCameraData cameraData;
            public bool renderPostProcessing;
            public LayerMask volumeLayerMask;
            public Transform volumeTrigger;
            public bool allowHDR;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"{kLogPrefix} Multiple URP post-processing controllers found; disabling {name}.");
                enabled = false;
                return;
            }

            Instance = this;
            DisableLegacyPostProcessing();
        }

        private void Start()
        {
            EnsureProfiles();
            EnsureGlobalVolume();
            RefreshCameras();
            DisableCompositionLayerEditorEmulationIfUnused();
            StartCoroutine(DisableCompositionLayerEditorEmulationAfterStartup());

            if (QualityControls.m_Instance != null)
            {
                QualityControls.m_Instance.OnQualityLevelChange += ApplyQuality;
                ApplyQuality(QualityControls.m_Instance.QualityLevel);
            }
            else
            {
                m_CurrentBloomMode = AppQualitySettingLevels.BloomMode.Full;
                ApplyBloomMode(m_CurrentBloomMode, hdrEnabled: true);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (QualityControls.m_Instance != null)
            {
                QualityControls.m_Instance.OnQualityLevelChange -= ApplyQuality;
            }
        }

        public void RefreshCameras()
        {
            DisableLegacyPostProcessing();

            Camera[] cameras = FindObjectsOfType<Camera>(includeInactive: true);
            int mainCount = 0;
            int captureCount = 0;

            foreach (Camera camera in cameras)
            {
                if (camera == null || camera.CompareTag("Ignore"))
                {
                    continue;
                }

                UniversalAdditionalCameraData cameraData =
                    camera.GetComponent<UniversalAdditionalCameraData>();
                if (cameraData == null)
                {
                    cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    Debug.Log($"{kLogPrefix} Added UniversalAdditionalCameraData to camera {camera.name}.");
                }

                bool isCapture = IsCaptureCamera(camera);
                ApplyCameraBaseline(camera, cameraData, isCapture);

                if (isCapture)
                {
                    captureCount++;
                }
                else
                {
                    mainCount++;
                }
            }

            Debug.Log($"{kLogPrefix} Camera baseline refreshed. main={mainCount} capture/offscreen={captureCount}.");
        }

        public void ConfigureScreenshotCamera(Camera camera, bool enableCaptureEffects)
        {
            RegisterCaptureCamera(camera);
            ConfigureCaptureCamera(camera, enableCaptureEffects, m_RuntimeCaptureProfile);
        }

        public void ConfigureDropCamCamera(Camera camera, bool enableCaptureEffects)
        {
            RegisterCaptureCamera(camera);
            ConfigureCaptureCamera(camera, enableCaptureEffects, m_RuntimeCaptureProfile);
        }

        public void SetRecordingPostProcessing(Camera camera, bool enabled)
        {
            RegisterCaptureCamera(camera);
            ConfigureCaptureCamera(camera, enabled, m_RuntimeCaptureProfile);
        }

        public void SetCapturePostEffects(bool enabled)
        {
            CameraConfig.PostEffects = enabled;
            Debug.Log($"{kLogPrefix} Capture post-processing default set to {enabled}.");
        }

        public void SetMobileBloomAmount(float amount)
        {
            EnsureProfilesIfNeeded();
            m_MobileBloomAmount = Mathf.Clamp01(amount);
            if (m_Bloom == null)
            {
                return;
            }

            if (m_CurrentBloomMode == AppQualitySettingLevels.BloomMode.Mobile && m_CurrentHdr)
            {
                ApplyBloomMode(m_CurrentBloomMode, m_CurrentHdr);
            }
        }

        public CameraPostProcessingState BeginCapturePostProcessing(
            Camera camera, bool enablePostProcessing)
        {
            CameraPostProcessingState state = new CameraPostProcessingState
            {
                camera = camera,
                allowHDR = camera != null && camera.allowHDR
            };

            if (camera == null || !enablePostProcessing)
            {
                RegisterCaptureCamera(camera);
                return state;
            }

            RegisterCaptureCamera(camera);
            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            state.cameraData = cameraData;
            state.renderPostProcessing = cameraData.renderPostProcessing;
            state.volumeLayerMask = cameraData.volumeLayerMask;
            state.volumeTrigger = cameraData.volumeTrigger;

            camera.allowHDR = true;
            ConfigureCaptureCamera(camera, enablePostProcessing, m_RuntimeCaptureProfile);
            Debug.Log(
                $"{kLogPrefix} Capture override camera={camera.name} " +
                $"post={enablePostProcessing} hdr={camera.allowHDR}.");
            return state;
        }

        public void EndCapturePostProcessing(CameraPostProcessingState state)
        {
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


        private void ConfigureCaptureCamera(Camera camera, bool enablePostProcessing, VolumeProfile profile)
        {
            if (camera == null)
            {
                return;
            }

            RegisterCaptureCamera(camera);
            UniversalAdditionalCameraData cameraData =
                camera.GetComponent<UniversalAdditionalCameraData>();
            if (cameraData == null)
            {
                cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            cameraData.renderPostProcessing = enablePostProcessing;
            cameraData.volumeLayerMask = m_VolumeLayerMask;
            cameraData.volumeTrigger = camera.transform;

            if (profile == null)
            {
                Debug.Log($"{kLogPrefix} Capture camera {camera.name} has no capture profile yet.");
            }
        }

        private void ApplyQuality(int qualityLevel)
        {
            if (QualityControls.m_Instance == null)
            {
                m_CurrentBloomMode = AppQualitySettingLevels.BloomMode.Full;
                ApplyBloomMode(m_CurrentBloomMode, hdrEnabled: true);
                return;
            }

            AppQualitySettingLevels.AppQualitySettings settings =
                QualityControls.m_Instance.AppQualityLevels[qualityLevel];
            m_CurrentHdr = settings.Hdr;
            m_CurrentFxaa = settings.Fxaa;
            m_CurrentBloomMode = settings.Bloom;

            ApplyBloomMode(settings.Bloom, settings.Hdr);
            RefreshCameras();
            Debug.Log(
                $"{kLogPrefix} Applied quality={qualityLevel} bloom={settings.Bloom} " +
                $"bloomActive={m_Bloom.active} intensity={m_Bloom.intensity.value} " +
                $"scatter={m_Bloom.scatter.value} hq={m_Bloom.highQualityFiltering.value} " +
                $"downscale={m_Bloom.downscale.value} maxIterations={m_Bloom.maxIterations.value} " +
                $"hdr={settings.Hdr} fxaa={settings.Fxaa} msaa={settings.MsaaLevel}.");
        }

        private void ApplyCameraBaseline(
            Camera camera,
            UniversalAdditionalCameraData cameraData,
            bool isCapture)
        {
            cameraData.volumeLayerMask = m_VolumeLayerMask;
            cameraData.volumeTrigger = camera.transform;
            cameraData.renderPostProcessing = !isCapture && m_EnablePostProcessingOnMainCameras;

            if (!isCapture)
            {
                camera.allowHDR = m_CurrentHdr;
                cameraData.antialiasing = m_CurrentFxaa
                    ? AntialiasingMode.FastApproximateAntialiasing
                    : AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }
            else
            {
                cameraData.antialiasing = AntialiasingMode.None;
            }
        }

        private void EnsureProfiles()
        {
            m_RuntimeMainProfile = m_MainProfile != null
                ? Instantiate(m_MainProfile)
                : ScriptableObject.CreateInstance<VolumeProfile>();
            m_RuntimeMainProfile.name = "OpenBrush URP Runtime Main Profile";

            m_RuntimeCaptureProfile = m_CaptureProfile != null
                ? Instantiate(m_CaptureProfile)
                : ScriptableObject.CreateInstance<VolumeProfile>();
            m_RuntimeCaptureProfile.name = "OpenBrush URP Runtime Capture Profile";

            EnsureMainProfileComponents();
            EnsureCaptureProfileComponents();
        }

        private void EnsureMainProfileComponents()
        {
            if (!m_RuntimeMainProfile.TryGet(out m_Bloom))
            {
                m_Bloom = m_RuntimeMainProfile.Add<Bloom>(true);
            }
            m_Bloom.active = true;
            m_Bloom.threshold.overrideState = true;
            m_Bloom.threshold.value = 1.05f;
            m_Bloom.intensity.overrideState = true;
            m_Bloom.intensity.value = kFullBloomIntensity;
            m_Bloom.scatter.overrideState = true;
            m_Bloom.scatter.value = 0.55f;
            m_Bloom.highQualityFiltering.overrideState = true;
            m_Bloom.highQualityFiltering.value = true;
            m_Bloom.downscale.overrideState = true;
            m_Bloom.downscale.value = BloomDownscaleMode.Half;
            m_Bloom.maxIterations.overrideState = true;
            m_Bloom.maxIterations.value = 6;

            if (!m_RuntimeMainProfile.TryGet(out m_Vignette))
            {
                m_Vignette = m_RuntimeMainProfile.Add<Vignette>(true);
            }
            m_Vignette.active = false;
            m_Vignette.intensity.overrideState = true;
            m_Vignette.intensity.value = 0f;
        }

        private void EnsureCaptureProfileComponents()
        {
            if (!m_RuntimeCaptureProfile.TryGet(out m_CaptureBloom))
            {
                m_CaptureBloom = m_RuntimeCaptureProfile.Add<Bloom>(true);
            }
            m_CaptureBloom.active = false;

            if (!m_RuntimeCaptureProfile.TryGet(out m_CaptureVignette))
            {
                m_CaptureVignette = m_RuntimeCaptureProfile.Add<Vignette>(true);
            }
            m_CaptureVignette.active = false;
        }

        private void EnsureGlobalVolume()
        {
            if (!m_CreateRuntimeGlobalVolume || m_RuntimeVolume != null)
            {
                return;
            }

            GameObject volumeObject = new GameObject(kRuntimeVolumeName);
            volumeObject.transform.SetParent(transform, worldPositionStays: false);
            m_RuntimeVolume = volumeObject.AddComponent<Volume>();
            m_RuntimeVolume.isGlobal = true;
            m_RuntimeVolume.priority = 0f;
            m_RuntimeVolume.weight = 1f;
            m_RuntimeVolume.sharedProfile = m_RuntimeMainProfile;

            Debug.Log($"{kLogPrefix} Runtime global Volume created with baseline profile.");
        }

        private void ApplyBloomMode(AppQualitySettingLevels.BloomMode bloomMode, bool hdrEnabled)
        {
            EnsureProfilesIfNeeded();

            bool enabled = bloomMode != AppQualitySettingLevels.BloomMode.None && hdrEnabled;
            m_Bloom.active = enabled;

            if (!enabled)
            {
                m_Bloom.intensity.value = kDisabledBloomIntensity;
                return;
            }

            switch (bloomMode)
            {
                case AppQualitySettingLevels.BloomMode.Fast:
                    m_Bloom.intensity.value = kFastBloomIntensity;
                    m_Bloom.scatter.value = kFastBloomScatter;
                    m_Bloom.highQualityFiltering.value = false;
                    m_Bloom.downscale.value = BloomDownscaleMode.Quarter;
                    m_Bloom.maxIterations.value = 3;
                    break;

                case AppQualitySettingLevels.BloomMode.Mobile:
                    m_Bloom.intensity.value = kMobileBloomIntensity * m_MobileBloomAmount;
                    m_Bloom.scatter.value = kMobileBloomScatter;
                    m_Bloom.highQualityFiltering.value = false;
                    m_Bloom.downscale.value = BloomDownscaleMode.Quarter;
                    m_Bloom.maxIterations.value = 2;
                    break;

                case AppQualitySettingLevels.BloomMode.Full:
                default:
                    m_Bloom.intensity.value = kFullBloomIntensity;
                    m_Bloom.scatter.value = kFullBloomScatter;
                    m_Bloom.highQualityFiltering.value = true;
                    m_Bloom.downscale.value = BloomDownscaleMode.Half;
                    m_Bloom.maxIterations.value = 6;
                    break;
            }
        }

        public void DisableLegacyPostProcessing()
        {
            int disabled = 0;
            disabled += DisableAll<SENaturalBloomAndDirtyLens>();
            disabled += DisableAll<FXAA>();
            disabled += DisableAll<MobileBloom>();
            disabled += DisableAll<TiltShift>();
            disabled += DisableAll<Kino.Vignette>();
            disabled += DisableAll<PostEffectsToggle>();

            if (disabled > 0)
            {
                Debug.Log($"{kLogPrefix} Disabled {disabled} legacy post-processing components.");
            }
        }

        private void DisableCompositionLayerEditorEmulationIfUnused()
        {
#if UNITY_EDITOR
            InvokeCompositionLayerEmulationMethod(
                "Unity.XR.CompositionLayers.Emulation.EmulationColorScaleBiasPass",
                "UnregisterScriptableRendererPass");
            InvokeCompositionLayerEmulationMethod(
                "Unity.XR.CompositionLayers.Emulation.EmulationLayerUniversalScriptableRendererPass",
                "UnregisterScriptableRendererPass");
#endif
        }

        private System.Collections.IEnumerator DisableCompositionLayerEditorEmulationAfterStartup()
        {
#if UNITY_EDITOR
            yield return null;
            yield return null;
            DisableCompositionLayerEditorEmulationIfUnused();
#else
            yield break;
#endif
        }

#if UNITY_EDITOR
        private static void InvokeCompositionLayerEmulationMethod(string typeName, string methodName)
        {
            Type type = Type.GetType($"{typeName}, Unity.XR.CompositionLayers");
            MethodInfo method = type?.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                Debug.LogWarning($"{kLogPrefix} Could not find Composition Layers emulation method {typeName}.{methodName}.");
                return;
            }

            method.Invoke(null, null);
            Debug.Log($"{kLogPrefix} Disabled Composition Layers editor emulation method {typeName}.{methodName}.");
        }
#endif

        private static int DisableAll<T>() where T : MonoBehaviour
        {
            int disabled = 0;
            foreach (T component in FindObjectsOfType<T>(includeInactive: true))
            {
                if (component.enabled)
                {
                    component.enabled = false;
                    disabled++;
                }
            }
            return disabled;
        }

        private void EnsureProfilesIfNeeded()
        {
            if (m_RuntimeMainProfile == null)
            {
                EnsureProfiles();
            }
        }

        private void RegisterCaptureCamera(Camera camera)
        {
            if (camera != null)
            {
                m_ExplicitCaptureCameras.Add(camera);
            }
        }

        private bool IsCaptureCamera(Camera camera)
        {
            if (m_ExplicitCaptureCameras.Contains(camera))
            {
                return true;
            }

            string cameraName = camera.name.ToLowerInvariant();
            if (camera.targetTexture != null)
            {
                return true;
            }

            if (camera.GetComponent<VideoRecorder>() != null)
            {
                return true;
            }

            return cameraName.Contains("screenshot")
                || cameraName.Contains("drop")
                || cameraName.Contains("video")
                || cameraName.Contains("gif")
                || cameraName.Contains("saveicon")
                || cameraName.Contains("capture");
        }
    }
}
