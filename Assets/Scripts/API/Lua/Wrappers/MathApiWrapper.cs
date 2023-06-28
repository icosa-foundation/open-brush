using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{

    [LuaDocsDescription("Various maths functions")]
    [MoonSharpUserData]
    public static class MathApiWrapper
    {
        [LuaDocsDescription("A constant that when multiplied by a value in degrees converts it to radians")]
        public static float deg2Rad => Mathf.Deg2Rad;

        [LuaDocsDescription("The smallest value that a float can have such that 1.0+ ε != 1.0")]
        public static float epsilon => Mathf.Epsilon;

        [LuaDocsDescription("Positive Infinity")]
        public static float positiveInfinity => float.PositiveInfinity;

        [LuaDocsDescription("Negative Infinity")]
        public static float negativeInfinity => float.NegativeInfinity;

        [LuaDocsDescription("The value of Pi")]
        public static float pi => Mathf.PI;

        [LuaDocsDescription("A constant that when multiplied by a value in radians converts it to degrees")]
        public static float rad2Deg => Mathf.Rad2Deg;

        [LuaDocsDescription("Returns the absolute value of f")]
        public static float Abs(float f) => Mathf.Abs(f);

        [LuaDocsDescription("Returns the arc-cosine of f - the angle in radians whose cosine is f")]
        public static float Acos(float f) => Mathf.Acos(f);

        [LuaDocsDescription("Compares two floating point values if they are similar")]
        public static bool Approximately(float a, float b) => Mathf.Approximately(a, b);

        [LuaDocsDescription("Returns the arc-sine of f - the angle in radians whose sine is f")]
        public static float Asin(float f) => Mathf.Asin(f);

        [LuaDocsDescription("Returns the arc-tangent of f - the angle in radians whose tangent is f")]
        public static float Atan(float f) => Mathf.Atan(f);

        [LuaDocsDescription("Returns the angle in radians whose tan is y/x")]
        public static float Atan2(float y, float x) => Mathf.Atan2(y, x);

        [LuaDocsDescription("Returns the smallest integer greater to or equal to f")]
        public static float Ceil(float f) => Mathf.Ceil(f);

        [LuaDocsDescription("Clamps the given value between the given minimum float and maximum float values. Returns the given value if it is within the min and max range")]
        public static float Clamp(float value, float min, float max) => Mathf.Clamp(value, min, max);

        [LuaDocsDescription("Clamps value between 0 and 1 and returns value")]
        public static float Clamp01(float value) => Mathf.Clamp01(value);

        [LuaDocsDescription("Returns the closest power of two value")]
        public static int ClosestPowerOfTwo(int value) => Mathf.ClosestPowerOfTwo(value);

        [LuaDocsDescription("Returns the cosine of angle f")]
        public static float Cos(float f) => Mathf.Cos(f);

        [LuaDocsDescription("Calculates the shortest difference between two given angles")]
        public static float DeltaAngle(float current, float target) => Mathf.DeltaAngle(current, target);

        [LuaDocsDescription("Returns e raised to the specified power")]
        public static float Exp(float power) => Mathf.Exp(power);
        [LuaDocsDescription("Rounds a float down to the largest integer less than or equal to it")]
        public static float Floor(float f) => Mathf.Floor(f);

        [LuaDocsDescription("Inverse linear interpolation between two values by given ratio")]
        public static float InverseLerp(float a, float b, float value) => Mathf.InverseLerp(a, b, value);

        [LuaDocsDescription("Determines whether a value is a power of two")]
        public static bool IsPowerOfTwo(int value) => Mathf.IsPowerOfTwo(value);

        [LuaDocsDescription("Linearly interpolates two floats by a ratio")]
        public static float Lerp(float a, float b, float t) => Mathf.Lerp(a, b, t);

        [LuaDocsDescription("Linearly interpolates two angles by a ratio")]
        public static float LerpAngle(float a, float b, float t) => Mathf.LerpAngle(a, b, t);

        [LuaDocsDescription("Linearly interpolates two floats by a ratio. The interpolation is not clamped")]
        public static float LerpUnclamped(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);

        [LuaDocsDescription("Returns the logarithm of a specified number in a specified base")]
        public static float Log(float f, float p) => Mathf.Log(f, p);

        [LuaDocsDescription("Returns the base 10 logarithm of a specified number")]
        public static float Log10(float f) => Mathf.Log10(f);

        [LuaDocsDescription("Returns the larger of two float numbers")]
        public static float Max(float a, float b) => Mathf.Max(a, b);

        [LuaDocsDescription("Returns the largest value in a sequence of float numbers")]
        public static float Max(params float[] values) => Mathf.Max(values);

        [LuaDocsDescription("Returns the smaller of two float numbers")]
        public static float Min(float a, float b) => Mathf.Min(a, b);

        [LuaDocsDescription("Returns the smallest value in a sequence of float numbers")]
        public static float Min(params float[] values) => Mathf.Min(values);

        [LuaDocsDescription("Moves a value current towards target")]
        public static float MoveTowards(float current, float target, float maxDelta) => Mathf.MoveTowards(current, target, maxDelta);

        [LuaDocsDescription("Returns the smallest power of two greater than or equal to the specified number")]
        public static int NextPowerOfTwo(int value) => Mathf.NextPowerOfTwo(value);

        [LuaDocsDescription("Creates a two-dimensional Perlin noise map")]
        public static float PerlinNoise(float x, float y) => Mathf.PerlinNoise(x, y);

        [LuaDocsDescription("Loops the value t, so that it is never larger than length and never smaller than 0")]
        public static float PingPong(float t, float length) => Mathf.PingPong(t, length);

        [LuaDocsDescription("Returns f raised to the specified power")]
        public static float Pow(float f, float p) => Mathf.Pow(f, p);

        [LuaDocsDescription("Loops the value t, so that it is never larger than length and never smaller than 0")]
        public static float Repeater(float t, float length) => Mathf.Repeat(t, length);

        [LuaDocsDescription("Rounds a float to the nearest integer")]
        public static float Round(float f) => Mathf.Round(f);

        [LuaDocsDescription("Returns the sign of a float")]
        public static float Sign(float f) => Mathf.Sign(f);

        [LuaDocsDescription("Returns the sine of an angle")]
        public static float Sin(float f) => Mathf.Sin(f);

        [LuaDocsDescription("Returns the square root of a float")]
        public static float Sqrt(float f) => Mathf.Sqrt(f);

        [LuaDocsDescription("Smoothly interpolates between the range [from, to] by the ratio t")]
        public static float SmoothStep(float from, float to, float t) => Mathf.SmoothStep(from, to, t);

        [LuaDocsDescription("Returns the tangent of an angle")]
        public static float Tan(float f) => Mathf.Tan(f);

        [LuaDocsDescription("Returns the hyperbolic sine of a float")]
        public static float Sinh(float f) => (float)System.Math.Sinh(f);

        [LuaDocsDescription("Returns the hyperbolic cosine of a float")]
        public static float Cosh(float f) => (float)System.Math.Cosh(f);

        [LuaDocsDescription("Returns the hyperbolic tangent of a float")]
        public static float Tanh(float f) => (float)System.Math.Tanh(f);    }
}
