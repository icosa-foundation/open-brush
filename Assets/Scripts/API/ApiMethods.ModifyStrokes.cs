// Copyright 2022 The Open Brush Authors
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
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint(
            "stroke.delete",
            "Delete a stroke by index",
            "2"
        )]
        public static void DeleteStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
            stroke.Uncreate();
        }

        [ApiEndpoint(
            "stroke.select",
            "Select a stroke by index.",
            "2"
        )]
        public static void SelectStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            SelectionManager.m_Instance.SelectStrokes(new List<Stroke> { stroke });
        }

        [ApiEndpoint(
            "strokes.select",
            "Select multiple strokes by index.",
            "1,4"
        )]
        public static void SelectStrokes(int from, int to)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(from, to);
            SelectionManager.m_Instance.SelectStrokes(strokes);
        }

        [ApiEndpoint("selection.recolor", "Recolors the currently selected strokes")]
        public static void RecolorSelection()
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, true, false, false);
            }
        }

        [ApiEndpoint(
            "strokes.move.to",
            "Moves several strokes to the given position",
            "1,2,5,12,-4"
        )]
        public static void TranslateStrokesTo(int start, int end, Vector3 position)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            foreach (var stroke in strokes)
            {
                stroke.RecreateAt(TrTransform.T(position));
            }
        }

        [ApiEndpoint(
            "strokes.move.by",
            "Moves several strokes to the given coordinates",
            "1,2,5,12,-4"
        )]
        public static void TranslateStrokesBy(int start, int end, Vector3 translation)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            TransformItemsCommand cmd = new TransformItemsCommand(strokes, null, TrTransform.T(translation), Vector3.zero);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint(
            "strokes.rotate.by",
            "Rotates multiple brushstrokes around the current brush position",
            "1,2,5,12,-4"
        )]
        public static void RotateStrokesBy(int start, int end, float angle)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            var axis = ApiManager.Instance.BrushRotation * Vector3.forward;
            var rot = TrTransform.R(angle, axis);
            var pivot = ApiManager.Instance.BrushPosition;
            TransformItemsCommand cmd = new TransformItemsCommand(strokes, null, rot, pivot);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint("strokes.scale.by", "Scales multiple brushstrokes around the current brush position",
            "1,2,0.5"
        )]
        public static void ScaleStrokesBy(int start, int end, float scale)
        {
            var strokes = SketchMemoryScript.GetStrokesBetween(start, end);
            var pivot = ApiManager.Instance.BrushPosition;
            TransformItemsCommand cmd = new TransformItemsCommand(strokes, null, TrTransform.S(scale), pivot);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
        }

        [ApiEndpoint(
            "selection.rebrush",
            "Rebrushes the currently selected strokes",
            "true"
        )]
        public static void RebrushSelection(bool jitter = false)
        {
            SketchMemoryScript.m_Instance.RepaintSelected(true, false, false, jitter);
        }

        [ApiEndpoint("selection.recolor", "Recolors the currently selected strokes")]
        public static void RecolorSelection(bool jitter = false)
        {
            SketchMemoryScript.m_Instance.RepaintSelected(false, true, false, jitter);
        }

        [ApiEndpoint("selection.resize", "Changes the brush size the currently selected strokes")]
        public static void ResizeSelection(bool jitter = false)
        {
            SketchMemoryScript.m_Instance.RepaintSelected(false, false, true, jitter);
        }

        [ApiEndpoint(
            "selection.trim",
            "Removes a number of points from the currently selected strokes",
            "4"
        )]
        public static void TrimSelection(int count)
        {
            foreach (Stroke stroke in SelectionManager.m_Instance.SelectedStrokes)
            {
                int newCount = Mathf.Max(0, stroke.m_ControlPoints.Length - count);
                if (newCount > 0)
                {
                    Array.Resize(ref stroke.m_ControlPoints, newCount);
                    stroke.Recreate(null, stroke.Canvas);
                    SketchMemoryScript.m_Instance.MemorizeStrokeRepaint(stroke, false, false, true);
                }
                else
                {
                    SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                    stroke.Uncreate();
                }
            }
        }

        [ApiEndpoint(
            "selection.points.perlin",
            "Moves the position of all control points in the selection using a noise function",
            "y,0.5,2,0.5"
        )]
        public static void PerlinNoiseSelection(string axis, Vector3 scale)
        {
            Enum.TryParse(axis.ToUpper(), out Axis _axis);
            Func<Vector3, Vector3> quantize = pos => _PerlinNoiseToPosition(pos, scale, _axis);
            _ModifyStrokeControlPoints(quantize);
        }

        [ApiEndpoint(
            "stroke.points.quantize",
            "Snaps all the points in selected strokes to a grid (buggy)",
            "2,2,2"
        )]
        public static void QuantizeSelection(Vector3 grid)
        {
            Func<Vector3, Vector3> quantize = pos => _QuantizePosition(pos, grid);
            _ModifyStrokeControlPoints(quantize);
        }

        [ApiEndpoint("stroke.join", "Joins a stroke with the previous one")]
        public static Stroke JoinStroke()
        {
            var stroke1 = SketchMemoryScript.m_Instance.GetStrokeAtIndex(0);
            var stroke2 = SketchMemoryScript.m_Instance.GetStrokeAtIndex(-1);
            return JoinStrokes(stroke1, stroke2);
        }

        public static Stroke JoinStrokes(Stroke stroke1, Stroke stroke2)
        {
            stroke2.m_ControlPoints = stroke2.m_ControlPoints.Concat(stroke1.m_ControlPoints).ToArray();
            stroke2.Uncreate();
            stroke2.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke2.m_ControlPoints.Length).ToArray();
            stroke2.Recreate(null, stroke2.Canvas);
            DeleteStroke(0);
            return stroke2;
        }

        [ApiEndpoint(
            "strokes.join",
            "Joins all strokes between the two indices (inclusive)",
            "1,4"
        )]
        public static Stroke JoinStrokes(int from, int to)
        {
            var strokesToJoin = SketchMemoryScript.GetStrokesBetween(from, to);
            var firstStroke = strokesToJoin[0];
            firstStroke.m_ControlPoints = strokesToJoin.SelectMany(x => x.m_ControlPoints).ToArray();
            for (int i = 1; i < strokesToJoin.Count; i++)
            {
                var stroke = strokesToJoin[i];
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.DestroyStroke();
            }
            firstStroke.Uncreate();
            firstStroke.m_ControlPointsToDrop = Enumerable.Repeat(false, firstStroke.m_ControlPoints.Length).ToArray();
            firstStroke.Recreate(null, firstStroke.Canvas);
            return firstStroke;
        }

        [ApiEndpoint(
            "stroke.add",
            "Adds a point at the current brush position to the specified stroke",
            "2"
        )]
        public static void AddPointToStroke(int index)
        {
            var stroke = SketchMemoryScript.m_Instance.GetStrokeAtIndex(index);
            var prevCP = stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1];
            Array.Resize(ref stroke.m_ControlPoints, stroke.m_ControlPoints.Length + 1);

            PointerManager.ControlPoint cp = new PointerManager.ControlPoint
            {
                m_Pos = ApiManager.Instance.BrushPosition,
                m_Orient = ApiManager.Instance.BrushRotation,
                m_Pressure = prevCP.m_Pressure,
                m_TimestampMs = prevCP.m_TimestampMs
            };

            stroke.m_ControlPoints[stroke.m_ControlPoints.Length - 1] = cp;
            stroke.Uncreate();
            stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
            stroke.Recreate(null, stroke.Canvas);
        }

        [ApiEndpoint(
            "strokes.crop.sphere",
            "Crops all strokes to the spherical volume defined by center (world space) and radius (meters)",
            "0,0,0,5"
        )]
        public static void CropStrokesToSphere(Vector3 center, float radius)
        {
            if (radius <= 0)
            {
                return;
            }

            int totalStrokesBeforeCrop = SketchMemoryScript.AllStrokes().Count();
            UnityEngine.Debug.Log($"CROP_DEBUG: Starting crop at center={center}, radius={radius}, totalStrokes={totalStrokesBeforeCrop}");

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

            int totalStrokesAfterCrop = SketchMemoryScript.AllStrokes().Count();
            UnityEngine.Debug.Log($"CROP_DEBUG: Finished crop - boundsTests(passed={boundsTestsPassed}, failed={boundsTestsFailed}), strokesPreserved={strokesInsideSphere.Count}, deleted={deletedCount}, totalStrokesAfter={totalStrokesAfterCrop}");
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
