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
        [LuaDocsReturnValue("instance of the Color")]
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

        [LuaDocsDescription("Gets or sets the color component at the specified index")]
        public float this[int index]
        {
            get => _Color[index];
            set => _Color[index] = value;
        }

        [LuaDocsDescription("Gets the red component of the color")]
        public float r => _Color.r;

        [LuaDocsDescription("Gets the green component of the color")]
        public float g => _Color.g;

        [LuaDocsDescription("Gets the blue component of the color")]
        public float b => _Color.b;

        [LuaDocsDescription("Gets the alpha component of the color")]
        public float a => _Color.a;

        [LuaDocsDescription("Gets the grayscale value of the color")]
        public float grayscale => _Color.grayscale;

        [LuaDocsDescription("Gets the gamma color space representation of the color")]
        public Color gamma => _Color.gamma;

        [LuaDocsDescription("Gets the linear color space representation of the color")]
        public Color linear => _Color.linear;

        [LuaDocsDescription("Gets the maximum color component value of the color")]
        public float maxColorComponent => _Color.maxColorComponent;

        [LuaDocsDescription("Calculates the grayscale value of the specified color")]
        [LuaDocsExample("grayAmount = myColor:Greyscale()")]
        [LuaDocsParameter("col", "The color")]
        [LuaDocsReturnValue("value of the color")]
        public static float Greyscale(Color color) => color.grayscale;

        [LuaDocsDescription("Gets the maximum color component value of the specified color")]
        [LuaDocsExample("amount = myColor:MaxColorComponent()")]
        [LuaDocsParameter("col", "The color")]
        [LuaDocsReturnValue("color component value of the color")]
        public static float MaxColorComponent(Color color) => color.maxColorComponent;

        [LuaDocsDescription("Converts the specified color to its HTML string representation")]
        [LuaDocsExample("htmlColor = myColor:ToHtmlString()")]
        [LuaDocsParameter("col", "The color")]
        [LuaDocsReturnValue("string representation of the color")]
        public static string ToHtmlString(Color col) => ColorUtility.ToHtmlStringRGB(col);

        [LuaDocsDescription("Parses the specified HTML string and returns the color")]
        [LuaDocsExample("myColor = Color:ParseHtmlString(htmlColor)")]
        [LuaDocsParameter("html", "The HTML string representing the color")]
        [LuaDocsReturnValue("The color parsed from the HTML string, or magenta if the parsing fails")]
        public static Color ParseHtmlString(string html)
        {
            var success = ColorUtility.TryParseHtmlString(html, out Color color);
            return success ? color : Color.magenta;
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

        [LuaDocsDescription("Converts an RGB color to an HSV color")]
        [LuaDocsExample("myVector = Color:RgbToHsv(myColor")]
        [LuaDocsParameter("rgb", "The RGB color")]
        [LuaDocsReturnValue("color represented as a Vector3, where x is the hue, y is the saturation, and z is the value")]
        public static Vector3 RgbToHsv(Color rgb)
        {
            Color.RGBToHSV(rgb, out float h, out float s, out float v);
            return new Vector3(h, s, v);
        }

        [LuaDocsDescription("Gets the black color")]
        public static Color black => Color.black;

        [LuaDocsDescription("Gets the blue color")]
        public static Color blue => Color.blue;

        [LuaDocsDescription("Gets the clear color")]
        public static Color clear => Color.clear;

        [LuaDocsDescription("Gets the cyan color")]
        public static Color cyan => Color.cyan;

        [LuaDocsDescription("Gets the gray color")]
        public static Color gray => Color.gray;

        [LuaDocsDescription("Gets the green color")]
        public static Color green => Color.green;

        [LuaDocsDescription("Gets the grey color")]
        public static Color grey => Color.grey;

        [LuaDocsDescription("Gets the magenta color")]
        public static Color magenta => Color.magenta;

        [LuaDocsDescription("Gets the red color")]
        public static Color red => Color.red;

        [LuaDocsDescription("Gets the white color")]
        public static Color white => Color.white;

        [LuaDocsDescription("Gets the yellow color")]
        public static Color yellow => Color.yellow;

        [LuaDocsDescription("Adds the specified color to this color")]
        [LuaDocsExample("newColor = color1:Add(color2)")]
        [LuaDocsParameter("b", "The color to add")]
        [LuaDocsReturnValue("color")]
        public Color Add(Color b) => _Color + b;

        [LuaDocsDescription("Adds the specified RGB values to this color")]
        [LuaDocsExample("newColor = color1:Add(0.5, 0, 0.1)")]
        [LuaDocsParameter("r", "The red component value to add")]
        [LuaDocsParameter("g", "The green component value to add")]
        [LuaDocsParameter("b", "The blue component value to add")]
        [LuaDocsReturnValue("color")]
        public Color Add(float r, float g, float b) => _Color + new Color(r, g, b);

        [LuaDocsDescription("Subtracts the specified color from this color")]
        [LuaDocsExample("newColor = color1:Subtract(color2)")]
        [LuaDocsParameter("b", "The color to subtract")]
        [LuaDocsReturnValue("color")]
        public Color Subtract(Color b) => _Color - b;

        [LuaDocsDescription("Subtracts the specified RGB values from this color")]
        [LuaDocsExample("newColor = color1:Subtract(0.5, 0.25, 0)")]
        [LuaDocsParameter("r", "The red component value to subtract")]
        [LuaDocsParameter("g", "The green component value to subtract")]
        [LuaDocsParameter("b", "The blue component value to subtract")]
        [LuaDocsReturnValue("color")]
        public Color Subtract(float r, float g, float b) => _Color - new Color(r, g, b);

        [LuaDocsDescription("Multiplies this color by the specified value")]
        [LuaDocsExample("newColor = color1:Multiply(0.5)")]
        [LuaDocsParameter("b", "The value to multiply")]
        [LuaDocsReturnValue("color")]
        public Color Multiply(float b) => _Color * b;

        [LuaDocsDescription("Multiplies this color by the specified RGB values")]
        [LuaDocsExample("newColor = color1:Multiply(0.85, 0, 0)")]
        [LuaDocsParameter("r", "The red component value to multiply")]
        [LuaDocsParameter("g", "The green component value to multiply")]
        [LuaDocsParameter("b", "The blue component value to multiply")]
        [LuaDocsReturnValue("color")]
        public Color Multiply(float r, float g, float b) => _Color * new Color(r, g, b);

        [LuaDocsDescription("Divides this color by the specified value")]
        [LuaDocsExample("newColor = color1:Divide(0.5)")]
        [LuaDocsParameter("b", "The value to divide")]
        [LuaDocsReturnValue("color")]
        public Color Divide(float b) => _Color / b;

        [LuaDocsDescription("Determines whether this color is equal to the specified color")]
        [LuaDocsExample("if color1:Equals(color2) then print(\"colors are the same\") end")]
        [LuaDocsParameter("b", "The color to compare")]
        [LuaDocsReturnValue("true if this color is equal to the specified color; otherwise, false")]
        public bool Equals(Color b) => _Color == b;

        [LuaDocsDescription("Determines whether this color is equal to the specified RGB values")]
        [LuaDocsExample("if color1:Equals(1, 0, 0) then print(\"the color is red\") end")]
        [LuaDocsParameter("r", "The red component value to compare")]
        [LuaDocsParameter("g", "The green component value to compare")]
        [LuaDocsParameter("b", "The blue component value to compare")]
        [LuaDocsReturnValue("true if this color is equal to the specified RGB values; otherwise, false")]
        public bool Equals(float r, float g, float b) => _Color == new Color(r, g, b);

        [LuaDocsDescription("Determines whether this color is not equal to the specified color")]
        [LuaDocsExample("if color1:NotEquals(color2) then print(\"colors are different\") end")]
        [LuaDocsParameter("b", "The color to compare")]
        [LuaDocsReturnValue("true if this color is not equal to the specified color; otherwise, false")]
        public bool NotEquals(Color b) => _Color != b;

        [LuaDocsDescription("Determines whether this color is not equal to the specified RGB values")]
        [LuaDocsExample("if color1:NotEquals(0, 1, 0) then print(\"color is not green\") end")]
        [LuaDocsParameter("r", "The red component value to compare")]
        [LuaDocsParameter("g", "The green component value to compare")]
        [LuaDocsParameter("b", "The blue component value to compare")]
        [LuaDocsReturnValue("true if this color is not equal to the specified RGB values; otherwise, false")]
        public bool NotEquals(float r, float g, float b) => _Color != new Color(r, g, b);

        [LuaDocsDescription("Adds two colors together")]
        [LuaDocsExample("newColor = Color:Add(color1, color2)")]
        [LuaDocsParameter("a", "The first color")]
        [LuaDocsParameter("b", "The second color")]
        [LuaDocsReturnValue("color")]
        public static Color Add(Color a, Color b) => a + b;

        [LuaDocsDescription("Subtracts the second color from the first color")]
        [LuaDocsExample("newColor = Color:Subtract(color1, color2)")]
        [LuaDocsParameter("a", "The first color")]
        [LuaDocsParameter("b", "The second color")]
        [LuaDocsReturnValue("color")]
        public static Color Subtract(Color a, Color b) => a - b;

        [LuaDocsDescription("Multiplies the color by the specified value")]
        [LuaDocsExample("newColor = Color:Multiply(color1, color2)")]
        [LuaDocsParameter("a", "The color")]
        [LuaDocsParameter("b", "The value to multiply")]
        [LuaDocsReturnValue("color")]
        public static Color Multiply(Color a, float b) => a * b;

        [LuaDocsDescription("Divides the color by the specified value")]
        [LuaDocsExample("newColor = Color:Divide(color1, color2)")]
        [LuaDocsParameter("a", "The color")]
        [LuaDocsParameter("b", "The value to divide")]
        [LuaDocsReturnValue("color")]
        public static Color Divide(Color a, float b) => a / b;

        [LuaDocsDescription("Determines whether two colors are equal")]
        [LuaDocsExample("colorsAreSame = Color:Equals(color1, color2)")]
        [LuaDocsParameter("a", "The first color")]
        [LuaDocsParameter("b", "The second color")]
        [LuaDocsReturnValue("true if the two colors are equal; otherwise, false")]
        public static bool Equals(Color a, Color b) => a == b;

        [LuaDocsDescription("Determines whether two colors are not equal")]
        [LuaDocsExample("colorsAreDifferent = Color:NotEquals(color1, color2)")]
        [LuaDocsParameter("a", "The first color")]
        [LuaDocsParameter("b", "The second color")]
        [LuaDocsReturnValue("true if the two colors are not equal; otherwise, false")]
        public static bool NotEquals(Color a, Color b) => a != b;
    }
}
