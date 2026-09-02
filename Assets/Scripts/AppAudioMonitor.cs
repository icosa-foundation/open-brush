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
        private float[] m_WaveformFloats;
        private float m_LastPeak;

        public float LastPeak { get { return m_LastPeak; } }

        void Start()
        {
            m_WaveformFloats = new float[VisualizerManager.m_Instance.FFTSize];
        }

        void Update()
        {
            AudioListener.GetOutputData(m_WaveformFloats, 0);
#if UNITY_ANDROID
            UpdateAndroidAudioPeak();
#endif
            VisualizerManager.m_Instance.ProcessAudio(m_WaveformFloats, AudioSettings.outputSampleRate);
        }

#if UNITY_ANDROID
        private void UpdateAndroidAudioPeak()
        {
            float peak = 0.0f;
            for (int i = 0; i < m_WaveformFloats.Length; ++i)
            {
                float sample = m_WaveformFloats[i];
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            m_LastPeak = peak;
        }
#endif
    }

}
