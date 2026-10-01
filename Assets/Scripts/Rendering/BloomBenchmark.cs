// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace TiltBrush
{
    /// Opt-in diagnostics. Does not alter UserConfig or allocate frame samples in normal use.
    public class BloomBenchmark : MonoBehaviour
    {
        private const string kPrefs = "OpenBrush.BloomBenchmark.v1";
        public class Options
        {
            public string Profile = "default";
            public int Msaa = 4;
            public float EyeScale = 1;
            public int Levels = 3, Downsample = 2, NativeIterations = 2;
            public bool NativeQuarter = true, NativeHighQuality;
            public float Amount = 1, Threshold, NativeScatter = 0.35f, NativeIntensity = 1;
            [JsonIgnore] public bool Hdr => Profile.StartsWith("native-hdr") || Profile.StartsWith("off-hdr");
            [JsonIgnore] public int Precision => Profile.EndsWith("64") ? 64 : 32;
            [JsonIgnore] public bool Encoded => Profile.StartsWith("encoded");
            [JsonIgnore] public bool Off => Profile.StartsWith("off") || Profile == "encoded-off";
        }

        private static Options s_Settings;
        private static Options s_Pending;
        public static Options Settings => s_Settings ?? (s_Settings = Load());
        public static bool Enabled => Settings.Profile != "default";
        public static BloomBenchmark Instance { get; private set; }
        // HTTP queries can run on a worker thread. They only read immutable published strings.
        public static volatile string StatusJson = "{\"state\":\"starting\"}";
        public static volatile string ResultJson = "{\"state\":\"no result\"}";
        private static readonly string[] kProfiles = {
            "default", "off-ldr", "encoded-off", "encoded-both", "encoded-alternate", "encoded-reproject",
            "native-ldr", "off-hdr32", "native-hdr32", "off-hdr64", "native-hdr64"
        };
        private string m_State = "idle", m_Token = "", m_Error = "";
        private double m_Start, m_LastPublish;
        private float m_Warmup, m_Duration;
        private string m_Fixture = "scene";
        private readonly List<Sample> m_Samples = new List<Sample>();
        private readonly List<XRDisplaySubsystem> m_Displays = new List<XRDisplaySubsystem>();
        private readonly FrameTiming[] m_Timing = new FrameTiming[1];
        private readonly Dictionary<Camera, int> m_TargetFrames = new Dictionary<Camera, int>();
        private readonly Dictionary<Camera, object> m_Targets = new Dictionary<Camera, object>();
        private GameObject m_FixtureRoot;
        private Material m_FixtureMaterial;
        private Mesh m_FixtureMesh;
        private Camera m_Camera;
        private int m_CullingMask, m_Quality;
        private bool m_Automatic;
        private CameraClearFlags m_ClearFlags;
        private Color m_Background;
        private bool m_QualitySaved;

        [Serializable]
        private class Sample
        {
            public double elapsed;
            public int frame;
            public float wallMs;
            public float? appGpuMs, compositorGpuMs, refreshHz;
            public int? droppedFrames;
            public double? cpuFrameMs, gpuFrameMs;
        }

        private static Options Load()
        {
            try
            {
                var options = JsonConvert.DeserializeObject<Options>(PlayerPrefs.GetString(kPrefs, "{}")) ?? new Options();
                Validate(options);
                return options;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OB_BLOOM_BENCH] Ignoring invalid benchmark preferences: {e.Message}");
                return new Options();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStatics()
        {
            s_Settings = Load();
            s_Pending = JsonConvert.DeserializeObject<Options>(JsonConvert.SerializeObject(s_Settings));
            Instance = null;
            StatusJson = "{\"state\":\"starting\"}";
            ResultJson = "{\"state\":\"no result\"}";
            UrpEncodedBloomRendererFeature.ResetDiagnostics();
            UrpEncodedBloomRendererFeature.AlternateEyes = false;
            UrpEncodedBloomRendererFeature.HistoryRevision++;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            new GameObject("Open Brush bloom benchmark diagnostics").AddComponent<BloomBenchmark>();
        }

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ApplyTuning();
            Publish();
        }

        private static void Validate(Options options)
        {
            if (!kProfiles.Contains(options.Profile)) throw new ArgumentException("Unknown benchmark profile.");
            if (options.Msaa != 1 && options.Msaa != 2 && options.Msaa != 4 && options.Msaa != 8)
                throw new ArgumentException("MSAA must be 1, 2, 4 or 8.");
            Check(options.EyeScale, 0.25f, 2, "eye scale");
            Check(options.Amount, 0, 1, "amount");
            Check(options.Threshold, 0, 100, "threshold");
            Check(options.NativeIntensity, 0, 100, "native intensity");
            Check(options.NativeScatter, 0, 1, "scatter");
            if (options.Levels < 1 || options.Levels > 5 || options.Downsample < 2 || options.Downsample > 4 ||
                options.NativeIterations < 1 || options.NativeIterations > 10)
                throw new ArgumentException("Invalid pyramid settings.");
        }

        private static void Check(float value, float min, float max, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
                throw new ArgumentException($"Invalid {name}; expected {min}..{max}.");
        }

        private void ApplyTuning()
        {
            var options = Settings;
            UrpEncodedBloomRendererFeature.RuntimeLevels = options.Levels;
            UrpEncodedBloomRendererFeature.RuntimeDownsample = options.Downsample;
            UrpEncodedBloomRendererFeature.AlternateEyes = Enabled && (options.Profile == "encoded-alternate" || options.Profile == "encoded-reproject");
            UrpEncodedBloomRendererFeature.ReprojectHistory = options.Profile == "encoded-reproject";
            UrpEncodedBloomRendererFeature.HistoryRevision++;
            if (Enabled && UrpPostProcessingController.Instance != null)
            {
                UrpPostProcessingController.Instance.ApplyBenchmarkSettings();
                if (options.Off) UrpPostProcessingController.Instance.SetBloomAmount(0);
            }
        }

        // Startup settings take effect only after restart. Active session settings stay immutable.
        public static void Configure(string profile, int msaa, float eyeScale, string token)
        {
            try
            {
                var next = new Options { Profile = profile, Msaa = msaa, EyeScale = eyeScale };
                if (profile == "native-ldr") next.Threshold = 0.5f;
                else if (next.Hdr) { next.Threshold = 1.05f; next.NativeIntensity = 0.1f; }
                Validate(next);
                if (Instance != null && (Instance.m_State == "sampling" || Instance.m_State == "warmup"))
                    throw new InvalidOperationException("Stop the capture before configuring a restart.");
                s_Pending = next;
                if (profile == "default") PlayerPrefs.DeleteKey(kPrefs);
                else PlayerPrefs.SetString(kPrefs, JsonConvert.SerializeObject(next));
                PlayerPrefs.Save();
                Instance?.Acknowledge(token);
            }
            catch (Exception e) { Instance?.Fail(token, e.Message); }
        }

        public void Tune(int levels, int downsample, float amount, float threshold, int iterations,
            bool quarter, bool highQuality, float scatter, float intensity, string token)
        {
            try
            {
                if (!Enabled) throw new InvalidOperationException("Configure a benchmark profile and restart first.");
                if (m_State == "sampling" || m_State == "warmup") throw new InvalidOperationException("Capture is running.");
                var next = JsonConvert.DeserializeObject<Options>(JsonConvert.SerializeObject(Settings));
                next.Levels = levels; next.Downsample = downsample; next.Amount = amount; next.Threshold = threshold;
                next.NativeIterations = iterations; next.NativeQuarter = quarter; next.NativeHighQuality = highQuality;
                next.NativeScatter = scatter; next.NativeIntensity = intensity;
                Validate(next);
                s_Settings = next;
                ApplyTuning();
                Acknowledge(token);
            }
            catch (Exception e) { Fail(token, e.Message); }
        }

        private void Acknowledge(string token) { m_Token = token; m_Error = ""; Publish(); }
        private void Fail(string token, string error)
        {
            m_Token = token; m_Error = error;
            Debug.LogError($"[OB_BLOOM_BENCH] {error}");
            Publish();
        }

        public void StartCapture(string token, float warmup, float duration, string fixture, int quality)
        {
            try
            {
                if (!Enabled) throw new InvalidOperationException("Configure a benchmark profile and restart first.");
                if (m_State == "warmup" || m_State == "sampling") throw new InvalidOperationException("Capture is running.");
                Check(warmup, 0, 300, "warmup seconds");
                Check(duration, 1, 300, "sample seconds");
                if (!new[] { "scene", "sparse", "dense", "white" }.Contains(fixture))
                    throw new ArgumentException("Fixture must be scene, sparse, dense or white.");
                StopFixture();
                var controls = QualityControls.m_Instance;
                if (controls == null || quality < 0 || quality >= controls.AppQualityLevels.Length)
                    throw new ArgumentException("Invalid quality level.");
                m_Quality = controls.QualityLevel; m_Automatic = controls.AutomaticQualityEnabled;
                m_QualitySaved = true;
                controls.AutomaticQualityEnabled = false;
                controls.QualityLevel = quality;
                // Quality presets apply bloom too. Reapply the explicitly requested benchmark values.
                ApplyTuning();
                m_Fixture = fixture;
                if (fixture != "scene") CreateFixture(fixture);
                m_Samples.Clear();
                m_Samples.Capacity = Math.Max(m_Samples.Capacity, (int)Math.Ceiling(duration * 144));
                m_Token = token; m_Error = "";
                m_Warmup = warmup; m_Duration = duration;
                m_Start = Time.realtimeSinceStartupAsDouble; m_State = "warmup";
                ResultJson = "{\"state\":\"pending\"}";
                SubsystemManager.GetInstances(m_Displays);
                Publish();
                Debug.Log($"[OB_BLOOM_BENCH] Begin {token}: {Settings.Profile}, {fixture}, warmup={warmup}s sample={duration}s");
            }
            catch (Exception e) { StopFixture(); m_State = "error"; Fail(token, e.Message); }
        }

        public void StopCapture(string token)
        {
            m_State = "idle"; StopFixture(); Acknowledge(token);
        }

        public bool WantsTarget(Camera camera)
        {
            if (m_TargetFrames.TryGetValue(camera, out int frame) && Time.frameCount - frame < 60) return false;
            m_TargetFrames[camera] = Time.frameCount;
            return true;
        }

        public void RecordTarget(Camera camera, TextureDescInfo info)
        {
            m_Targets[camera] = new {
                camera = camera.name, format = info.format, info.width, info.height, info.slices, info.msaa
            };
        }

        public struct TextureDescInfo { public string format; public int width, height, slices, msaa; }

        private void LateUpdate()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (m_State == "warmup" && now - m_Start >= m_Warmup)
            {
                m_Start = now; m_State = "sampling";
                if (FrameTimingManager.IsFeatureEnabled()) FrameTimingManager.CaptureFrameTimings();
            }
            if (m_State == "sampling")
            {
                var sample = new Sample { elapsed = now - m_Start, frame = Time.frameCount, wallMs = Time.unscaledDeltaTime * 1000 };
                foreach (var display in m_Displays)
                {
                    if (!display.running) continue;
                    if (display.TryGetAppGPUTimeLastFrame(out float gpu) && gpu > 0) sample.appGpuMs = gpu * 1000;
                    if (display.TryGetCompositorGPUTimeLastFrame(out float comp) && comp > 0) sample.compositorGpuMs = comp * 1000;
                    if (display.TryGetDisplayRefreshRate(out float hz)) sample.refreshHz = hz;
                    if (display.TryGetDroppedFrameCount(out int dropped)) sample.droppedFrames = dropped;
                    break;
                }
                if (FrameTimingManager.IsFeatureEnabled())
                {
                    if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
                    {
                        if (m_Timing[0].cpuFrameTime > 0) sample.cpuFrameMs = m_Timing[0].cpuFrameTime;
                        if (m_Timing[0].gpuFrameTime > 0) sample.gpuFrameMs = m_Timing[0].gpuFrameTime;
                    }
                    FrameTimingManager.CaptureFrameTimings();
                }
                m_Samples.Add(sample);
                if (sample.elapsed >= m_Duration) Finish();
            }
            if (now - m_LastPublish >= 1) { m_LastPublish = now; Publish(); }
        }

        private object Snapshot()
        {
            var controller = UrpPostProcessingController.Instance;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Bloom native = null;
            controller?.MainProfile?.TryGet(out native);
            return new {
                schema = 1, state = m_State, token = m_Token, error = m_Error,
                active = Settings, pending = s_Pending, restartRequired = s_Pending != null &&
                    (s_Pending.Profile != Settings.Profile || s_Pending.Msaa != Settings.Msaa || s_Pending.EyeScale != Settings.EyeScale),
                profiles = kProfiles, samples = m_Samples.Count, fixture = m_Fixture,
                sessionHdr = controller?.SessionHdr, sessionMsaa = controller?.SessionMsaaLevel,
                encoded = controller?.UsesEncodedBloom,
                encodingKeyword = Shader.IsKeywordEnabled("HDR_SIMPLE"),
                nativeBloom = native == null ? null : new { native.active, amount = native.intensity.value,
                    threshold = native.threshold.value, iterations = native.maxIterations.value,
                    downscale = native.downscale.value.ToString(), highQuality = native.highQualityFiltering.value },
                reuse = UrpEncodedBloomRendererFeature.GetReuse(App.Instance != null && App.VrSdk != null ? App.VrSdk.GetVrCamera() : null),
                encodedPasses = controller != null && controller.UsesEncodedBloom && controller.EncodedBloomAmount > 0
                    ? UrpEncodedBloomRendererFeature.LastPassCount : 0,
                targets = m_Targets.Values.ToArray(), eyeScale = XRSettings.eyeTextureResolutionScale,
                viewportScale = XRSettings.renderViewportScale, pipelineScale = pipeline?.renderScale,
                hdrPrecision = pipeline?.hdrColorBufferPrecision.ToString(), quality = QualityControls.m_Instance?.QualityLevel,
                qualityLevels = QualityControls.m_Instance?.AppQualityLevels.Length,
                automaticQuality = QualityControls.m_Instance?.AutomaticQualityEnabled,
                actualEncodedAmount = controller?.EncodedBloomAmount, actualEncodedThreshold = controller?.EncodedBloomThreshold,
                maximumShaderLod = Shader.globalMaximumLOD, frameTimingEnabled = FrameTimingManager.IsFeatureEnabled(),
                device = SystemInfo.deviceModel, gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), colorSpace = QualitySettings.activeColorSpace.ToString(),
                version = Application.version, unity = Application.unityVersion, debugBuild = Debug.isDebugBuild,
                warmup = m_Warmup, duration = m_Duration
            };
        }

        private void Publish() { StatusJson = JsonConvert.SerializeObject(Snapshot()); }

        private void Finish()
        {
            m_State = "complete";
            var result = new { metadata = Snapshot(), samples = m_Samples,
                appGpu = Summarize(m_Samples.Where(x => x.appGpuMs.HasValue).Select(x => (double)x.appGpuMs.Value)),
                wallFrame = Summarize(m_Samples.Select(x => (double)x.wallMs)),
                gpuSampleCoverage = m_Samples.Count == 0 ? 0 : m_Samples.Count(x => x.appGpuMs.HasValue) / (double)m_Samples.Count };
            ResultJson = JsonConvert.SerializeObject(result);
            // Fixed directory; tokens never become paths.
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "BloomBenchmarks");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, $"bloom-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json"), ResultJson);
            }
            catch (Exception e) { Debug.LogWarning($"[OB_BLOOM_BENCH] Could not save report: {e.Message}"); }
            Publish();
            Debug.Log($"[OB_BLOOM_BENCH] Complete {m_Token}: {m_Samples.Count} samples; GPU samples={m_Samples.Count(x => x.appGpuMs.HasValue)}");
            // Leave the fixture visible for capture. Stop restores scene and quality.
        }

        private static object Summarize(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(x => x).ToArray();
            if (sorted.Length == 0) return null;
            return new { count = sorted.Length, mean = sorted.Average(),
                median = sorted[(sorted.Length - 1) / 2], p95 = sorted[(int)Math.Ceiling((sorted.Length - 1) * 0.95)] };
        }

        private void CreateFixture(string fixture)
        {
            m_Camera = App.VrSdk.GetVrCamera();
            if (m_Camera == null || !m_Camera.isActiveAndEnabled) throw new InvalidOperationException("VR camera is not active.");
            if (FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(x => x.gameObject.layer == 31))
                throw new InvalidOperationException("Fixture layer 31 is occupied; use the scene fixture.");
            var descriptor = Resources.Load<BrushDescriptor>(fixture == "white" ? "Brushes/Basic/Flat/Flat" : "Brushes/Basic/Light/Light");
            if (descriptor == null || descriptor.NormalMaterial == null) throw new InvalidOperationException("Light material is unavailable.");
            m_FixtureMaterial = new Material(descriptor.NormalMaterial);
            m_FixtureMaterial.mainTexture = Texture2D.whiteTexture;
            if (m_FixtureMaterial.HasProperty("_EmissionGain")) m_FixtureMaterial.SetFloat("_EmissionGain", 0.45f);
            m_FixtureMesh = new Mesh { name = "Bloom benchmark quads" };
            int count = fixture == "dense" ? 144 : 9;
            int columns = fixture == "dense" ? 12 : 3;
            var vertices = new Vector3[count * 4];
            var colors = new Color[count * 4];
            var uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            for (int i = 0; i < count; i++)
            {
                float x = ((i % columns + 0.5f) / columns - 0.5f) * 1.5f;
                float y = ((i / columns + 0.5f) / columns - 0.5f) * 1.5f;
                float size = fixture == "dense" ? 0.025f : 0.08f;
                Color color = fixture == "white" ? Color.white : Color.HSVToRGB((i % 7) / 7f, 0.65f, 1);
                int v = i * 4, t = i * 6;
                vertices[v] = new Vector3(x-size, y-size, 2); vertices[v+1] = new Vector3(x-size, y+size, 2);
                vertices[v+2] = new Vector3(x+size, y+size, 2); vertices[v+3] = new Vector3(x+size, y-size, 2);
                uv[v] = new Vector2(0,0); uv[v+1] = new Vector2(0,1); uv[v+2] = new Vector2(1,1); uv[v+3] = new Vector2(1,0);
                for (int j = 0; j < 4; j++) colors[v+j] = color;
                triangles[t] = v; triangles[t+1] = v+1; triangles[t+2] = v+2;
                triangles[t+3] = v; triangles[t+4] = v+2; triangles[t+5] = v+3;
            }
            m_FixtureMesh.vertices = vertices; m_FixtureMesh.colors = colors;
            m_FixtureMesh.uv = uv; m_FixtureMesh.triangles = triangles; m_FixtureMesh.RecalculateBounds();
            m_FixtureRoot = new GameObject("Bloom benchmark fixture") { layer = 31 };
            m_FixtureRoot.transform.SetParent(m_Camera.transform, false);
            m_FixtureRoot.AddComponent<MeshFilter>().sharedMesh = m_FixtureMesh;
            m_FixtureRoot.AddComponent<MeshRenderer>().sharedMaterial = m_FixtureMaterial;
            m_CullingMask = m_Camera.cullingMask; m_ClearFlags = m_Camera.clearFlags; m_Background = m_Camera.backgroundColor;
            m_Camera.cullingMask = 1 << 31; m_Camera.clearFlags = CameraClearFlags.SolidColor; m_Camera.backgroundColor = Color.black;
        }

        private void StopFixture()
        {
            if (m_FixtureRoot != null && m_Camera != null)
            { m_Camera.cullingMask = m_CullingMask; m_Camera.clearFlags = m_ClearFlags; m_Camera.backgroundColor = m_Background; }
            Destroy(m_FixtureRoot); Destroy(m_FixtureMesh); Destroy(m_FixtureMaterial);
            m_FixtureRoot = null; m_FixtureMesh = null; m_FixtureMaterial = null; m_Camera = null;
            if (m_QualitySaved && QualityControls.m_Instance != null)
            {
                QualityControls.m_Instance.QualityLevel = m_Quality;
                QualityControls.m_Instance.AutomaticQualityEnabled = m_Automatic;
                ApplyTuning();
            }
            m_QualitySaved = false;
        }

        private void OnDestroy() { StopFixture(); if (Instance == this) Instance = null; }
    }
}
