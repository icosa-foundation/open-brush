// Copyright 2025 The Open Brush Authors
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
using System.Linq;
using UnityEngine;
namespace TiltBrush
{
    public class StrokeCropping
    {
        public static void CropStrokesToSphere(Vector3 center_ws, float radius_ws)
        {
            if (radius_ws <= 0)
            {
                return;
            }

            Vector3 center = App.Scene.Pose.inverse * center_ws;
            float radius = App.Scene.Pose.inverse.scale * radius_ws;

            var strokesInsideSphere = new HashSet<Stroke>();
            int boundsTestsPassed = 0;
            int boundsTestsFailed = 0;

            // Process all strokes on all canvases
            var allStrokes = SketchMemoryScript.AllStrokes().ToArray();
            foreach (var stroke in allStrokes)
            {
                if (stroke == null)
                {
                    continue;
                }

                var canvas = stroke.Canvas;
                if (canvas == null || stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length == 0)
                {
                    continue;
                }

                var canvasPose = canvas.Pose;
                Vector3 sphereCenterCs = canvasPose.inverse * center;
                float sphereRadiusCs = radius / canvasPose.scale;

                // Fast bounds test: check if stroke's bounding box intersects the sphere
                // This avoids expensive clipping for strokes that are clearly outside
                if (stroke.m_BatchSubset != null)
                {
                    Bounds bounds = stroke.m_BatchSubset.m_Bounds;
                    if (!BoundsIntersectsSphere(bounds, sphereCenterCs, sphereRadiusCs))
                    {
                        boundsTestsFailed++;
                        continue;
                    }
                    boundsTestsPassed++;
                }

                var clippedSegments = ClipStrokeToSphere(stroke.m_ControlPoints, sphereCenterCs, sphereRadiusCs);
                if (clippedSegments.Count == 0)
                {
                    // Stroke is completely outside the sphere - will be deleted
                    continue;
                }

                if (clippedSegments.Count == 1)
                {
                    ApplySegmentToStroke(stroke, clippedSegments[0]);
                    strokesInsideSphere.Add(stroke);
                    continue;
                }

                // Stroke crosses sphere boundary multiple times - split into multiple strokes
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.Uncreate();
                stroke.DestroyStroke();

                for (int i = 0; i < clippedSegments.Count; i++)
                {
                    var newStroke = new Stroke(stroke)
                    {
                        m_ControlPoints = clippedSegments[i],
                        m_ControlPointsToDrop = Enumerable.Repeat(false, clippedSegments[i].Length).ToArray(),
                        m_IntendedCanvas = canvas,
                        m_Type = Stroke.Type.NotCreated
                    };
                    SketchMemoryScript.m_Instance.MemoryListAdd(newStroke);
                    newStroke.Recreate(null, newStroke.Canvas);
                    strokesInsideSphere.Add(newStroke);
                }
            }

            // Now delete all strokes that aren't in strokesInsideSphere
            var allStrokesAfterClipping = SketchMemoryScript.AllStrokes().ToArray();
            int deletedCount = 0;
            for (int i = 0; i < allStrokesAfterClipping.Length; i++)
            {
                var stroke = allStrokesAfterClipping[i];
                if (stroke == null || strokesInsideSphere.Contains(stroke))
                {
                    continue;
                }

                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();
                deletedCount++;
            }
        }

        private static bool BoundsIntersectsSphere(Bounds bounds, Vector3 sphereCenter, float sphereRadius)
        {
            // Find the closest point on the AABB to the sphere center
            Vector3 closestPoint = new Vector3(
                Mathf.Clamp(sphereCenter.x, bounds.min.x, bounds.max.x),
                Mathf.Clamp(sphereCenter.y, bounds.min.y, bounds.max.y),
                Mathf.Clamp(sphereCenter.z, bounds.min.z, bounds.max.z)
            );

            // Check if the closest point is within the sphere
            float distanceSq = (closestPoint - sphereCenter).sqrMagnitude;
            return distanceSq <= sphereRadius * sphereRadius;
        }

