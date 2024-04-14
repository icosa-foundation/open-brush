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

        public static void DrawSingleTrList(
            IEnumerable<TrTransform> path,
            TrTransform tr,
            float brushScale = 1f,
            float smoothing = 0.2f,
            bool rawStrokes = false)
        {
            DrawNestedTrList(new List<IEnumerable<TrTransform>> { path }, tr, null, brushScale, rawStrokes, smoothing);
        }

        public static void DrawNestedTrList(
            IEnumerable<IEnumerable<TrTransform>> pathEnumerable,
            TrTransform tr,
            List<Color> colors = null,
            float brushScale = 1f,
            bool rawStrokes = false,
            float smoothing = 0.2f,
            uint group = GroupManager.kIdSketchGroupTagNone)
        {
            var paths = pathEnumerable.ToList();
            var brush = PointerManager.m_Instance.MainPointer.CurrentBrush;
            uint time = 0;
            int pathIndex = 0;
            for (var i = 0; i < paths.Count; i++)
            {
                var color = colors != null ? colors[i] : App.BrushColor.CurrentColor;
                var item = paths[i];
                if (item == null) continue;
                var path = item.ToList();
                // Single joined paths
                if (path.Count < 2) continue;
                int cpCount = path.Count - 1;
                if (!rawStrokes) cpCount *= 3; // Three control points per original vertex
                var controlPoints = new List<PointerManager.ControlPoint>(cpCount);

                for (var vertexIndex = 0; vertexIndex < path.Count - 1; vertexIndex++)
                {
                    Vector3 position = path[vertexIndex].translation;
                    Quaternion orientation = path[vertexIndex].rotation;
                    float pressure = path[vertexIndex].scale;
                    Vector3 nextPosition = path[(vertexIndex + 1) % path.Count].translation;

                    void addPoint(Vector3 pos)
                    {
                        controlPoints.Add(new PointerManager.ControlPoint
                        {
                            m_Pos = pos,
                            m_Orient = orientation,
                            m_Pressure = pressure,
                            m_TimestampMs = time++
                        });
                    }

                    if (rawStrokes)
                    {
                        addPoint(position);
                    }
                    else
                    {
                        // smoothing controls much to pull extra vertices towards the middle
                        // 0.25 smooths corners a lot, 0.1 is tighter
                        addPoint(position + (nextPosition - position));
                        // addPoint(position + (nextPosition - position) * smoothing);
                        // addPoint(position + (nextPosition - position) * .5f);
                        // addPoint(position + (nextPosition - position) * (1 - smoothing));
                    }

                }
                var stroke = new Stroke
                {
                    m_Type = Stroke.Type.NotCreated,
                    m_IntendedCanvas = App.Scene.ActiveCanvas,
                    m_BrushGuid = brush.m_Guid,
                    m_BrushScale = brushScale,
                    m_BrushSize = PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                    m_Color = color,
                    m_Seed = 0,
                    m_ControlPoints = controlPoints.ToArray(),
                };
                stroke.m_ControlPointsToDrop = Enumerable.Repeat(false, stroke.m_ControlPoints.Length).ToArray();
                stroke.Group = new SketchGroupTag(group);
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

        public static void DrawPolygon(int sides, TrTransform tr = default)
        {
            var path = new List<TrTransform>(sides);
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

        public static void DrawText(string text, TrTransform tr)
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
            DrawNestedTrList(SvgDocumentToNestedPaths(svg).paths, tr);
        }

        public static List<List<TrTransform>> SvgPathStringToApiPaths(string svgPathString)
        {
            var svgText = $"<svg xmlns=\"http: //www.w3.org/2000/svg\"><path d=\"{svgPathString}\"/></svg>";
            return SvgDocumentToNestedPaths(svgText).paths;
        }

        public static (List<List<TrTransform>> paths, List<Color> colors) SvgDocumentToNestedPaths(string svgText, float offsetPerPath = 0, bool includeColors = false)
        {
            svgText = _PreProcessSvg(svgText);
            var geoms = _ParseSvg(svgText);
            var svgPolyline = new List<List<TrTransform>>(geoms.Count);
            List<Color> colors = null;
            if (includeColors)
            {
                colors = new List<Color>(geoms.Count);
            }
            float offset = 0;
            for (var i = 0; i < geoms.Count; i++)
            {
                var geom = geoms[i];
                var verts = new List<TrTransform>(geom.Vertices.Length);
                for (var j = 0; j < geom.Vertices.Length; j++)
                {
                    var v = geom.Vertices[j];
                    verts.Add(TrTransform.T(new Vector3(v.x, -v.y, offset))); // SVG is Y down, Unity is Y up
                }
                svgPolyline.Add(verts);
                if (includeColors)
                {
                    colors.Add(geom.Color);
                }
                offset += offsetPerPath;
            }
            return (svgPolyline, colors);
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

        public static void DrawCameraPath(CameraPath path, float step, TrTransform tr = default)
        {
            var points = path.AsTrList(step);
            DrawNestedTrList(new List<List<TrTransform>> { points }, tr);
        }
    }
}
