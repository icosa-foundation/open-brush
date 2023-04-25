// Copyright 2021 The Open Brush Authors
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
using System.IO;
using System.Linq;
using Unity.VectorGraphics;
using UnityEngine;
namespace TiltBrush
{
    public static class DrawStrokes
    {

        public static void DrawSingleTrList(IEnumerable<TrTransform> path, TrTransform tr, float brushScale = 1f, bool rawStrokes = false)
        {
            DrawNestedTrList(new List<IEnumerable<TrTransform>> { path }, tr, brushScale, rawStrokes);
        }

        public static void DrawNestedTrList(
            IEnumerable<IEnumerable<TrTransform>> pathEnumerable,
            TrTransform tr,
            float brushScale = 1f,
            bool rawStrokes = false)
        {
            var paths = pathEnumerable.ToList();
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            uint time = 0;
            var group = App.GroupManager.NewUnusedGroup();
            int pathIndex = 0;
            foreach (var item in paths)
            {
                var path = item.ToList();
                // Single joined paths
                if (path.Count < 2) continue;
                var controlPoints = new List<PointerManager.ControlPoint>();
                for (var vertexIndex = 0; vertexIndex < path.Count - 1; vertexIndex++)
                {
                    Vector3 position = path[vertexIndex].translation;
                    Quaternion orientation = path[vertexIndex].rotation;
                    float pressure = path[vertexIndex].scale;
                    Vector3 nextPosition = path[(vertexIndex + 1) % path.Count].translation;

                    if (rawStrokes)
                    {
                        controlPoints.Add(new PointerManager.ControlPoint
                        {
                            m_Pos = position,
                            m_Orient = orientation,
                            m_Pressure = pressure,
                            m_TimestampMs = time++
                        });
                    }
                    else
                    {
                        // Create extra control points if needed
                        // Procedural strokes need to have extra control points added to avoid being smoothed out.
                        for (float step = 0; step <= 1f; step += 0.25f)
                        {
                            controlPoints.Add(new PointerManager.ControlPoint
                            {
                                m_Pos = position + (nextPosition - position) * step,
                                m_Orient = orientation,
                                m_Pressure = pressure,
                                m_TimestampMs = time++
                            });
                        }
                    }

                }
                var stroke = new Stroke
                {
                    m_Type = Stroke.Type.NotCreated,
                    m_IntendedCanvas = App.Scene.ActiveCanvas,
                    m_BrushGuid = brush.m_Guid,
                    m_BrushScale = brushScale,
                    m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                    m_Color = App.BrushColor.CurrentColor,
                    m_Seed = 0,
                    m_ControlPoints = controlPoints.ToArray(),
                };
                stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
                stroke.Group = group;
                stroke.Recreate(tr, App.Scene.ActiveCanvas);
                if (pathIndex != 0) stroke.m_Flags = SketchMemoryScript.StrokeFlags.IsGroupContinue;
                SketchMemoryScript.m_Instance.MemoryListAdd(stroke);
                var undoParent = ApiManager.Instance.ActiveUndo;
                var cmd = new BrushStrokeCommand(
                    stroke, WidgetManager.m_Instance.ActiveStencil, 123, undoParent
                );
                if (undoParent == null)
                {
                    // No active undo. So actually perform the command
                    SketchMemoryScript.m_Instance.PerformAndRecordCommand(cmd);
                }
                pathIndex++;
            }
        }

        public static void Polygon(int sides, TrTransform tr = default)
        {
            var path = new List<TrTransform>();
            for (float i = 0; i <= sides; i++)
            {
                var theta = Mathf.PI * (i / sides) * 2f;
                theta += Mathf.Deg2Rad;
                var point = new Vector3(
                    Mathf.Cos(theta),
                    Mathf.Sin(theta),
                    0
                );
                point = ApiManager.Instance.BrushRotation * point;
                path.Add(TrTransform.T(point));
            }
            DrawNestedTrList(new List<List<TrTransform>> { path }, tr);
        }

        public static void Text(string text, TrTransform tr)
        {
            var textToStroke = new TextToStrokes(ApiManager.Instance.TextFont);
            DrawNestedTrList(textToStroke.Build(text), tr);
        }

        public static void DrawSvgPathString(string svgPathString, TrTransform tr)
        {
            DrawNestedTrList(SvgPathStringToApiPaths(svgPathString), tr);
        }

        public static void DrawSvg(string svg, TrTransform tr)
        {
            DrawNestedTrList(SvgToApiPaths(svg), tr);
        }

        public static List<List<TrTransform>> SvgPathStringToApiPaths(string svgPathString)
        {
            var svgText = $"<svg xmlns=\"http: //www.w3.org/2000/svg\"><path d=\"{svgPathString}\"/></svg>";
            return SvgToApiPaths(svgText);
        }

        public static List<List<TrTransform>> SvgToApiPaths(string svgText)
        {
            svgText = _PreProcessSvg(svgText);
            var geoms = _ParseSvg(svgText);
            var svgPolyline = new List<List<TrTransform>>();
            foreach (var geom in geoms)
            {
                var verts = geom.Vertices.Select(v => new Vector3(v.x, -v.y, 0)); // SVG is Y down, Unity is Y up
                svgPolyline.Add(verts.Select(TrTransform.T).ToList());
            }
            return svgPolyline;
        }

        private static string _PreProcessSvg(string svgText)
        {
            var colorString = ColorUtility.ToHtmlStringRGB(App.BrushColor.CurrentColor);
            svgText = svgText.Replace("currentcolor", $"#{colorString}", StringComparison.OrdinalIgnoreCase);
            return svgText;
        }

        private static List<VectorUtils.Geometry> _ParseSvg(string svgText, bool outlinesOnly = true, bool convexOutlinesOnly = false)
        {
            TextReader stringReader = new StringReader(svgText);
            var sceneInfo = SVGParser.ImportSVG(stringReader);
            VectorUtils.TessellationOptions tessellationOptions = new VectorUtils.TessellationOptions
            {
                StepDistance = 100.0f,
                MaxCordDeviation = 0.5f,
                MaxTanAngleDeviation = 0.1f,
                SamplingStepSize = 0.01f,
                OutlinesOnly = outlinesOnly,
                ConvexOutlinesOnly = convexOutlinesOnly,
            };
            return VectorUtils.TessellateScene(sceneInfo.Scene, tessellationOptions);
        }

        public static void CameraPath(CameraPath path, TrTransform tr = default)
        {
            var points = new List<TrTransform>();
            for (float t = 0; t < path.Segments.Count; t += .1f)
            {
                points.Add(TrTransform.TR(
                    path.GetPosition(new PathT(t)),
                    path.GetRotation(new PathT(t))
                ));
            }
            DrawNestedTrList(new List<List<TrTransform>> { points }, tr);
        }
    }
}
