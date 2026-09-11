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
        public static List<Stroke> CropStrokesToSphere(Vector3 center_ws, float radius_ws,
            IEnumerable<Stroke> strokes = null)
        {
            if (radius_ws <= 0)
            {
                return (strokes ?? SketchMemoryScript.AllStrokes()).ToList();
            }

            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Sphere,
                TrTransform.T(center_ws), Vector3.one * radius_ws), strokes);
        }

        public static List<Stroke> CropStrokesToBox(Vector3 center_ws, Vector3 size_ws,
            Quaternion rotation_ws, IEnumerable<Stroke> strokes = null)
        {
            if (size_ws.x <= 0 || size_ws.y <= 0 || size_ws.z <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(size_ws));
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Box,
                TrTransform.TR(center_ws, rotation_ws), size_ws * 0.5f), strokes);
        }

        public static List<Stroke> CropStrokesToCapsule(Vector3 center_ws, float radius_ws,
            float height_ws, Quaternion rotation_ws, IEnumerable<Stroke> strokes = null)
        {
            if (radius_ws <= 0) throw new System.ArgumentOutOfRangeException(nameof(radius_ws));
            if (height_ws <= 0) throw new System.ArgumentOutOfRangeException(nameof(height_ws));
            if (height_ws < 2 * radius_ws)
                throw new System.ArgumentException("Capsule height must be at least twice its radius");
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Capsule,
                TrTransform.TR(center_ws, rotation_ws), new Vector3(radius_ws, height_ws * 0.5f, radius_ws)), strokes);
        }

        public static List<Stroke> CropStrokesToEllipsoid(Vector3 center_ws, Vector3 size_ws,
            Quaternion rotation_ws, IEnumerable<Stroke> strokes = null)
        {
            if (size_ws.x <= 0 || size_ws.y <= 0 || size_ws.z <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(size_ws));
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Ellipsoid,
                TrTransform.TR(center_ws, rotation_ws), size_ws * 0.5f), strokes);
        }

        public static List<Stroke> CropStrokesToPlane(Vector3 point_ws, Vector3 normal_ws,
            IEnumerable<Stroke> strokes = null)
        {
            if (!(normal_ws.sqrMagnitude > 0) || float.IsInfinity(normal_ws.sqrMagnitude))
                throw new System.ArgumentException("Plane normal must be finite and nonzero");
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Plane,
                TrTransform.TR(point_ws, Quaternion.FromToRotation(Vector3.up, normal_ws)), Vector3.one), strokes);
        }

        private static List<Stroke> CropStrokes(StrokeCropVolume volume, IEnumerable<Stroke> strokes)
        {
            var retainedStrokes = new HashSet<Stroke>();
            var result = new List<Stroke>();

            // Snapshot the requested strokes so splitting cannot add work or affect other lists.
            var allStrokes = (strokes ?? SketchMemoryScript.AllStrokes())
                .Where(stroke => stroke != null && stroke.IsGeometryEnabled)
                .Distinct()
                .ToArray();
            foreach (var stroke in allStrokes)
            {
                var canvas = stroke.Canvas;
                if (canvas == null || stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length == 0)
                {
                    continue;
                }

                var canvasPose = canvas.Pose;
                // Canvas.Pose already includes the scene pose: convert world to canvas once.
                Vector3 sphereCenterCs = canvasPose.inverse * volume.Pose.translation;
                float sphereRadiusCs = volume.BoundingRadius / canvasPose.scale;

                // Fast bounds test against a sphere enclosing the crop volume.
                // This avoids expensive clipping for strokes that are clearly outside
                if (stroke.m_BatchSubset != null)
                {
                    Bounds bounds = stroke.m_BatchSubset.m_Bounds;
                    if (!BoundsIntersectsSphere(bounds, sphereCenterCs, sphereRadiusCs))
                    {
                        continue;
                    }
                }

                var clippedSegments = ClipStrokeToVolume(stroke.m_ControlPoints, volume,
                    volume.Pose.inverse * canvasPose);
                if (clippedSegments.Count == 0)
                {
                    // Stroke is completely outside the volume - will be deleted.
                    continue;
                }

                if (clippedSegments.Count == 1)
                {
                    ApplySegmentToStroke(stroke, clippedSegments[0]);
                    retainedStrokes.Add(stroke);
                    result.Add(stroke);
                    continue;
                }

                // Stroke crosses the volume boundary multiple times - split into multiple strokes.
                bool wasSelected = DeregisterSelectedStroke(stroke);
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();

                var newStrokes = wasSelected ? new List<Stroke>() : null;
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
                    retainedStrokes.Add(newStroke);
                    result.Add(newStroke);
                    newStrokes?.Add(newStroke);
                }

                if (wasSelected)
                {
                    SelectionManager.m_Instance.RegisterStrokesInSelectionCanvas(newStrokes);
                }
            }

            // Now delete all strokes that aren't in retainedStrokes
            var allStrokesAfterClipping = allStrokes.Where(stroke => stroke.IsGeometryEnabled).ToArray();
            for (int i = 0; i < allStrokesAfterClipping.Length; i++)
            {
                var stroke = allStrokesAfterClipping[i];
                if (retainedStrokes.Contains(stroke))
                {
                    continue;
                }

                DeregisterSelectedStroke(stroke);
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();
            }
            return result;
        }

        private static bool DeregisterSelectedStroke(Stroke stroke)
        {
            var selectionManager = SelectionManager.m_Instance;
            if (selectionManager == null || !selectionManager.IsStrokeSelected(stroke))
            {
                return false;
            }

            selectionManager.DeregisterStrokesInSelectionCanvas(new[] { stroke });
            return true;
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

        internal static List<PointerManager.ControlPoint[]> ClipStrokeToVolume(
            PointerManager.ControlPoint[] controlPoints, StrokeCropVolume volume, TrTransform canvasToVolume)
        {
            var result = new List<PointerManager.ControlPoint[]>();
            if (controlPoints.Length == 0)
            {
                return result;
            }

            // We keep any stroke portion whose control points are inside the crop volume.
            bool Inside(Vector3 p) => volume.Contains(canvasToVolume * p);

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
                bool intersects = volume.ClipSegment(canvasToVolume * a.m_Pos,
                    canvasToVolume * b.m_Pos, out enter, out exit);
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
                m_TimestampMs = (uint)(a.m_TimestampMs +
                    ((double)b.m_TimestampMs - a.m_TimestampMs) * t)
            };
        }

    }
}
