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

using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{

    // This is the stack of audio objects that we create, and notes on how to clean them up.
    //
    // Class                Interface       Notes                                   Needs disposal?
    // --------------------------------------------------------------------------------------------
    // MMDevice                             Needs dispose                           MUST DISPOSE
    //
    // WasapiCapture        ISoundIn        Does _not_ dispose this.Device          MUST DISPOSE
    //  m_AudioCapture                      this.Device (sometimes a default device)
    //                                      Provides DataAvailable
    //
    // SoundInSource        IWaveSource     Removes self from base.DataAvailable    MUST DISPOSE
    //                                      Does _not_ dispose this.SoundIn
    //                                      this.SoundIn (cannot get at if disposed)
    //                                      Consumes base.DataAvailable, provides DataAvailable
    //
    // WaveToSampleBase     ISampleSource   Just disposes wrapped
    //                                      Cannot get at wrapped
    //
    // SingleBlockNotify    ISampleSource   Disposes wrapped if DisposeBaseSource=true
    //  m_FinalSouce                        this.BaseSource
    //

    public class AudioCaptureManager : MonoBehaviour
    {
#if UNITY_ANDROID
        private const string kAndroidAudioDebugPrefix = "AR_AUDIO_DBG_20260605";
        private const float kAndroidDebugLogInterval = 1.0f;
#endif
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
        private const string kAndroidAppAudioLogPrefix = "AR_ANDROID_APP_AUDIO_20260603";
        private const string kAndroidMicAudioLogPrefix = "AR_ANDROID_MIC_AUDIO_20260604";
        private const string kAndroidPlaybackAudioLogPrefix = "AR_ANDROID_PLAYBACK_AUDIO_20260604";
#endif
        private const float kAndroidSourceProbeSeconds = 6.0f;
        private const float kAndroidSignalThreshold = 0.0001f;

        // Number of seconds to delay before searching for active audio device.
        // From experimentation this seems to be the minimum to ensure we don't pick up
        // any residual audio.
        const float DEVICE_SEARCH_DELAY = 1.0f;

        private enum AudioCaptureType
        {
            File,
            System,
            SystemPlayback,
            App,
            Mic,
            Script
        }

        static public AudioCaptureManager m_Instance;

        [SerializeField] private SystemAudioMonitor m_SystemAudio;
        [SerializeField] private GameObject m_FileAudio;
        [SerializeField] private GameObject m_AppAudio;

        private AudioCaptureType m_Type;
        private AndroidPlaybackAudioMonitor m_PlaybackAudio;
        private AndroidMicAudioMonitor m_MicAudio;
        private int m_CaptureRequestedCount;
        private float m_AndroidSourceProbeStartTime;
#if UNITY_ANDROID
        private float m_NextAndroidDebugLogTime;
#endif

        void Awake()
        {
            m_Instance = this;
            EnsurePlaybackAudioMonitor();
            EnsureMicAudioMonitor();
            ResetAudioCaptureType();
        }

        private void ResetAudioCaptureType()
        {
            m_Instance = this;
            if (LuaManager.Instance != null) LuaManager.Instance.VisualizerScriptingEnabled = false;
#if UNITY_ANDROID
            // Probe Android external sources in order: other-app/system playback, app audio, mic.
            m_Type = AudioCaptureType.SystemPlayback;
#elif UNITY_IOS
            m_Type = AudioCaptureType.App;
#else
            m_Type = AudioCaptureType.System;
#endif
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
            Debug.Log($"{kAndroidMicAudioLogPrefix} ResetAudioCaptureType selected {m_Type}");
#endif
#if UNITY_ANDROID
            Debug.Log($"{kAndroidAudioDebugPrefix} AudioCaptureManager ResetAudioCaptureType selected={m_Type}");
#endif
            m_CaptureRequestedCount = 0;

        }

        private void EnsurePlaybackAudioMonitor()
        {
            if (m_PlaybackAudio != null)
            {
                return;
            }

            var playbackAudio = new GameObject("AndroidPlaybackAudio");
            playbackAudio.transform.SetParent(transform, false);
            playbackAudio.SetActive(true);
            m_PlaybackAudio = playbackAudio.AddComponent<AndroidPlaybackAudioMonitor>();
        }

        private void EnsureMicAudioMonitor()
        {
            if (m_MicAudio != null)
            {
                return;
            }

            var micAudio = new GameObject("AndroidMicAudio");
            micAudio.transform.SetParent(transform, false);
            micAudio.SetActive(true);
            m_MicAudio = micAudio.AddComponent<AndroidMicAudioMonitor>();
        }

        public bool CaptureRequested
        {
            get { return m_CaptureRequestedCount > 0; }
        }

        public void EnableScripting()
        {
            m_FileAudio.SetActive(false);
            m_AppAudio.SetActive(false);
            m_PlaybackAudio.Activate(false);
            m_MicAudio.Activate(false);
            m_SystemAudio.gameObject.SetActive(false);
            m_Type = AudioCaptureType.Script;
            LuaManager.Instance.VisualizerScriptingEnabled = true;
            App.Instance.AudioReactiveBrushesActive(true);
        }

        public void DisableScripting()
        {
            App.Instance.AudioReactiveBrushesActive(false);
            ResetAudioCaptureType();
        }

        public void EnableAudioFileSource(bool enable, string path)
        {
            if (enable)
            {
                LuaManager.Instance.VisualizerScriptingEnabled = false;
                m_AppAudio.SetActive(false);
                m_PlaybackAudio.Activate(false);
                m_MicAudio.Activate(false);
                m_SystemAudio.Deactivate();
                m_SystemAudio.gameObject.SetActive(false);
                m_Type = AudioCaptureType.File;
                StartCoroutine(LoadAudio(path));
            }
            else
            {
                ResetAudioCaptureType();
            }
        }

        IEnumerator LoadAudio(string path)
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var audioSource = m_FileAudio.GetComponent<AudioSource>();
                    AudioClip audioClip = DownloadHandlerAudioClip.GetContent(www);
                    audioSource.clip = audioClip;
                    audioSource.Play();
                }
                else
                {
                    Debug.LogError("Failed to load audio: " + www.error);
                }
            }
        }

        public int SampleRate
        {
            get
            {
                switch (m_Type)
                {
                    case AudioCaptureType.File:
                        // TODO: Define this.
                        return 0;
                    case AudioCaptureType.System:
                        return m_SystemAudio.GetAudioDeviceSampleRate();
                    case AudioCaptureType.SystemPlayback:
                        return m_PlaybackAudio.SampleRate;
                    case AudioCaptureType.App:
                        return AudioSettings.outputSampleRate;
                    case AudioCaptureType.Mic:
                        return m_MicAudio.SampleRate;
                    case AudioCaptureType.Script:
                        return LuaManager.Instance.ScriptedWaveformSampleRate;
                }
                return 0;
            }
        }

        public bool IsCapturingAudio
        {
            get
            {
                switch (m_Type)
                {
                    case AudioCaptureType.File:
                        return m_FileAudio.activeSelf;
                    case AudioCaptureType.System:
                        return m_SystemAudio.gameObject.activeSelf && m_SystemAudio.AudioDeviceSelected();
                    case AudioCaptureType.SystemPlayback:
                        return m_PlaybackAudio.IsCapturing;
                    case AudioCaptureType.App:
                        return m_AppAudio.activeSelf;
                    case AudioCaptureType.Mic:
                        return m_MicAudio.IsCapturing;
                    case AudioCaptureType.Script:
                        return LuaManager.Instance.VisualizerScriptingEnabled;
                }
                return false;
            }
        }

        public string GetCaptureStatusMessage()
        {
            switch (m_Type)
            {
                case AudioCaptureType.File: return "Listening to audio file";
                case AudioCaptureType.System: return m_SystemAudio.GetCaptureStatusMessage();
                case AudioCaptureType.SystemPlayback: return "Listening to system audio";
                case AudioCaptureType.App: return "Listening to app audio";
                case AudioCaptureType.Mic: return "Listening to microphone";
                case AudioCaptureType.Script: return "Scripted Waveform";
            }
            return "";
        }

        public void CaptureAudio(bool bCapture)
        {
            bool bWasRequested = CaptureRequested;
            m_CaptureRequestedCount += bCapture ? 1 : -1;
            Debug.Assert(m_CaptureRequestedCount >= 0);
#if UNITY_ANDROID
            Debug.Log($"{kAndroidAudioDebugPrefix} AudioCaptureManager CaptureAudio input={bCapture} type={m_Type} wasRequested={bWasRequested} nowRequested={CaptureRequested} requests={m_CaptureRequestedCount} playbackCapturing={m_PlaybackAudio.IsCapturing} playbackPending={m_PlaybackAudio.IsRequestPending} playbackPeak={m_PlaybackAudio.LastPeak:F5} playbackSamples={m_PlaybackAudio.SamplesWritten} playbackRead={m_PlaybackAudio.LastReadResult} playbackError='{m_PlaybackAudio.LastError}'");
#endif

            switch (m_Type)
            {
                case AudioCaptureType.File:
                    // TODO: Handle case where bCapture=true is called while capture is active.
                    m_FileAudio.SetActive(bCapture);
                    m_FileAudio.GetComponent<VisualizerScript>().Activate(bCapture);
                    break;
                case AudioCaptureType.System:
                    // Protect against spamming requests.
                    if (!bWasRequested && CaptureRequested)
                    {
                        AudioManager.m_Instance.StopAudio();
                        AudioManager.Enabled = false;
                        PointerManager.m_Instance.ResetPointerAudio();
                        m_SystemAudio.gameObject.SetActive(true);
                        m_SystemAudio.Activate(DEVICE_SEARCH_DELAY);
                    }
                    else if (bWasRequested && !CaptureRequested)
                    {
                        AudioManager.Enabled = true;
                        PointerManager.m_Instance.ResetPointerAudio();
                        m_SystemAudio.Deactivate();
                        m_SystemAudio.gameObject.SetActive(false);
                    }
                    break;
                case AudioCaptureType.SystemPlayback:
                    m_PlaybackAudio.Activate(CaptureRequested);
                    VisualizerManager.m_Instance.AudioCaptureStatusChange(CaptureRequested);
                    ResetAndroidSourceProbeTimer();
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
                    Debug.Log($"{kAndroidPlaybackAudioLogPrefix} CaptureAudio({bCapture}) set PlaybackAudio active={m_PlaybackAudio.IsCapturing} requests={m_CaptureRequestedCount}");
#endif
                    break;
                case AudioCaptureType.App:
                    bool appAudioWasActive = m_AppAudio.activeSelf;
                    m_AppAudio.SetActive(CaptureRequested);
                    if (appAudioWasActive != m_AppAudio.activeSelf)
                    {
                        VisualizerManager.m_Instance.AudioCaptureStatusChange(m_AppAudio.activeSelf);
                    }
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
                    Debug.Log($"{kAndroidAppAudioLogPrefix} CaptureAudio({bCapture}) set AppAudio active={m_AppAudio.activeSelf} requests={m_CaptureRequestedCount}");
#endif
                    break;
                case AudioCaptureType.Mic:
                    m_MicAudio.Activate(CaptureRequested);
                    VisualizerManager.m_Instance.AudioCaptureStatusChange(CaptureRequested);
#if UNITY_ANDROID && (UNITY_EDITOR || DEVELOPMENT_BUILD)
                    Debug.Log($"{kAndroidMicAudioLogPrefix} CaptureAudio({bCapture}) set MicAudio active={m_MicAudio.IsCapturing} requests={m_CaptureRequestedCount}");
#endif
                    break;
                case AudioCaptureType.Script:
                    break;
            }
        }

