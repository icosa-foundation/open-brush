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
using UnityEngine.Video;

namespace TiltBrush
{
    [System.Serializable]
    public class ReferenceVideo
    {
        /// A disposable playback instance. ReferenceVideo shares source metadata and the catalog
        /// thumbnail; each controller owns its decoder, texture and mutable playback state.
        public class Controller : IDisposable
        {
            private Action m_OnVideoInitialized;
            private readonly ReferenceVideo m_ReferenceVideo;
            private VideoPlayer m_VideoPlayer;
            private IEnumerator<Null> m_Preparation;
            private bool m_VideoInitialized;
            private bool m_Disposed;
            private bool m_ErrorReported;
            private bool m_Playing;
            private bool m_Loop;
            private float m_Volume;
            private float m_Time;
            private float? m_Position;

            public bool Initialized => m_VideoInitialized;
            public string Error { get; private set; }
            public bool IsLoading => !m_Disposed && !Initialized && Error == null;
            internal bool IsDisposed => m_Disposed;
            internal long Frame => m_VideoPlayer != null ? m_VideoPlayer.frame : -1;

            /// Late subscribers are notified immediately once this instance is ready.
            public event Action OnVideoInitialized
            {
                add
                {
                    if (m_Disposed) return;
                    m_OnVideoInitialized += value;
                    if (Initialized) OnInitialization();
                }
                remove { m_OnVideoInitialized -= value; }
            }

            public Texture VideoTexture => Initialized ? m_VideoPlayer.texture : null;

            // These setters also retain requests made during preparation. A future shared-player
            // optimization would detach here before changing transport/volume/loop, copying the shared
            // position into a newly prepared player. No automatic sharing/rejoining is needed now.
            public bool Playing
            {
                get => Initialized ? m_VideoPlayer.isPlaying : m_Playing;
                set
                {
                    m_Playing = value;
                    if (!Initialized || value == m_VideoPlayer.isPlaying) return;
                    if (value) m_VideoPlayer.Play();
                    else m_VideoPlayer.Pause();
                }
            }

            public bool Loop
            {
                get => m_Loop;
                set
                {
                    m_Loop = value;
                    // Apply during preparation too, before the first frame can reach the end.
                    if (m_VideoPlayer != null) m_VideoPlayer.isLooping = value;
                }
            }

            public float Volume
            {
                get => m_Volume;
                set
                {
                    m_Volume = Mathf.Clamp01(value);
                    if (!Initialized) return;
                    for (ushort track = 0; track < m_VideoPlayer.audioTrackCount; ++track)
                    {
                        m_VideoPlayer.SetDirectAudioVolume(track, m_Volume);
                        m_VideoPlayer.SetDirectAudioMute(track, m_Volume <= 0.005f);
                    }
                }
            }

            public float Position
            {
                get => Initialized && Length > 0 ? Time / Length : m_Position ?? 0f;
                set
                {
                    m_Position = Mathf.Clamp01(value);
                    if (Initialized) Time = Length * m_Position.Value;
                }
            }

            public float Time
            {
                get => Initialized ? (float)m_VideoPlayer.time : m_Time;
                set
                {
                    m_Time = Mathf.Max(0, value);
                    m_Position = null;
                    if (Initialized) m_VideoPlayer.time = Mathf.Clamp(m_Time, 0, Length);
                }
            }

            public float Length => Initialized ? (float)m_VideoPlayer.length : 0f;

            public Controller(ReferenceVideo referenceVideo) : this(referenceVideo, true, 0, 0, true) { }

            public Controller(Controller other) : this(other.m_ReferenceVideo,
                other.Playing, other.Volume, other.Time, other.Loop)
            { }

            private Controller(ReferenceVideo referenceVideo, bool playing, float volume, float time,
                bool loop)
            {
                m_ReferenceVideo = referenceVideo;
                m_Playing = playing;
                m_Loop = loop;
                m_Volume = volume;
                m_Time = time;
                // Cloned videos are uncommon. Independent ownership keeps video consistent with
                // other media without asynchronous copy-on-change complexity. If sharing becomes
                // worthwhile, acquire a shared playback session here and detach in the setters.
                referenceVideo.m_Controllers.Add(this);
                m_Preparation = PrepareVideoPlayer();
                VideoCatalog.Instance.StartCoroutine(m_Preparation);
            }

