using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{

    [MoonSharpUserData]
    public static class MathfApiWrapper
    {
        public static float Deg2Rad => Mathf.Deg2Rad;
        public static float Epsilon => Mathf.Epsilon;
        public static float Infinity => float.PositiveInfinity;
        public static float NegativeInfinity => float.NegativeInfinity;
        public static float PI => Mathf.PI;
        public static float Rad2Deg => Mathf.Rad2Deg;
        public static float Abs(float f) => Mathf.Abs(f);
        public static float Acos(float f) => Mathf.Acos(f);
        public static bool Approximately(float a, float b) => Mathf.Approximately(a, b);
        public static float Asin(float f) => Mathf.Asin(f);
        public static float Atan(float f) => Mathf.Atan(f);
        public static float Atan2(float y, float x) => Mathf.Atan2(y, x);
        public static float Ceil(float f) => Mathf.Ceil(f);
        public static float Clamp(float value, float min, float max) => Mathf.Clamp(value, min, max);
        public static float Clamp01(float value) => Mathf.Clamp01(value);
        public static int ClosestPowerOfTwo(int value) => Mathf.ClosestPowerOfTwo(value);
        public static float Cos(float f) => Mathf.Cos(f);
        public static float DeltaAngle(float current, float target) => Mathf.DeltaAngle(current, target);
        public static float Exp(float power) => Mathf.Exp(power);
        public static float Floor(float f) => Mathf.Floor(f);
        public static float InverseLerp(float a, float b, float value) => Mathf.InverseLerp(a, b, value);
        public static bool IsPowerOfTwo(int value) => Mathf.IsPowerOfTwo(value);
        public static float Lerp(float a, float b, float t) => Mathf.Lerp(a, b, t);
        public static float LerpAngle(float a, float b, float t) => Mathf.LerpAngle(a, b, t);
        public static float LerpUnclamped(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);
        public static float Log(float f, float p) => Mathf.Log(f, p);
        public static float Log10(float f) => Mathf.Log10(f);
        public static float Max(float a, float b) => Mathf.Max(a, b);
        public static float Max(params float[] values) => Mathf.Max(values);
        public static float Min(float a, float b) => Mathf.Min(a, b);
        public static float Min(params float[] values) => Mathf.Min(values);
        public static float MoveTowards(float current, float target, float maxDelta) => Mathf.MoveTowards(current, target, maxDelta);
        public static int NextPowerOfTwo(int value) => Mathf.NextPowerOfTwo(value);
        public static float PerlinNoise(float x, float y) => Mathf.PerlinNoise(x, y);
        public static float PingPong(float t, float length) => Mathf.PingPong(t, length);
        public static float Pow(float f, float p) => Mathf.Pow(f, p);
        public static float Repeat(float t, float length) => Mathf.Repeat(t, length);
        public static float Round(float f) => Mathf.Round(f);
        public static float Sign(float f) => Mathf.Sign(f);
        public static float Sin(float f) => Mathf.Sin(f);
        public static float Sqrt(float f) => Mathf.Sqrt(f);
        public static float SmoothStep(float from, float to, float t) => Mathf.SmoothStep(from, to, t);
        public static float Tan(float f) => Mathf.Tan(f);
    }
}