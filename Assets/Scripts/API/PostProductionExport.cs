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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush
{
    // Full-quality export of the post-production composite (POSTPROD_FAST_PREVIEW_DESIGN.md,
    // "Export"), over the trim range, matching the original lens. Runs inside Open Brush with
    // every slow step overlapped, at most one frame per Unity frame:
    //   - plate colour: the clip's source video decoded in order by the bundled ffmpeg, so
    //     every frame is exact and full resolution (frame-exact seek checked 2026-10-24);
    //   - plate depth: the clip's export depth set (else the preview's), decoded ahead on
    //     the thread pool (pooled);
    //   - CG: PostProductionLiveCg's render at full resolution in the undistorted working
    //     frame, distorted back into the original lens by the compositor (design 4.6 (b)),
    //     so the real footage is never resampled;
    //   - output: one asynchronous GPU readback per frame, written to an ffmpeg encoder by a
    //     writer thread.
    // Driven by PostProductionPreviewController: Stage() early in Update (before stroke
    // playback, so the seek is applied this frame) and Render() in LateUpdate.
    public static class PostProductionExport
    {
        public enum State { Idle, Running, Done, Cancelled, Failed }

        private class Format
        {
            public string Id, Label, Extension, Codec, PixFmt, AudioCodec;
            public double BytesPerFrame; // rough, for the free-space check
        }

        // Open Brush's place in the chain (2026-10-24): correct spatial and temporal alignment
        // at source quality, then hand a professional file to the editor (Resolve, Final Cut,
        // Premiere) or deliver. So the export never exceeds the source resolution, keeps its
        // frame rate, bit depth, chroma, colour, timecode and audio, and "Match source" uses
        // the source's own ProRes flavour, or ProRes 422 HQ (4444 for 4:4:4) when the source
        // isn't ProRes, since re-encoding to a lossy delivery codec would lose quality.
        // Bundled ffmpeg only (a 2021 build: no H.265 or hardware encoding, see
        // REPORT_NOTES_FOR_ANDY.md).
        public const string FormatList = "source:Match source,prores422hq:ProRes 422 HQ,h264:H.264 (review)";

        private static readonly string[] kProResProfiles = { "Proxy", "LT", "Standard", "HQ", "4444", "4444 XQ" };
        // Rough MB per 4096x2160 frame by ProRes profile (HQ measured at 4.1 on C006).
        private static readonly double[] kProResMBPer4K = { 0.7, 1.7, 3.5, 5, 7.5, 11 };

        private static Format BuildFormat(string id, PostProductionClip.SourceInfo src)
        {
            double pixels = (double)src.width * src.height / (4096.0 * 2160.0);
            bool hasAudio = src.audio != null && !string.IsNullOrEmpty(src.audio.codec);
            // Lossless and cut exactly on the frame (stream copy can only cut on packets).
            string movAudio = !hasAudio ? "" :
                "-c:a " + (src.audio.codec.StartsWith("pcm_") ? src.audio.codec : "pcm_s24le");
            switch (id)
            {
                case "source":
                    {
                        int profile = SourceProResProfile(src);
                        bool sourceIsProRes = profile >= 0;
                        if (!sourceIsProRes) { profile = (src.pixFmt ?? "").Contains("444") ? 4 : 3; }
                        string label = sourceIsProRes
                            ? $"Match source (ProRes {kProResProfiles[profile]})"
                            : $"Match source (ProRes {kProResProfiles[profile]}; the source is {src.codec})";
                        return ProRes(id, label, profile, profile >= 4 && (src.pixFmt ?? "").StartsWith("yuva"),
                            movAudio, pixels);
                    }
                case "prores422hq":
                    return ProRes(id, "ProRes 422 HQ", 3, false, movAudio, pixels);
                case "h264":
                    return new Format
                    {
                        Id = id, Label = "H.264 (review)", Extension = ".mp4",
                        Codec = "-c:v libx264 -preset fast -crf 18", PixFmt = "yuv420p",
                        AudioCodec = hasAudio ? "-c:a aac -b:a 320k" : "", BytesPerFrame = 6e5 * pixels
                    };
                default:
                    return null;
            }
        }

        private static Format ProRes(string id, string label, int profile, bool alpha, string audio, double pixels)
        {
            return new Format
            {
                Id = id, Label = label, Extension = ".mov",
                Codec = $"-c:v prores_ks -profile:v {profile} -vendor apl0",
                PixFmt = profile >= 4 ? (alpha ? "yuva444p10le" : "yuv444p10le") : "yuv422p10le",
                AudioCodec = audio,
                BytesPerFrame = kProResMBPer4K[profile] * 1e6 * pixels,
            };
        }

        // -1 if the source isn't ProRes (or its profile isn't recognised).
        private static int SourceProResProfile(PostProductionClip.SourceInfo src)
        {
            if (src.codec != "prores") { return -1; }
            switch ((src.profile ?? "").Replace(" ", "").ToUpperInvariant())
            {
                case "PROXY": return 0;
                case "LT": return 1;
                case "STANDARD": return 2;
                case "HQ": return 3;
                case "4444": return 4;
                case "XQ":
                case "4444XQ": return 5;
                default: return -1;
            }
        }

        // ffprobe's colour names, as ffmpeg's scale filter wants the matrix. Unknown -> BT.709,
        // the usual assumption for HD and larger.
        private static string ScaleMatrix(string colorSpace)
        {
            switch (colorSpace)
            {
                case "smpte170m":
                case "bt470bg": return "bt601";
                case "smpte240m": return "smpte240m";
                case "fcc": return "fcc";
                case "bt2020nc":
                case "bt2020c": return "bt2020";
                default: return "bt709";
            }
        }

        // The source's timecode advanced to the first exported frame, or "" if the source has
        // none or uses drop-frame timecode (not handled yet).
        private static string StartTimecode(string sourceTimecode, double fps, int frameOffset)
        {
            if (string.IsNullOrEmpty(sourceTimecode) || sourceTimecode.Contains(";")) { return ""; }
            var parts = sourceTimecode.Split(':');
            if (parts.Length != 4) { return ""; }
            int rate = (int)Math.Round(fps);
            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m) ||
                !int.TryParse(parts[2], out int s) || !int.TryParse(parts[3], out int f)) { return ""; }
            long total = (((long)h * 60 + m) * 60 + s) * rate + f + frameOffset;
            long frames = total % rate;
            long seconds = total / rate;
            return $"{(seconds / 3600) % 24:D2}:{(seconds / 60) % 60:D2}:{seconds % 60:D2}:{frames:D2}";
        }

        // Rendered frames allowed to wait for the encoder (~35MB each at 4096x2160).
        private const int kMaxBufferedFrames = 4;
        private const int kDepthLookahead = 8;

        public static State CurrentState { get; private set; } = State.Idle;
        public static bool IsRunning => CurrentState == State.Running;

        // Editor Play sessions keep static state (no domain reload). Leaving Play mode
        // cancels a running export (OnQuit), but its cleanup runs on the main thread in a
        // later frame that never comes, so it is finished here and the next session starts
        // idle, as a build does.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_Generation++;
            StopProcesses();
            s_ReadySignal.Set();
            if (s_Writer != null && s_Writer.IsAlive) { s_Writer.Join(500); }
            s_Writer = null;
            if (s_NeedsCleanup && CurrentState != State.Done)
            {
                try { if (File.Exists(s_Output)) { File.Delete(s_Output); } }
                catch (Exception e) { Debug.LogWarning($"[PostProdExport] could not delete {s_Output}: {e.Message}"); }
            }
            s_NeedsCleanup = false;
            // Not scene objects, so they outlive the previous session unless destroyed.
            foreach (var rt in new[] { s_CgColor, s_CgDepth, s_CgResolved, s_Out })
            {
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            }
            s_CgColor = s_CgDepth = s_CgResolved = s_Out = null;
            if (s_PlateTex != null) { UnityEngine.Object.DestroyImmediate(s_PlateTex); }
            if (s_PlateDepthTex != null) { UnityEngine.Object.DestroyImmediate(s_PlateDepthTex); }
            s_PlateTex = s_PlateDepthTex = null;
            s_PendingPlate = null;
            s_Ready.Clear();
            while (s_FramePool.TryTake(out _)) { }
            lock (s_DepthLock)
            {
                s_Depth.Clear();
                s_DepthInFlight.Clear();
                s_DepthPool.Clear();
            }
            s_FpsSamples.Clear();
            s_Clock.Reset();
            s_Decoder = s_Encoder = null;
            s_Format = null;
            s_First = s_Last = s_Total = 0;
            s_NextFrame = 0;
            s_StagedFrame = -1;
            s_Rendered = s_Written = 0;
            s_ReadbacksInFlight = 0;
            s_Waiting = s_Error = s_Output = s_StartError = s_Timecode = "";
            s_ElapsedAtEnd = 0;
            s_Fps = 0;
            s_DepthDir = null;
            s_DepthNote = "";
            s_DepthFirst = s_DepthLast = s_DepthStride = 0;
            s_DepthWidth = s_DepthHeight = 0;
            s_FreeAtStart = 0;
            CurrentState = State.Idle;
        }

        // Commands that would change what the export renders (seeks, playback, offsets,
        // layers, camera, lens, bloom, the range, loading) call this first: a change landing
        // between the export's seek and its render would silently put the wrong strokes in a
        // frame, and anything else would change the rest of the export partway through.
        public static void ThrowIfExporting(string command)
        {
            if (IsRunning)
            {
                throw new InvalidOperationException(
                    $"{command}: an export is in progress; cancel it or wait for it to finish");
            }
        }

        private static Format s_Format;
        private static int s_First, s_Last, s_Total;
        private static int s_NextFrame, s_StagedFrame = -1;
        private static volatile int s_Rendered, s_Written;
        private static int s_ReadbacksInFlight;
        private static volatile int s_Generation;
        private static volatile string s_Waiting = "", s_Error = "", s_Output = "";
        private static bool s_NeedsCleanup;
        private static double s_RestoreSketchTime;
        private static readonly System.Diagnostics.Stopwatch s_Clock = new System.Diagnostics.Stopwatch();
        private static double s_ElapsedAtEnd;
        private static volatile float s_Fps;
        private static readonly Queue<KeyValuePair<double, int>> s_FpsSamples = new Queue<KeyValuePair<double, int>>();

        private static FfmpegPipe s_Decoder, s_Encoder;
        private static Bytable s_PendingPlate;
        private static bool s_HighPrecision;
        private static Thread s_Writer;
        private static readonly ConcurrentDictionary<int, byte[]> s_Ready = new ConcurrentDictionary<int, byte[]>();
        private static readonly AutoResetEvent s_ReadySignal = new AutoResetEvent(false);
        private static readonly ConcurrentBag<byte[]> s_FramePool = new ConcurrentBag<byte[]>();

        private static string s_DepthDir;
        // Which depth set the export uses, for the status and log.
        private static volatile string s_DepthNote = "";
        private static int s_DepthFirst, s_DepthLast, s_DepthStride;
        private static readonly object s_DepthLock = new object();
        private static readonly Dictionary<int, float[]> s_Depth = new Dictionary<int, float[]>();
        private static readonly HashSet<int> s_DepthInFlight = new HashSet<int>();
        private static readonly Stack<float[]> s_DepthPool = new Stack<float[]>();
        private static int s_DepthWidth, s_DepthHeight;

        private static RenderTexture s_CgColor, s_CgDepth, s_CgResolved, s_Out;
        private static Texture2D s_PlateTex, s_PlateDepthTex;

        // Why the last Start failed (empty if it didn't), shown in Status: an API command's
        // error never reaches the page.
        private static volatile string s_StartError = "";

        // Returns null on success, otherwise why the export could not start.
        public static string Start(string formatId)
        {
            string error = TryStart(formatId);
            s_StartError = error ?? "";
            if (error != null) { Debug.LogWarning("[PostProdExport] could not start: " + error); }
            return error;
        }

        private static string TryStart(string formatId)
        {
            if (IsRunning) { return "an export is already running"; }
            if (s_NeedsCleanup) { Cleanup(); }
            if (!SketchMemoryScript.m_Instance.IsPlayingBack)
            {
                return "no playback timeline: load the sketch first (with sketch.scrub=1)";
            }
            if (!PostProductionClip.IsLoaded) { return "no clip loaded (postprod.clip.load)"; }
            var src = PostProductionClip.Source;
            var format = BuildFormat(formatId, src);
            if (format == null) { return $"unknown format \"{formatId}\" (formats: {FormatList})"; }
            string video = PostProductionClip.SourceVideo;
            if (!File.Exists(video)) { return $"source video not found: {video}"; }
            if (!ScreenshotManager.UseCalibratedProjection || !PostProductionLens.HasCalibration ||
                !PostProductionPreviewController.HasTargetPose)
            {
                return "camera, lens or pose not set (they come from the clip)";
            }
            int? start = PostProductionSettings.CropStartFrame, end = PostProductionSettings.CropEndFrame;
            if (start == null || end == null) { return "set the trim start and end frames first"; }
            if (end < start) { return $"trim end {end} is before trim start {start}"; }
            if (PostProductionSettings.C006FrameToSketchTimeMs(start.Value) < 0)
            {
                return "trim start maps to a sketch time before zero; move the trim start later or change the global time offset";
            }

            // The clip's export depth set if it has one (e.g. full resolution, every frame),
            // otherwise the preview's (depth sets with roles, 2026-10-25).
            string depthSet = PostProductionClip.PreviewDepthDir;
            string exportSet = PostProductionClip.ExportDepthDir;
            s_DepthNote = $"depth {depthSet} (the preview's; the clip has no export depth set)";
            if (exportSet != "")
            {
                if (File.Exists(Path.Combine(PostProductionClip.FullDir, exportSet, "manifest.json")))
                {
                    depthSet = exportSet;
                    s_DepthNote = $"depth {exportSet}";
                }
                else
                {
                    s_DepthNote = $"depth {depthSet} (the preview's: export depth set {exportSet} not found)";
                }
            }
            s_DepthDir = Path.Combine(PostProductionClip.FullDir, depthSet);
            try
            {
                DepthFrameResolver.ReadRange(s_DepthDir, out s_DepthFirst, out s_DepthLast, out s_DepthStride);
            }
            catch (Exception e)
            {
                return $"could not read {s_DepthDir}/manifest.json: {e.Message}";
            }
            if (start < s_DepthFirst || end > s_DepthLast)
            {
                return $"trim {start}-{end} is outside the depth frames {s_DepthFirst}-{s_DepthLast}";
            }

            int width = Mathf.RoundToInt(ScreenshotManager.CalibratedRefWidth);
            int height = Mathf.RoundToInt(ScreenshotManager.CalibratedRefHeight);
            if (width != PostProductionClip.SourceWidth || height != PostProductionClip.SourceHeight)
            {
                return $"calibration is {width}x{height} but the source video is " +
                    $"{PostProductionClip.SourceWidth}x{PostProductionClip.SourceHeight}";
            }
            double fps = PostProductionSettings.SourceFps;
            int total = end.Value - start.Value + 1;

            string outDir = Path.Combine(PostProductionClip.FullDir, "exports");
            string sketch = string.IsNullOrEmpty(PostProductionSettings.LoadedSketchName)
                ? "sketch" : PostProductionSettings.LoadedSketchName;
            // GetFullPath: the clip folder is stored as "PostProd/C006", and a mixed-slash path
            // isn't accepted everywhere in Windows.
            string output = Path.GetFullPath(Path.Combine(outDir,
                $"{sketch}_{start.Value:D5}-{end.Value:D5}_{format.Id}_{DateTime.Now:yyyyMMdd-HHmmss}{format.Extension}"));
            try
            {
                Directory.CreateDirectory(outDir);
                long free = FreeBytes(outDir);
                double needed = total * format.BytesPerFrame * 1.2;
                if (free < needed)
                {
                    return $"not enough space for {outDir}: about {needed / 1e9:F1}GB needed, " +
                        $"{free / 1e9:F1}GB free";
                }
                s_FreeAtStart = free;
            }
            catch (Exception e)
            {
                return $"could not prepare {outDir}: {e.Message}";
            }

            // Quality exports retain more than eight bits through decode, compositing and readback.
            s_HighPrecision = format.Id != "h264";
            if (s_HighPrecision && (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBA64) ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)))
            {
                return "quality export requires RGBA64 textures and floating-point render targets on this GPU";
            }
            string pipePixelFormat = s_HighPrecision ? "rgba64le" : "rgba";
            TextureFormat plateFormat = s_HighPrecision ? TextureFormat.RGBA64 : TextureFormat.RGBA32;
            RenderTextureFormat colorFormat = s_HighPrecision ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32;

            string fpsArg = fps.ToString(CultureInfo.InvariantCulture);
            var inv = CultureInfo.InvariantCulture;
            // Colour follows the source both ways: its YUV matrix and range for decoding and
            // re-encoding, and its own tags on the output (only those it has; BT.709, limited
            // range when it says nothing). setparams + write_colr are what actually get tags
            // into a .mov with this ffmpeg (checked 2026-10-24); the -color_* options alone
            // did not.
            string matrix = ScaleMatrix(src.colorSpace);
            string range = src.colorRange == "pc" ? "pc" : "tv";
            string colorSpace = string.IsNullOrEmpty(src.colorSpace) ? "bt709" : src.colorSpace;
            var tagParams = new List<string> { $"colorspace={colorSpace}", $"range={range}" };
            string tagOptions = $"-colorspace {colorSpace} -color_range {range} ";
            if (!string.IsNullOrEmpty(src.colorPrimaries) && src.colorPrimaries != "unknown")
            {
                tagParams.Add($"color_primaries={src.colorPrimaries}");
                tagOptions += $"-color_primaries {src.colorPrimaries} ";
            }
            if (!string.IsNullOrEmpty(src.colorTransfer) && src.colorTransfer != "unknown")
            {
                tagParams.Add($"color_trc={src.colorTransfer}");
                tagOptions += $"-color_trc {src.colorTransfer} ";
            }
            // The source timecode of the first exported frame, so the export lines up with the
            // original clip in the editor.
            s_Timecode = StartTimecode(src.timecode, fps, start.Value);
            // The source's audio for exactly the exported frames rides along as a second input.
            bool audio = !string.IsNullOrEmpty(format.AudioCodec);
            // Unity's readback rows are bottom-up, hence vflip.
            string encodeArgs =
                $"-v error -nostats -y -f rawvideo -pix_fmt {pipePixelFormat} -s {width}x{height} -r {fpsArg} -i pipe:0 " +
                (audio ? $"-ss {(start.Value / fps).ToString("F6", inv)} -t {(total / fps).ToString("F6", inv)} " +
                    $"-i \"{video}\" -map 0:v -map 1:a " : "-map 0:v ") +
                $"-vf \"vflip,scale=out_color_matrix={matrix}:out_range={range},format={format.PixFmt}," +
                $"setparams={string.Join(":", tagParams)}\" " +
                $"{format.Codec} -pix_fmt {format.PixFmt} {format.AudioCodec} {tagOptions}" +
                (s_Timecode != "" ? $"-timecode {s_Timecode} " : "") +
                (format.Extension == ".mov" ? "-movflags +write_colr " : "") + $"\"{output}\"";
            // Seek half a frame early: ffmpeg keeps frames from the seek time on, so seeking to
            // a frame's exact time can land on the next one after rounding.
            double seek = (start.Value - 0.5) / fps;
            string decodeArgs =
                $"-v error -nostats -ss {seek.ToString("F6", inv)} -i \"{video}\" " +
                $"-frames:v {total} -an -sn -dn " +
                $"-vf \"scale=in_color_matrix={matrix}:in_range={range}:out_range=pc,format={pipePixelFormat},vflip\" " +
                $"-f rawvideo -pix_fmt {pipePixelFormat} pipe:1";

            s_Generation++;
            s_Format = format;
            s_First = start.Value;
            s_Last = end.Value;
            s_Total = total;
            s_NextFrame = s_First;
            s_StagedFrame = -1;
            s_Rendered = s_Written = 0;
            s_ReadbacksInFlight = 0;
            s_Waiting = s_Error = "";
            s_Output = output;
            s_Fps = 0;
            s_FpsSamples.Clear();
            s_Ready.Clear();
            s_PendingPlate = null;

            s_Encoder = new FfmpegPipe { ReleaseFrame = b => s_FramePool.Add((byte[])b.GetArray()) };
            if (!s_Encoder.Start("pipe:0", output, width, height, (float)fps, true, encodeArgs))
            {
                return "could not start the ffmpeg encoder (see the Unity log)";
            }
            s_Decoder = new FfmpegPipe();
            if (!s_Decoder.Start(video, "pipe:1", width, height, (float)fps, false, decodeArgs, rawVideoBytesPerPixel: s_HighPrecision ? 8 : 4))
            {
                StopProcesses();
                return "could not start the ffmpeg decoder (see the Unity log)";
            }

            // Keep encoded depth byte-based; quality colour targets retain source precision.
            int workW = PostProductionLens.WorkWidth, workH = PostProductionLens.WorkHeight;
            s_CgColor = MakeTarget(workW, workH, 24, 4, RenderTextureReadWrite.Default, "PostProdExportCgColor", colorFormat);
            s_CgDepth = MakeTarget(workW, workH, 24, 1, RenderTextureReadWrite.Linear, "PostProdExportCgDepth");
            s_CgResolved = MakeTarget(workW, workH, 0, 1, RenderTextureReadWrite.Default, "PostProdExportCgResolved", colorFormat);
            s_CgResolved.filterMode = FilterMode.Bilinear;
            s_Out = MakeTarget(width, height, 0, 1, RenderTextureReadWrite.Default, "PostProdExportOut", colorFormat);
            s_PlateTex = new Texture2D(width, height, plateFormat, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            s_RestoreSketchTime = App.Instance.CurrentSketchTime;
            PostProductionPreviewController.SetShuttleRate(0f);
            s_Writer = new Thread(WriterLoop) { IsBackground = true, Name = "PostProdExportWriter" };
            s_Writer.Start(s_Generation);
            Application.quitting -= OnQuit;
            Application.quitting += OnQuit;
            s_Clock.Restart();
            CurrentState = State.Running;
            Debug.Log($"[PostProdExport] started: {format.Label}, frames {s_First}-{s_Last} ({total}), " +
                $"{width}x{height} @ {fpsArg}fps, working frame {PostProductionLens.WorkWidth}x{PostProductionLens.WorkHeight}, " +
                $"{s_FreeAtStart / 1e9:F1}GB free, timecode {(s_Timecode != "" ? s_Timecode : "none")}, {s_DepthNote}, " +
                $"depth filter {(PostProductionSettings.DepthNearest ? "nearest" : "bilinear")} -> {output}\n" +
                $"encode: ffmpeg {encodeArgs}");
            return null;
        }

        // Shows the last export in Explorer (selected), or the clip's exports folder if there
        // is no finished file. The web page can't open local folders itself.
        public static void Reveal()
        {
            string path = s_Output;
            bool haveFile = CurrentState == State.Done && !string.IsNullOrEmpty(path) && File.Exists(path);
            string folder = haveFile ? null : Path.GetFullPath(Path.Combine(PostProductionClip.FullDir, "exports"));
            if (!haveFile && (string.IsNullOrEmpty(PostProductionClip.FullDir) || !Directory.Exists(folder)))
            {
                throw new InvalidOperationException("no export or exports folder to show yet");
            }
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", haveFile ? $"/select,\"{path}\"" : $"\"{folder}\"");
#else
            throw new NotSupportedException("showing the export folder is only implemented on Windows");
