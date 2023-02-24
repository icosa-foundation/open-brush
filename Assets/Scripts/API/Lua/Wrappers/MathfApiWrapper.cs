using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{

    [MoonSharpUserData]
    public static class MathfApiWrapper
    {
        public static float deg2Rad => Mathf.Deg2Rad;
        public static float epsilon => Mathf.Epsilon;
        public static float infinity => float.PositiveInfinity;
        public static float negativeInfinity => float.NegativeInfinity;
        public static float pI => Mathf.PI;
        public static float rad2Deg => Mathf.Rad2Deg;
        public static float abs(float f) => Mathf.Abs(f);
        public static float acos(float f) => Mathf.Acos(f);
        public static bool approximately(float a, float b) => Mathf.Approximately(a, b);
        public static float asin(float f) => Mathf.Asin(f);
        public static float atan(float f) => Mathf.Atan(f);
        public static float atan2(float y, float x) => Mathf.Atan2(y, x);
        public static float ceil(float f) => Mathf.Ceil(f);
        public static float clamp(float value, float min, float max) => Mathf.Clamp(value, min, max);
        public static float clamp01(float value) => Mathf.Clamp01(value);
        public static int closestPowerOfTwo(int value) => Mathf.ClosestPowerOfTwo(value);
        public static float cos(float f) => Mathf.Cos(f);
        public static float deltaAngle(float current, float target) => Mathf.DeltaAngle(current, target);
        public static float exp(float power) => Mathf.Exp(power);
        public static float floor(float f) => Mathf.Floor(f);
        public static float inverseLerp(float a, float b, float value) => Mathf.InverseLerp(a, b, value);
        public static bool isPowerOfTwo(int value) => Mathf.IsPowerOfTwo(value);
        public static float lerp(float a, float b, float t) => Mathf.Lerp(a, b, t);
        public static float lerpAngle(float a, float b, float t) => Mathf.LerpAngle(a, b, t);
        public static float lerpUnclamped(float a, float b, float t) => Mathf.LerpUnclamped(a, b, t);
        public static float log(float f, float p) => Mathf.Log(f, p);
        public static float log10(float f) => Mathf.Log10(f);
        public static float max(float a, float b) => Mathf.Max(a, b);
        public static float max(params float[] values) => Mathf.Max(values);
        public static float min(float a, float b) => Mathf.Min(a, b);
        public static float min(params float[] values) => Mathf.Min(values);
        public static float moveTowards(float current, float target, float maxDelta) => Mathf.MoveTowards(current, target, maxDelta);
        public static int nextPowerOfTwo(int value) => Mathf.NextPowerOfTwo(value);
        public static float perlinNoise(float x, float y) => Mathf.PerlinNoise(x, y);
        public static float pingPong(float t, float length) => Mathf.PingPong(t, length);
        public static float pow(float f, float p) => Mathf.Pow(f, p);
        public static float repeat(float t, float length) => Mathf.Repeat(t, length);
        public static float round(float f) => Mathf.Round(f);
        public static float sign(float f) => Mathf.Sign(f);
        public static float sin(float f) => Mathf.Sin(f);
        public static float sqrt(float f) => Mathf.Sqrt(f);
        public static float smoothStep(float from, float to, float t) => Mathf.SmoothStep(from, to, t);
        public static float tan(float f) => Mathf.Tan(f);
    }
}