            public void Dispose()
            {
                if (m_Disposed) return;
                m_Disposed = true;
                m_OnVideoInitialized = null;
                if (m_Preparation != null && VideoCatalog.Instance != null)
                    VideoCatalog.Instance.StopCoroutine(m_Preparation);
                m_Preparation = null;
                ReleasePlayer();
                m_ReferenceVideo.m_Controllers.Remove(this);
            }

            private void ReleasePlayer()
            {
                m_VideoInitialized = false;
                if (m_VideoPlayer == null) return;
                m_VideoPlayer.errorReceived -= OnError;
                m_VideoPlayer.Stop();
                UnityEngine.Object.Destroy(m_VideoPlayer.gameObject);
                m_VideoPlayer = null;
            }

            private void OnInitialization()
            {
                var callback = m_OnVideoInitialized;
                m_OnVideoInitialized = null;
                callback?.Invoke();
            }

            internal void ReportErrorToUser()
            {
                if (m_Disposed || Error == null || m_ErrorReported) return;
                m_ErrorReported = true;
                // Only selected scene playback reports here. Catalog thumbnail controllers
                // retain their errors for catalog feedback without opening the output window.
                OutputWindowScript.Error("Video playback failed", $"{m_ReferenceVideo.HumanName}: {Error}");
            }

            private void OnError(VideoPlayer player, string error)
            {
                if (m_Disposed) return;
                Error = error;
                Debug.LogWarning($"[OB_VIDEO_INSTANCE] {m_ReferenceVideo.HumanName}: {error}");
                ReleasePlayer();
            }

            private IEnumerator<Null> PrepareVideoPlayer()
            {
                try
                {
                    var gobj = new GameObject(m_ReferenceVideo.HumanName);
                    gobj.transform.SetParent(VideoCatalog.Instance.transform);
                    m_VideoPlayer = gobj.AddComponent<VideoPlayer>();
                    m_VideoPlayer.playOnAwake = false;
                    m_VideoPlayer.url = m_ReferenceVideo.NetworkVideo
                        ? System.IO.File.ReadAllText(m_ReferenceVideo.AbsolutePath)
                        : m_ReferenceVideo.AbsolutePath;
                    m_VideoPlayer.isLooping = m_Loop;
                    m_VideoPlayer.renderMode = VideoRenderMode.APIOnly;
                    m_VideoPlayer.skipOnDrop = true;
                    m_VideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                    m_VideoPlayer.errorReceived += OnError;
                    m_VideoPlayer.Prepare();
                }
                catch (Exception ex)
                {
                    Error = ex.Message;
                    Debug.LogWarning($"[OB_VIDEO_INSTANCE] {m_ReferenceVideo.HumanName}: {ex.Message}");
                    ReleasePlayer();
                    yield break;
                }

                try
                {
                    while (!m_Disposed && Error == null && !m_VideoPlayer.isPrepared)
                        yield return null;
                    if (m_Disposed || Error != null) yield break;

                    m_ReferenceVideo.Width = m_VideoPlayer.width;
                    m_ReferenceVideo.Height = m_VideoPlayer.height;
                    m_ReferenceVideo.Aspect = m_ReferenceVideo.Width / (float)m_ReferenceVideo.Height;
                    // Decode a first frame silently so paused restores also receive a valid texture.
                    for (ushort track = 0; track < m_VideoPlayer.audioTrackCount; ++track)
                        m_VideoPlayer.SetDirectAudioMute(track, true);
                    m_VideoPlayer.Play();
                    while (!m_Disposed && Error == null &&
                        (m_VideoPlayer.texture == null || m_VideoPlayer.frame < 0))
                        yield return null;
                    if (m_Disposed || Error != null) yield break;

                    m_VideoInitialized = true;
                    Volume = m_Volume;
                    float time = m_Position.HasValue ? Length * m_Position.Value : m_Time;
                    if (time > 0) Time = time;
                    Playing = m_Playing;
                    OnInitialization();
                }
                finally
                {
                    m_Preparation = null;
                    if (!Initialized) ReleasePlayer();
                }
            }
        }

        public static ReferenceVideo CreateDummyVideo()
        {
            return new ReferenceVideo();
        }

