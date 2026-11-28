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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

namespace TiltBrush
{
    // Fast preview plate cache (POSTPROD_FAST_PREVIEW_DESIGN.md section 4.1): keeps a
    // window of plate frames around the playhead in RAM so scrubbing never waits on disk.
    //
    // - Colour: <clipDir>/<preview>/color/frame_NNNNN_color.jpg, every frame (<preview> is
    //   the clip manifest's plates.preview, preview_1280x676 for C006).
    //   Compressed bytes read on a worker thread; decoded on the main thread on demand
    //   (Texture2D.LoadImage is main-thread only).
    // - Depth: the clip's preview depth set, <clipDir>/<plates.depth>/frame_NNNNN_depth.exr
    //   (depth_frames for C006: 1280x676, every 2nd frame). Read and decoded to floats entirely on the worker thread
    //   (ExrDepthReader is pure C#); only the upload happens on the main thread.
    //
    // A frame that isn't ready yet is never silently replaced: the previous frame is held
    // and flagged (Holding), per the preview's "never silently substitute" rule.
    public static class PostProductionPlateCache
    {
        public enum PlateState { Ready, Holding, Loading, OutOfRange }

        public static bool Enabled { get; private set; }

        // The first frame with both preview colour and depth, or null when fast plates are off.
        public static int? FirstCompositableFrame =>
            Enabled ? Mathf.Max(s_ColorFirst, s_DepthFirst) : (int?)null;
        public static volatile string LastStatus = "fast plates off";

        // Windows around the playhead, in C006 frames (60fps). Ahead-biased for playback.
        private const int kColorAhead = 300, kColorBehind = 120;
        // Depth is ~3.5MB per frame decoded (every 2nd frame), so its window is smaller:
        // ~60 frames in RAM, ~200MB.
        private const int kDepthAhead = 90, kDepthBehind = 30;
        private const int kMaxDecodedPerKind = 24;

        private static string s_ColorDir, s_DepthDir;
        private static int s_ColorFirst, s_ColorLast;
        private static int s_DepthFirst, s_DepthLast, s_DepthStride;

        private static readonly object s_Lock = new object();
        private static readonly Dictionary<int, byte[]> s_ColorBytes = new Dictionary<int, byte[]>();
        // Rows already flipped bottom-up, ready for SetPixelData (see PostProdCompositor.BuildDepthTexture).
        private static readonly Dictionary<int, float[]> s_DepthPixels = new Dictionary<int, float[]>();
        private static int s_DepthWidth, s_DepthHeight;
        // Depth pixel arrays (~3.5MB each) are recycled rather than garbage-collected:
        // evicted frames return theirs here and decodes take from it (2026-10-23, see
        // ExrDepthReader.ReadSingleChannelFloatPooled). Guarded by s_Lock.
        private static readonly Stack<float[]> s_DepthPool = new Stack<float[]>();
        private const int kMaxPooledDepthArrays = 16;
        private static volatile string s_PooledDecodeCheck = "not run";

        private static volatile int s_Playhead = int.MinValue;
        private static Thread s_Worker;
        private static volatile bool s_StopWorker;
        private static readonly AutoResetEvent s_Wake = new AutoResetEvent(false);

        // Depth EXRs decode in parallel: each takes ~110ms here, and 1x playback needs 30 a
        // second. One at a time fell ~4s behind within seconds (2026-10-21); 4 managed ~26 a
        // second and stalled after ~35s of 1x play (2026-10-23). The default scales with the
        // machine's cores (8 on a 32-thread machine); postprod.preview.fastplates.decoders
        // overrides it for slower or faster computers.
        public static int DefaultDepthDecoders => Mathf.Clamp(System.Environment.ProcessorCount / 4, 2, 8);
        private static int s_DepthDecoders = DefaultDepthDecoders;
        public static int DepthDecoders => s_DepthDecoders;
        private static SemaphoreSlim s_DecodeSlots = new SemaphoreSlim(s_DepthDecoders);

        // count <= 0 restores the default. Takes effect the next time fast plates are enabled.
        public static void SetDepthDecoders(int count)
        {
            s_DepthDecoders = count > 0 ? count : DefaultDepthDecoders;
        }

        // During forward play only frames ahead of the playhead are loaded, and if the
        // playhead has overtaken the cache, loading restarts this far ahead of it so the
        // plate catches up instead of chasing a playhead it can never reach.
        private const int kForwardJump = 120;
        private const int kCatchUpLead = 20;
        private static int s_PrevPassPlayhead = int.MinValue;
        private static readonly HashSet<int> s_DepthInFlight = new HashSet<int>();

