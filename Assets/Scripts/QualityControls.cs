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

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace TiltBrush
{

    using BloomMode = AppQualitySettingLevels.BloomMode;

    public class QualityControls : MonoBehaviour
    {
        static public QualityControls m_Instance;
        private const string kAutoSimplificationEnabled = "Autosimplification Enabled";
        private const string kLegacyPostLogPrefix = "[OB_URP_POST]";

        private List<Camera> m_Cameras;

        public event Action<int> OnQualityLevelChange;

        public RdpStrokeSimplifier StrokeSimplifier { get; private set; }
        public RdpStrokeSimplifier UserStrokeSimplifier { get; private set; }

        /// Non-active cameras need to be explicitly registered by the user.
        /// These special case cameras are ignored on mobile hardware.
        [SerializeField] private List<Camera> m_OptInCamerasForPC;

        /// Cameras can explicitly opt out of this manipulation.
        /// These are ignored on mobile hardware.
        [SerializeField] private List<Camera> m_OptOutCamerasForPC;

        [SerializeField] private bool m_enableHdr = true;
        [SerializeField] private int m_msaaLevel = 1;

        [SerializeField] private AppQualitySettingLevels m_QualityLevels;
        [UsedImplicitly] // on Android
        [SerializeField] private AppQualitySettingLevels m_MobileQualityLevels;

        /// Used to track when quality level actually changes.
        private int m_lastQualityLevel = -1;

        private int m_targetMaxControlPoints = 500000;
        private float m_maxLoadingSimplification = 150f;

        private Queue<double> m_FrameTimeStamps;
        private double m_TimeSinceStart;
        private int m_FramesInLastSecond;

        private int m_NumFramesFpsTooLow;
        private int m_NumFramesFpsHighEnough;
        private readonly Dictionary<int, (int msaa, int foveation)> m_RuntimeQualityOverrides =
            new Dictionary<int, (int msaa, int foveation)>();
        private float? m_RuntimeLowerFps;
        private float? m_RuntimeHigherFps;
        private int? m_RuntimeLowerFrames;
        private int? m_RuntimeHigherFrames;
        private int? m_PreparedInitialQualityLevel;
        private bool m_QualityInitialized;
        public bool AutomaticQualityEnabled { get; set; } = true;

        public void ConfigureQualityLevel(int level, int msaa, int foveation)
        {
            if (level < 0 || level >= AppQualityLevels.Length)
                throw new ArgumentOutOfRangeException(nameof(level));
            if (msaa != 1 && msaa != 2 && msaa != 4 && msaa != 8)
                throw new ArgumentOutOfRangeException(nameof(msaa));
            if (foveation < 0 || foveation > 3)
                throw new ArgumentOutOfRangeException(nameof(foveation));
            var fixedSamples = UrpPostProcessingController.Instance?.FixedXrMsaaLevel;
            if (fixedSamples.HasValue && msaa != fixedSamples.Value)
                throw new InvalidOperationException($"XR session MSAA is fixed at {fixedSamples.Value}x; restart to change it.");
            m_RuntimeQualityOverrides[level] = (msaa, foveation);
            if (level == QualityLevel) SetQualityLevel(level);
            Debug.Log($"[OB_QUALITY_TUNING_20260927] level={level} msaa={msaa} foveation={foveation}.");
        }

        public void ConfigureQualityThresholds(float lowerFps, float higherFps,
            int lowerFrames, int higherFrames)
        {
            if (float.IsNaN(lowerFps) || float.IsInfinity(lowerFps) || lowerFps <= 0 ||
                float.IsNaN(higherFps) || float.IsInfinity(higherFps) || higherFps <= lowerFps ||
                lowerFrames < 1 || higherFrames < 1)
                throw new ArgumentException("Use positive FPS thresholds with higher > lower, and frame counts >= 1.");
            m_RuntimeLowerFps = lowerFps;
            m_RuntimeHigherFps = higherFps;
            m_RuntimeLowerFrames = lowerFrames;
            m_RuntimeHigherFrames = higherFrames;
            m_NumFramesFpsTooLow = m_NumFramesFpsHighEnough = 0;
            Debug.Log($"[OB_QUALITY_TUNING_20260927] thresholds lower={lowerFps}/{lowerFrames} higher={higherFps}/{higherFrames}.");
        }

        /// Index into the active platform's quality ladder, from lowest to highest.
        public int QualityLevel
        {
            get { return QualitySettings.GetQualityLevel(); }
            set { SetQualityLevel(value); }
        }

        public static bool AutosimplifyEnabled
        {
            get { return PlayerPrefs.GetInt(kAutoSimplificationEnabled, 1) == 1; }
            set { PlayerPrefs.SetInt(kAutoSimplificationEnabled, value ? 1 : 0); }
        }

        public float SimplificationLevel
        {
            get { return (StrokeSimplifier == null) ? 0.0f : StrokeSimplifier.Level; }
            set
            {
                SetSimplificationLevel(value, AppQualityLevels[QualityLevel].MaxSimplificationUserStrokes);
            }
        }

        private void SetSimplificationLevel(float level, float maxUserStrokeLevel)
        {
            if (App.UserConfig.Profiling.HasStrokeSimplification)
            {
                level = App.UserConfig.Profiling.StrokeSimplification;
                Debug.Log($"Simplification overridden to be: {level}.");
            }
            StrokeSimplifier = new RdpStrokeSimplifier(level);
            UserStrokeSimplifier = new RdpStrokeSimplifier(Mathf.Min(level, maxUserStrokeLevel));
        }

        public int MSAALevel
        {
            get { return UrpPostProcessingController.Instance?.FixedXrMsaaLevel ?? m_msaaLevel; }
        }

        public int FramesInLastSecond => m_FramesInLastSecond;

        public RenderTextureFormat FramebufferFormat
        {
            get
            {
                return m_enableHdr ? RenderTextureFormat.DefaultHDR
                    : RenderTextureFormat.ARGB32;
            }
        }
        public AppQualitySettingLevels AppQualityLevels
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return m_MobileQualityLevels;
#else
                return m_QualityLevels;
#endif
            }
        }

        public AppQualitySettingLevels.AppQualitySettings AppQualitySettings
        {
            get { return AppQualityLevels[QualityLevel]; }
        }

        public int InitialQualityLevel
        {
            get
            {
                if (m_PreparedInitialQualityLevel.HasValue)
                    return m_PreparedInitialQualityLevel.Value;
                int defaultLevel = App.Config.IsMobileHardware ? AppQualityLevels.Length - 1 : 2;
                int configuredLevel = App.UserConfig.Profiling.QualityLevel;
                return configuredLevel >= 0 && configuredLevel < AppQualityLevels.Length
                    ? configuredLevel : defaultLevel;
            }
        }

        public void PrepareInitialQualityLevel(UrpPostProcessingController rendering)
        {
            int requestedLevel = InitialQualityLevel;
            m_PreparedInitialQualityLevel = GetAutomaticQualityLevel(requestedLevel, rendering);
            // VrSdk can prepare XR after Awake has already applied the requested
            // level. Apply its supported fallback too, so quality and eye MSAA agree.
            if (m_QualityInitialized && QualityLevel != m_PreparedInitialQualityLevel.Value)
                SetQualityLevel(m_PreparedInitialQualityLevel.Value);
            Debug.Log($"[OB_QUALITY_TUNING_20260927] startup requestedLevel={requestedLevel} selectedLevel={m_PreparedInitialQualityLevel.Value}.");
        }

        private int GetAutomaticQualityLevel(int desiredLevel,
            UrpPostProcessingController rendering = null)
        {
            rendering = rendering != null ? rendering : UrpPostProcessingController.Instance;
            // Explicit MSAA overrides are test settings and apply across the ladder.
            if (!App.Config.IsMobileHardware || rendering == null || rendering.UsesFixedXrMsaa ||
                App.UserConfig.Profiling.MsaaLevel > 0)
                return desiredLevel;
            while (desiredLevel > 0)
            {
                var settings = AppQualityLevels[desiredLevel];
                int samples = m_RuntimeQualityOverrides.TryGetValue(desiredLevel, out var runtime)
                    ? runtime.msaa : settings.MsaaLevel;
                samples = samples == 0 ? 1 : samples;
                if (rendering.GetSupportedMsaa(samples, settings.Hdr) == samples)
                    break;
                desiredLevel--;
            }
            return desiredLevel;
        }

        void Awake()
        {
            m_Instance = this;

            m_Cameras = new List<Camera>();

            // Apply the quality level.
            QualityLevel = InitialQualityLevel;
            m_QualityInitialized = true;
            SimplificationLevel = 0.0f;
        }

        /// Should be called after correct cameras are enabled
        public void Init()
        {
            var cameras = new HashSet<Camera>(
                FindObjectsOfType<Camera>(), new ReferenceComparer<Camera>());
            if (!App.Config.IsMobileHardware)
            {
                cameras.UnionWith(m_OptInCamerasForPC);
                cameras.ExceptWith(m_OptOutCamerasForPC);
            }
            m_Cameras = cameras.Where(x => x.tag != "Ignore").ToList();

            DisableLegacyPostProcessing();

            m_FrameTimeStamps = new Queue<double>();

            // Push current level to camera settings.
            SetQualityLevel(QualityLevel);
        }

        void Update()
        {
            if (!App.Config.IsMobileHardware)
            {
                return;
            }

            // Count actual frames in the last second
            m_TimeSinceStart += Time.deltaTime;
            m_FrameTimeStamps.Enqueue(m_TimeSinceStart);
            m_FramesInLastSecond++;
            double oneSecondAgo = m_TimeSinceStart - 1;
            while (m_FrameTimeStamps.Peek() <= oneSecondAgo)
            {
                m_FrameTimeStamps.Dequeue();
                m_FramesInLastSecond--;
            }

            if (!AutomaticQualityEnabled)
            {
                m_NumFramesFpsTooLow = m_NumFramesFpsHighEnough = 0;
                return;
            }

            // Update the frame counts. There is no cross-platform GPU load signal,
            // so the scaler runs on framerate alone; see LlmDocs/openxr-perf-migration.md.
            int fps = m_FramesInLastSecond;
            if (fps <= (m_RuntimeLowerFps ?? AppQualityLevels.LowerQualityFpsTrigger))
            {
                m_NumFramesFpsTooLow++;
            }
            else
            {
                m_NumFramesFpsTooLow = 0;
            }

            if (fps >= (m_RuntimeHigherFps ?? AppQualityLevels.HigherQualityFpsTrigger))
            {
                m_NumFramesFpsHighEnough++;
            }
            else
            {
                m_NumFramesFpsHighEnough = 0;
            }

            if (SelectionQualityOverrideActive)
            {
                m_NumFramesFpsTooLow = 0;
                m_NumFramesFpsHighEnough = 0;
                return;
            }

            // Update quality level if needed
            int limit = m_RuntimeLowerFrames ?? AppQualityLevels.FramesForLowerQuality;
            if (m_NumFramesFpsTooLow >= limit)
            {
                if (QualityLevel > 0)
                {
                    QualityLevel = GetAutomaticQualityLevel(QualityLevel - 1);
                }
                m_NumFramesFpsTooLow = 0;
            }

            limit = m_RuntimeHigherFrames ?? AppQualityLevels.FramesForHigherQuality;
            if (m_NumFramesFpsHighEnough >= limit)
            {
                if (QualityLevel < AppQualityLevels.Length - 1)
                {
                    int nextLevel = GetAutomaticQualityLevel(QualityLevel + 1);
                    if (nextLevel > QualityLevel) QualityLevel = nextLevel;
                }
                m_NumFramesFpsHighEnough = 0;
            }

        }

        private static bool SelectionQualityOverrideActive
        {
            get
            {
                if (UrpSelectionRendererFeature.MobileSelectionQualityOverrideActive)
                {
                    return true;
                }

                return SelectionManager.m_Instance != null &&
                    SelectionManager.m_Instance.HasSelection &&
                    !SelectionEffect.DisableSelectionEffects;
            }
        }

        void SetQualityLevel(int value)
        {
            AppQualitySettingLevels settingLevels = AppQualityLevels;
            if (settingLevels != null && (value < 0 || value >= settingLevels.Length))
                throw new ArgumentOutOfRangeException(nameof(value));
            var settings = new AppQualitySettingLevels.AppQualitySettings();
            if (settingLevels == null)
            {
                Debug.LogError("Main -> App -> QualityControl -> QualityLevels object not set.");
            }
            else
            {
                settings = settingLevels[value];
            }

            SetBloomMode(settings.Bloom);
            EnableHDR(settings.Hdr);
            EnableFxaa(settings.Fxaa);
            Shader.globalMaximumLOD = settings.MaxLod;
            m_msaaLevel = settings.MsaaLevel;
            QualitySettings.anisotropicFiltering = settings.Anisotropic;
            SetSimplificationLevel(settings.StrokeSimplification, settings.MaxSimplificationUserStrokes);
            m_targetMaxControlPoints = settings.TargetMaxControlPoints;
            m_maxLoadingSimplification = settings.MaxSimplification;

            float viewportScale = App.UserConfig.Profiling.ViewportScaling > 0 ?
                App.UserConfig.Profiling.ViewportScaling :
                settings.ViewportScale;

            float eyeScale = App.UserConfig.Profiling.EyeTextureScaling > 0 ?
                App.UserConfig.Profiling.EyeTextureScaling :
                settings.EyeTextureScale;

            if (App.UserConfig.Profiling.GlobalMaximumLOD > 0)
            {
                Shader.globalMaximumLOD = App.UserConfig.Profiling.GlobalMaximumLOD;
            }

            if (App.UserConfig.Profiling.MsaaLevel > 0)
            {
                m_msaaLevel = App.UserConfig.Profiling.MsaaLevel;
            }

            int foveation = settings.FixedFoveationLevel;
            if (m_RuntimeQualityOverrides.TryGetValue(value, out var runtimeSettings))
            {
                m_msaaLevel = runtimeSettings.msaa;
                foveation = runtimeSettings.foveation;
            }

            UnityEngine.XR.XRSettings.renderViewportScale = viewportScale;
            UnityEngine.XR.XRSettings.eyeTextureResolutionScale = eyeScale;

            if (value != m_lastQualityLevel && Debug.isDebugBuild && App.UserConfig.Profiling.AutoProfile)
            {
                Debug.Log("Profile: Quality Level: " + value
                    + " renderViewportScale: " + viewportScale
                    + " eyeTexture scale: " + eyeScale
                    + " MSAA: " + m_msaaLevel
                    + " GlobalMaximumLOD: " + Shader.globalMaximumLOD);
                m_lastQualityLevel = value;
            }

            App.VrSdk.SetGpuClockLevel(settings.GpuLevel);
            App.VrSdk.SetFixedFoveation(foveation);

            QualitySettings.SetQualityLevel(value, applyExpensiveChanges: !App.Config.IsMobileHardware);

            if (OnQualityLevelChange != null)
            {
                OnQualityLevelChange(value);
            }
        }

        void SetBloomMode(BloomMode rMode)
        {
            // URP bloom is owned by UrpPostProcessingController during the migration.
        }

        void EnableFxaa(bool bEnable)
        {
            // Legacy FXAA is disabled during the URP migration. URP camera/post settings own AA.
        }

        void DisableLegacyPostProcessing()
        {
            if (UrpPostProcessingController.Instance != null)
            {
                UrpPostProcessingController.Instance.DisableLegacyPostProcessing();
                return;
            }

            int disabled = 0;
            disabled += DisableAll<SENaturalBloomAndDirtyLens>();
            disabled += DisableAll<FXAA>();
            disabled += DisableAll<MobileBloom>();
            disabled += DisableAll<TiltShift>();
            disabled += DisableAll<Kino.Vignette>();
            disabled += DisableAll<PostEffectsToggle>();

            if (disabled > 0)
            {
                Debug.Log($"{kLegacyPostLogPrefix} Disabled {disabled} legacy post-processing components.");
            }
        }

        int DisableAll<T>() where T : MonoBehaviour
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

        void EnableHDR(bool bEnable)
        {
            m_enableHdr = bEnable;
            if (UrpPostProcessingController.Instance != null)
            {
                return;
            }

            foreach (var camera in m_Cameras)
            {
                if (camera.gameObject.activeSelf)
                {
                    camera.allowHDR = bEnable;
                }
            }

            App.VrSdk.GetVrCamera().allowHDR = bEnable;
        }

        public void ResetAutoQuality()
        {
            /* a no-op for now, since we don't do any auto quality scaling */
        }

        /// Counts the number of control points in a sketch and estimates a simplification level in an
        /// attempt to keep the framerate high.
        public void AutoAdjustSimplifierLevel(List<Stroke> strokes, Guid[] brushes)
        {
            if (App.UserConfig.Profiling.HasStrokeSimplification)
            {
                Debug.LogFormat("Simplification overridden to be: {0}.",
                    App.UserConfig.Profiling.StrokeSimplification);
                return;
            }

            Dictionary<Guid, int> controlPointCount = brushes.Distinct().ToDictionary(x => x, x => 0);
            foreach (var stroke in strokes)
            {
                controlPointCount[stroke.m_BrushGuid] += stroke.m_ControlPoints.Length;
            }
            float total = 0;
            foreach (var pair in controlPointCount)
            {
                total += pair.Value * AppQualityLevels.GetWeightForBrush(pair.Key);
            }
            if (total < m_targetMaxControlPoints)
            {
                Debug.LogFormat("Complexity ({0}) is less than {1}. No extra simplification required.",
                    total, m_targetMaxControlPoints);
                return;
            }
            float reduction = m_targetMaxControlPoints / total;
            float level = Mathf.Max(Mathf.Min(RdpStrokeSimplifier.CalculateLevelForReduction(reduction),
                m_maxLoadingSimplification), StrokeSimplifier.Level);
            if (AutosimplifyEnabled)
            {
                Debug.LogFormat(
                    "Complexity ({0}) is greater than {1}. Reduction of {2} using level {3} simplification.",
                    total, m_targetMaxControlPoints, reduction, level);
                SimplificationLevel = level;
            }
        }
    }
} // namespace TiltBrush
