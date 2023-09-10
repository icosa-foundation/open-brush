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
using UnityEngine;

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
        public class Controller : IDisposable
        {
            private Action m_OnSoundClipInitialized;
            private SoundClip m_SoundClip;
            private bool m_SoundClipInitialized;

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

            private AudioSource SoundClipAudioSource => m_SoundClip.m_AudioSource;

            public Texture SoundClipTexture
            {
                get { return m_SoundClipInitialized ? SoundClipAudioSource.texture : null; }
            }

            public bool Playing
            {
                get => m_SoundClipInitialized ? SoundClipAudioSource.isPlaying : false;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        if (SoundClipAudioSource.isPlaying)
                        {
                            SoundClipAudioSource.Pause();
                        }
                        else
                        {
                            SoundClipAudioSource.Play();
                        }
                    }
                }
            }

            public float Volume
            {
                get => (!m_SoundClipInitialized || SoundClipAudioSource.GetDirectAudioMute(0))
                    ? 0f : SoundClipAudioSource.GetDirectAudioVolume(0);
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        if (value <= 0.005f)
                        {
                            SoundClipAudioSource.SetDirectAudioVolume(0, 0f);
                            SoundClipAudioSource.SetDirectAudioMute(0, true);
                        }
                        else
                        {
                            SoundClipAudioSource.SetDirectAudioMute(0, false);
                            SoundClipAudioSource.SetDirectAudioVolume(0, value);
                        }
                    }
                }
            }

            public float Position
            {
                get => m_SoundClipInitialized ? (float)(SoundClipAudioSource.time / SoundClipAudioSource.length) : 0f;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        SoundClipAudioSource.time = SoundClipAudioSource.length * Mathf.Clamp01(value);
                    }
                }
            }

            public float Time
            {
                get => m_SoundClipInitialized ? (float)SoundClipAudioSource.time : 0f;
                set
                {
                    if (m_SoundClipInitialized)
                    {
                        SoundClipAudioSource.time = Mathf.Clamp(value, 0, (float)SoundClipAudioSource.length);
                    }
                }
            }

            public float Length => m_SoundClipInitialized ? (float)SoundClipAudioSource.length : 0f;

            public Controller(SoundClip soundClip)
            {
                m_SoundClip = soundClip;
                if (m_SoundClip.m_SoundClipAudioSource != null)
                {
                    m_SoundClipInitialized = m_SoundClip.m_SoundClipAudioSource.isPrepared;
                }
            }

            public Controller(Controller other)
            {
                m_SoundClip = other.m_SoundClip;
                m_SoundClipInitialized = other.m_SoundClipInitialized;
                m_SoundClip.m_Controllers.Add(this);
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

        private AudioSource m_SoundClipAudioSource;
        private HashSet<Controller> m_Controllers = new HashSet<Controller>();

        /// Persistent path is relative to the Tilt Brush/Media Library/SoundClips directory, if it is a
        /// filename.
        public string PersistentPath { get; }
        public string AbsolutePath { get; }
        public bool NetworkSoundClip { get; }
        public string HumanName { get; }

        public Texture2D Thumbnail { get; private set; }

        public uint Width { get; private set; }

        public uint Height { get; private set; }

        public float Aspect { get; private set; }

        public bool IsInitialized { get; private set; }

        public bool HasInstances => m_Controllers.Count > 0;

        public string Error { get; private set; }

        public SoundClip(string filePath)
        {
            NetworkSoundClip = filePath.EndsWith(".txt");
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
            NetworkSoundClip = false;
            HumanName = "";
        }

        /// Creates a controller for this sound clip. Controllers are Disposable and it is important
        /// to Dispose a controller after it is finished with. If disposal does not happen, then the
        /// clip decoder will keep decoding, using up memory and bandwidth. If the audio is turned on
        /// then the audio will continue. DISPOSE OF YOUR CONTROLLERS.
        public Controller CreateController()
        {
            Controller controller = new Controller(this);
            bool alreadyPrepared = HasInstances;
            m_Controllers.Add(controller);
            if (!alreadyPrepared)
            {
                SoundClipCatalog.Instance.StartCoroutine(PrepareAudioPlayer(InitializeControllers));
            }
            return controller;
        }

        private void InitializeControllers()
        {
            foreach (var controller in m_Controllers)
            {
                controller.OnInitialization();
            }
        }

        private void OnControllerDisposed(Controller controller)
        {
            m_Controllers.Remove(controller);
            if (!HasInstances && m_SoundClipAudioSource != null)
            {
                m_SoundClipAudioSource.Stop();
                UnityEngine.Object.Destroy(m_SoundClipAudioSource.gameObject);
                m_SoundClipAudioSource = null;
            }
        }

        private IEnumerator<Null> PrepareAudioPlayer(Action onCompletion)
        {
            Error = null;
            var gobj = new GameObject(HumanName);
            gobj.transform.SetParent(SoundClipCatalog.Instance.gameObject.transform);
            try
            {
                m_SoundClipAudioSource = gobj.AddComponent<AudioSource>();
                m_SoundClipAudioSource.playOnAwake = false;
                if (NetworkSoundClip)
                {
                    if (System.IO.File.Exists(AbsolutePath))
                    {
                        m_SoundClipAudioSource.url = System.IO.File.ReadAllText(AbsolutePath);
                    }
                }
                else
                {
                    string fullPath = System.IO.Path.Combine(App.SoundClipLibraryPath(), PersistentPath);
                    m_SoundClipAudioSource.url = $"{fullPath}";
                }
                m_SoundClipAudioSource.isLooping = true;
                m_SoundClipAudioSource.renderMode = SoundClipRenderMode.APIOnly;
                m_SoundClipAudioSource.skipOnDrop = true;
                m_SoundClipAudioSource.audioOutputMode = SoundClipAudioOutputMode.Direct;
                m_SoundClipAudioSource.Prepare();
                m_SoundClipAudioSource.errorReceived += OnError;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Error = ex.Message;
                yield break;
            }

            while (!m_SoundClipAudioSource.isPrepared)
            {
                if (Error != null)
                {
                    yield break;
                }
                yield return null;
            }

            // This code is *super* useful for testing the sound clip panel, and I've written it at
            // least five times, so I'd like to just leave it here as it may well be useful in the future.
#if false
    // Delays the clip load by two seconds
    for (var wait = DateTime.Now + TimeSpan.FromSeconds(2); wait > DateTime.Now;) {
      yield return null;
    }
#endif

            Width = 128;
            Height = 128;
            Aspect = 1;

            // Video does this but I don't think it makes sense for audio
            // m_SoundClipAudioSource.mute = true;
            // m_SoundClipAudioSource.Play();

            if (onCompletion != null)
            {
                onCompletion();
            }
        }

        private void OnError(AudioSource player, string error)
        {
            Error = error;
        }

        public IEnumerator<Null> Initialize()
        {
            Controller thumbnailExtractor = CreateController();
            while (!thumbnailExtractor.Initialized)
            {
                if (Error != null)
                {
                    thumbnailExtractor.Dispose();
                    yield break;
                }
                yield return null;
            }
            int width, height;
            if (Aspect > 1)
            {
                width = 128;
                height = Mathf.RoundToInt(width / Aspect);
            }
            else
            {
                height = 128;
                width = Mathf.RoundToInt(height * Aspect);
            }
            // A frame does not always seem to be immediately available, so wait until we've hit at least
            // the second frame before continuing.
            while (m_SoundClipAudioSource.time < 0.1)
            {
                yield return null;
            }
            // Because the Thumbnail needs to be a Texture2D, we need to do the little dance of copying
            // the rendertexture over to the Texture2D.
            var rt = RenderTexture.GetTemporary(width, height, 0);
            Graphics.Blit(m_SoundClipAudioSource.texture, rt);
            Thumbnail = new Texture2D(width, height, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            RenderTexture.active = rt;
            Thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = oldActive;
            Thumbnail.Apply(false);
            RenderTexture.ReleaseTemporary(rt);
            thumbnailExtractor.Dispose();
            IsInitialized = true;
        }

        public void Dispose()
        {
            if (m_SoundClipAudioSource != null)
            {
                Debug.Assert(m_Controllers.Count > 0,
                    "There should be controllers if the SoundClipAudioSource is not null.");
                foreach (var controller in m_Controllers.ToArray())
                {
                    // Controller.Dispose handles removing itself from m_Controllers, so we don't do it here.
                    controller.Dispose();
                }
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
    }
} // namespace TiltBrush
