// Copyright 2026 The Open Brush Authors
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
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace TiltBrush
{
    // Owns the post-production preview's real-footage media (see
    // POSTPROD_PREVIEW_PLAN.md, stage 4): the H.264 preview proxy, played via
    // VideoPlayer, and the baked real-footage depth EXR sequence, read on demand
    // via ExrDepthReader.
    //
    // Both are addressed by "C006 native video frame number" - the one coordinate
    // space shared by the 60fps proxy (untrimmed, so frame N = native frame N) and
    // the depth sequence, whose files are named by that same true frame number
    // directly (frame_02950_depth.exr = C006 frame 2950). Depth is only present at
    // every manifest.stride'th frame within [firstFrame, lastFrame], so a requested
    // frame is snapped to the nearest one actually on disk - see SeekToC006Frame.
    // Mapping sketch time (plus the stage 2/3 postprod offsets) to a C006 frame
    // number is a separate, later concern for the compositor (stage 5/6) - this
    // class only loads media for a frame number it's given.
    //
    // Seeking follows this codebase's own established ReferenceVideo.cs pattern
    // exactly: Play() and leave the player playing (never Pause()), then set .time
    // directly. Pausing before seeking was tried first and does not work reliably
    // in this Unity version - confirmed by comparing decoded pixel content against
    // ffmpeg-extracted reference frames, not by trusting VideoPlayer.time/.frame,
    // which are separately documented as unreliable to read immediately after a
    // seek (Unity Issue Tracker).
    public class PostProductionMediaLoader : MonoBehaviour
    {
        private static PostProductionMediaLoader m_Instance;
        public static PostProductionMediaLoader Instance
        {
            get
            {
                if (m_Instance == null)
                {
                    var go = new GameObject("PostProductionMediaLoader");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    m_Instance = go.AddComponent<PostProductionMediaLoader>();
                }
                return m_Instance;
            }
        }

        [Serializable]
        private class DepthManifest
        {
            public int frameCount;
            public int width;
            public int height;
            public int firstFrame;
            public int lastFrame;
            public int stride;
        }

        private VideoPlayer m_VideoPlayer;
        private string m_DepthFramesDir;
        private DepthManifest m_DepthManifest;
        private bool m_IsPrepared;
        private int m_QueuedSeekFrame = -1; // set if a seek came in before Prepare() finished

        // Plain-data status, safe to read from the HTTP listener thread (see
        // query.postprod.plate.status in ApiManager.cs) - never touch m_VideoPlayer
        // or any other Unity object outside the main thread. Kept as two independent
        // fields rather than one string mutated in place: the depth half is known
        // synchronously in SeekToC006Frame; the video half is updated later, once
        // the seek has had time to actually take effect.
        private static string s_DepthStatus = "not loaded";
        private static string s_VideoStatus = "not loaded";
        private static bool s_DepthAvailable;
        public static string LastStatus => $"{s_DepthStatus} | video: {s_VideoStatus}";

        // True only when the most recent SeekToC006Frame resolved to a real depth
        // file (whether exact or stride-snapped to the nearest one on disk) - false
        // for a genuinely out-of-range request. Plain data, safe off the main thread
        // for the same reason as s_DepthStatus/s_VideoStatus above.
        public static bool DepthAvailable => s_DepthAvailable;

        public void LoadClip(string videoPath, string depthFramesDir)
        {
            if (!File.Exists(videoPath))
            {
                throw new FileNotFoundException($"Post-prod proxy video not found: {videoPath}");
            }
            string manifestPath = Path.Combine(depthFramesDir, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"Post-prod depth manifest not found: {manifestPath}");
            }

            m_DepthFramesDir = depthFramesDir;
            m_DepthManifest = JsonUtility.FromJson<DepthManifest>(File.ReadAllText(manifestPath));
            m_IsPrepared = false;
            m_QueuedSeekFrame = -1;

            if (m_VideoPlayer == null)
            {
                m_VideoPlayer = gameObject.AddComponent<VideoPlayer>();
                m_VideoPlayer.playOnAwake = false;
                m_VideoPlayer.renderMode = VideoRenderMode.APIOnly;
                m_VideoPlayer.prepareCompleted += OnPrepareCompleted;
                m_VideoPlayer.errorReceived += OnErrorReceived;
            }
            m_VideoPlayer.source = VideoSource.Url;
            m_VideoPlayer.url = videoPath;
            m_VideoPlayer.Prepare();

            s_DepthStatus = $"loaded, {m_DepthManifest.frameCount} depth frames";
            s_VideoStatus = "preparing";
        }

        // Seeks the proxy video and loads the matching depth EXR frame for the given
        // C006 native video frame number. The depth half of the status is set here,
        // synchronously (plain file I/O). If Prepare() hasn't finished yet, the
        // actual video seek is queued and issued from OnPrepareCompleted instead.
        public void SeekToC006Frame(int frame)
        {
            if (m_VideoPlayer == null || m_DepthManifest == null)
            {
                throw new InvalidOperationException(
                    "PostProductionMediaLoader.SeekToC006Frame: call LoadClip first");
            }

            // Genuinely out of range (e.g. frame 60 when depth only covers
            // [2950, 8830]) is NOT the same as an in-range frame that just falls
            // between stride steps - only the latter should be silently snapped.
            bool inRange = frame >= m_DepthManifest.firstFrame && frame <= m_DepthManifest.lastFrame;
            if (!inRange)
            {
                s_DepthAvailable = false;
                s_DepthStatus = $"c006Frame={frame} depth UNAVAILABLE - out of range " +
                    $"(valid range [{m_DepthManifest.firstFrame}, {m_DepthManifest.lastFrame}])";
            }
            else
            {
                int offset = frame - m_DepthManifest.firstFrame;
                int snappedOffset = Mathf.RoundToInt(offset / (float)m_DepthManifest.stride) * m_DepthManifest.stride;
                int depthFrame = m_DepthManifest.firstFrame + snappedOffset;
                string depthPath = Path.Combine(m_DepthFramesDir, $"frame_{depthFrame:D5}_depth.exr");
                try
                {
                    float[] pixels = ExrDepthReader.ReadSingleChannelFloat(depthPath, out int w, out int h);
                    s_DepthAvailable = true;
                    s_DepthStatus = $"c006Frame={frame} depthFrame={depthFrame} depth[w={w},h={h}] " +
                        $"first={pixels[0]:R} mid={pixels[pixels.Length / 2]:R} last={pixels[pixels.Length - 1]:R}";
                }
                catch (Exception e)
                {
                    s_DepthAvailable = false;
                    s_DepthStatus = $"c006Frame={frame} depthFrame={depthFrame} depth UNAVAILABLE - " +
                        $"expected file missing or unreadable ({depthPath}): {e.Message}";
                }
            }

            if (!m_IsPrepared)
            {
                m_QueuedSeekFrame = frame;
                s_VideoStatus = $"queued (requested frame {frame} before player was prepared)";
                return;
            }
            DoSeek(frame);
        }

        private void DoSeek(int frame)
        {
            s_VideoStatus = $"seek requested to frame {frame} ({frame / 60.0:F4}s)";
            m_VideoPlayer.time = frame / 60.0; // proxy is untrimmed native 60fps
            StartCoroutine(ReportAfterSeek(frame));
        }

        // Gives the seek time to land, then updates the status with what the
        // decoded texture actually shows. VideoPlayer.time/.frame are not used to
        // verify this (see class comment) - the player is left playing throughout,
        // so .time keeps advancing on its own regardless of whether the seek itself
        // succeeded, and Unity's own Issue Tracker documents these two properties
        // as unreliable to read immediately after a seek anyway.
        private IEnumerator ReportAfterSeek(int frame)
        {
            const int waitFrames = 30; // ~0.5s at 60fps
            for (int i = 0; i < waitFrames; i++)
            {
                yield return null;
            }

            string pixelSample = "texture null";
            Texture tex = m_VideoPlayer.texture;
            if (tex != null)
            {
                var tmp = RenderTexture.GetTemporary(tex.width, tex.height, 0);
                Graphics.Blit(tex, tmp);
                var prevActive = RenderTexture.active;
                RenderTexture.active = tmp;
                var readback = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                // ReadPixels' Rect is bottom-left origin - flip from top-left/image
                // convention (confirmed empirically against ffmpeg-extracted stills).
                int sampleX = tex.width / 2;
                int sampleYTopLeft = tex.height / 2;
                readback.ReadPixels(new Rect(sampleX, tex.height - sampleYTopLeft, 1, 1), 0, 0);
                readback.Apply();
                Color c = readback.GetPixel(0, 0);
                pixelSample = $"centrePixel=({c.r:F3},{c.g:F3},{c.b:F3})";
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(tmp);
                UnityEngine.Object.Destroy(readback);
            }

            s_VideoStatus = $"after seek to frame {frame}: " +
                $"rawTime={m_VideoPlayer.time:F4} rawFrame={m_VideoPlayer.frame} {pixelSample}";
        }

        private void OnPrepareCompleted(VideoPlayer vp)
        {
            s_VideoStatus = $"prepared: length={vp.length:F4}s frameCount={vp.frameCount} " +
                $"frameRate={vp.frameRate} canSetTime={vp.canSetTime} width={vp.width} height={vp.height}; priming...";
            StartCoroutine(PrimeThenAllowSeeking());
        }

        // Play() and leave the player playing - never Pause() (see class comment
        // for why). "frame does not always seem to be immediately available"
        // (ReferenceVideo.cs's own comment on this exact wait) so wait for it.
        private IEnumerator PrimeThenAllowSeeking()
        {
            m_VideoPlayer.Play();
            while (m_VideoPlayer.frame < 1)
            {
                yield return null;
            }

            m_IsPrepared = true;
            s_VideoStatus = $"primed and ready (frame={m_VideoPlayer.frame})";

            if (m_QueuedSeekFrame >= 0)
            {
                int frame = m_QueuedSeekFrame;
                m_QueuedSeekFrame = -1;
                DoSeek(frame);
            }
        }

        private void OnErrorReceived(VideoPlayer vp, string message)
        {
            s_VideoStatus = $"ERROR: {message}";
        }
    }
}
