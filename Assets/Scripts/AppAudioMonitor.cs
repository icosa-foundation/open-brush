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

    public class AppAudioMonitor : MonoBehaviour
    {
        private const string kAndroidAppAudioLogPrefix = "AR_ANDROID_APP_AUDIO_20260603";
        private const float kAndroidLogInterval = 2.0f;

        [SerializeField] private bool m_DebugPlayTestTone;
        [SerializeField] private float m_DebugTestToneFrequency = 440.0f;
        [SerializeField] private float m_DebugTestToneVolume = 0.05f;

        private float[] m_WaveformFloats;
        private float m_NextAndroidLogTime;
        private int m_NonZeroLogCount;

        void Start()
        {
            m_WaveformFloats = new float[VisualizerManager.m_Instance.FFTSize];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (m_DebugPlayTestTone)
            {
                StartDebugTestTone();
            }
#endif
        }

#if UNITY_ANDROID
        void OnEnable()
        {
            Debug.Log($"{kAndroidAppAudioLogPrefix} AppAudioMonitor enabled sampleRate={AudioSettings.outputSampleRate}");
        }

        void OnDisable()
        {
            Debug.Log($"{kAndroidAppAudioLogPrefix} AppAudioMonitor disabled");
        }
#endif

        void Update()
        {
            AudioListener.GetOutputData(m_WaveformFloats, 0);
#if UNITY_ANDROID
            LogAndroidAudioSamples();
#endif
            VisualizerManager.m_Instance.ProcessAudio(m_WaveformFloats, AudioSettings.outputSampleRate);
        }

#if UNITY_ANDROID
        private void LogAndroidAudioSamples()
        {
            float peak = 0.0f;
            float sumSquares = 0.0f;
            for (int i = 0; i < m_WaveformFloats.Length; ++i)
            {
                float sample = m_WaveformFloats[i];
                peak = Mathf.Max(peak, Mathf.Abs(sample));
                sumSquares += sample * sample;
            }

            float rms = Mathf.Sqrt(sumSquares / m_WaveformFloats.Length);
            bool shouldLog = m_NonZeroLogCount < 3 && peak > 0.0001f;
            if (Time.unscaledTime >= m_NextAndroidLogTime || shouldLog)
            {
                bool audioReactiveEnabled = Shader.IsKeywordEnabled("AUDIO_REACTIVE");
                Debug.Log($"{kAndroidAppAudioLogPrefix} AppAudioMonitor samples peak={peak:F5} rms={rms:F5} sampleRate={AudioSettings.outputSampleRate} AUDIO_REACTIVE={audioReactiveEnabled}");
                m_NextAndroidLogTime = Time.unscaledTime + kAndroidLogInterval;
                if (peak > 0.0001f)
                {
                    ++m_NonZeroLogCount;
                }
            }
        }
#endif

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void StartDebugTestTone()
        {
            int sampleRate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int sampleCount = sampleRate;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; ++i)
            {
                samples[i] = Mathf.Sin(2.0f * Mathf.PI * m_DebugTestToneFrequency * i / sampleRate);
            }

            AudioClip clip = AudioClip.Create("AppAudioMonitorDebugTone", sampleCount, 1,
                sampleRate, false);
            clip.SetData(samples, 0);
            AudioSource debugTestToneSource = gameObject.AddComponent<AudioSource>();
            debugTestToneSource.clip = clip;
            debugTestToneSource.loop = true;
            debugTestToneSource.volume = m_DebugTestToneVolume;
            debugTestToneSource.spatialBlend = 0.0f;
            debugTestToneSource.Play();
#if UNITY_ANDROID
            Debug.Log($"{kAndroidAppAudioLogPrefix} Debug test tone enabled frequency={m_DebugTestToneFrequency:F1} volume={m_DebugTestToneVolume:F3} sampleRate={sampleRate}");
#endif
        }
#endif
    }

}
