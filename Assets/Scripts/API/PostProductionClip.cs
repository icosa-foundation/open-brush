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
using System.IO;
using UnityEngine;

namespace TiltBrush
{
    // One-command clip setup (POSTPROD_FAST_PREVIEW_DESIGN.md section 1.1): preprocessing
    // writes <clipDir>/clip.json with the camera calibration, pose, source fps and plate
    // folders, and postprod.clip.load=<clipDir> applies all of it. The sketch stores only
    // the clip folder (PostProductionMetadata.ClipDir), never a copy of these values, so
    // the manifest stays the single source of truth.
    public static class PostProductionClip
    {
        public const string ManifestName = "clip.json";
        public const string DefaultPreviewDir = "preview_1280x676";
        public const string DefaultDepthDir = "depth_frames";

        [Serializable]
        private class Manifest
        {
            public string clip;
            public SourceInfo source;
            public CameraSetup camera;
            public Plates plates;
        }
        // Everything about the source the export matches (2026-10-24): written by
        // make_clip_manifest.py from ffprobe. Older manifests lack all but the first four.
        [Serializable]
        public class SourceInfo
        {
            public string video;
            public int width;
            public int height;
            public float fps;
            public string codec = "";
            public string profile = "";
            public string pixFmt = "";
            public int bitDepth;
            public string colorSpace = "";
            public string colorTransfer = "";
            public string colorPrimaries = "";
            public string colorRange = "";
            public string timecode = "";
            public AudioInfo audio;
        }
        [Serializable]
        public class AudioInfo { public string codec = ""; public int sampleRate; public int channels; }
        [Serializable]
        private class CameraSetup
        {
            public float[] intrinsics; // fx, fy, cx, cy, width, height (px)
            public float[] distortion; // k1, k2, p1, p2, k3 (OpenCV)
            public float[] nearFar;    // Open Brush units
            public float[] position;   // Open Brush units, ChArUco board corner as origin
            public float[] rotation;   // x, y, z, w
        }
        [Serializable]
        private class Plates
        {
            public string preview;
            // The depth set the preview uses, light enough to scrub (e.g. 1280x676, every 2nd
            // frame), and the export's fallback.
            public string depth;
            // Optional: a heavier depth set for the export only (e.g. full resolution, every
            // frame). Any depth set: metres, 0 = no depth, the raw (distorted) camera frame at
            // the source aspect ratio, true source frame numbers, its own manifest.json.
            public string depthExport;
        }

        // Clips live in Open Brush's Media Library under PostProd/ (each one can be a
        // junction to wherever the media really is). A clip dir relative to the Media
        // Library ("PostProd/C006") is what the picker returns and what gets saved, so a
        // sketch keeps working when the media moves; absolute paths are still accepted.
        public const string ClipsFolder = "PostProd";
        public static string ClipsRoot => Path.Combine(App.MediaLibraryPath(), ClipsFolder);

        public static string ResolveDir(string clipDir)
        {
            return Path.IsPathRooted(clipDir) ? clipDir : Path.Combine(App.MediaLibraryPath(), clipDir);
        }

        // Clip folders (with a clip.json) under the Media Library's PostProd/, as
        // "PostProd/<name>", one per line. Plain file I/O, safe from the HTTP thread.
        public static string ListClips()
        {
            if (!Directory.Exists(ClipsRoot)) { return ""; }
            var names = new System.Collections.Generic.List<string>();
            foreach (string dir in Directory.GetDirectories(ClipsRoot))
            {
                if (File.Exists(Path.Combine(dir, ManifestName)))
                {
                    names.Add(ClipsFolder + "/" + Path.GetFileName(dir));
                }
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("\n", names);
        }

        // Sketch names (no .tilt) in Open Brush's Sketches folder, as load.realtime takes them.
        public static string ListSketches()
        {
            string dir = App.UserSketchPath();
            if (!Directory.Exists(dir)) { return ""; }
            var names = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.GetFiles(dir, "*" + SaveLoadScript.TILT_SUFFIX))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }
            names.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("\n", names);
        }