#if UNITY_ANDROID
        void Update()
        {
            if (!CaptureRequested)
            {
                return;
            }

            if (m_Type == AudioCaptureType.SystemPlayback)
            {
                LogAndroidProbeState("SystemPlayback");
                if (m_PlaybackAudio.IsRequestPending)
                {
                    ResetAndroidSourceProbeTimer();
                    return;
                }
                if (m_PlaybackAudio.LastPeak > kAndroidSignalThreshold)
                {
                    return;
                }
                if (Time.unscaledTime - m_AndroidSourceProbeStartTime > kAndroidSourceProbeSeconds)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"{kAndroidPlaybackAudioLogPrefix} no playback signal; falling back to app audio");
#endif
                    Debug.Log($"{kAndroidAudioDebugPrefix} AudioCaptureManager fallback SystemPlayback->App elapsed={Time.unscaledTime - m_AndroidSourceProbeStartTime:F2} playbackCapturing={m_PlaybackAudio.IsCapturing} playbackPending={m_PlaybackAudio.IsRequestPending} playbackPeak={m_PlaybackAudio.LastPeak:F5} playbackSamples={m_PlaybackAudio.SamplesWritten} playbackRead={m_PlaybackAudio.LastReadResult} playbackError='{m_PlaybackAudio.LastError}'");
                    SwitchAndroidCaptureSource(AudioCaptureType.App);
                }
            }
            else if (m_Type == AudioCaptureType.App)
            {
                var appMonitor = m_AppAudio.GetComponent<AppAudioMonitor>();
                float appPeak = appMonitor != null ? appMonitor.LastPeak : -1.0f;
                LogAndroidProbeState($"App appPeak={appPeak:F5} appMonitorNull={appMonitor == null}");
                if (appMonitor != null && appMonitor.LastPeak > kAndroidSignalThreshold)
                {
                    return;
                }
                if (Time.unscaledTime - m_AndroidSourceProbeStartTime > kAndroidSourceProbeSeconds)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"{kAndroidAppAudioLogPrefix} no app-audio signal; falling back to microphone");