#endif
        }

        public static void Cancel()
        {
            if (!IsRunning) { return; }
            EndWith(State.Cancelled, "");
        }

        // Main thread, early in Update: picks the next frame to render once its plate and
        // depth are ready and the encoder has room, and seeks the sketch to it.
        public static void Stage()
        {
            if (!IsRunning) { return; }
            PrefetchDepth();
            if (s_StagedFrame >= 0 || s_NextFrame > s_Last) { return; }
            if (s_ReadbacksInFlight + s_Ready.Count >= kMaxBufferedFrames) { s_Waiting = "encoder"; return; }
            if (s_PendingPlate == null)
            {
                s_Decoder.GetFrame(ref s_PendingPlate);
                if (s_PendingPlate == null)
                {
                    if (s_Decoder.DidExit)
                    {
                        Fail($"the video decoder stopped before frame {s_NextFrame}");
                    }
                    else
                    {
                        s_Waiting = "video decode";
                    }
                    return;
                }
            }
            int depthFrame = DepthFrameFor(s_NextFrame);
            lock (s_DepthLock)
            {
                if (!s_Depth.ContainsKey(depthFrame)) { s_Waiting = "depth decode"; return; }
            }
            App.Instance.CurrentSketchTime = PostProductionSettings.C006FrameToSketchTimeMs(s_NextFrame) / 1000.0;
            s_StagedFrame = s_NextFrame;
            s_Waiting = "";
        }

        // Main thread, LateUpdate (after stroke playback has applied this frame's seek).
        public static void Render()
        {
            if (s_NeedsCleanup && !IsRunning) { Cleanup(); return; }
            if (!IsRunning) { return; }
            UpdateFps();
            if (s_StagedFrame < 0) { return; }

            var plate = (byte[])s_PendingPlate.GetArray();
            s_PlateTex.SetPixelData(plate, 0);
            s_PlateTex.Apply(false);
            s_PendingPlate = null;

            int depthFrame = DepthFrameFor(s_StagedFrame);
            lock (s_DepthLock)
            {
                var pixels = s_Depth[depthFrame];
                if (s_PlateDepthTex == null)
                {
                    // Bilinear, so the compositor can sample either way: it snaps to the
                    // nearest texel unless the depth filter is bilinear.
                    s_PlateDepthTex = new Texture2D(s_DepthWidth, s_DepthHeight, TextureFormat.RFloat, false, true)
                    {
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                    };
                }
                s_PlateDepthTex.SetPixelData(pixels, 0);
            }
            s_PlateDepthTex.Apply(false);

            if (!PostProductionLiveCg.RenderForExport(s_CgColor, s_CgDepth, true,
                PostProductionPreviewController.TargetPosition, PostProductionPreviewController.TargetRotation,
                out float farClipMetres))
            {
                Fail("the snapshot camera rig was not found");
                return;
            }
            Graphics.Blit(s_CgColor, s_CgResolved); // resolves MSAA; sampled bilinearly
            PostProdCompositor.CompositeInto(s_Out, s_CgResolved, s_CgDepth, s_PlateTex, s_PlateDepthTex,
                naiveMode: false, cgDepthEncoded: true, cgFarClipMetres: farClipMetres,
                undistortPlate: false, distortCg: true);

            int index = s_StagedFrame - s_First;
            int generation = s_Generation;
            s_ReadbacksInFlight++;
            AsyncGPUReadback.Request(s_Out, 0, s_HighPrecision ? TextureFormat.RGBA64 : TextureFormat.RGBA32, request => OnReadback(request, index, generation));
            s_StagedFrame = -1;
            s_NextFrame++;
            s_Rendered++;
            EvictDepthBefore(DepthFrameFor(Mathf.Min(s_NextFrame, s_Last)));
        }

        private static void OnReadback(AsyncGPUReadbackRequest request, int index, int generation)
        {
            if (generation != s_Generation) { return; }
            s_ReadbacksInFlight--;
            if (request.hasError)
            {
                Fail($"GPU readback failed at frame {s_First + index}");
                return;
            }
            var data = request.GetData<byte>();
            if (!s_FramePool.TryTake(out var frame) || frame.Length != data.Length)
            {
                frame = new byte[data.Length];
            }
            data.CopyTo(frame);
            s_Ready[index] = frame;
            s_ReadySignal.Set();
        }

        // Writer thread: hands frames to ffmpeg strictly in order, then closes its input so
        // it finishes the file.
        private static void WriterLoop(object generationObj)
        {
            int generation = (int)generationObj;
            var encoder = s_Encoder;
            int next = 0;
            try
            {
                while (next < s_Total)
                {
                    if (generation != s_Generation) { return; }
                    if (encoder.HasFailed)
                    {
                        Fail($"ffmpeg stopped accepting frames after {encoder.FrameCount}: " +
                            (encoder.FailureMessage ?? "the encoder exited") + " (see the Unity log)");
                        return;
                    }
                    if (s_Ready.TryRemove(next, out var frame))
                    {
                        encoder.QueueFrame(new BytesBytable(frame)); // waits while the previous frame is written
                        next++;
                        s_Written = encoder.FrameCount;
                    }
                    else
                    {
                        s_ReadySignal.WaitOne(100);
                        s_Written = encoder.FrameCount;
                    }
                }
                // Every frame handed over must reach ffmpeg; never wait forever if one didn't.
                var drain = System.Diagnostics.Stopwatch.StartNew();
                while (encoder.FrameCount < s_Total && generation == s_Generation && !encoder.HasFailed &&
                    drain.Elapsed.TotalSeconds < 60)
                {
                    Thread.Sleep(10);
                }
                if (generation != s_Generation) { return; }
                if (encoder.FrameCount < s_Total)
                {
                    Fail($"the encoder received only {encoder.FrameCount} of {s_Total} frames");
                    return;
                }
                s_Written = encoder.FrameCount;
                encoder.Stop();
                bool exited = encoder.WaitForEncoderExit(120000);
                if (generation != s_Generation) { return; }
                if (!exited) { Fail("ffmpeg did not finish writing the file within 2 minutes"); return; }
                if (encoder.ExitCode != 0)
                {
                    Fail($"ffmpeg failed with exit code {encoder.ExitCode} (see the Unity log)");
                    return;
                }
                if (!File.Exists(s_Output) || new FileInfo(s_Output).Length == 0)
                {
                    Fail("ffmpeg exited without writing the file (see the Unity log)");
                    return;
                }
                EndWith(State.Done, "");
            }
            catch (Exception e)
            {
                if (generation == s_Generation) { Fail($"writer: {e.GetType().Name}: {e.Message}"); }
            }
        }

        private static void Fail(string error) => EndWith(State.Failed, error);

        // Any thread. GPU objects are released on the main thread, in Cleanup.
        private static void EndWith(State state, string error)
        {
            if (!IsRunning) { return; }
            s_Generation++;
            s_ElapsedAtEnd = s_Clock.Elapsed.TotalSeconds;
            s_Error = error;
            if (state != State.Done) { StopProcesses(); }
            CurrentState = state;
            s_NeedsCleanup = true;
            s_ReadySignal.Set();
            Debug.Log($"[PostProdExport] {state}{(error != "" ? ": " + error : "")} after {s_Written}/{s_Total} frames, " +
                $"{s_ElapsedAtEnd:F1}s -> {s_Output}");
        }

        private static void StopProcesses()
        {
            // WaitForEncoderExit(0) kills a process that hasn't exited.
            if (s_Decoder != null) { s_Decoder.Stop(); s_Decoder.WaitForEncoderExit(0); }
            if (s_Encoder != null) { s_Encoder.Stop(); s_Encoder.WaitForEncoderExit(0); }
        }

        private static void OnQuit()
        {
            if (IsRunning) { EndWith(State.Cancelled, "Open Brush closed"); }
        }

        // Main thread.
        private static void Cleanup()
        {
            s_NeedsCleanup = false;
            Release(ref s_CgColor);
            Release(ref s_CgDepth);
            Release(ref s_CgResolved);
            Release(ref s_Out);
            if (s_PlateTex != null) { UnityEngine.Object.Destroy(s_PlateTex); s_PlateTex = null; }
            if (s_PlateDepthTex != null) { UnityEngine.Object.Destroy(s_PlateDepthTex); s_PlateDepthTex = null; }
            s_PendingPlate = null;
            s_Ready.Clear();
            while (s_FramePool.TryTake(out _)) { }
            lock (s_DepthLock)
            {
                s_Depth.Clear();
                s_DepthPool.Clear();
            }
            s_Decoder = s_Encoder = null;
            if (CurrentState != State.Done)
            {
                try { if (File.Exists(s_Output)) { File.Delete(s_Output); } }
                catch (Exception e) { Debug.LogWarning($"[PostProdExport] could not delete {s_Output}: {e.Message}"); }
            }
            // Back to where the user was, held still.
            if (SketchMemoryScript.m_Instance.IsPlayingBack)
            {
                App.Instance.CurrentSketchTime = s_RestoreSketchTime;
                PostProductionPreviewController.SetShuttleRate(0f);
            }
        }

        private static long s_FreeAtStart;
        private static volatile string s_Timecode = "";

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode,
            SetLastError = true)]
        private static extern bool GetDiskFreeSpaceEx(string directory, out ulong freeToCaller,
            out ulong totalBytes, out ulong totalFree);