        // As given to Load (relative or absolute); empty when no clip is loaded (a sketch
        // without one uses its own saved camera).
        public static string ClipDir { get; private set; } = "";
        public static bool IsLoaded => !string.IsNullOrEmpty(ClipDir);
        public static string ClipName { get; private set; } = "";
        // Absolute clip folder, and the full-quality source video the export decodes.
        public static string FullDir { get; private set; } = "";
        public static string SourceVideo { get; private set; } = "";
        public static int SourceWidth { get; private set; }
        public static int SourceHeight { get; private set; }
        // The full source description, for the export's "Match source"; null if no clip.
        public static SourceInfo Source { get; private set; }
        public static string PreviewDir { get; private set; } = DefaultPreviewDir;
        // Depth set folders, relative to the clip folder (see Plates).
        public static string PreviewDepthDir { get; private set; } = DefaultDepthDir;
        public static string ExportDepthDir { get; private set; } = "";
        public static volatile string LastStatus = "no clip loaded";

        // Returns null on success, otherwise the error (also in LastStatus).
        public static string Load(string clipDir)
        {
            Manifest m;
            clipDir = (clipDir ?? "").Replace('\\', '/');
            string fullDir = ResolveDir(clipDir);
            string path = Path.Combine(fullDir, ManifestName);
            try
            {
                m = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Clear();
                return LastStatus = $"CLIP OFFLINE: could not read {path}: {e.Message}";
            }
            var cam = m.camera;
            if (m.source == null || m.source.fps <= 0 || cam == null ||
                cam.intrinsics?.Length != 6 || cam.distortion?.Length != 5 ||
                cam.nearFar?.Length != 2 || cam.position?.Length != 3 || cam.rotation?.Length != 4)
            {
                Clear();
                return LastStatus = $"error: {path} is missing source.fps or a camera field";
            }

            // Order matters: the lens working frame is built on the intrinsics.
            var k = cam.intrinsics;
            ScreenshotManager.SetCalibratedIntrinsics(k[0], k[1], k[2], k[3], k[4], k[5]);
            ScreenshotManager.SetCalibratedClip(cam.nearFar[0], cam.nearFar[1]);
            var d = cam.distortion;
            PostProductionLens.SetCalibration(k[0], k[1], k[2], k[3], d[0], d[1], d[2], d[3], d[4]);
            PostProductionPreviewController.SetTargetPose(
                new Vector3(cam.position[0], cam.position[1], cam.position[2]),
                new Quaternion(cam.rotation[0], cam.rotation[1], cam.rotation[2], cam.rotation[3]));
            PostProductionSettings.SourceFps = m.source.fps;

            ClipDir = clipDir;
            FullDir = fullDir;
            SourceVideo = m.source.video ?? "";
            SourceWidth = m.source.width;
            SourceHeight = m.source.height;
            Source = m.source;
            ClipName = string.IsNullOrEmpty(m.clip) ? Path.GetFileName(clipDir) : m.clip;
            PreviewDir = string.IsNullOrEmpty(m.plates?.preview) ? DefaultPreviewDir : m.plates.preview;
            PreviewDepthDir = string.IsNullOrEmpty(m.plates?.depth) ? DefaultDepthDir : m.plates.depth;
            ExportDepthDir = m.plates?.depthExport ?? "";
            PostProductionPreviewController.SetAutoPlateClipDir(fullDir);
            if (PostProductionPlateCache.Enabled)
            {
                string error = PostProductionPlateCache.SetEnabled(true, fullDir);
                if (error != null) { PostProductionPlateCache.LastStatus = "error: " + error; }
            }

            LastStatus = $"clip {ClipName} loaded from {clipDir}: {m.source.width}x{m.source.height} " +
                $"@ {m.source.fps}fps, preview {PreviewDir}, depth {PreviewDepthDir}" +
                (ExportDepthDir != "" ? $", export depth {ExportDepthDir}" : "");
            return null;
        }

        // Editor Play sessions keep static state (no domain reload); start each one with no clip.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Clear();

        public static void Clear()
        {
            ClipDir = "";
            FullDir = "";
            SourceVideo = "";
            SourceWidth = SourceHeight = 0;
            Source = null;
            ClipName = "";
            PreviewDir = DefaultPreviewDir;
            PreviewDepthDir = DefaultDepthDir;
            ExportDepthDir = "";
            LastStatus = "no clip loaded";
        }
    }
}
