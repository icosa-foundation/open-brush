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
    // Shared depth_frames/manifest.json reading and stride-snapping logic (see
    // POSTPROD_PREVIEW_PLAN.md's frame-naming rework, 2026-09-08): filenames are the
    // true C006 video frame number directly (frame_02950_depth.exr = frame 2950), and
    // a requested frame is snapped to the nearest one actually on disk within
    // [firstFrame, lastFrame] at the manifest's stride. Used by
    // PostProductionPreviewController's auto-plate-sync (stage 2); kept separate from
    // PostProductionMediaLoader's own copy of this logic (SeekToC006Frame) rather than
    // introducing a dependency between those two otherwise-independent classes.
    public static class DepthFrameResolver
    {
        [Serializable]
        private class Manifest
        {
            public int firstFrame;
            public int lastFrame;
            public int stride;
        }

        // Returns false if requestedFrame is genuinely outside [firstFrame, lastFrame]
        // (resolvedFrame is still the clamped-and-snapped nearest frame in that case,
        // for callers that want a best-effort fallback rather than nothing).
        public static bool TryResolve(string depthFramesDir, int requestedFrame, out int resolvedFrame)
        {
            ReadRange(depthFramesDir, out int first, out int last, out int stride);
            return Snap(requestedFrame, first, last, stride, out resolvedFrame);
        }

        // For callers resolving many frames (the export): read the manifest once, then Snap.
        public static void ReadRange(string depthFramesDir, out int firstFrame, out int lastFrame, out int stride)
        {
            string manifestPath = Path.Combine(depthFramesDir, "manifest.json");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            firstFrame = manifest.firstFrame;
            lastFrame = manifest.lastFrame;
            stride = Mathf.Max(1, manifest.stride);
        }

        public static bool Snap(int requestedFrame, int firstFrame, int lastFrame, int stride, out int resolvedFrame)
        {
            bool inRange = requestedFrame >= firstFrame && requestedFrame <= lastFrame;
            int clamped = Mathf.Clamp(requestedFrame, firstFrame, lastFrame);
            int offset = clamped - firstFrame;
            int snappedOffset = Mathf.RoundToInt(offset / (float)stride) * stride;
            resolvedFrame = firstFrame + snappedOffset;
            return inRange;
        }

        public static string DepthPath(string depthFramesDir, int frame) =>
            Path.Combine(depthFramesDir, $"frame_{frame:D5}_depth.exr");

        public static string ColorPath(string colorFramesDir, int frame) =>
            Path.Combine(colorFramesDir, $"frame_{frame:D5}_color.png");
    }
}
