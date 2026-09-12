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
            IEnumerable<Stroke> strokes = null, bool keepInside = true)
        {
            if (radius_ws <= 0)
            {
                return (strokes ?? SketchMemoryScript.AllStrokes()).ToList();
            }

            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Sphere,
                TrTransform.T(center_ws), Vector3.one * radius_ws), strokes, keepInside);
        }

        public static List<Stroke> CropStrokesToBox(Vector3 center_ws, Vector3 size_ws,
            Quaternion rotation_ws, IEnumerable<Stroke> strokes = null, bool keepInside = true)
        {
            if (size_ws.x <= 0 || size_ws.y <= 0 || size_ws.z <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(size_ws));
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Box,
                TrTransform.TR(center_ws, rotation_ws), size_ws * 0.5f), strokes, keepInside);
        }

        public static List<Stroke> CropStrokesToCapsule(Vector3 center_ws, float radius_ws,
            float height_ws, Quaternion rotation_ws, IEnumerable<Stroke> strokes = null, bool keepInside = true)
        {
            if (radius_ws <= 0) throw new System.ArgumentOutOfRangeException(nameof(radius_ws));
            if (height_ws <= 0) throw new System.ArgumentOutOfRangeException(nameof(height_ws));
            if (height_ws < 2 * radius_ws)
                throw new System.ArgumentException("Capsule height must be at least twice its radius");
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Capsule,
                TrTransform.TR(center_ws, rotation_ws), new Vector3(radius_ws, height_ws * 0.5f, radius_ws)), strokes, keepInside);
        }

        public static List<Stroke> CropStrokesToEllipsoid(Vector3 center_ws, Vector3 size_ws,
            Quaternion rotation_ws, IEnumerable<Stroke> strokes = null, bool keepInside = true)
        {
            if (size_ws.x <= 0 || size_ws.y <= 0 || size_ws.z <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(size_ws));
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Ellipsoid,
                TrTransform.TR(center_ws, rotation_ws), size_ws * 0.5f), strokes, keepInside);
        }

        public static List<Stroke> CropStrokesToPlane(Vector3 point_ws, Vector3 normal_ws,
            IEnumerable<Stroke> strokes = null, bool keepInside = true)
        {
            if (!(normal_ws.sqrMagnitude > 0) || float.IsInfinity(normal_ws.sqrMagnitude))
                throw new System.ArgumentException("Plane normal must be finite and nonzero");
            return CropStrokes(new StrokeCropVolume(StrokeCropVolume.Shape.Plane,
                TrTransform.TR(point_ws, Quaternion.FromToRotation(Vector3.up, normal_ws)), Vector3.one), strokes, keepInside);
        }

        private static List<Stroke> CropStrokes(StrokeCropVolume volume, IEnumerable<Stroke> strokes, bool keepInside)
        {
            var originals = (strokes ?? SketchMemoryScript.AllStrokes())
                .Where(stroke => stroke != null && stroke.IsGeometryEnabled).Distinct().ToArray();
            var replacements = new Dictionary<Stroke, List<Stroke>>();
            var result = new List<Stroke>();
            foreach (var stroke in originals)
            {
                var canvas = stroke.Canvas;
                if (canvas == null || stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length == 0)
                {
                    result.Add(stroke);
                    continue;
                }
                var toVolume = volume.Pose.inverse * canvas.Pose;
                if (keepInside && stroke.m_ControlPoints.All(cp => volume.Contains(toVolume * cp.m_Pos)))
                {
                    // A convex volume contains every segment if it contains every endpoint.
                    result.Add(stroke);
                    continue;
                }

                var sourceIndices = new List<float[]>();
                var segments = ClipStrokeToVolume(stroke.m_ControlPoints, volume, toVolume, sourceIndices, keepInside);
                if (segments.Count == 1 && segments[0].SequenceEqual(stroke.m_ControlPoints))
                {
                    result.Add(stroke);
                    continue;
                }
                var clipped = new List<Stroke>();
                for (int i = 0; i < segments.Count; i++)
                {
                    var replacement = new Stroke(stroke)
                    {
                        m_ControlPoints = segments[i],
                        m_ControlPointsToDrop = new bool[segments[i].Length],
                        m_IntendedCanvas = canvas,
                        m_PreviousCanvas = stroke.m_PreviousCanvas,
                        m_Type = Stroke.Type.NotCreated
                    };
                    if (stroke.m_OverrideColors != null)
                    {
                        // Bake the visible colors at each new boundary point; keep originals untouched for Undo.
                        replacement.m_OverrideColors = sourceIndices[i].Select(index =>
                        {
                            int a = Mathf.FloorToInt(index);
                            int b = Mathf.Min(a + 1, stroke.m_ControlPoints.Length - 1);
                            return (Color32?)(Color32)Color.Lerp(stroke.GetColor(a), stroke.GetColor(b), index - a);
                        }).ToList();
                        replacement.m_ColorOverrideMode = ColorOverrideMode.Replace;
                    }
                    clipped.Add(replacement);
                }
                replacements.Add(stroke, clipped);
                result.AddRange(clipped);
            }
            if (replacements.Count == 0) return strokes as List<Stroke> ?? result;

            var parent = ApiManager.Instance != null ? ApiManager.Instance.ActiveUndo : null;
            var liveList = strokes as List<Stroke> ?? result;
            var command = new CropStrokesCommand(originals, replacements, result, liveList, parent);
            if (parent == null) SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
            else command.Redo(); // Apply now; the tool/API undo group records its children on completion.
            return liveList;
        }

        internal static List<PointerManager.ControlPoint[]> ClipStrokeToVolume(
            PointerManager.ControlPoint[] controlPoints, StrokeCropVolume volume, TrTransform canvasToVolume,
            List<float[]> sourceIndices = null, bool keepInside = true)
        {
            var result = new List<PointerManager.ControlPoint[]>();
            if (controlPoints.Length == 1)
            {
                if (volume.Contains(canvasToVolume * controlPoints[0].m_Pos) == keepInside)
                {
                    result.Add(controlPoints);
                    sourceIndices?.Add(new[] { 0f });
                }
                return result;
            }
            List<PointerManager.ControlPoint> current = null;
            List<float> indices = null;
            void Flush()
            {
                if (current != null && current.Count > 1)
                {
                    result.Add(current.ToArray());
                    sourceIndices?.Add(indices.ToArray());
                }
                current = null;
                indices = null;
            }
            for (int i = 0; i < controlPoints.Length - 1; i++)
            {
                var a = controlPoints[i];
                var b = controlPoints[i + 1];
                void Append(float start, float end)
                {
                    if (start == end) return;
                    if (start > 0) Flush();
                    if (current == null)
                    {
                        current = new List<PointerManager.ControlPoint> {
                            start == 0 ? a : InterpolateControlPoint(a, b, start)
                        };
                        indices = new List<float> { i + start };
                    }
                    current.Add(end == 1 ? b : InterpolateControlPoint(a, b, end));
                    indices.Add(i + end);
                    if (end < 1) Flush();
                }
                bool intersects = volume.ClipSegment(canvasToVolume * a.m_Pos, canvasToVolume * b.m_Pos,
                    out float enter, out float exit) && enter < exit;
                if (keepInside)
                {
                    if (intersects) Append(enter, exit);
                    else Flush();
                }
                else if (!intersects)
                {
                    Append(0, 1);
                }
                else
                {
                    Append(0, enter);
                    Flush(); // The removed interior separates the two outside portions.
                    Append(exit, 1);
                }
            }
            Flush();
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
