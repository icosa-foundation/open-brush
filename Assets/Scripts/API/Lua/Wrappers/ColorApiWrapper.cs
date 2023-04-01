// Copyright 2023 The Open Brush Authors
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

using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public static class ColorApiWrapper
    {
        public static float greyscale(Color col) => col.grayscale;
        public static float maxColorComponent(Color col) => col.maxColorComponent;
        public static string toHtmlString(Color col) => ColorUtility.ToHtmlStringRGB(col);
        public static Color parseHtmlString(string html)
        {
            var success = ColorUtility.TryParseHtmlString(html, out Color color);
            return success ? color : Color.magenta;
        }
        public static Color lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);
        public static Color lerpUnclamped(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);
        public static Color hsvToRgb(float h, float s, float v) => Color.HSVToRGB(
            Mathf.Clamp01(h),
            Mathf.Clamp01(s),
            Mathf.Clamp01(v)
        );

        public static Vector3 rgbToHsv(Color rgb)
        {
            Color.RGBToHSV(rgb, out float h, out float s, out float v);
            return new Vector3(h, s, v);
        }

        // Operators
        public static Color add(Color a, Color b) => a + b;
        public static Color subtract(Color a, Color b) => a - b;
        public static Color multiply(Color a, float b) => a * b;
        public static Color divide(Color a, float b) => a / b;
        public static bool equals(Color a, Color b) => a == b;
        public static bool notEquals(Color a, Color b) => a != b;
    }
}
