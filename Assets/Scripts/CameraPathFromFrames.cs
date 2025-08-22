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

            // Create the camera path widget
            CameraPathWidget pathWidget = CreateCameraPathWidget();
            if (pathWidget == null)
            {
                Debug.LogError("CameraPathFromFrames: Failed to create camera path widget");
                return null;
            }

            // Create position knots from simplified frames
            CreatePositionKnots(pathWidget, simplifiedFrames);

            // Create rotation knots at key points
            CreateRotationKnots(pathWidget, simplifiedFrames);

            // Create speed knots based on recorded speeds
            CreateSpeedKnots(pathWidget, simplifiedFrames);

            // Refresh the path to build splines
            pathWidget.Path.RefreshPath();
            
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

        private static CameraPathWidget CreateCameraPathWidget()
        {
            // Create a new camera path widget
            GameObject pathGo = new GameObject("RecordedCameraPath");
            CameraPathWidget pathWidget = pathGo.AddComponent<CameraPathWidget>();
            
            // Initialize the widget
            pathWidget.transform.parent = App.Instance.m_WidgetManager.transform;
            
            // Make sure the path has the necessary components
            if (pathWidget.Path == null)
            {
                pathWidget.CreatePath();
            }
            
            return pathWidget;
        }

        private static void CreatePositionKnots(CameraPathWidget pathWidget, List<FlyPathRecorder.RecordedFrame> frames)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                
                // Create position knot
                GameObject knotGo = Object.Instantiate(WidgetManager.m_Instance.CameraPathPositionKnotPrefab);
                CameraPathPositionKnot posKnot = knotGo.GetComponent<CameraPathPositionKnot>();
                
                if (posKnot == null)
                {
                    Debug.LogError("CameraPathFromFrames: Position knot prefab missing CameraPathPositionKnot component");
                    continue;
                }
                
                // Set position in scene space
                TrTransform knotXf_SS = new TrTransform();
                knotXf_SS.translation = frame.position;
                knotXf_SS.rotation = Quaternion.LookRotation(GetDirectionToNext(frames, i), Vector3.up);
                knotXf_SS.scale = 1.0f;
                
                // Convert to local space and set transform
                TrTransform knotXf_LS = Coords.ScenePose.inverse * knotXf_SS;
                knotGo.transform.position = knotXf_LS.translation;
                knotGo.transform.rotation = knotXf_LS.rotation;
                
                // Set tangent magnitude based on distance to next knot
                if (i < frames.Count - 1)
                {
                    float distance = Vector3.Distance(frame.position, frames[i + 1].position);
                    posKnot.TangentMagnitude = distance * 0.3f; // 30% of distance as tangent length
                }
                else
                {
                    posKnot.TangentMagnitude = 1.0f; // Default for last knot
                }
                
                // Add to path
                pathWidget.Path.AddPositionKnot(posKnot);
            }
        }

        private static void CreateRotationKnots(CameraPathWidget pathWidget, List<FlyPathRecorder.RecordedFrame> frames)
        {
            // Create rotation knots at key points (every few position knots to avoid overcomplicating)
            int rotationKnotInterval = Mathf.Max(1, frames.Count / 10); // Up to 10 rotation knots
            
            for (int i = 0; i < frames.Count; i += rotationKnotInterval)
            {
                var frame = frames[i];
                
                GameObject knotGo = Object.Instantiate(WidgetManager.m_Instance.CameraPathRotationKnotPrefab);
                CameraPathRotationKnot rotKnot = knotGo.GetComponent<CameraPathRotationKnot>();
                
                if (rotKnot == null)
                {
                    Debug.LogError("CameraPathFromFrames: Rotation knot prefab missing CameraPathRotationKnot component");
                    continue;
                }
                
                // Set rotation knot at the corresponding path position
                float pathT = (float)i / (frames.Count - 1) * (pathWidget.Path.PositionKnots.Count - 1);
                rotKnot.PathT = new PathT(pathT);
                
                // Set the actual rotation
                TrTransform rotKnotXf_SS = new TrTransform();
                rotKnotXf_SS.translation = frame.position;
                rotKnotXf_SS.rotation = frame.rotation;
                rotKnotXf_SS.scale = 1.0f;
                
                TrTransform rotKnotXf_LS = Coords.ScenePose.inverse * rotKnotXf_SS;
                knotGo.transform.position = rotKnotXf_LS.translation;
                knotGo.transform.rotation = rotKnotXf_LS.rotation;
                
                // Add to path
                pathWidget.Path.AddRotationKnot(rotKnot);
            }
        }

        private static void CreateSpeedKnots(CameraPathWidget pathWidget, List<FlyPathRecorder.RecordedFrame> frames)
        {
            // Create speed knots to preserve the timing of the original flight
            int speedKnotInterval = Mathf.Max(1, frames.Count / 8); // Up to 8 speed knots
            
            for (int i = 0; i < frames.Count; i += speedKnotInterval)
            {
                var frame = frames[i];
                
                GameObject knotGo = Object.Instantiate(WidgetManager.m_Instance.CameraPathSpeedKnotPrefab);
                CameraPathSpeedKnot speedKnot = knotGo.GetComponent<CameraPathSpeedKnot>();
                
                if (speedKnot == null)
                {
                    Debug.LogError("CameraPathFromFrames: Speed knot prefab missing CameraPathSpeedKnot component");
                    continue;
                }
                
                // Set speed knot at the corresponding path position
                float pathT = (float)i / (frames.Count - 1) * (pathWidget.Path.PositionKnots.Count - 1);
                speedKnot.PathT = new PathT(pathT);
                
                // Set speed based on recorded movement speed (normalized to reasonable camera path speeds)
                float normalizedSpeed = Mathf.Clamp(frame.speed * 0.5f, 0.1f, 2.0f); // Scale and clamp speed
                speedKnot.SpeedValue = normalizedSpeed;
                
                // Position the speed knot on the path
                Vector3 pathPos = pathWidget.Path.GetPosition(speedKnot.PathT);
                TrTransform speedKnotXf_SS = new TrTransform();
                speedKnotXf_SS.translation = pathPos;
                speedKnotXf_SS.rotation = Quaternion.identity;
                speedKnotXf_SS.scale = 1.0f;
                
                TrTransform speedKnotXf_LS = Coords.ScenePose.inverse * speedKnotXf_SS;
                knotGo.transform.position = speedKnotXf_LS.translation;
                
                // Add to path
                pathWidget.Path.AddSpeedKnot(speedKnot);
            }
        }

        private static Vector3 GetDirectionToNext(List<FlyPathRecorder.RecordedFrame> frames, int currentIndex)
        {
            if (currentIndex >= frames.Count - 1)
            {
                // For the last frame, use the previous direction
                if (currentIndex > 0)
                {
                    return (frames[currentIndex].position - frames[currentIndex - 1].position).normalized;
                }
                return Vector3.forward; // Fallback
            }
            
            return (frames[currentIndex + 1].position - frames[currentIndex].position).normalized;
        }
    }
}