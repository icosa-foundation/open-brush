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
    [LuaDocsDescription("An RGB color")]
    [MoonSharpUserData]
    public class ColorApiWrapper
    {
        public Color _Color;

        public ColorApiWrapper(float r = 0, float g = 0, float b = 0)
        {
            _Color = new Color(r, g, b);
        }

        public ColorApiWrapper(Color color)
        {
            _Color = color;
        }

        public ColorApiWrapper(string html)
        {
            _Color = ParseHtmlString(html);
        }

        public static ColorApiWrapper New(float r = 0, float g = 0, float b = 0)
        {
            var instance = new ColorApiWrapper(r, g, b);
            return instance;
        }

        public static ColorApiWrapper New(string html)
        {
            return new ColorApiWrapper(html);
        }

        public override string ToString()
        {
            return $"Color({_Color.r}, {_Color.g}, {_Color.b})";
        }

        public float this[int index]
        {
            get => _Color[index];
            set => _Color[index] = value;
        }

        public float r => _Color.r;
        public float g => _Color.g;
        public float b => _Color.b;
        public float a => _Color.a;
        public float grayscale => _Color.grayscale;
        public Color gamma => _Color.gamma;
        public Color linear => _Color.linear;
        public float maxColorComponent => _Color.maxColorComponent;

        public static float Greyscale(Color col) => col.grayscale;
        public static float MaxColorComponent(Color col) => col.maxColorComponent;
        public static string ToHtmlString(Color col) => ColorUtility.ToHtmlStringRGB(col);
        public static Color ParseHtmlString(string html)
        {
            var success = ColorUtility.TryParseHtmlString(html, out Color color);
            return success ? color : Color.magenta;
        }
        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);
        public static Color LerpUnclamped(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);
        public static Color HsvToRgb(float h, float s, float v) => Color.HSVToRGB(
            Mathf.Clamp01(h),
            Mathf.Clamp01(s),
            Mathf.Clamp01(v)
        );

        public static Vector3 RgbToHsv(Color rgb)
        {
            Color.RGBToHSV(rgb, out float h, out float s, out float v);
            return new Vector3(h, s, v);
        }

        public static Color black => Color.black;
        public static Color blue => Color.blue;
        public static Color clear => Color.clear;
        public static Color cyan => Color.cyan;
        public static Color gray => Color.gray;
        public static Color green => Color.green;
        public static Color grey => Color.grey;
        public static Color magenta => Color.magenta;
        public static Color red => Color.red;
        public static Color white => Color.white;
        public static Color yellow => Color.yellow;

        // Operators
        public Color Add(Color b) => _Color + b;
        public Color Add(float r, float g, float b) => _Color + new Color(r, g, b);
        public Color Subtract(Color b) => _Color - b;
        public Color Subtract(float r, float g, float b) => _Color - new Color(r, g, b);
        public Color Multiply(float b) => _Color * b;
        public Color Multiply(float r, float g, float b) => _Color * new Color(r, g, b);
        public Color Divide(float b) => _Color / b;
        public bool Equals(Color b) => _Color == b;
        public bool Equals(float r, float g, float b) => _Color == new Color(r, g, b);
        public bool NotEquals(Color b) => _Color != b;
        public bool NotEquals(float r, float g, float b) => _Color != new Color(r, g, b);

        // Static Operators
        public static Color Add(Color a, Color b) => a + b;
        public static Color Subtract(Color a, Color b) => a - b;
        public static Color Multiply(Color a, float b) => a * b;
        public static Color Divide(Color a, float b) => a / b;
        public static bool Equals(Color a, Color b) => a == b;
        public static bool NotEquals(Color a, Color b) => a != b;
    }
}
