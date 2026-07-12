// Copyright 2024 The Open Brush Authors
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
    /// Converts recorded fly path frames into camera paths with splined interpolation
    /// </summary>
    public static class CameraPathFromFrames
    {
        /// <summary>
        /// Create a camera path from recorded frames, simplifying and smoothing the data
        /// </summary>
        /// <param name="frames">Recorded frames from FlyPathRecorder</param>
        /// <param name="simplificationThreshold">Distance threshold for removing intermediate points</param>
        /// <param name="maxKnots">Maximum number of knots to create (0 = no limit)</param>
        /// <returns>Created CameraPathWidget or null if failed</returns>
        public static CameraPathWidget CreateCameraPath(List<FlyPathRecorder.RecordedFrame> frames,
                                                       float simplificationThreshold = 0.5f,
                                                       int maxKnots = 50)
        {
            if (frames == null || frames.Count < 2)
            {
                Debug.LogError("CameraPathFromFrames: Need at least 2 frames to create a camera path");
                return null;
            }

            Debug.Log($"CameraPathFromFrames: Creating camera path from {frames.Count} frames");

            // Simplify the frame data by removing redundant points
            List<FlyPathRecorder.RecordedFrame> simplifiedFrames = SimplifyFrames(frames, simplificationThreshold, maxKnots);
            Debug.Log($"CameraPathFromFrames: Simplified to {simplifiedFrames.Count} key frames");

            CreateCameraPathFromFramesCommand command =
                new CreateCameraPathFromFramesCommand(simplifiedFrames);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);

            CameraPathWidget pathWidget = command.Widget;
            if (pathWidget == null)
            {
                Debug.LogError("CameraPathFromFrames: Failed to create camera path widget");
                return null;
            }

            Debug.Log($"CameraPathFromFrames: Created camera path with {pathWidget.Path.PositionKnots.Count} position knots");

            return pathWidget;
        }

        private static List<FlyPathRecorder.RecordedFrame> SimplifyFrames(List<FlyPathRecorder.RecordedFrame> frames,
                                                                         float threshold,
                                                                         int maxKnots)
        {
            List<FlyPathRecorder.RecordedFrame> simplified = new List<FlyPathRecorder.RecordedFrame>();

            // Always keep the first frame
            simplified.Add(frames[0]);

            int step = maxKnots > 0 ? Mathf.Max(1, frames.Count / maxKnots) : 1;

            for (int i = 1; i < frames.Count - 1; i += step)
            {
                var current = frames[i];
                var last = simplified[simplified.Count - 1];

                // Check if this frame is far enough from the last kept frame
                float distance = Vector3.Distance(current.position, last.position);
                float rotationDiff = Quaternion.Angle(current.rotation, last.rotation);

                if (distance >= threshold || rotationDiff >= 10f) // 10 degrees rotation threshold
                {
                    simplified.Add(current);
                }

                // Limit the number of simplified frames
                if (maxKnots > 0 && simplified.Count >= maxKnots - 1)
                {
                    break;
                }
            }

            // Always keep the last frame
            if (simplified[simplified.Count - 1] != frames[frames.Count - 1])
            {
                simplified.Add(frames[frames.Count - 1]);
            }

            return simplified;
        }
    }
}
