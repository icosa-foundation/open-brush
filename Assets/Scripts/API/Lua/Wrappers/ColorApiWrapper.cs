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

using System;
using MoonSharp.Interpreter;
using UnityEngine;
using Object = System.Object;

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
            var success = ColorUtility.TryParseHtmlString(html, out Color color);
            _Color = success ? color : Color.magenta;
        }

        [LuaDocsDescription("Creates a new instance of a color with the specified RGB values")]
        [LuaDocsExample("myColor = Color:New(0.5, 0, 1)")]
        [LuaDocsParameter("r", "The red component of the color. Default is 0")]
        [LuaDocsParameter("g", "The green component of the color. Default is 0")]
        [LuaDocsParameter("b", "The blue component of the color. Default is 0")]
        [LuaDocsReturnValue("instance of the Color")]
        public static ColorApiWrapper New(float r = 0, float g = 0, float b = 0)
        {
            var instance = new ColorApiWrapper(r, g, b);
            return instance;
        }

        [LuaDocsDescription("Creates a new instance of the Color with the color parsed from the specified HTML string")]
        [LuaDocsExample("myColor = Color:New(\"D3B322\")")]
        [LuaDocsParameter("html", "The HTML string representing the color")]
        [LuaDocsReturnValue("Returns the color. Invalid html inputs return bright magenta (r=1, g=0, b=1)")]
        public static ColorApiWrapper New(string html)
        {
            return new ColorApiWrapper(html);
        }

        [LuaDocsDescription("Converts the color to its string representation")]
        [LuaDocsReturnValue("A string representation of the color")]
        public override string ToString()
        {
            return $"Color({_Color.r}, {_Color.g}, {_Color.b})";
        }

        [LuaDocsDescription("The color component at the specified index")]
        public float this[int index]
        {
            get => _Color[index];
            set => _Color[index] = value;
        }

        [LuaDocsDescription("The red component")]
        public float r => _Color.r;

        [LuaDocsDescription("The green component")]
        public float g => _Color.g;

        [LuaDocsDescription("The blue component")]
        public float b => _Color.b;

        [LuaDocsDescription("The alpha component")]
        public float a => _Color.a;

        [LuaDocsDescription("The grayscale value")]
        public float grayscale => _Color.grayscale;

        [LuaDocsDescription("The gamma color space representation")]
        public Color gamma => _Color.gamma;

        [LuaDocsDescription("The linear color space representation")]
        public Color linear => _Color.linear;

        [LuaDocsDescription("The maximum color component value")]
        public float maxColorComponent => _Color.maxColorComponent;


        [LuaDocsDescription("The HTML hex string of the color (for example \"A4D0FF\")")]
        public string html => ColorUtility.ToHtmlStringRGB(_Color);

        [LuaDocsDescription("The grayscale value")]
        public float greyscale => _Color.grayscale;

        [LuaDocsDescription("The hue, saturation and brightess")]
        public Vector3 hsv
        {
            get
            {
                Color.RGBToHSV(_Color, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
        }

        [LuaDocsDescription("Performs a linear interpolation between two colors")]
        [LuaDocsExample("newColor = Color:Lerp(color1, color2, 0.5)")]
        [LuaDocsParameter("a", "The start color")]
        [LuaDocsParameter("b", "The end color")]
        [LuaDocsParameter("t", "The interpolation value. Should be between 0 and 1")]
        [LuaDocsReturnValue("The interpolated color")]
        public static Color Lerp(Color a, Color b, float t) => Color.Lerp(a, b, t);

        [LuaDocsDescription("Performs a linear interpolation between two colors without clamping the interpolation parameter")]
        [LuaDocsExample("newColor = Color:Lerp(color1, color2, 1.5)")]
        [LuaDocsParameter("a", "The start color")]
        [LuaDocsParameter("b", "The end color")]
        [LuaDocsParameter("t", "The interpolation value")]
        [LuaDocsReturnValue("color")]
        public static Color LerpUnclamped(Color a, Color b, float t) => Color.LerpUnclamped(a, b, t);

        [LuaDocsDescription("Converts an HSV color to an RGB color")]
        [LuaDocsExample("newColor = Color:HsvToRgb(0.5, 0.9, 0.5)")]
        [LuaDocsParameter("h", "The hue value. Should be between 0 and 1")]
        [LuaDocsParameter("s", "The saturation value. Should be between 0 and 1")]
        [LuaDocsParameter("v", "The value value. Should be between 0 and 1")]
        [LuaDocsReturnValue("color")]
        public static Color HsvToRgb(float h, float s, float v) => Color.HSVToRGB(
            Mathf.Clamp01(h),
            Mathf.Clamp01(s),
            Mathf.Clamp01(v)
        );

        [LuaDocsDescription("Converts an HSV Vector3 to an RGB color")]
        [LuaDocsExample("newColor = Color:HsvToRgb(myHsv)")]
        [LuaDocsParameter("hsv", "A Vector3 with xyz representing hsv. All values between 0 and 1")]
        [LuaDocsReturnValue("color")]
        public static Color HsvToRgb(Vector3 hsv) => Color.HSVToRGB(
            Mathf.Clamp01(hsv.x),
            Mathf.Clamp01(hsv.y),
            Mathf.Clamp01(hsv.z)
        );

        [LuaDocsDescription("The color black")]
        public static Color black => Color.black;

        [LuaDocsDescription("The color blue")]
        public static Color blue => Color.blue;

        [LuaDocsDescription("The color cyan")]
        public static Color cyan => Color.cyan;

        [LuaDocsDescription("The color gray")]
        public static Color gray => Color.gray;

        [LuaDocsDescription("The color green")]
        public static Color green => Color.green;

        [LuaDocsDescription("The color grey")]
        public static Color grey => Color.grey;

        [LuaDocsDescription("The color magenta")]
        public static Color magenta => Color.magenta;

        [LuaDocsDescription("The color red")]
        public static Color red => Color.red;

        [LuaDocsDescription("The color white")]
        public static Color white => Color.white;

        [LuaDocsDescription("The color yellow")]
        public static Color yellow => Color.yellow;

        // Operators

        public static Color operator +(ColorApiWrapper a, ColorApiWrapper b) => a._Color + b._Color;
        public static Color operator -(ColorApiWrapper a, ColorApiWrapper b) => a._Color - b._Color;
        public static Color operator *(ColorApiWrapper a, ColorApiWrapper b) => a._Color * b._Color;
        public static Color operator /(ColorApiWrapper a, float b) => a._Color / b;

        [LuaDocsDescription("Adds the specified color to this color")]
        [LuaDocsExample("newColor = color1:Add(color2)")]
        [LuaDocsParameter("other", "The color to add")]
        [LuaDocsReturnValue("color")]
        public Color Add(Color other) => _Color + other;

        [LuaDocsDescription("Adds the specified RGB values to this color")]
        [LuaDocsExample("newColor = color1:Add(0.5, 0, 0.1)")]
        [LuaDocsParameter("r", "The red component value to add")]
        [LuaDocsParameter("g", "The green component value to add")]
        [LuaDocsParameter("b", "The blue component value to add")]
        [LuaDocsReturnValue("color")]
        public Color Add(float r, float g, float b) => _Color + new Color(r, g, b);

        [LuaDocsDescription("Subtracts the specified color from this color")]
        [LuaDocsExample("newColor = color1:Subtract(color2)")]
        [LuaDocsParameter("other", "The color to subtract")]
        [LuaDocsReturnValue("color")]
        public Color Subtract(Color other) => _Color - other;

        [LuaDocsDescription("Subtracts the specified RGB values from this color")]
        [LuaDocsExample("newColor = color1:Subtract(0.5, 0.25, 0)")]
        [LuaDocsParameter("r", "The red component value to subtract")]
        [LuaDocsParameter("g", "The green component value to subtract")]
        [LuaDocsParameter("b", "The blue component value to subtract")]
        [LuaDocsReturnValue("color")]
        public Color Subtract(float r, float g, float b) => _Color - new Color(r, g, b);

        [LuaDocsDescription("Multiplies this color by the specified value")]
        [LuaDocsExample("newColor = color1:Multiply(0.5)")]
        [LuaDocsParameter("value", "The value to multiply")]
        [LuaDocsReturnValue("color")]
        public Color Multiply(float value) => _Color * value;

        [LuaDocsDescription("Multiplies this color by the specified RGB values")]
        [LuaDocsExample("newColor = color1:Multiply(0.85, 0, 0)")]
        [LuaDocsParameter("r", "The red component value to multiply")]
        [LuaDocsParameter("g", "The green component value to multiply")]
        [LuaDocsParameter("b", "The blue component value to multiply")]
        [LuaDocsReturnValue("color")]
        public Color Multiply(float r, float g, float b) => _Color * new Color(r, g, b);

        [LuaDocsDescription("Divides this color by the specified value")]
        [LuaDocsExample("newColor = color1:Divide(0.5)")]
        [LuaDocsParameter("value", "The value to divide")]
        [LuaDocsReturnValue("color")]
        public Color Divide(float value) => _Color / value;

        [LuaDocsDescription("Determines whether this color is equal to the specified color")]
        [LuaDocsExample("if color1:Equals(color2) then print(\"colors are the same\") end")]
        [LuaDocsParameter("other", "The color to compare")]
        [LuaDocsReturnValue("true if this color is equal to the specified color; otherwise, false")]
        public bool Equals(ColorApiWrapper other) => Equals(other._Color);

        public override bool Equals(Object obj)
        {
            var other = obj as ColorApiWrapper;
            return other != null && _Color == other._Color;
        }
        public override int GetHashCode() => 0; // Always return 0. Dicts and HashSets will have to use Equals to compare

        [LuaDocsDescription("Determines whether this color is equal to the specified RGB values")]
        [LuaDocsExample("if color1:Equals(1, 0, 0) then print(\"the color is red\") end")]
        [LuaDocsParameter("r", "The red component value to compare")]
        [LuaDocsParameter("g", "The green component value to compare")]
        [LuaDocsParameter("b", "The blue component value to compare")]
        [LuaDocsReturnValue("true if this color is equal to the specified RGB values; otherwise, false")]
        public bool Equals(float r, float g, float b) => _Color == new Color(r, g, b);

        [LuaDocsDescription("Determines whether this color is not equal to the specified color")]
        [LuaDocsExample("if color1:NotEquals(color2) then print(\"colors are different\") end")]
        [LuaDocsParameter("other", "The color to compare")]
        [LuaDocsReturnValue("true if this color is not equal to the specified color; otherwise, false")]
        public bool NotEquals(ColorApiWrapper other) => !Equals(other);

        [LuaDocsDescription("Determines whether this color is not equal to the specified RGB values")]
        [LuaDocsExample("if color1:NotEquals(0, 1, 0) then print(\"color is not green\") end")]
        [LuaDocsParameter("r", "The red component value to compare")]
        [LuaDocsParameter("g", "The green component value to compare")]
        [LuaDocsParameter("b", "The blue component value to compare")]
        [LuaDocsReturnValue("true if this color is not equal to the specified RGB values; otherwise, false")]
        public bool NotEquals(float r, float g, float b) => _Color != new Color(r, g, b);
    }
}