        private static void ApplySegmentToStroke(Stroke stroke, PointerManager.ControlPoint[] controlPoints)
        {
            stroke.m_ControlPoints = controlPoints;
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, controlPoints.Length).ToArray();
            stroke.InvalidateCopy();
            stroke.Uncreate();
            stroke.Recreate(null, stroke.Canvas);
        }

        private static List<PointerManager.ControlPoint[]> ClipStrokeToSphere(
            PointerManager.ControlPoint[] controlPoints, Vector3 sphereCenter, float sphereRadius)
        {
            var result = new List<PointerManager.ControlPoint[]>();
            if (controlPoints.Length == 0)
            {
                return result;
            }

            float radiusSq = sphereRadius * sphereRadius;
            // We keep any stroke portion whose control points are inside the crop volume.
            bool Inside(Vector3 p) => (p - sphereCenter).sqrMagnitude <= radiusSq;

            if (controlPoints.Length == 1)
            {
                if (Inside(controlPoints[0].m_Pos))
                {
                    result.Add(controlPoints);
                }
                return result;
            }

            List<PointerManager.ControlPoint> current = null;

            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                var a = controlPoints[i];
                var b = controlPoints[i + 1];
                bool insideA = Inside(a.m_Pos);
                bool insideB = Inside(b.m_Pos);

                if (insideA && current == null)
                {
                    current = new List<PointerManager.ControlPoint> { a };
                }

                float enter, exit;
                bool intersects = SegmentSphereIntersectionParams(a.m_Pos, b.m_Pos, sphereCenter, sphereRadius, out enter, out exit);
                if (intersects)
                {
                    float start = Mathf.Clamp01(Mathf.Min(enter, exit));
                    float end = Mathf.Clamp01(Mathf.Max(enter, exit));

                    if (!insideA && !insideB && start != end)
                    {
                        var newSegment = new List<PointerManager.ControlPoint>
                        {
                            InterpolateControlPoint(a, b, start),
                            InterpolateControlPoint(a, b, end)
                        };
                        result.Add(newSegment.ToArray());
                    }
                    else if (insideA && !insideB)
                    {
                        current ??= new List<PointerManager.ControlPoint>();
                        current.Add(InterpolateControlPoint(a, b, end));
                        if (current.Count > 1)
                        {
                            result.Add(current.ToArray());
                        }
                        current = null;
                    }
                    else if (!insideA && insideB)
                    {
                        current = new List<PointerManager.ControlPoint>
                        {
                            InterpolateControlPoint(a, b, start)
                        };
                    }
                }

                if (insideB)
                {
                    current ??= new List<PointerManager.ControlPoint>();
                    current.Add(b);
                }
            }

            if (current != null && current.Count > 1)
            {
                result.Add(current.ToArray());
            }

            return result;
        }

        private static PointerManager.ControlPoint InterpolateControlPoint(
            PointerManager.ControlPoint a, PointerManager.ControlPoint b, float t)
        {
            return new PointerManager.ControlPoint
            {
                m_Pos = Vector3.Lerp(a.m_Pos, b.m_Pos, t),
                m_Orient = Quaternion.Slerp(a.m_Orient, b.m_Orient, t),
                m_Pressure = Mathf.Lerp(a.m_Pressure, b.m_Pressure, t),
                m_TimestampMs = (uint)Mathf.Lerp(a.m_TimestampMs, b.m_TimestampMs, t)
            };
        }

        private static bool SegmentSphereIntersectionParams(
            Vector3 a, Vector3 b, Vector3 center, float radius, out float enter, out float exit)
        {
            Vector3 d = b - a;
            Vector3 f = a - center;

            float aCoeff = Vector3.Dot(d, d);
            float bCoeff = 2f * Vector3.Dot(f, d);
            float cCoeff = Vector3.Dot(f, f) - radius * radius;

            float discriminant = bCoeff * bCoeff - 4f * aCoeff * cCoeff;
            if (discriminant < 0f || Mathf.Approximately(aCoeff, 0f))
            {
                enter = exit = 0f;
                return false;
            }

            float sqrtDisc = Mathf.Sqrt(discriminant);
            enter = (-bCoeff - sqrtDisc) / (2f * aCoeff);
            exit = (-bCoeff + sqrtDisc) / (2f * aCoeff);

            return (enter <= 1f && exit >= 0f);
        }

    }
}
