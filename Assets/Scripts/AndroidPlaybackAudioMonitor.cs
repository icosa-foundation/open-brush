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

using UnityEngine;

namespace TiltBrush
{
    public class AndroidPlaybackAudioMonitor : MonoBehaviour
    {
#if UNITY_ANDROID
        private const string kAndroidAudioDebugPrefix = "AR_AUDIO_DBG_20260605";
        private const float kAndroidDebugLogInterval = 2.0f;
        private string m_LastAndroidEvent = "";
#endif
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        private const string kAndroidPlaybackAudioLogPrefix = "AR_ANDROID_PLAYBACK_AUDIO_20260604";
        private const float kAndroidLogInterval = 2.0f;
#endif

#if UNITY_ANDROID
        private AndroidJavaClass m_Plugin;
#endif
        private bool m_CaptureRequested;
        private bool m_RequestSent;
        private float[] m_Samples;
#if UNITY_ANDROID
        private float m_NextAndroidDebugLogTime;
#endif
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        private float m_NextAndroidLogTime;
        private int m_NonZeroLogCount;
#endif
        private float m_LastPeak;

        public float LastPeak { get { return m_LastPeak; } }
        public int SampleRate
        {
            get
            {
#if UNITY_ANDROID
                return PluginReady ? m_Plugin.CallStatic<int>("getSampleRate") : 48000;
#else
                return 48000;
#endif
            }
        }

        public bool IsCapturing
        {
            get
            {
#if UNITY_ANDROID
                return PluginReady && m_Plugin.CallStatic<bool>("isCapturing");
#else
                return false;
#endif
            }
        }

#if UNITY_ANDROID
        public string LastError
        {
            get { return PluginReady ? m_Plugin.CallStatic<string>("getLastError") : "plugin not ready"; }
        }

        public long SamplesWritten
        {
            get { return PluginReady ? m_Plugin.CallStatic<long>("getSamplesWritten") : 0; }
        }

        public int LastReadResult
        {
            get { return PluginReady ? m_Plugin.CallStatic<int>("getLastReadResult") : 0; }
        }

        public bool RequestSent
        {
            get { return m_RequestSent; }
        }

        public string LastAndroidEvent
        {
            get { return m_LastAndroidEvent; }
        }
#else
        public string LastError { get { return ""; } }
        public long SamplesWritten { get { return 0; } }
        public int LastReadResult { get { return 0; } }
        public bool RequestSent { get { return false; } }
        public string LastAndroidEvent { get { return ""; } }
#endif

        public bool IsRequestPending
        {
            get
            {
#if UNITY_ANDROID
                return PluginReady && m_Plugin.CallStatic<bool>("isRequestPending");
#else
                return false;
#endif
            }
        }

#if UNITY_ANDROID
        private bool PluginReady
        {
            get { return m_Plugin != null; }
        }
#endif

        public void Activate(bool active)
        {
#if UNITY_ANDROID
            Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor Activate active={active} wasRequested={m_CaptureRequested} pluginReady={PluginReady} isCapturing={IsCapturing} requestPending={IsRequestPending} requestSent={m_RequestSent} lastError='{LastError}'");
#endif
            m_CaptureRequested = active;
            if (active)
            {
                RequestCapture();
            }
            else
            {
                StopCapture();
            }
        }

        void Update()
        {
            if (!m_CaptureRequested || !IsCapturing)
            {
                return;
            }

            EnsureSampleBuffer();
#if UNITY_ANDROID
            LogAndroidDebugState("Update", force: false);
            float[] latest = m_Plugin.CallStatic<float[]>("readLatest", m_Samples.Length);
            if (latest == null || latest.Length != m_Samples.Length)
            {
                Debug.LogWarning($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor readLatest invalid latestNull={latest == null} latestLength={(latest == null ? -1 : latest.Length)} expected={m_Samples.Length} lastRead={LastReadResult} samplesWritten={SamplesWritten} lastError='{LastError}'");
                return;
            }
            latest.CopyTo(m_Samples, 0);
            LogAndroidPlaybackSamples();
            VisualizerManager.m_Instance.ProcessAudio(m_Samples, SampleRate);
#endif
        }

        private void RequestCapture()
        {
#if UNITY_ANDROID
            EnsurePlugin();
            if (!PluginReady || IsCapturing || m_RequestSent)
            {
                Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor RequestCapture skipped pluginReady={PluginReady} isCapturing={IsCapturing} requestPending={IsRequestPending} requestSent={m_RequestSent} lastError='{LastError}'");
                return;
            }

            if (!m_Plugin.CallStatic<bool>("isSupported"))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"{kAndroidPlaybackAudioLogPrefix} AudioPlaybackCapture is not supported on this Android version");
#endif
                Debug.LogWarning($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor AudioPlaybackCapture unsupported lastError='{LastError}'");
                return;
            }

            m_RequestSent = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            m_NextAndroidLogTime = 0.0f;
            m_NonZeroLogCount = 0;
#endif
            m_Plugin.CallStatic("requestCapture");
            Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor requested MediaProjection playback capture requestPending={IsRequestPending} requestSent={m_RequestSent} lastError='{LastError}'");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"{kAndroidPlaybackAudioLogPrefix} requested MediaProjection playback capture");
#endif
#endif
        }