        // Colour JPEGs are read in parallel too: each file open costs ~12ms on the USB SSD
        // the media is on (measured 2026-10-23, cold reads of 61KB files), so reading them
        // one at a time on the loader thread capped the whole cache at ~27 depth frames/s.
        private const int kColorReaders = 8;
        private static SemaphoreSlim s_ColorSlots = new SemaphoreSlim(kColorReaders);
        private static readonly HashSet<int> s_ColorInFlight = new HashSet<int>();
        // Bumped on enable/disable so decodes that finish after a reset are discarded.
        private static volatile int s_Generation;
        private static long s_DecodeCount, s_DecodeTicks;
        // TEMP diagnostics for the "video stops, strokes carry on" stall (2026-10-23).
        private static volatile string s_WorkerPhase = "idle";
        private static long s_LastDecodeDoneTicks;

        // A pass keeps going while the playhead moves forward by less than this (playback);
        // a bigger jump, or any backwards move, restarts it around the new position (scrub).
        private const int kRestartAhead = 30;

        // Main-thread only.
        private static readonly Dictionary<int, Texture2D> s_ColorTex = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Texture2D> s_DepthTex = new Dictionary<int, Texture2D>();
        private static int s_ShownColorFrame = int.MinValue, s_ShownDepthFrame = int.MinValue;

        // Editor Play sessions keep static state (no domain reload), including the loader
        // thread, which would otherwise keep running into the next session: stop it and start
        // with fast plates off, as a build does.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Interlocked.Increment(ref s_Generation);
            Enabled = false;
            s_StopWorker = true;
            s_Wake.Set();
            if (s_Worker != null && s_Worker.IsAlive) { s_Worker.Join(500); }
            s_Worker = null;
            s_StopWorker = false;
            lock (s_Lock)
            {
                s_ColorBytes.Clear();
                s_DepthPixels.Clear();
                s_DepthPool.Clear();
                s_DepthInFlight.Clear();
                s_ColorInFlight.Clear();
            }
            // Not scene objects, so they outlive the previous session unless destroyed.
            foreach (var t in s_ColorTex.Values) { if (t != null) { UnityEngine.Object.DestroyImmediate(t); } }
            foreach (var t in s_DepthTex.Values) { if (t != null) { UnityEngine.Object.DestroyImmediate(t); } }
            s_ColorTex.Clear();
            s_DepthTex.Clear();
            s_ShownColorFrame = s_ShownDepthFrame = int.MinValue;
            s_Playhead = int.MinValue;
            s_PrevPassPlayhead = int.MinValue;
            s_ColorDir = s_DepthDir = null;
            s_ColorFirst = s_ColorLast = 0;
            s_DepthFirst = s_DepthLast = s_DepthStride = 0;
            s_DepthWidth = s_DepthHeight = 0;
            s_DepthDecoders = DefaultDepthDecoders;
            s_DecodeCount = s_DecodeTicks = 0;
            s_LastDecodeDoneTicks = 0;
            s_WorkerPhase = "idle";
            s_PooledDecodeCheck = "not run";
            LastStatus = "fast plates off";
        }

        [Serializable]
        private class PreviewManifest { public PreviewRange color; }
        [Serializable]
        private class PreviewRange { public int firstFrame; public int lastFrame; }
        [Serializable]
        private class DepthManifest { public int firstFrame; public int lastFrame; public int stride; }