        private HashSet<Controller> m_Controllers = new HashSet<Controller>();

        /// Persistent path is relative to the Tilt Brush/Media Library/Videos directory, if it is a
        /// filename.
        public string PersistentPath { get; }
        public string AbsolutePath { get; }
        public bool NetworkVideo { get; }
        public string HumanName { get; }

        public Texture2D Thumbnail { get; private set; }

        public uint Width { get; private set; }

        public uint Height { get; private set; }

        public float Aspect { get; private set; }

        public bool IsInitialized { get; private set; }

        public bool HasInstances => m_Controllers.Count > 0;

        public string Error { get; private set; }

        public ReferenceVideo(string filePath)
        {
            // Case-insensitively, because discovery accepts extensions in any case. A
            // .TXT pointer that classified as an ordinary video would be handed to
            // VideoPlayer as the text file itself rather than the URL it contains.
            NetworkVideo = filePath.EndsWith(".txt", System.StringComparison.OrdinalIgnoreCase);
            PersistentPath = _GetPersistentPath(filePath);
            HumanName = System.IO.Path.GetFileName(PersistentPath);
            AbsolutePath = filePath;
        }

        /// A video inside the video library is identified by its path relative to that library.
        /// Anything else keeps just its filename, which is what the pre-existing sketch metadata
        /// format stores.
        private static string _GetPersistentPath(string filePath)
        {
            string libraryPath = System.IO.Path.GetFullPath(App.VideoLibraryPath())
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar) +
                System.IO.Path.DirectorySeparatorChar;
            string fullPath = System.IO.Path.GetFullPath(filePath);
            StringComparison comparison = System.IO.Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (fullPath.StartsWith(libraryPath, comparison))
            {
                return fullPath.Substring(libraryPath.Length);
            }
            return System.IO.Path.GetFileName(filePath);
        }

        // Dummy ReferenceVideo - this is used when a video referenced in a sketch cannot be found.
        private ReferenceVideo()
        {
            IsInitialized = false;
            Width = 160;
            Height = 90;
            Aspect = 16 / 9f;
            PersistentPath = "";
            AbsolutePath = "";
            NetworkVideo = false;
            HumanName = "";
        }

        /// Creates a controller for this reference video. Controllers are Disposable and it is important
        /// to Dispose a controller after it is finished with. If disposal does not happen, then the
        /// video decoder will keep decoding, using up memory and bandwidth. If the audio is turned on
        /// then the audio will continue. DISPOSE OF YOUR CONTROLLERS.
        public Controller CreateController() => new Controller(this);

        public IEnumerator<Null> Initialize()
        {
            Error = null;
            // Thumbnail extraction owns a temporary player; it must never seek or stop a widget.
            using (Controller thumbnailExtractor = CreateController())
            {
                while (!thumbnailExtractor.Initialized || thumbnailExtractor.Frame < 1)
                {
                    if (thumbnailExtractor.IsDisposed) yield break;
                    if (thumbnailExtractor.Error != null)
                    {
                        Error = thumbnailExtractor.Error;
                        yield break;
                    }
                    yield return null;
                }
                int width = Aspect > 1 ? 128 : Mathf.RoundToInt(128 * Aspect);
                int height = Aspect > 1 ? Mathf.RoundToInt(128 / Aspect) : 128;
                var rt = RenderTexture.GetTemporary(width, height, 0);
                var oldActive = RenderTexture.active;
                try
                {
                    Graphics.Blit(thumbnailExtractor.VideoTexture, rt);
                    Thumbnail = new Texture2D(width, height, TextureFormat.RGB24, false);
                    RenderTexture.active = rt;
                    Thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    Thumbnail.Apply(false);
                }
                finally
                {
                    RenderTexture.active = oldActive;
                    RenderTexture.ReleaseTemporary(rt);
                }
                IsInitialized = true;
            }
        }

        public void Dispose()
        {
            foreach (var controller in m_Controllers.ToArray()) controller.Dispose();
            if (Thumbnail != null) UnityEngine.Object.Destroy(Thumbnail);
        }

        public override string ToString()
        {
            return $"{HumanName}: {Width}x{Height} {Aspect}";
        }
    }
} // namespace TiltBrush