#endif
                    Debug.Log($"{kAndroidAudioDebugPrefix} AudioCaptureManager fallback App->Mic elapsed={Time.unscaledTime - m_AndroidSourceProbeStartTime:F2} appPeak={appPeak:F5} appMonitorNull={appMonitor == null}");
                    SwitchAndroidCaptureSource(AudioCaptureType.Mic);
                }
            }
        }

        private void SwitchAndroidCaptureSource(AudioCaptureType nextType)
        {
            AudioCaptureType previousType = m_Type;
            m_PlaybackAudio.Activate(false);
            m_AppAudio.SetActive(false);
            m_MicAudio.Activate(false);

            m_Type = nextType;
            ResetAndroidSourceProbeTimer();

            if (m_Type == AudioCaptureType.App)
            {
                m_AppAudio.SetActive(true);
                VisualizerManager.m_Instance.AudioCaptureStatusChange(true);
            }
            else if (m_Type == AudioCaptureType.Mic)
            {
                m_MicAudio.Activate(true);
                VisualizerManager.m_Instance.AudioCaptureStatusChange(true);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"{kAndroidPlaybackAudioLogPrefix} switched Android capture source to {m_Type}");
#endif
            Debug.Log($"{kAndroidAudioDebugPrefix} AudioCaptureManager switched Android capture source {previousType}->{m_Type} playbackCapturing={m_PlaybackAudio.IsCapturing} playbackPending={m_PlaybackAudio.IsRequestPending} micCapturing={m_MicAudio.IsCapturing}");
        }

        private void ResetAndroidSourceProbeTimer()
        {
            m_AndroidSourceProbeStartTime = Time.unscaledTime;
        }

        private void LogAndroidProbeState(string reason)
        {
            if (Time.unscaledTime < m_NextAndroidDebugLogTime)
            {
                return;
            }

            Debug.Log($"{kAndroidAudioDebugPrefix} AudioCaptureManager probe reason={reason} type={m_Type} elapsed={Time.unscaledTime - m_AndroidSourceProbeStartTime:F2} requests={m_CaptureRequestedCount} playbackCapturing={m_PlaybackAudio.IsCapturing} playbackPending={m_PlaybackAudio.IsRequestPending} playbackPeak={m_PlaybackAudio.LastPeak:F5} playbackSamples={m_PlaybackAudio.SamplesWritten} playbackRead={m_PlaybackAudio.LastReadResult} playbackError='{m_PlaybackAudio.LastError}' micCapturing={m_MicAudio.IsCapturing} micPeak={m_MicAudio.LastPeak:F5}");
            m_NextAndroidDebugLogTime = Time.unscaledTime + kAndroidDebugLogInterval;
        }
#else
        private void ResetAndroidSourceProbeTimer() { }
#endif
    }
}
