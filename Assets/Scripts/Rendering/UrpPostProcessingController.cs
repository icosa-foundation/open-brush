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

        public VolumeProfile MainProfile => m_RuntimeMainProfile;
        public VolumeProfile CaptureProfile => m_RuntimeCaptureProfile;

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

            if (QualityControls.m_Instance != null)
            {
                QualityControls.m_Instance.OnQualityLevelChange += ApplyQuality;
                ApplyQuality(QualityControls.m_Instance.QualityLevel);
            }
            else
            {
                ApplyBaselineBloom(true);
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

                cameraData.volumeLayerMask = m_VolumeLayerMask;
                cameraData.volumeTrigger = camera.transform;

                bool isCapture = IsCaptureCamera(camera);
                cameraData.renderPostProcessing = !isCapture && m_EnablePostProcessingOnMainCameras;

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
            ConfigureCaptureCamera(camera, enableCaptureEffects, m_RuntimeCaptureProfile);
        }

        public void ConfigureDropCamCamera(Camera camera, bool enableCaptureEffects)
        {
            ConfigureCaptureCamera(camera, enableCaptureEffects, m_RuntimeCaptureProfile);
        }

        public void SetRecordingPostProcessing(Camera camera, bool enabled)
        {
            ConfigureCaptureCamera(camera, enabled, m_RuntimeCaptureProfile);
        }

        private void ConfigureCaptureCamera(Camera camera, bool enablePostProcessing, VolumeProfile profile)
        {
            if (camera == null)
            {
                return;
            }

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
                ApplyBaselineBloom(true);
                return;
            }

            AppQualitySettingLevels.AppQualitySettings settings =
                QualityControls.m_Instance.AppQualityLevels[qualityLevel];
            bool enableBloom = settings.Bloom != AppQualitySettingLevels.BloomMode.None && settings.Hdr;

            ApplyBaselineBloom(enableBloom);
            Debug.Log($"{kLogPrefix} Applied quality={qualityLevel} bloom={settings.Bloom} hdr={settings.Hdr}.");
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
            m_Bloom.intensity.value = 0.2f;
            m_Bloom.scatter.overrideState = true;
            m_Bloom.scatter.value = 0.55f;

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
            Bloom captureBloom;
            if (!m_RuntimeCaptureProfile.TryGet(out captureBloom))
            {
                captureBloom = m_RuntimeCaptureProfile.Add<Bloom>(true);
            }
            captureBloom.active = false;

            Vignette captureVignette;
            if (!m_RuntimeCaptureProfile.TryGet(out captureVignette))
            {
                captureVignette = m_RuntimeCaptureProfile.Add<Vignette>(true);
            }
            captureVignette.active = false;
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

        private void ApplyBaselineBloom(bool enabled)
        {
            EnsureProfilesIfNeeded();
            m_Bloom.active = enabled;
            if (!enabled)
            {
                m_Bloom.intensity.value = 0f;
            }
            else if (m_Bloom.intensity.value <= 0f)
            {
                m_Bloom.intensity.value = 0.2f;
            }
        }

        private void DisableLegacyPostProcessing()
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

        private static bool IsCaptureCamera(Camera camera)
        {
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
