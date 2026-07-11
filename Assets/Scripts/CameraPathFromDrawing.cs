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

using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// <summary>
    /// Converts a stroke drawn with the trigger into a camera path.
    /// Camera orientation is intentionally not taken from the controller; it is
    /// derived from the direction of travel along the drawn curve, so the camera
    /// looks where it is heading and stays level with the world horizon.
    /// </summary>
    public static class CameraPathFromDrawing
    {
        public struct Sample
        {
            public Vector3 position; // Canvas/scene space
            public float timestamp;  // Time.time when the sample was taken
        }

        // Number of neighboring samples (on each side) used to smooth tangents
        // and speeds, to filter out hand jitter.
        const int kSmoothingWindow = 2;
        // Above this |dot(tangent, up)| the heading is too close to vertical for
        // world up to define a stable roll, so the previous frame's up is reused.
        const float kVerticalHeadingDot = 0.95f;

        public static CameraPathWidget CreateCameraPath(List<Sample> samples)
        {
            if (samples == null || samples.Count < 2)
            {
                return null;
            }

            float totalLength = 0f;
            for (int i = 1; i < samples.Count; ++i)
            {
                totalLength += Vector3.Distance(samples[i].position, samples[i - 1].position);
            }
            if (totalLength < 1e-4f)
            {
                return null;
            }

            List<FlyPathRecorder.RecordedFrame> frames = BuildFrames(samples);

            // Scale the simplification threshold with the size of the drawing so
            // short and long strokes both keep a sensible number of knots.
            float threshold = Mathf.Max(0.05f, totalLength / 40f);
            return CameraPathFromFrames.CreateCameraPath(frames, threshold, maxKnots: 50);
        }

        static List<FlyPathRecorder.RecordedFrame> BuildFrames(List<Sample> samples)
        {
            var frames = new List<FlyPathRecorder.RecordedFrame>(samples.Count);

            // World up expressed in canvas coordinates, so the camera stays level
            // with the real horizon once the canvas pose is re-applied at playback.
            Vector3 up = Quaternion.Inverse(App.Scene.Pose.rotation) * Vector3.up;

            Quaternion rotation = Quaternion.identity;
            bool hasRotation = false;
            for (int i = 0; i < samples.Count; ++i)
            {
                Vector3 tangent = SmoothedTangent(samples, i);
                if (tangent.sqrMagnitude > 1e-8f)
                {
                    tangent.Normalize();
                    Vector3 upRef = Mathf.Abs(Vector3.Dot(tangent, up)) < kVerticalHeadingDot
                        ? up
                        : (hasRotation ? rotation * Vector3.up : Vector3.forward);
                    rotation = Quaternion.LookRotation(tangent, upRef);
                    hasRotation = true;
                }
                // Degenerate tangent (e.g. the stroke doubles back on itself
                // exactly): keep the previous rotation.

                frames.Add(new FlyPathRecorder.RecordedFrame(
                    samples[i].position, rotation, samples[i].timestamp,
                    SmoothedSpeed(samples, i)));
            }

            return frames;
        }

        static Vector3 SmoothedTangent(List<Sample> samples, int i)
        {
            int i0 = Mathf.Max(0, i - kSmoothingWindow);
            int i1 = Mathf.Min(samples.Count - 1, i + kSmoothingWindow);
            return samples[i1].position - samples[i0].position;
        }

        static float SmoothedSpeed(List<Sample> samples, int i)
        {
            int i0 = Mathf.Max(0, i - kSmoothingWindow);
            int i1 = Mathf.Min(samples.Count - 1, i + kSmoothingWindow);
            float dt = samples[i1].timestamp - samples[i0].timestamp;
            if (dt <= 0f)
            {
                return 0f;
            }
            float distance = 0f;
            for (int j = i0; j < i1; ++j)
            {
                distance += Vector3.Distance(samples[j + 1].position, samples[j].position);
            }
            return distance / dt;
        }
    }
}
