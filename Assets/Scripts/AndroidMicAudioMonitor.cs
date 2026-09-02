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
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace TiltBrush
{
    public class AndroidMicAudioMonitor : MonoBehaviour
    {
        private const int kBufferSeconds = 1;
        private const int kPreferredSampleRate = 48000;

        private AudioClip m_MicClip;
        private float[] m_Samples;
        private string m_DeviceName = "";
        private int m_SampleRate = kPreferredSampleRate;
        private int m_ClipSampleCount;
        private bool m_CaptureRequested;
        private bool m_WaitingForPermission;
        private float m_LastPeak;

        public int SampleRate { get { return m_SampleRate; } }
        public bool IsCapturing { get { return m_MicClip != null && Microphone.IsRecording(m_DeviceName); } }
        public float LastPeak { get { return m_LastPeak; } }

        public void Activate(bool active)
        {
            m_CaptureRequested = active;
            if (active)
            {
                StartCapture();
            }
            else
            {
                StopCapture();
            }
        }

        void Update()
        {
            if (!m_CaptureRequested)
            {
                return;
            }

#if UNITY_ANDROID
            if (m_WaitingForPermission)
            {
                if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
                {
                    return;
                }
                m_WaitingForPermission = false;
                StartCapture();
            }
#endif

            if (!IsCapturing)
            {
                return;
            }

            EnsureSampleBuffer();

            int micPosition = Microphone.GetPosition(m_DeviceName);
            if (micPosition < m_Samples.Length)
            {
                return;
            }

            int readPosition = micPosition - m_Samples.Length;
            if (readPosition < 0)
            {
                readPosition += m_ClipSampleCount;
            }

            if (!m_MicClip.GetData(m_Samples, readPosition))
            {
                return;
            }

#if UNITY_ANDROID
            UpdateAndroidMicPeak();
#endif
            VisualizerManager.m_Instance.ProcessAudio(m_Samples, m_SampleRate);
        }

        private void StartCapture()
        {
            if (IsCapturing)
            {
                return;
            }

            EnsureSampleBuffer();

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                m_WaitingForPermission = true;
                Permission.RequestUserPermission(Permission.Microphone);
                return;
            }
#endif

            SelectDeviceAndSampleRate();
            m_MicClip = Microphone.Start(m_DeviceName, true, kBufferSeconds, m_SampleRate);
            m_ClipSampleCount = m_MicClip != null ? m_MicClip.samples : 0;
        }

        private void StopCapture()
        {
            if (Microphone.IsRecording(m_DeviceName))
            {
                Microphone.End(m_DeviceName);
            }
            m_MicClip = null;
            m_WaitingForPermission = false;
        }

        private void EnsureSampleBuffer()
        {
            if (m_Samples == null || m_Samples.Length != VisualizerManager.m_Instance.FFTSize)
            {
                m_Samples = new float[VisualizerManager.m_Instance.FFTSize];
            }
        }

        private void SelectDeviceAndSampleRate()
        {
            m_DeviceName = Microphone.devices.Length > 0 ? Microphone.devices[0] : "";
            Microphone.GetDeviceCaps(m_DeviceName, out int minFrequency, out int maxFrequency);

            if (maxFrequency <= 0)
            {
                m_SampleRate = kPreferredSampleRate;
            }
            else
            {
                int min = minFrequency > 0 ? minFrequency : 1;
                m_SampleRate = Mathf.Clamp(kPreferredSampleRate, min, maxFrequency);
            }
        }

#if UNITY_ANDROID
        private void UpdateAndroidMicPeak()
        {
            float peak = 0.0f;
            for (int i = 0; i < m_Samples.Length; ++i)
            {
                float sample = m_Samples[i];
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            m_LastPeak = peak;
        }
#endif
    }
}
