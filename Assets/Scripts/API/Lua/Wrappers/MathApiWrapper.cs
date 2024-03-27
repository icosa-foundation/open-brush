using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{

    [LuaDocsDescription("Various maths functions. See https://docs.unity3d.com/ScriptReference/Mathf.html for further documentation")]
    [MoonSharpUserData]
    public static class MathApiWrapper
    {
        [LuaDocsDescription("A constant that you multiply with a value in degrees to convert it to radians")]
        public static float deg2Rad => Mathf.Deg2Rad;

        [LuaDocsDescription("The smallest value that a float can have such that 1.0 plus this does not equal 1.0")]
        public static float epsilon => Mathf.Epsilon;

        [LuaDocsDescription("Positive Infinity")]
        public static float positiveInfinity => float.PositiveInfinity;

        [LuaDocsDescription("Negative Infinity")]
        public static float negativeInfinity => float.NegativeInfinity;

        [LuaDocsDescription("The value of Pi")]
        public static float pi => Mathf.PI;

        [LuaDocsDescription("A constant that you multiply with a value in radians to convert it to degrees")]
        public static float rad2Deg => Mathf.Rad2Deg;

        [LuaDocsDescription("The absolute value function")]
        [LuaDocsExample("result = Math:Abs(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The absolute value of f")]
        public static float Abs(float f) => Mathf.Abs(f);

        [LuaDocsDescription("The arc-cosine function")]
        [LuaDocsExample("result = Math:Acos(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The angle in radians whose cosine is f")]
        public static float Acos(float f) => Mathf.Acos(f);

        [LuaDocsDescription("Compares two floating point values if they are similar")]
        [LuaDocsExample("nearlySame = Math:Approximately(0.1000000000000000011, 0.100000000000000001)")]
        [LuaDocsParameter("a", "The first value")]
        [LuaDocsParameter("b", "The second value")]
        [LuaDocsReturnValue("True if the difference between the values is less than Math.epsilon")]
        public static bool Approximately(float a, float b) => Mathf.Approximately(a, b);

        [LuaDocsDescription("The arc-sine function")]
        [LuaDocsExample("result = Math:Asin(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The angle in radians whose sine is f")]
        public static float Asin(float f) => Mathf.Asin(f);

        [LuaDocsDescription("The arc-tangent function")]
        [LuaDocsExample("result = Math:Atan(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The angle in radians whose tangent is f")]
        public static float Atan(float f) => Mathf.Atan(f);

        [LuaDocsDescription("The two argument arc-tangent function")]
        [LuaDocsExample("result = Math:Atan2(0.1, 3)")]
        [LuaDocsParameter("y", "The numerator value")]
        [LuaDocsParameter("x", "The denominator value")]
        [LuaDocsReturnValue("The angle in radians whose tan is y/x")]
        public static float Atan2(float y, float x) => Mathf.Atan2(y, x);

        [LuaDocsDescription("The ceiling function")]
        [LuaDocsExample("result = Math:Ceil(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The smallest integer greater to or equal to f")]
        public static float Ceil(float f) => Mathf.Ceil(f);

        [LuaDocsDescription("Clamps the given value between the given minimum float and maximum float values. Returns the given value if it is within the min and max range")]
        [LuaDocsExample("result = Math:Clamp(input, -1, 1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsParameter("min", "The minimum value")]
        [LuaDocsParameter("max", "The maximum value")]
        [LuaDocsReturnValue("min if f < min, max if f > max otherwise f")]
        public static float Clamp(float f, float min, float max) => Mathf.Clamp(f, min, max);

        [LuaDocsDescription("Clamps value between 0 and 1 and returns value")]
        [LuaDocsExample("result = Math:Clamp01(1.3)")]
        [LuaDocsParameter("value", "The input value")]
        [LuaDocsReturnValue("0 if f < 0, 1 if f > 1 otherwise f")]
        public static float Clamp01(float value) => Mathf.Clamp01(value);

        [LuaDocsDescription("Calculates the closest power of two")]
        [LuaDocsExample("result = Math:ClosestPowerOfTwo(13)")]
        [LuaDocsParameter("value", "The input value")]
        [LuaDocsReturnValue("The closest power of two")]
        public static int ClosestPowerOfTwo(int value) => Mathf.ClosestPowerOfTwo(value);

        [LuaDocsDescription("The cosine function")]
        [LuaDocsExample("result = Math:Cos(0.1)")]
        [LuaDocsParameter("f", "The input value in radians")]
        [LuaDocsReturnValue("The cosine of angle f")]
        public static float Cos(float f) => Mathf.Cos(f);

        [LuaDocsDescription("Calculates the shortest difference between two given angles")]
        [LuaDocsExample("result = Math:DeltaAngle(1080, 90)")]
        [LuaDocsParameter("a", "The first value in degrees")]
        [LuaDocsParameter("b", "The second value in degrees")]
        [LuaDocsReturnValue("The smaller of the two angles in degrees between input and target")]
        public static float DeltaAngle(float a, float b) => Mathf.DeltaAngle(a, b);

        [LuaDocsDescription("The exponent function")]
        [LuaDocsExample("result = Math:Exp(100)")]
        [LuaDocsParameter("power", "The input value")]
        [LuaDocsReturnValue("Returns e raised to the specified power")]
        public static float Exp(float power) => Mathf.Exp(power);

        [LuaDocsDescription("The floor function")]
        [LuaDocsExample("result = Math:Floor(2.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The largest integer that is less than or equal to the input")]
        public static float Floor(float f) => Mathf.Floor(f);

        [LuaDocsDescription("Inverse linear interpolation between two values by given ratio")]
        [LuaDocsExample("result = Math:InverseLerp(min, max, 23)")]
        [LuaDocsParameter("t", "The input value")]
        [LuaDocsParameter("min", "The minimum value")]
        [LuaDocsParameter("max", "The maximum value")]
        [LuaDocsReturnValue("A value between 0 and 1 representing how far t is between min and max")]
        public static float InverseLerp(float min, float max, float t) => Mathf.InverseLerp(min, max, t);

        [LuaDocsDescription("Determines whether a value is a power of two")]
        [LuaDocsExample("isPower = Math:IsPowerOfTwo(value)")]
        [LuaDocsParameter("value", "The input value")]
        [LuaDocsReturnValue("The logarithm of f in base b")]
        public static bool IsPowerOfTwo(int value) => Mathf.IsPowerOfTwo(value);

        [LuaDocsDescription("Linearly interpolates two floats by a ratio")]
        [LuaDocsExample("result = Math:Lerp(-1, 1, 0.25)")]
        [LuaDocsParameter("t", "The input value")]
        [LuaDocsParameter("min", "The minimum value")]
        [LuaDocsParameter("max", "The maximum value")]
        [LuaDocsReturnValue("A value between min and max representing how far t is between 0 and 1")]
        public static float Lerp(float min, float max, float t) => Mathf.Lerp(min, max, t);

        [LuaDocsDescription("Same as Lerp but takes the shortest path between the specified angles wrapping around a circle")]
        [LuaDocsExample("result = Math:LerpAngle(-30, 90, angle)")]
        [LuaDocsParameter("a", "The input value in degrees")]
        [LuaDocsParameter("min", "The start angle in degrees")]
        [LuaDocsParameter("max", "The end angle in degrees")]
        [LuaDocsReturnValue("An angle between min and max representing how far t is between 0 and 1")]
        public static float LerpAngle(float min, float max, float a) => Mathf.LerpAngle(min, max, a);

        [LuaDocsDescription("Same as Math:Lerp but allows extrapolated values")]
        [LuaDocsExample("result = Math:Lerp(-1, 1, 0.25)")]
        [LuaDocsParameter("t", "The input value")]
        [LuaDocsParameter("min", "The minimum value")]
        [LuaDocsParameter("max", "The maximum value")]
        [LuaDocsReturnValue("A value representing t scaled from the range 0:1 to a new range min:max")]
        public static float LerpUnclamped(float min, float max, float t) => Mathf.LerpUnclamped(min, max, t);

        [LuaDocsDescription("The logarithm of a specified number in a specified base")]
        [LuaDocsExample("result = Math:Log(input, 2)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsParameter("b", "The base")]
        [LuaDocsReturnValue("The logarithm of f in base b")]
        public static float Log(float f, float b) => Mathf.Log(f, b);

        [LuaDocsDescription("The base 10 logarithm of a specified number")]
        [LuaDocsExample("result = Math:Log10(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The base 10 logarithm of a specified number")]
        public static float Log10(float f) => Mathf.Log10(f);

        [LuaDocsDescription("The larger of two float numbers")]
        [LuaDocsExample("biggest = Math:Max(a, b)")]
        [LuaDocsParameter("a", "The first input value")]
        [LuaDocsParameter("b", "The second input value")]
        [LuaDocsReturnValue("The largest of a and b")]
        public static float Max(float a, float b) => Mathf.Max(a, b);

        [LuaDocsDescription("The largest value in a sequence of float numbers")]
        [LuaDocsExample("biggest = Math:Max({1, 4, 6, 2, -3, 32, 5})")]
        [LuaDocsParameter("values", "A list of numbers")]
        [LuaDocsReturnValue("The largest value in the list")]
        public static float Max(params float[] values) => Mathf.Max(values);

        [LuaDocsDescription("The smaller of two float numbers")]
        [LuaDocsExample("smallest = Min:Min(a, b)")]
        [LuaDocsParameter("a", "The first input value")]
        [LuaDocsParameter("b", "The second input value")]
        [LuaDocsReturnValue("The smaller of a and b")]
        public static float Min(float a, float b) => Mathf.Min(a, b);

        [LuaDocsDescription("The smallest value in a sequence of float numbers")]
        [LuaDocsExample("smallest = Math:Min({1, 4, 6, 2, -3, 32, 5})")]
        [LuaDocsParameter("values", "A list of numbers")]
        [LuaDocsReturnValue("The smallest value in a sequence of float numbers")]
        public static float Min(params float[] values) => Mathf.Min(values);

        [LuaDocsDescription("Moves a value towards a target value by a given amount")]
        [LuaDocsExample("x = Math:MoveTowards(x, 10, 0.5)")]
        [LuaDocsParameter("current", "The input value")]
        [LuaDocsParameter("target", "The target value")]
        [LuaDocsParameter("maxDelta", "The largest change allowed each time")]
        [LuaDocsReturnValue("The input + or - maxDelta but clamped to it won't overshoot the target value")]
        public static float MoveTowards(float current, float target, float maxDelta) => Mathf.MoveTowards(current, target, maxDelta);

        [LuaDocsDescription("The smallest power of two greater than or equal to the specified number")]
        [LuaDocsExample("result = Math:NextPowerOfTwo(26)")]
        [LuaDocsParameter("value", "The input value")]
        [LuaDocsReturnValue("The smallest power of two greater than or equal to the specified number")]
        public static int NextPowerOfTwo(int value) => Mathf.NextPowerOfTwo(value);

        [LuaDocsDescription("Samples a two-dimensional Perlin noise map")]
        [LuaDocsExample("result = Math:PerlinNoise(0.4, 1.2)")]
        [LuaDocsParameter("x", "The input value")]
        [LuaDocsParameter("y", "The power to raise to")]
        [LuaDocsReturnValue("Returns the value of the perlin noise as coordinates x,y")]
        public static float PerlinNoise(float x, float y) => Mathf.PerlinNoise(x, y);

        [LuaDocsDescription("Similar to Math:Round except the values alternate between forward and backwards in the range")]
        [LuaDocsExample("result = Math:PingPong(0.4, 1.2)")]
        [LuaDocsParameter("t", "The input value")]
        [LuaDocsParameter("length", "The upper limit")]
        [LuaDocsReturnValue("A value that is never larger than length and never smaller than 0")]
        public static float PingPong(float t, float length) => Mathf.PingPong(t, length);

        [LuaDocsDescription("The raised to the specified power")]
        [LuaDocsExample("result = Math:Pow(0.1, 16)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsParameter("p", "The power to raise to")]
        [LuaDocsReturnValue("Returns f raised to the specified power")]
        public static float Pow(float f, float p) => Mathf.Pow(f, p);

        [LuaDocsDescription("Loops the value t - similar to \"clock\" arithmetic")]
        [LuaDocsExample("result = Math:Round(0.1)")]
        [LuaDocsParameter("t", "The input value")]
        [LuaDocsParameter("length", "The upper limit")]
        [LuaDocsReturnValue("A value that is never larger than length and never smaller than 0")]
        public static float Repeater(float t, float length) => Mathf.Repeat(t, length);

        [LuaDocsDescription("The rounding function")]
        [LuaDocsExample("result = Math:Round(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The nearest integer value to f")]
        public static float Round(float f) => Mathf.Round(f);

        [LuaDocsDescription("The sign function")]
        [LuaDocsExample("result = Math:Sign(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The sign of f")]
        public static float Sign(float f) => Mathf.Sign(f);

        [LuaDocsDescription("The sine function")]
        [LuaDocsExample("result = Math:Sin(0.1)")]
        [LuaDocsParameter("f", "The input value in radians")]
        [LuaDocsReturnValue("The sine of angle f")]
        public static float Sin(float f) => Mathf.Sin(f);

        [LuaDocsDescription("The square root function")]
        [LuaDocsExample("result = Math:Sqrt(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The square root of f")]
        public static float Sqrt(float f) => Mathf.Sqrt(f);

        [LuaDocsDescription("The smoothstep function")]
        [LuaDocsExample("result = Math:SmoothStep(0, 1, 0.5)")]
        [LuaDocsParameter("from", "The lower range")]
        [LuaDocsParameter("to", "The upper range")]
        [LuaDocsParameter("t", "The input value")]
        [LuaDocsReturnValue("The input smoothly interpolated between the range [from, to] by the ratio t")]
        public static float SmoothStep(float from, float to, float t) => Mathf.SmoothStep(from, to, t);

        [LuaDocsDescription("The tangent of an angle")]
        [LuaDocsExample("result = Math:Tan(0.1)")]
        [LuaDocsParameter("f", "The input value")]
        [LuaDocsReturnValue("The tangent of an angle")]
        public static float Tan(float f) => Mathf.Tan(f);

        [LuaDocsDescription("The hyperbolic sine function")]
        [LuaDocsExample("result = Math:Sinh(0.1)")]
        [LuaDocsParameter("f", "The input value in radians")]
        [LuaDocsReturnValue("The hyperbolic sine of f")]
        public static float Sinh(float f) => (float)System.Math.Sinh(f);

        [LuaDocsDescription("The hyperbolic cosine function")]
        [LuaDocsExample("result = Math:Cosh(0.1)")]
        [LuaDocsParameter("f", "The input value in radians")]
        [LuaDocsReturnValue("The hyperbolic cosine of f")]
        public static float Cosh(float f) => (float)System.Math.Cosh(f);

        [LuaDocsDescription("The hyperbolic tangent function")]
        [LuaDocsExample("result = Math:Tanh(0.1)")]
        [LuaDocsParameter("f", "The input value in radians")]
        [LuaDocsReturnValue("The hyperbolic tangent of f")]
        public static float Tanh(float f) => (float)System.Math.Tanh(f);
    }
}