        private void StopCapture()
        {
#if UNITY_ANDROID
            if (PluginReady)
            {
                m_Plugin.CallStatic("stop");
                Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor stop requested samplesWritten={SamplesWritten} lastRead={LastReadResult} lastError='{LastError}'");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"{kAndroidPlaybackAudioLogPrefix} playback capture stopped");
#endif
            }
#endif
            m_RequestSent = false;
            m_LastPeak = 0.0f;
        }

#if UNITY_ANDROID
        public void OnAndroidPlaybackCaptureEvent(string message)
        {
            m_LastAndroidEvent = message;
            Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor UnityEvent message='{message}' requested={m_CaptureRequested} pluginReady={PluginReady} isCapturing={IsCapturing} requestPending={IsRequestPending} requestSent={m_RequestSent} samplesWritten={SamplesWritten} lastRead={LastReadResult} lastError='{LastError}'");
        }
#endif

        private void EnsureSampleBuffer()
        {
            if (m_Samples == null || m_Samples.Length != VisualizerManager.m_Instance.FFTSize)
            {
                m_Samples = new float[VisualizerManager.m_Instance.FFTSize];
            }
        }

#if UNITY_ANDROID
        private void EnsurePlugin()
        {
            if (m_Plugin != null)
            {
                return;
            }

            m_Plugin = new AndroidJavaClass("org.openbrush.audio.OpenBrushAudioPlaybackCapture");
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                m_Plugin.CallStatic("initialize", activity);
                Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor plugin initialized activityNull={activity == null} supported={m_Plugin.CallStatic<bool>("isSupported")} lastError='{LastError}'");
            }
        }

        private void LogAndroidDebugState(string reason, bool force)
        {
            if (!force && Time.unscaledTime < m_NextAndroidDebugLogTime)
            {
                return;
            }

            Debug.Log($"{kAndroidAudioDebugPrefix} AndroidPlaybackAudioMonitor {reason} requested={m_CaptureRequested} pluginReady={PluginReady} isCapturing={IsCapturing} requestPending={IsRequestPending} requestSent={m_RequestSent} lastPeak={m_LastPeak:F5} samplesWritten={SamplesWritten} lastRead={LastReadResult} sampleRate={SampleRate} lastEvent='{m_LastAndroidEvent}' lastError='{LastError}'");
            m_NextAndroidDebugLogTime = Time.unscaledTime + kAndroidDebugLogInterval;
        }

        private void LogAndroidPlaybackSamples()
        {
            float peak = 0.0f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float sumSquares = 0.0f;
#endif
            for (int i = 0; i < m_Samples.Length; ++i)
            {
                float sample = m_Samples[i];
                peak = Mathf.Max(peak, Mathf.Abs(sample));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                sumSquares += sample * sample;
#endif
            }

            m_LastPeak = peak;
            LogAndroidDebugState("Samples", force: false);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float rms = Mathf.Sqrt(sumSquares / m_Samples.Length);
            bool shouldLog = m_NonZeroLogCount < 3 && peak > 0.0001f;
            if (Time.unscaledTime >= m_NextAndroidLogTime || shouldLog)
            {
                bool audioReactiveEnabled = Shader.IsKeywordEnabled("AUDIO_REACTIVE");
                string error = m_Plugin.CallStatic<string>("getLastError");
                Debug.Log($"{kAndroidPlaybackAudioLogPrefix} playback samples peak={peak:F5} rms={rms:F5} sampleRate={SampleRate} AUDIO_REACTIVE={audioReactiveEnabled} error='{error}'");
                m_NextAndroidLogTime = Time.unscaledTime + kAndroidLogInterval;
                if (peak > 0.0001f)
                {
                    ++m_NonZeroLogCount;
                }
            }
#endif
        }
#endif
    }
}
