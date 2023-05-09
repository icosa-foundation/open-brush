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
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        public static void _DrawFromFloatList(List<List<List<float>>> floatPaths, TrTransform tr, float brushScale = 1f, bool rawStroke = false)
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
            DrawStrokes.DrawNestedTrList(allTrList, tr, null, brushScale, rawStroke);
        }

        [ApiEndpoint("draw.paths", "Draws a series of paths at the current brush position [[[x1,y1,z1],[x2,y2,z2], etc...]]. Does not move the brush position")]
        public static void DrawPaths(string jsonString)
        {
            var origin = TrTransform.T(ApiManager.Instance.BrushPosition);
            var paths = JsonConvert.DeserializeObject<List<List<List<float>>>>($"[{jsonString}]");
            _DrawFromFloatList(paths, origin);
        }

        [ApiEndpoint("draw.path", "Draws a path at the current brush position [x1,y1,z1],[x2,y2,z2], etc.... Does not move the brush position")]
        public static void DrawPath(string jsonString)
        {
            var origin = ApiManager.Instance.BrushPosition;
            var path = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            _DrawSinglePath(path, origin);
        }

        [ApiEndpoint("draw.stroke", "Draws an exact brush stroke as recorded in another app")]
        public static void DrawStroke(string jsonString)
        {
            // TODO Use brush rotation
            var strokeData = JsonConvert.DeserializeObject<List<List<float>>>($"[{jsonString}]");
            _DrawSinglePath(strokeData, Vector3.zero, rawStroke: true);
        }

        public static void _DrawSinglePath(List<List<float>> floatPath, Vector3 origin, float scale = 1f, float brushScale = 1f, bool rawStroke = false)
        {
            var floatPaths = new List<List<List<float>>> { floatPath };
            var tr = TrTransform.TRS(origin, Quaternion.identity, scale);
            _DrawFromFloatList(floatPaths, tr, brushScale, rawStroke);
        }

        [ApiEndpoint("draw.polygon", "Draws a polygon at the current brush position. Does not move the brush position")]
        public static void DrawPolygon(int sides, float radius, float angle)
        {
            var tr = TrTransform.TRS(ApiManager.Instance.BrushPosition, Quaternion.Euler(0, 0, angle), radius);
            DrawStrokes.DrawPolygon(sides, tr);
        }

        [ApiEndpoint("draw.text", "Draws the characters supplied at the current brush position")]
        public static void Text(string text)
        {
            var trMatrix = TrTransform.T(ApiManager.Instance.BrushPosition);
            DrawStrokes.DrawText(text, trMatrix);
        }

        [ApiEndpoint("draw.svg", "Draws the path supplied as an SVG Path string at the current brush position")]
        public static void SvgPath(string svgPathString)
        {
            // SVG paths are usually scaled rather large so scale down 100x
            var trMatrix = TrTransform.TRS(ApiManager.Instance.BrushPosition, Quaternion.identity, 0.01f);
            DrawStrokes.DrawSvgPathString(svgPathString, trMatrix);

        }

        [ApiEndpoint("brush.type", "Changes the brush. brushType can either be the brush name or it's guid. brushes are listed in the /help screen")]
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
                brushType = brushType.ToLower().Trim().Replace(" ", "");
                try
                {
                    brushDescriptor = BrushCatalog.m_Instance.AllBrushes
                        .First(x => x.m_Description
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
            return brushDescriptor;
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

        [ApiEndpoint("color.set.rgb", "Sets the current color. Values are red, green and blue")]
        public static void SetColorRGB(Vector3 rgb)
        {
            App.BrushColor.CurrentColor = new Color(rgb.x, rgb.y, rgb.z);
        }

        [ApiEndpoint("color.set.hsv", "Sets the current color. Values are hue, saturation and value")]
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
        public static void DrawCameraPath(int index, float step)
        {
            CameraPathWidget widget = _GetActiveCameraPath(index);
            CameraPath path = widget.Path;
            DrawStrokes.DrawCameraPath(path, step);
        }
    }

}
