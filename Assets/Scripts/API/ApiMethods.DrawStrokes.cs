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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        public static List<List<TrTransform>> _FloatListToNestedPaths(List<List<List<float>>> floatPaths)
        {
            var allTrList = new List<List<TrTransform>>(floatPaths.Count);
            for (var i = 0; i < floatPaths.Count; i++)
            {
                var item = floatPaths[i];
                var trList = new List<TrTransform>(item.Count);
                for (var j = 0; j < item.Count; j++)
                {
                    var floats = item[j];
                    if (floats.Count == 3)
                    {
                        trList.Add(TrTransform.T(
                            new Vector3(floats[0], floats[1], floats[2])
                        ));
                    }
                    else if (floats.Count == 6)
                    {
                        trList.Add(TrTransform.TR(
                            new Vector3(floats[0], floats[1], floats[2]),
                            Quaternion.Euler(floats[0], floats[1], floats[2])
                        ));
                    }
                    else if (floats.Count == 7)
                    {
                        trList.Add(TrTransform.TRS(
                            new Vector3(floats[0], floats[1], floats[2]),
                            Quaternion.Euler(floats[0], floats[1], floats[2]),
                            floats[3]
                        ));
                    }
                }
                allTrList.Add(trList);
            }
            return allTrList;
        }

        [ApiEndpoint(
            "draw.paths",
            "Draws a series of paths at the current brush position [[[x1,y1,z1],[x2,y2,z2], etc...]]. Does not move the brush position",
            "[[0,0,0],[1,0,0],[1,1,0]],[[0,0,-1],[-1,0,-1],[-1,1,-1]]"
        )]
        public static void DrawPaths(string jsonString)
        {
            var origin = TrTransform.T(ApiManager.Instance.BrushPosition);
            List<List<List<float>>> floatlist = JsonConvert.DeserializeObject<List<List<List<float>>>>($"[{jsonString}]");
            var paths = _FloatListToNestedPaths(floatlist);
            DrawStrokes.DrawNestedTrList(
                paths,
                origin,
                smoothing: ApiManager.Instance.PathSmoothing
            );
        }

        [ApiEndpoint(
            "draw.path",
            "Draws a path at the current brush position [x1,y1,z1],[x2,y2,z2], etc.... Does not move the brush position",
            "[0,0,0],[1,0,0],[1,1,0],[0,1,0]"
        )]
        public static void DrawPath(string jsonString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var path = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            var floatPaths = new List<List<List<float>>> { path };
            var tr = TrTransform.TRS(origin, Quaternion.identity, 1f);
            var paths = _FloatListToNestedPaths(floatPaths);
            DrawStrokes.DrawNestedTrList(paths, tr, smoothing: ApiManager.Instance.PathSmoothing);
        }

        [ApiEndpoint(
            "draw.stroke",
            "Draws an exact brush stroke including orientation and pressure",
            "[0,0,0,0,180,90,.75],[1,0,0,0,180,90,.75],[1,1,0,0,180,90,.75],[0,1,0,0,180,90,.75]"
        )]
        public static void DrawStroke(string jsonString)
        {
            var strokeData = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            var floatPaths = new List<List<List<float>>> { strokeData };
            var paths = _FloatListToNestedPaths(floatPaths);
            DrawStrokes.DrawNestedTrList(paths, TrTransform.identity, smoothing: 0);
        }

        [ApiEndpoint(
            "draw.polygon",
            "Draws a polygon at the current brush position. Does not move the brush position",
            "5,2.5,45"
        )]
        public static void DrawPolygon(int sides, float radius, float angle)
        {
            var tr = TrTransform.TRS(ApiManager.Instance.BrushPosition, Quaternion.Euler(0, 0, angle), radius);
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
            DrawStrokes.DrawNestedTrList(
                new List<List<TrTransform>> { path }, tr,
                smoothing: ApiManager.Instance.PathSmoothing
            );
        }

        [ApiEndpoint(
            "draw.text",
            "Draws the characters supplied at the current brush position",
            "hello world"
        )]
        public static void Text(string text)
        {
            var tr = TrTransform.T(ApiManager.Instance.BrushPosition);
            var textToStroke = new TextToStrokes(ApiManager.Instance.TextFont);
            DrawStrokes.DrawNestedTrList(
                textToStroke.Build(text),
                tr,
                smoothing: ApiManager.Instance.PathSmoothing
            );
        }

        [ApiEndpoint(
            "draw.opentypetext",
            "Same as draw text but uses an opentype font (the font should be in a Fonts folder in your Open Brush folder)",
            "hello world,hello world,calibri.ttf"
        )]
        public static void OpenTypeText(string text, string fontPath)
        {
            var tr = TrTransform.T(ApiManager.Instance.BrushPosition);
            var svg = SvgTextUtils.GenerateSvgOutlineForText(text, fontPath);
            (List<List<TrTransform>> paths, List<Color> colors) = DrawStrokes.SvgDocumentToNestedPaths(svg);
            DrawStrokes.DrawNestedTrList(paths, tr, colors);
        }

        private static bool IsFullSvgDocument(string input)
        {
            using (XmlReader reader = XmlReader.Create(new StringReader(input)))
            {
                try
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name.Equals("svg", StringComparison.OrdinalIgnoreCase))
                        {
                            return true; // Found the <svg> element
                        }
                        // Break early if not the expected start elements to avoid parsing entire document
                        if (reader.NodeType == XmlNodeType.Element && !reader.Name.Equals("xml", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }
                catch (XmlException)
                {
                    // Handle case where input is not well-formed XML
                    return false;
                }
            }
            return false; // No <svg> element found at the beginning of the document
        }


        [ApiEndpoint(
            "draw.svg",
            "Draws an entire SVG document"
        )]
        public static void DrawSvg(string svg)
        {
            // SVG paths are usually scaled rather large so scale down 100x
            float scale = 100f;
            var tr = TrTransform.TRS(ApiManager.Instance.BrushPosition, Quaternion.identity, 1f / scale);
            List<List<TrTransform>> paths;
            List<Color> colors;

            if (!IsFullSvgDocument(svg))
            {
                // For backwards compatibility, also support SVG path strings
                DrawSvgPath(svg);
                return;
            }

            (paths, colors) = DrawStrokes.SvgDocumentToNestedPaths(svg, offsetPerPath: -0.001f, includeColors: true);
            DrawStrokes.DrawNestedTrList(
                paths,
                tr,
                colors: colors,
                brushScale: scale,
                smoothing: ApiManager.Instance.PathSmoothing
            );
        }

        [ApiEndpoint(
            "draw.svg.path",
            "Draws the path supplied as an SVG Path string at the current brush position",
            "M 184,199 116,170 53,209.6 60,136.2 4.3,88"
        )]
        public static void DrawSvgPath(string svgPath)
        {
            // SVG paths are usually scaled rather large so scale down 100x
            float scale = 100f;
            var tr = TrTransform.TRS(ApiManager.Instance.BrushPosition, Quaternion.identity, 1f / scale);
            var paths = DrawStrokes.SvgPathStringToApiPaths(svgPath);
            DrawStrokes.DrawNestedTrList(
                paths,
                tr,
                brushScale: scale,
                smoothing: ApiManager.Instance.PathSmoothing
            );
        }

        [ApiEndpoint(
            "brush.type",
            "Changes the brush. brushType can either be the brush name or it's guid. brushes are listed in the /help screen",
            "ink"
        )]
        public static void Brush(string brushType)
        {
            var brushDescriptor = LookupBrushDescriptor(brushType);
            if (brushDescriptor != null)
            {
                PointerManager.m_Instance.SetBrushForAllPointers(brushDescriptor);
            }
            else
            {
                Debug.LogError($"No brush found with the name or guid: {brushType}");
            }
        }

        // TODO Find a better home for this
        // Accepts either guid or "Description"
        // Description is matched based on removing all non-alphanumeric characters and lower-casing
        public static BrushDescriptor LookupBrushDescriptor(string brushType)
        {
            if (brushType == null) return null;
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
                string AlphaNumericLowerCased(string s) => Regex.Replace(s, @"\W|_", "").ToLower();
                brushType = AlphaNumericLowerCased(brushType);

                try
                {
                    brushDescriptor = BrushCatalog.m_Instance.AllBrushes
                        .First(x => AlphaNumericLowerCased(x.Description) == brushType);
                }
                catch (InvalidOperationException e)
                {
                    Debug.LogError($"No brush found called: {brushType}");
                }
            }
            return brushDescriptor;
        }

        [ApiEndpoint(
            "color.add.hsv",
            "Adds the supplied values to the current color. Values are hue, saturation and value",
            "0.1,0.2,0.3"
        )]
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

        [ApiEndpoint(
            "color.add.rgb",
            "Adds the supplied values to the current color. Values are red green and blue",
            "0.1,0.2,0.3"
        )]
        public static void AddColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor += new Color(rgb.x, rgb.y, rgb.z);
        }

        [ApiEndpoint(
            "color.set.rgb",
            "Sets the current color. Values are red, green and blue",
            "0.1,0.2,0.3"
        )]
        public static void SetColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor = new Color(rgb.x, rgb.y, rgb.z);
        }

        [ApiEndpoint(
            "color.set.hsv",
            "Sets the current color. Values are hue, saturation and value",
            "0.1,0.2,0.3"
        )]
        public static void SetColorHSV(Vector3 hsv)
        {
            App.BrushColor.CurrentColor = Color.HSVToRGB(hsv.x, hsv.y, hsv.z);
        }

        [ApiEndpoint(
            "color.set.html",
            "Sets the current color. colorString can either be a hex value or a css color name.",
            "darkblue"
        )]
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

        [ApiEndpoint(
            "brush.size.set",
            "Sets the current brush size",
            "0.5"
        )]
        public static void BrushSizeSet(float size)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 = size;
        }

        [ApiEndpoint(
            "brush.size.add",
            "Changes the current brush size by the given amount",
            "0.1"
        )]
        public static void BrushSizeAdd(float amount)
        {
            PointerManager.m_Instance.MainPointer.BrushSize01 += amount;
        }

        [ApiEndpoint(
            "draw.camerapath",
            "Draws along a camera path with the current brush settings",
            "0"
        )]
        public static void DrawCameraPath(int index, float step)
        {
            CameraPathWidget widget = _GetActiveCameraPath(index);
            CameraPath path = widget.Path;
            var points = path.AsTrList(step);
            DrawStrokes.DrawNestedTrList(
                new List<List<TrTransform>> { points },
                TrTransform.identity,
                smoothing: ApiManager.Instance.PathSmoothing
            );
        }
    }

}
