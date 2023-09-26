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
using Newtonsoft.Json;
using SVGMeshUnity;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("draw.paths", "Draws a series of paths at the current brush position [[[x1,y1,z1],[x2,y2,z2], etc...]]. Does not move the brush position")]
        public static void DrawPaths(string jsonString)
        {
            // TODO Use brush rotation 
            var origin = ApiManager.Instance.BrushPosition;
            var paths = JsonConvert.DeserializeObject<List<List<List<float>>>>($"[{jsonString}]");
            DrawStrokes.MultiPathsToStrokes(paths, origin);
        }

        [ApiEndpoint("draw.path", "Draws a path at the current brush position [x1,y1,z1],[x2,y2,z2], etc.... Does not move the brush position")]
        public static void DrawPath(string jsonString)
        {
            // TODO Use brush rotation
            var origin = ApiManager.Instance.BrushPosition;
            var path = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            DrawStrokes.SinglePathToStroke(path, origin);
        }

        [ApiEndpoint("draw.stroke", "Draws an exact brush stroke as recorded in another app")]
        public static void DrawStroke(string jsonString)
        {
            // TODO Use brush rotation
            var strokeData = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            DrawStrokes.SinglePathToStroke(strokeData, Vector3.zero, rawStroke: true);
        }

        [ApiEndpoint("draw.polygon", "Draws a polygon at the current brush position. Does not move the brush position")]
        public static void DrawPolygon(int sides, float radius, float angle)
        {
            var path = new List<Vector3>();
            for (float i = 0; i <= sides; i++)
            {
                var theta = Mathf.PI * (i / sides) * 2f;
                theta += angle * Mathf.Deg2Rad;
                var point = new Vector3(
                    Mathf.Cos(theta),
                    Mathf.Sin(theta),
                    0
                ) * radius;
                point = ApiManager.Instance.BrushRotation * point;
                path.Add(point);
            }
            DrawStrokes.PositionPathsToStroke(path, ApiManager.Instance.BrushPosition);
        }

        [ApiEndpoint("draw.text", "Draws the characters supplied at the current brush position")]
        public static void Text(string text)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var font = Resources.Load<CHRFont>("arcade");
            var textToStroke = new TextToStrokes(font);
            var polyline2d = textToStroke.Build(text);
            DrawStrokes.MultiPositionPathsToStrokes(polyline2d, null, null, origin);
        }

        [ApiEndpoint("draw.svg", "Draws the path supplied as an SVG Path string at the current brush position")]
        public static void SvgPath(string svgPathString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            SVGData svgData = new SVGData();
            svgData.Path(svgPathString);
            SVGPolyline svgPolyline = new SVGPolyline();
            svgPolyline.Fill(svgData);
            DrawStrokes.MultiPath2dToStrokes(svgPolyline.Polyline, origin, 0.01f, true);
        }

        [ApiEndpoint("brush.type", "Changes the brush. brushType can either be the brush name or it's guid. brushes are listed in the /help screen")]
        public static void Brush(string brushType)
        {
            BrushDescriptor brushDescriptor = null;
            try
            {
                var guid = new Guid(brushType);
                brushDescriptor = BrushCatalog.m_Instance.GetBrush(guid);
            }
            catch (FormatException e)
            {
            }

            if (brushDescriptor == null)
            {
                brushType = brushType.ToLower().Trim().Replace(" ", "");
                try
                {
                    brushDescriptor = BrushCatalog.m_Instance.AllBrushes
                        .First(x => x.Description
                            .Replace(" ", "")
                            .Replace(".", "")
                            .Replace("(", "")
                            .Replace(")", "")
                            .ToLower() == brushType);
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogError($"No brush found called: {brushType}");
                }
            }

            if (brushDescriptor != null)
            {
                PointerManager.m_Instance.SetBrushForAllPointers(brushDescriptor);
            }
            else
            {
                Debug.LogError($"No brush found with the name or guid: {brushType}");
            }
        }

        [ApiEndpoint("color.add.hsv", "Adds the supplied values to the current color. Values are hue, saturation and value")]
        public static void AddColorHSV(Vector3 hsv)
        {
            float h, s, v;
            Color.RGBToHSV(App.BrushColor.CurrentColor, out h, out s, out v);
            App.BrushColor.CurrentColor = Color.HSVToRGB(
                (h + hsv.x) % 1f,
                (s + hsv.y) % 1f,
                (v + hsv.z) % 1f
            );
        }

        [ApiEndpoint("color.add.rgb", "Adds the supplied values to the current color. Values are red green and blue")]
        public static void AddColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor += new Color(rgb.x, rgb.y, rgb.z);
        }

        [ApiEndpoint("color.set.rgb", "Sets the current color. Values are hue, saturation and value")]
        public static void SetColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor = new Color(rgb.x, rgb.y, rgb.z);
        }

        [ApiEndpoint("color.set.hsv", "Sets the current color. Values are red, green and blue")]
        public static void SetColorHSV(Vector3 hsv)
        {
            App.BrushColor.CurrentColor = Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
        }

        [ApiEndpoint("color.set.html", "Sets the current color. colorString can either be a hex value or a css color name.")]
        public static void SetColorHTML(string color)
        {
            Color c;
            color = color.ToLower();
            if (CssColors.NamesToHex.ContainsKey(color)) color = CssColors.NamesToHex[color];
            if (!color.StartsWith("#")) color = $"#{color}";
            if (ColorUtility.TryParseHtmlString(color, out c))
            {
                App.BrushColor.CurrentColor = c;
            }
            else
            {
                Debug.LogError($"Invalid color: {color}");
            }
        }

        [ApiEndpoint("brush.size.set", "Sets the current brush size")]
        public static void BrushSizeSet(float size)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 = size;
        }

        [ApiEndpoint("brush.size.add", "Changes the current brush size by 'amount'")]
        public static void BrushSizeAdd(float amount)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 += amount;
        }

        [ApiEndpoint("draw.camerapath", "Draws along a camera path with the current brush settings")]
        public static void DrawCameraPath(int index)
        {
            CameraPathWidget widget = _GetActiveCameraPath(index);
            CameraPath path = widget.Path;
            var positions = new List<Vector3>();
            var rotations = new List<Quaternion>();
            for (float t = 0; t < path.Segments.Count; t += .1f)
            {
                positions.Add(path.GetPosition(new PathT(t)));
                rotations.Add(path.GetRotation(new PathT(t)));
            }
            DrawStrokes.MultiPositionPathsToStrokes(
                new List<List<Vector3>> { positions },
                new List<List<Quaternion>> { rotations },
                null,
                Vector3.zero
            );
        }

    }

}