#endif

        // Free space where the directory really is. The clip folder is normally reached
        // through a junction under the Media Library (on C:) pointing at another drive, and
        // DriveInfo would report the junction's drive letter; GetDiskFreeSpaceEx follows it.
        private static long FreeBytes(string directory)
        {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            if (GetDiskFreeSpaceEx(Path.GetFullPath(directory) + Path.DirectorySeparatorChar,
                out ulong freeToCaller, out _, out _))
            {
                return (long)freeToCaller;
            }
#endif
            return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(directory))).AvailableFreeSpace;
        }

        private static RenderTexture MakeTarget(int width, int height, int depthBits, int antiAliasing,
            RenderTextureReadWrite readWrite, string name, RenderTextureFormat format = RenderTextureFormat.ARGB32)
        {
            var rt = new RenderTexture(width, height, depthBits, format, readWrite)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = antiAliasing,
                name = name,
            };
            rt.Create();
            return rt;
        }

        private static void Release(ref RenderTexture rt)
        {
            if (rt == null) { return; }
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            rt = null;
        }

        private static int DepthFrameFor(int frame)
        {
            DepthFrameResolver.Snap(frame, s_DepthFirst, s_DepthLast, s_DepthStride, out int resolved);
            return resolved;
        }

        private static void PrefetchDepth()
        {
            int d = DepthFrameFor(s_NextFrame);
            int last = DepthFrameFor(s_Last);
            for (int n = 0; n < kDepthLookahead && d <= last; n++, d += s_DepthStride)
            {
                int frame = d;
                lock (s_DepthLock)
                {
                    if (s_Depth.ContainsKey(frame) || s_DepthInFlight.Contains(frame)) { continue; }
                    s_DepthInFlight.Add(frame);
                }
                string path = DepthFrameResolver.DepthPath(s_DepthDir, frame);
                int generation = s_Generation;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        // Rows come back bottom row first, ready for SetPixelData.
                        float[] pixels = ExrDepthReader.ReadSingleChannelFloatPooled(path, RentDepth, out int w, out int h);
                        lock (s_DepthLock)
                        {
                            if (generation == s_Generation)
                            {
                                s_DepthWidth = w;
                                s_DepthHeight = h;
                                s_Depth[frame] = pixels;
                            }
                            else
                            {
                                s_DepthPool.Push(pixels);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (generation == s_Generation) { Fail($"depth frame {frame}: {e.Message}"); }
                    }
                    finally
                    {
                        lock (s_DepthLock) { s_DepthInFlight.Remove(frame); }
                    }
                });
            }
        }

        private static float[] RentDepth(int length)
        {
            lock (s_DepthLock)
            {
                while (s_DepthPool.Count > 0)
                {
                    float[] pooled = s_DepthPool.Pop();
                    if (pooled.Length == length) { return pooled; }
                }
            }
            return new float[length];
        }

        // Frames go forward only, so depth frames before the next one are finished with.
        private static void EvictDepthBefore(int depthFrame)
        {
            lock (s_DepthLock)
            {
                var done = new List<int>();
                foreach (int f in s_Depth.Keys) { if (f < depthFrame) { done.Add(f); } }
                foreach (int f in done)
                {
                    s_DepthPool.Push(s_Depth[f]);
                    s_Depth.Remove(f);
                }
            }
        }

        private static void UpdateFps()
        {
            double now = s_Clock.Elapsed.TotalSeconds;
            s_FpsSamples.Enqueue(new KeyValuePair<double, int>(now, s_Written));
            while (s_FpsSamples.Count > 2 && now - s_FpsSamples.Peek().Key > 5.0) { s_FpsSamples.Dequeue(); }
            var oldest = s_FpsSamples.Peek();
            double span = now - oldest.Key;
            s_Fps = span > 0.5 ? (float)((s_Written - oldest.Value) / span) : 0f;
        }

        // The latest exported frame (already on the GPU), for the Game window while exporting.
        public static Texture DisplayTexture => IsRunning && s_Rendered > 0 ? s_Out : null;

        public static string OverlayText()
        {
            float fps = s_Fps;
            double eta = fps > 0 ? (s_Total - s_Written) / fps : 0;
            return $"Exporting {s_Format?.Label}   {s_Written:N0} / {s_Total:N0}   {fps:F1} fps   " +
                (fps > 0 ? $"{Duration(eta)} left" : "estimating time left") +
                "\nEditing is locked until the export finishes or is cancelled.";
        }

        // Safe from the HTTP thread (plain fields only). First line is machine-readable
        // key=value pairs for the control page; the second is for people.
        public static string Status()
        {
            var state = CurrentState;
            int done = state == State.Done ? s_Total : s_Written;
            double elapsed = state == State.Running ? s_Clock.Elapsed.TotalSeconds : s_ElapsedAtEnd;
            float fps = state == State.Running ? s_Fps : (elapsed > 0 ? (float)(done / elapsed) : 0f);
            double eta = state == State.Running && fps > 0 ? (s_Total - done) / fps : 0;
            var inv = CultureInfo.InvariantCulture;
            string machine = $"state={state} done={done} total={s_Total} fps={fps.ToString("F2", inv)} " +
                $"elapsed={elapsed.ToString("F1", inv)} eta={eta.ToString("F0", inv)}";
            string label = s_Format?.Label ?? "";
            string human;
            switch (state)
            {
                case State.Idle:
                    human = "No export yet. Formats: " + FormatList;
                    break;
                case State.Running:
                    human = $"Exporting {label}: {done} of {s_Total} frames ({(s_Total > 0 ? 100.0 * done / s_Total : 0):F1}%), " +
                        $"{fps:F1} fps, {Duration(elapsed)} elapsed, " +
                        (fps > 0 ? $"about {Duration(eta)} left" : "estimating time left") +
                        $". Rendered {s_Rendered}{(s_Waiting != "" ? ", waiting on " + s_Waiting : "")}, {s_DepthNote}. -> {s_Output}";
                    break;
                case State.Done:
                    human = $"Done: {s_Total} frames of {label} in {Duration(elapsed)} ({fps:F1} fps), " +
                        (s_Timecode != "" ? $"timecode from {s_Timecode}" : "no timecode") + $", {s_DepthNote} -> {s_Output}";
                    break;
                case State.Cancelled:
                    human = $"Cancelled after {done} of {s_Total} frames; partial file deleted.";
                    break;
                default:
                    human = $"Failed after {done} of {s_Total} frames: {s_Error}";
                    break;
            }
            if (s_StartError != "" && state != State.Running)
            {
                human = "Could not start: " + s_StartError + "\n" + human;
            }
            return machine + "\n" + human;
        }

        private static string Duration(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:D2}m" :
                t.TotalMinutes >= 1 ? $"{t.Minutes}m {t.Seconds:D2}s" : $"{t.Seconds}s";
        }
    }
}