        // Returns null on success, otherwise an error describing what's missing.
        public static string SetEnabled(bool enabled, string clipDir)
        {
            if (!enabled)
            {
                Disable();
                return null;
            }
            if (string.IsNullOrEmpty(clipDir))
            {
                return "fast plates need a clip dir: set postprod.preview.autoplate first";
            }
            try
            {
                string previewDir = Path.Combine(clipDir, PostProductionClip.PreviewDir);
                var preview = JsonUtility.FromJson<PreviewManifest>(
                    File.ReadAllText(Path.Combine(previewDir, "manifest.json")));
                var depth = JsonUtility.FromJson<DepthManifest>(
                    File.ReadAllText(Path.Combine(clipDir, PostProductionClip.PreviewDepthDir, "manifest.json")));
                Disable();
                s_ColorDir = Path.Combine(previewDir, "color");
                s_DepthDir = Path.Combine(clipDir, PostProductionClip.PreviewDepthDir);
                s_ColorFirst = preview.color.firstFrame;
                s_ColorLast = preview.color.lastFrame;
                s_DepthFirst = depth.firstFrame;
                s_DepthLast = depth.lastFrame;
                s_DepthStride = Mathf.Max(1, depth.stride);
            }
            catch (Exception e)
            {
                return $"fast plates: could not read manifests under {clipDir}: {e.Message}";
            }

            Interlocked.Exchange(ref s_DecodeCount, 0);
            Interlocked.Exchange(ref s_DecodeTicks, 0);
            Interlocked.Exchange(ref s_LastDecodeDoneTicks, 0);
            // Decodes from before Disable release the slots object they took, so replacing
            // it here is safe.
            s_DecodeSlots = new SemaphoreSlim(s_DepthDecoders);
            s_ColorSlots = new SemaphoreSlim(kColorReaders);
            s_PrevPassPlayhead = int.MinValue;
            s_StopWorker = false;
            s_Worker = new Thread(WorkerLoop) { IsBackground = true, Name = "PostProdPlateCache" };
            s_Worker.Start();
            Application.quitting -= Disable;
            Application.quitting += Disable;
            Enabled = true;
            LastStatus = $"fast plates on: colour {s_ColorFirst}-{s_ColorLast}, depth {s_DepthFirst}-{s_DepthLast}/{s_DepthStride}";
            return null;
        }

        // Main thread, every frame: the C006 frame the preview should show.
        public static void SetPlayhead(int c006Frame)
        {
            if (s_Playhead == c006Frame) { return; }
            s_Playhead = c006Frame;
            s_Wake.Set();
        }

        // Main thread (OnRenderImage). depth is null when this frame has no depth (outside
        // the depth range), which the compositor treats as the naive fallback.
        public static PlateState TryGetPlate(out Texture2D color, out Texture2D depth)
        {
            color = null;
            depth = null;
            int frame = s_Playhead;
            if (!Enabled || frame == int.MinValue) { return PlateState.Loading; }
            if (frame < s_ColorFirst || frame > s_ColorLast)
            {
                UpdateStatus(frame, "out of range");
                return PlateState.OutOfRange;
            }

            int depthFrame = NearestDepthFrame(frame);
            Texture2D c = GetOrDecodeColor(frame);
            Texture2D d = depthFrame == int.MinValue ? null : GetOrUploadDepth(depthFrame);
            bool depthReady = depthFrame == int.MinValue || d != null;

            if (c != null && depthReady)
            {
                s_ShownColorFrame = frame;
                s_ShownDepthFrame = depthFrame;
                color = c;
                depth = d;
                EvictDecoded(frame);
                UpdateStatus(frame, "ready");
                return PlateState.Ready;
            }

            // Not ready: hold the last shown frame, if any.
            s_ColorTex.TryGetValue(s_ShownColorFrame, out color);
            if (s_ShownDepthFrame != int.MinValue) { s_DepthTex.TryGetValue(s_ShownDepthFrame, out depth); }
            if (color == null) { UpdateStatus(frame, "loading"); return PlateState.Loading; }
            UpdateStatus(frame, $"holding {s_ShownColorFrame}");
            return PlateState.Holding;
        }

        private static int NearestDepthFrame(int frame)
        {
            if (frame < s_DepthFirst || frame > s_DepthLast) { return int.MinValue; }
            int offset = Mathf.RoundToInt((frame - s_DepthFirst) / (float)s_DepthStride) * s_DepthStride;
            return Mathf.Min(s_DepthFirst + offset, s_DepthLast);
        }

        private static Texture2D GetOrDecodeColor(int frame)
        {
            if (s_ColorTex.TryGetValue(frame, out var tex)) { return tex; }
            return DecodeColor(frame);
        }

        private static Texture2D DecodeColor(int frame)
        {
            Texture2D tex;
            byte[] bytes;
            lock (s_Lock) { s_ColorBytes.TryGetValue(frame, out bytes); }
            if (bytes == null) { return null; }
            tex = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.LoadImage(bytes, markNonReadable: true);
            s_ColorTex[frame] = tex;
            return tex;
        }

        private static Texture2D GetOrUploadDepth(int depthFrame)
        {
            if (s_DepthTex.TryGetValue(depthFrame, out var tex)) { return tex; }
            return UploadDepth(depthFrame);
        }

