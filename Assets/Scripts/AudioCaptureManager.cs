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
        // Number of seconds to delay before searching for active audio device.
        // From experimentation this seems to be the minimum to ensure we don't pick up
        // any residual audio.
        const float DEVICE_SEARCH_DELAY = 1.0f;

        private enum AudioCaptureType
        {
            File,
            System,
            App,
            Script
        }

        static public AudioCaptureManager m_Instance;

        [SerializeField] private SystemAudioMonitor m_SystemAudio;
        [SerializeField] private GameObject m_FileAudio;
        [SerializeField] private GameObject m_AppAudio;

        private AudioCaptureType m_Type;
        private int m_CaptureRequestedCount;

        void Awake()
        {
            ResetAudioCaptureType();
        }

        private void ResetAudioCaptureType()
        {
            m_Instance = this;
            if (LuaManager.Instance != null) LuaManager.Instance.VisualizerScriptingEnabled = false;
#if UNITY_ANDROID || UNITY_IOS
            m_Type = AudioCaptureType.App;
#else
            m_Type = AudioCaptureType.System;
#endif
            m_CaptureRequestedCount = 0;

        }

        public bool CaptureRequested
        {
            get { return m_CaptureRequestedCount > 0; }
        }

        public void EnableScripting()
        {
            m_FileAudio.SetActive(false);
            m_AppAudio.SetActive(false);
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
                    case AudioCaptureType.App:
                        return AudioSettings.outputSampleRate;
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
                    case AudioCaptureType.App:
                        return m_AppAudio.activeSelf;
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
                case AudioCaptureType.File: return "Listening to Mic'";
                case AudioCaptureType.System: return m_SystemAudio.GetCaptureStatusMessage();
                case AudioCaptureType.App: return "Jammin'";
                case AudioCaptureType.Script: return "Scripted Waveform";
            }
            return "";
        }

        public void CaptureAudio(bool bCapture)
        {
            bool bWasRequested = CaptureRequested;
            m_CaptureRequestedCount += bCapture ? 1 : -1;
            Debug.Assert(m_CaptureRequestedCount >= 0);

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
                case AudioCaptureType.App:
                    m_AppAudio.SetActive(bCapture);
                    break;
                case AudioCaptureType.Script:
                    break;
            }
        }
    }
}
