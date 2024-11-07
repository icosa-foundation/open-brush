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

using System.IO;
using TMPro;
using Unity.VectorGraphics;
using UnityEngine;
using WaterTrans.GlyphLoader;

namespace TiltBrush
{
    public static class SvgTextUtils
    {
        public static string GenerateSvgOutlineForText(string text, string fontPath)
        {
            Color color = Color.white;

            fontPath = Path.Combine(App.UserPath(), "Fonts", fontPath);

            float unit = 1f;

            float x = 0;
            float y = 0;
            var svg = new System.Text.StringBuilder();
            svg.AppendLine(
                "<svg width='440' height='140' viewBox='0 0 440 140' xmlns='http://www.w3.org/2000/svg' version='1.1'>");
            var stream = new FileStream(fontPath, FileMode.Open, FileAccess.Read);
            var typeface = new Typeface(stream);
            double baseline = typeface.Baseline * unit;

            foreach (char character in text)
            {
                var glyphIndex = typeface.CharacterToGlyphMap[character];
                var geometry = typeface.GetGlyphOutline(glyphIndex, unit);
                double advanceWidth = typeface.AdvanceWidths[glyphIndex] * unit;
                string svgPath = geometry.Figures.ToString(x, y + baseline);
                svg.AppendLine($"<path d='{svgPath}' fill='#{ColorUtility.ToHtmlStringRGB(color)}' stroke-width='0' />");
                x += (float)advanceWidth;
            }
            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        public static Mesh GenerateOpenTypeTextMesh(string text, string fontPath, float extrusionDepth, TrTransform tr)
        {
            var svg = GenerateSvgOutlineForText(text, fontPath);
            var importer = new RuntimeSVGImporter();
            return importer.ParseToMesh(svg, tr.ToMatrix4x4(), extrusionDepth);
        }

        public static TMP_FontAsset ConvertOpenTypeToTMPro(string path)
        {
            Font osFont = new Font(path);
            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(osFont);

            return fontAsset;
        }
    }
}