        private static Texture2D UploadDepth(int depthFrame)
        {
            Texture2D tex;
            lock (s_Lock) { if (!s_DepthPixels.ContainsKey(depthFrame)) { return null; } }
            // Bilinear, so the compositor can sample either way: it snaps to the nearest
            // texel unless the depth filter is bilinear.
            tex = new Texture2D(s_DepthWidth, s_DepthHeight, TextureFormat.RFloat, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            // The copy happens under the lock: the array goes back to the pool for reuse
            // once evicted, so it must not be evicted mid-copy.
            lock (s_Lock)
            {
                if (!s_DepthPixels.TryGetValue(depthFrame, out var pixels))
                {
                    UnityEngine.Object.Destroy(tex);
                    return null;
                }
                tex.SetPixelData(pixels, 0);
            }
            tex.Apply(false, true);
            s_DepthTex[depthFrame] = tex;
            return tex;
        }

        // Keeps the decoded-texture caches bounded, dropping the frames farthest from the
        // playhead but never the ones on screen.
        private static void EvictDecoded(int playhead)
        {
            Evict(s_ColorTex, playhead, s_ShownColorFrame);
            Evict(s_DepthTex, playhead, s_ShownDepthFrame);
        }

        private static void Evict(Dictionary<int, Texture2D> cache, int playhead, int keep)
        {
            while (cache.Count > kMaxDecodedPerKind)
            {
                int farthest = int.MinValue;
                int farthestDistance = -1;
                foreach (int f in cache.Keys)
                {
                    int dist = Math.Abs(f - playhead);
                    if (f != keep && dist > farthestDistance) { farthest = f; farthestDistance = dist; }
                }
                if (farthest == int.MinValue) { return; }
                UnityEngine.Object.Destroy(cache[farthest]);
                cache.Remove(farthest);
            }
        }

        private static void UpdateStatus(int frame, string state)
        {
            int colorBytes, depthPixels;
            lock (s_Lock) { colorBytes = s_ColorBytes.Count; depthPixels = s_DepthPixels.Count; }
            long count = Interlocked.Read(ref s_DecodeCount);
            double avgMs = count > 0
                ? Interlocked.Read(ref s_DecodeTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency / count
                : 0;
            LastStatus = $"fast plates: frame {frame} {state} | in RAM: {colorBytes} colour, " +
                $"{depthPixels} depth | decoded: {s_ColorTex.Count} colour, {s_DepthTex.Count} depth | " +
                $"depth EXR decode avg {avgMs:F0}ms x{s_DepthDecoders} threads" + Diagnostics(frame);
        }

        // TEMP: separates "decoding can't keep up" from "loader stuck" from "logic bug".
        private static string Diagnostics(int frame)
        {
            int colorAhead = 0, depthAhead = 0, inFlight, colorInFlight;
            lock (s_Lock)
            {
                while (colorAhead < kColorAhead && s_ColorBytes.ContainsKey(frame + colorAhead)) { colorAhead++; }
                int d = NearestDepthFrame(frame);
                while (d != int.MinValue && d + depthAhead * s_DepthStride <= s_DepthLast &&
                    s_DepthPixels.ContainsKey(d + depthAhead * s_DepthStride)) { depthAhead++; }
                inFlight = s_DepthInFlight.Count;
                colorInFlight = s_ColorInFlight.Count;
            }
            long last = Interlocked.Read(ref s_LastDecodeDoneTicks);
            double sinceMs = last == 0 ? -1 :
                (System.Diagnostics.Stopwatch.GetTimestamp() - last) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            ThreadPool.GetAvailableThreads(out int poolFree, out _);
            ThreadPool.GetMaxThreads(out int poolMax, out _);
            return $"\n  DIAG worker={s_WorkerPhase} ready ahead: colour {colorAhead}, depth {depthAhead * s_DepthStride} frames | " +
                $"in flight {inFlight}, done {Interlocked.Read(ref s_DecodeCount)}, last {sinceMs:F0}ms ago, " +
                $"colour reads in flight {colorInFlight} | " +
                $"pool free {poolFree}/{poolMax}\n  {s_PooledDecodeCheck}";
        }

        // ---- worker thread -------------------------------------------------------------

        private static void WorkerLoop()
        {
            CheckPooledDecode();
            while (!s_StopWorker)
            {
                s_WorkerPhase = "idle";
                s_Wake.WaitOne(50);
                s_WorkerPhase = "loading";
                int p = s_Playhead;
                if (p == int.MinValue) { continue; }
                try
                {
                    bool forward = p > s_PrevPassPlayhead && p - s_PrevPassPlayhead <= kForwardJump;
                    s_PrevPassPlayhead = p;
                    EvictRam(p);
                    LoadAround(p, forward);
                }
                catch (Exception e)
                {
                    LastStatus = $"fast plates worker error: {e.GetType().Name}: {e.Message}";
                    Thread.Sleep(200);
                }
            }
        }

        private static void EvictRam(int p)
        {
            lock (s_Lock)
            {
                RemoveOutside(s_ColorBytes, p - kColorBehind, p + kColorAhead);
                // Margin of one stride: the nearest depth frame to the window edge can sit
                // just outside it, and must not be evicted and re-read every pass.
                int lo = p - kDepthBehind - s_DepthStride, hi = p + kDepthAhead + s_DepthStride;
                var remove = new List<int>();
                foreach (int f in s_DepthPixels.Keys) { if (f < lo || f > hi) { remove.Add(f); } }
                foreach (int f in remove)
                {
                    ReturnDepthArray(s_DepthPixels[f]);
                    s_DepthPixels.Remove(f);
                }
            }
        }

        // Caller holds s_Lock.
        private static void ReturnDepthArray(float[] pixels)
        {
            if (s_DepthPool.Count < kMaxPooledDepthArrays) { s_DepthPool.Push(pixels); }
        }

        private static float[] RentDepthArray(int length)
        {
            lock (s_Lock)
            {
                while (s_DepthPool.Count > 0)
                {
                    float[] pooled = s_DepthPool.Pop();
                    if (pooled.Length == length) { return pooled; }
                }
            }
            return new float[length];
        }

        // Once per enable: the pooled decode must give exactly the original reader's
        // values (that reader was verified bit-exact against Python's OpenEXR).
        private static void CheckPooledDecode()
        {
            try
            {
                string path = Path.Combine(s_DepthDir, $"frame_{s_DepthFirst:D5}_depth.exr");
                float[] reference = ExrDepthReader.ReadSingleChannelFloat(path, out int w, out int h);
                float[] pooled = ExrDepthReader.ReadSingleChannelFloatPooled(path, n => new float[n], out _, out _);
                int mismatches = 0;
                for (int row = 0; row < h; row++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (BitConverter.SingleToInt32Bits(reference[row * w + x]) !=
                            BitConverter.SingleToInt32Bits(pooled[(h - 1 - row) * w + x])) { mismatches++; }
                    }
                }
                s_PooledDecodeCheck = mismatches == 0
                    ? $"pooled decode matches reference bit for bit (frame {s_DepthFirst}, {w}x{h})"
                    : $"POOLED DECODE MISMATCH: {mismatches} of {w * h} pixels differ (frame {s_DepthFirst})";
            }
            catch (Exception e)
            {
                s_PooledDecodeCheck = $"pooled decode check failed: {e.Message}";
            }
        }

        private static void RemoveOutside<T>(Dictionary<int, T> dict, int lo, int hi)
        {
            var remove = new List<int>();
            foreach (int f in dict.Keys) { if (f < lo || f > hi) { remove.Add(f); } }
            foreach (int f in remove) { dict.Remove(f); }
        }

        // Nearest frames first (ahead weighted 2:1 over behind); restarts as soon as the
        // playhead moves, so a scrub immediately reprioritises around the new position.
        // Playing forward, only ahead is loaded, from kCatchUpLead ahead if the playhead's
        // own frame isn't in RAM yet (the cache has fallen behind).
        private static void LoadAround(int p, bool forward)
        {
            int start = p;
            if (forward)
            {
                int d = NearestDepthFrame(p);
                lock (s_Lock)
                {
                    bool ready = s_ColorBytes.ContainsKey(p) &&
                        (d == int.MinValue || s_DepthPixels.ContainsKey(d));
                    if (!ready) { start = p + kCatchUpLead; }
                }
            }
            int ahead = 0, behind = forward ? kColorBehind + 1 : 0;
            while (ahead <= kColorAhead || behind <= kColorBehind)
            {
                int now = s_Playhead;
                if (s_StopWorker || now < p || now > p + kRestartAhead) { return; }
                int offset;
                if (ahead <= kColorAhead && (behind > kColorBehind || ahead <= 2 * behind))
                {
                    offset = ahead++;
                }
                else
                {
                    offset = -(++behind);
                }
                int f = start + offset;
                // Windows are relative to the playhead (as EvictRam's are), not to start, so
                // a catch-up pass doesn't load frames that the next pass evicts.
                int rel = f - p;
                if (rel > kColorAhead) { break; }
                LoadColor(f);
                if (rel >= -kDepthBehind && rel <= kDepthAhead)
                {
                    LoadDepth(NearestDepthFrame(f));
                }
            }
        }

        // Queues the read on the thread pool; blocks only while all read slots are busy.
        // No separate File.Exists: on this drive that is a second ~12ms round trip, and a
        // missing file shows up as the read's own error.
        private static void LoadColor(int f)
        {
            if (f < s_ColorFirst || f > s_ColorLast) { return; }
            lock (s_Lock)
            {
                if (s_ColorBytes.ContainsKey(f) || s_ColorInFlight.Contains(f)) { return; }
            }
            string path = Path.Combine(s_ColorDir, $"frame_{f:D5}_color.jpg");
            s_WorkerPhase = "waiting for a colour read slot";
            var slots = s_ColorSlots;
            slots.Wait();
            s_WorkerPhase = "loading";
            int generation = s_Generation;
            lock (s_Lock) { s_ColorInFlight.Add(f); }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    lock (s_Lock)
                    {
                        if (generation == s_Generation) { s_ColorBytes[f] = bytes; }
                    }
                }
                catch (Exception e)
                {
                    LastStatus = $"fast plates colour read error (frame {f}): {e.Message}";
                }
                finally
                {
                    lock (s_Lock) { s_ColorInFlight.Remove(f); }
                    slots.Release();
                }
            });
        }

        // Queues the decode on the thread pool; blocks only while all decode slots are busy.
        private static void LoadDepth(int f)
        {
            if (f == int.MinValue) { return; }
            lock (s_Lock)
            {
                if (s_DepthPixels.ContainsKey(f) || s_DepthInFlight.Contains(f)) { return; }
            }
            string path = Path.Combine(s_DepthDir, $"frame_{f:D5}_depth.exr");
            s_WorkerPhase = "waiting for a decode slot";
            var slots = s_DecodeSlots;
            slots.Wait();
            s_WorkerPhase = "loading";
            int generation = s_Generation;
            lock (s_Lock) { s_DepthInFlight.Add(f); }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    // Rows come back bottom row first, ready for SetPixelData.
                    float[] flipped = ExrDepthReader.ReadSingleChannelFloatPooled(
                        path, RentDepthArray, out int w, out int h);
                    Interlocked.Add(ref s_DecodeTicks, System.Diagnostics.Stopwatch.GetTimestamp() - start);
                    Interlocked.Increment(ref s_DecodeCount);
                    Interlocked.Exchange(ref s_LastDecodeDoneTicks, System.Diagnostics.Stopwatch.GetTimestamp());
                    lock (s_Lock)
                    {
                        if (generation == s_Generation)
                        {
                            s_DepthWidth = w;
                            s_DepthHeight = h;
                            s_DepthPixels[f] = flipped;
                        }
                        else
                        {
                            ReturnDepthArray(flipped);
                        }
                    }
                }
                catch (Exception e)
                {
                    LastStatus = $"fast plates depth decode error (frame {f}): {e.Message}";
                }
                finally
                {
                    lock (s_Lock) { s_DepthInFlight.Remove(f); }
                    slots.Release();
                }
            });
        }

        private static void Disable()
        {
            Interlocked.Increment(ref s_Generation);
            Enabled = false;
            s_StopWorker = true;
            s_Wake.Set();
            if (s_Worker != null && s_Worker.IsAlive) { s_Worker.Join(500); }
            s_Worker = null;
            lock (s_Lock)
            {
                s_ColorBytes.Clear();
                s_DepthPixels.Clear();
                s_DepthPool.Clear();
            }
            foreach (var t in s_ColorTex.Values) { if (t != null) { UnityEngine.Object.Destroy(t); } }
            foreach (var t in s_DepthTex.Values) { if (t != null) { UnityEngine.Object.Destroy(t); } }
            s_ColorTex.Clear();
            s_DepthTex.Clear();
            s_ShownColorFrame = s_ShownDepthFrame = int.MinValue;
            s_Playhead = int.MinValue;
            LastStatus = "fast plates off";
        }
    }
}
