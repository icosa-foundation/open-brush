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
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace TiltBrush
{
    [System.Serializable]
    public class SoundClip
    {
        /// The controller is used as a handle for controlling the clip - clips stay instantiated as
        /// long as there are controllers in existence that reference them. For this reason it is
        /// important to Dispose() of controllers once they are no longer needed.
        ///
        /// Properties of sound clips that can change (playing state, volume, scrub position) are all accessed
        /// through the Controller - properties of clips that are unchanging are accessed through the
        /// SoundClip.
        public class SoundClipController : IDisposable
        {
            private Action m_OnSoundClipInitialized;
            private SoundClip m_SoundClip;
            private bool m_SoundClipInitialized;
            private float m_MinDistance = 1f;
            private float m_MaxDistance = 500f;
            private float m_DistanceScale = 1f;

            public AudioSource m_SoundClipAudioSource;

            public bool Initialized => m_SoundClipInitialized;

            /// Clips do not start playing immediately; this event is triggered when the clip is ready.
            /// However, as several controllers may point at the same clip, if a controller is made to
            /// point at an already playing clip, when a user adds a value to OnSoundClipInitialized, the event
            /// will be made to trigger immediately. The event is always cleared after triggering so this
            /// will not cause OnSoundClipInitialized functions to be called more than once.
            public event Action OnSoundClipInitialized
            {
                add
                {
                    m_OnSoundClipInitialized += value;
                    if (m_SoundClipInitialized)
                    {
                        OnInitialization();
                    }
                }
                remove { m_OnSoundClipInitialized -= value; }
            }

            public bool Playing
            {
                get => m_SoundClipInitialized ? m_SoundClipAudioSource.isPlaying : false;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        if (m_SoundClipAudioSource.isPlaying)
                        {
                            m_SoundClipAudioSource.Pause();
                        }
                        else
                        {
                            m_SoundClipAudioSource.Play();
                        }
                    }
                }
            }

            public float Volume
            {
                get => (!m_SoundClipInitialized || m_SoundClipAudioSource.mute)
                    ? 0f : m_SoundClipAudioSource.volume;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        if (value <= 0.005f)
                        {
                            m_SoundClipAudioSource.volume = 0;
                            m_SoundClipAudioSource.mute = true;
                        }
                        else
                        {
                            m_SoundClipAudioSource.mute = false;
                            m_SoundClipAudioSource.volume = value;
                        }
                    }
                }
            }

            public float Position
            {
                get => m_SoundClipInitialized ? (float)(m_SoundClipAudioSource.time / m_SoundClipAudioSource.clip.length) : 0f;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        m_SoundClipAudioSource.time = m_SoundClipAudioSource.clip.length * Mathf.Clamp01(value);
                    }
                }
            }

            public float Time
            {
                get => m_SoundClipInitialized ? (float)m_SoundClipAudioSource.time : 0f;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        m_SoundClipAudioSource.time = Mathf.Clamp(value, 0, (float)m_SoundClipAudioSource.clip.length);
                    }
                }
            }

            public float Length => m_SoundClipInitialized ? (float)m_SoundClipAudioSource.clip.length : 0f;

            public bool Loop
            {
                get => m_SoundClipInitialized && m_SoundClipAudioSource.loop;
                set { if (m_SoundClipInitialized) m_SoundClipAudioSource.loop = value; }
            }

            /// 0 = 2D (flat), 1 = fully 3D spatial
            public float SpatialBlend
            {
                get => m_SoundClipInitialized ? m_SoundClipAudioSource.spatialBlend : 0f;
                set { if (m_SoundClipInitialized) m_SoundClipAudioSource.spatialBlend = value; }
            }

            /// Authored min distance in scene/canvas space.
            public float MinDistance
            {
                get => m_MinDistance;
                set
                {
                    m_MinDistance = value;
                    ApplyDistanceScale(m_DistanceScale);
                }
            }

            /// Authored max distance in scene/canvas space.
            public float MaxDistance
            {
                get => m_MaxDistance;
                set
                {
                    m_MaxDistance = value;
                    ApplyDistanceScale(m_DistanceScale);
                }
            }

            /// Update the scene/canvas scale factor used to convert authored distances
            /// to world-space AudioSource distances.
            public void ApplyDistanceScale(float scale)
            {
                m_DistanceScale = scale;
                if (m_SoundClipInitialized)
                {
                    m_SoundClipAudioSource.minDistance = m_MinDistance * scale;
                    m_SoundClipAudioSource.maxDistance = m_MaxDistance * scale;
                }
            }

            public SoundClipController(SoundClip soundClip, SoundClipWidget widget)
            {
                m_SoundClip = soundClip;
                m_SoundClipAudioSource = widget.gameObject.GetComponent<AudioSource>();
                if (m_SoundClipAudioSource != null)
                {
                    m_SoundClipInitialized = m_SoundClipAudioSource.clip != null;
                }
            }

            public SoundClipController(SoundClipController other)
            {
                m_SoundClip = other.m_SoundClip;
                m_SoundClipAudioSource = other.m_SoundClipAudioSource;
                m_SoundClipInitialized = other.m_SoundClipInitialized;
                m_SoundClip.m_Controller = this;
            }

            public void Dispose()
            {
                if (m_SoundClip != null)
                {
                    m_SoundClip.OnControllerDisposed(this);
                    m_SoundClip = null;
                }
            }

            public void OnInitialization()
            {
                m_SoundClipInitialized = true;
                m_OnSoundClipInitialized?.Invoke();
                m_OnSoundClipInitialized = null;
            }
        }

        public static SoundClip CreateDummySoundClip()
        {
            return new SoundClip();
        }

        private SoundClipController m_Controller;

        /// Persistent path is relative to the Tilt Brush/Media Library/SoundClips directory, if it is a
        /// filename.
        public string PersistentPath { get; }
        public string AbsolutePath { get; }
        public string HumanName { get; }

        public Texture2D Thumbnail { get; private set; }

        public uint Width { get; private set; }

        public uint Height { get; private set; }

        public float Aspect { get; private set; }

        public bool IsInitialized { get; private set; }

        public bool HasInstances => m_Controller != null;

        public string Error { get; private set; }

        public SoundClip(string filePath)
        {
            PersistentPath = filePath.Substring(App.SoundClipLibraryPath().Length + 1);
            HumanName = System.IO.Path.GetFileName(PersistentPath);
            AbsolutePath = filePath;
        }

        // Dummy SoundClip - this is used when a clip referenced in a sketch cannot be found.
        private SoundClip()
        {
            IsInitialized = false;
            Width = 160;
            Height = 90;
            Aspect = 16 / 9f;
            PersistentPath = "";
            AbsolutePath = "";
            HumanName = "";
        }

        /// Creates a controller for this sound clip. Controllers are Disposable and it is important
        /// to Dispose a controller after it is finished with. If disposal does not happen, then the
        /// clip decoder will keep decoding, using up memory and bandwidth. If the audio is turned on
        /// then the audio will continue. DISPOSE OF YOUR CONTROLLERS.
        public SoundClipController CreateController(SoundClipWidget widget)
        {
            SoundClipController soundClipController = new SoundClipController(this, widget);
            bool alreadyPrepared = HasInstances;
            m_Controller = soundClipController;
            if (!alreadyPrepared)
            {
                SoundClipCatalog.Instance.StartCoroutine(PrepareAudioPlayer(InitializeControllers));
            }
            return soundClipController;
        }

        private void InitializeControllers()
        {
            m_Controller.OnInitialization();
        }

        private void OnControllerDisposed(SoundClipController soundClipController)
        {
            if (!HasInstances && m_Controller.m_SoundClipAudioSource != null)
            {
                m_Controller.m_SoundClipAudioSource.Stop();
                UnityEngine.Object.Destroy(m_Controller.m_SoundClipAudioSource.gameObject);
                m_Controller.m_SoundClipAudioSource = null;
            }
        }

        async Task<AudioClip> LoadClip(string path)
        {
            AudioClip clip = null;
            AudioType audioType = path.ToLower() switch
            {
                var a when a.EndsWith(".wav") => AudioType.WAV,
                var a when a.EndsWith(".mp3") => AudioType.MPEG,
                var a when a.EndsWith(".ogg") => AudioType.OGGVORBIS,
                var a when a.EndsWith(".aiff") || a.EndsWith(".aif") => AudioType.AIFF,
                var a when a.EndsWith(".mod") => AudioType.MOD,
                var a when a.EndsWith(".it") => AudioType.IT,
                var a when a.EndsWith(".s3m") => AudioType.S3M,
                var a when a.EndsWith(".xm") => AudioType.XM,
                _ => throw new ArgumentOutOfRangeException(nameof(path), $"Unsupported audio type: {path}.")
            };

            using (UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(path, audioType))
            {
                uwr.SendWebRequest();

                try
                {
                    while (!uwr.isDone) await Task.Delay(5);

                    if (uwr.isNetworkError || uwr.isHttpError) Debug.Log($"{uwr.error}");
                    else
                    {
                        clip = DownloadHandlerAudioClip.GetContent(uwr);
                    }
                }
                catch (Exception err)
                {
                    Debug.Log($"{err.Message}, {err.StackTrace}");
                }
            }
            return clip;
        }


        private IEnumerator<Null> PrepareAudioPlayer(Action onCompletion)
        {
            Error = null;
            m_Controller.m_SoundClipAudioSource.playOnAwake = false;
            string fullPath = System.IO.Path.Combine(App.SoundClipLibraryPath(), PersistentPath);
            var audioClipTask = LoadClip(fullPath);
            while (!audioClipTask.IsCompleted)
            {
                yield return null;
            }
            m_Controller.m_SoundClipAudioSource.clip = audioClipTask.Result;
            m_Controller.m_SoundClipAudioSource.loop = true;

            Width = 128;
            Height = 128;
            Aspect = 1;

            m_Controller.m_SoundClipAudioSource.mute = false;
            m_Controller.m_SoundClipAudioSource.Play();

            if (onCompletion != null)
            {
                onCompletion();
            }
        }

        private void OnError(GvrAudioSource player, string error)
        {
            Error = error;
        }

        public IEnumerator<Null> Initialize()
        {
            Width = 128;
            Height = 128;
            Aspect = 1;

            var audioClipTask = LoadClip(AbsolutePath);
            while (!audioClipTask.IsCompleted)
            {
                yield return null;
            }

            var clip = audioClipTask.Result;
            if (clip != null)
            {
                Thumbnail = GetWaveform(0.8f, Color.white, clip);
                UnityEngine.Object.Destroy(clip);
            }
            IsInitialized = true;
        }

        public void Dispose()
        {
            if (m_Controller?.m_SoundClipAudioSource != null)
            {
                Debug.Assert(m_Controller != null,
                    "There should be a controller if the SoundClipAudioSource is not null.");
                m_Controller.Dispose();
            }
            if (Thumbnail != null)
            {
                UnityEngine.Object.Destroy(Thumbnail);
            }
        }

        public override string ToString()
        {
            return $"{HumanName}: {Width}x{Height} {Aspect}";
        }

        public Texture2D GetWaveform(float saturation, Color col, AudioClip audio = null, float aspect = 0)
        {
            int height = (int)Height;
            int width = aspect > 0 ? Mathf.RoundToInt(height * aspect) : (int)Width;
            audio ??= m_Controller?.m_SoundClipAudioSource?.clip;
            if (audio == null) return null;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float[] samples = new float[audio.samples * audio.channels];
            audio.GetData(samples, 0);

            // Find peak amplitude per column using max abs value in each range
            float[] waveform = new float[width];
            int packSize = Mathf.Max(samples.Length / width, 1);
            for (int x = 0; x < width; x++)
            {
                float peak = 0f;
                int start = x * packSize;
                int end = Mathf.Min(start + packSize, samples.Length);
                for (int i = start; i < end; i++)
                {
                    float abs = Mathf.Abs(samples[i]);
                    if (abs > peak) peak = abs;
                }
                waveform[x] = peak;
            }

            // Normalize so the loudest peak fills the available height
            float maxPeak = 0f;
            for (int x = 0; x < width; x++)
            {
                if (waveform[x] > maxPeak) maxPeak = waveform[x];
            }
            if (maxPeak > 0f)
            {
                for (int x = 0; x < width; x++)
                {
                    waveform[x] /= maxPeak;
                }
            }

            // Clear to transparent
            Color[] clear = new Color[width * height];
            tex.SetPixels(clear);

            // Draw centered waveform bars
            int halfHeight = height / 2;
            float scale = halfHeight * saturation;
            for (int x = 0; x < width; x++)
            {
                int barHeight = Mathf.Clamp(Mathf.RoundToInt(waveform[x] * scale), 0, halfHeight - 1);
                for (int y = 0; y <= barHeight; y++)
                {
                    tex.SetPixel(x, halfHeight + y, col);
                    tex.SetPixel(x, halfHeight - y, col);
                }
            }
            tex.Apply();

            return tex;
        }
    }
} // namespace TiltBrush
