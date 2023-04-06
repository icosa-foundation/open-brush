using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class Vector2ApiWrapper
    {
        public static float angle(Vector2 a, Vector2 b) => Vector2.Angle(a, b);
        public static Vector2 clampMagnitude(Vector2 v, float maxLength) => Vector2.ClampMagnitude(v, maxLength);
        public static float distance(Vector2 a, Vector2 b) => Vector2.Distance(a, b);
        public static float magnitude(Vector2 a) => a.magnitude;
        public static float sqrMagnitude(Vector2 a) => Vector2.SqrMagnitude(a);
        public static float dot(Vector2 a, Vector2 b) => Vector2.Dot(a, b);
        public static Vector2 lerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);
        public static Vector2 lerpUnclamped(Vector2 a, Vector2 b, float t) => Vector2.LerpUnclamped(a, b, t);
        public static Vector2 max(Vector2 a, Vector2 b) => Vector2.Max(a, b);
        public static Vector2 min(Vector2 a, Vector2 b) => Vector2.Min(a, b);
        public static Vector2 moveTowards(Vector2 current, Vector2 target, float maxDistanceDelta) => Vector2.MoveTowards(current, target, maxDistanceDelta);
        public static Vector2 normalized(Vector2 a) => a.normalized;
        public static Vector2 reflect(Vector2 a, Vector2 b) => Vector2.Reflect(a, b);
        public static Vector2 scale(Vector2 a, Vector2 b) => Vector2.Scale(a, b);
        public static float signedAngle(Vector2 from, Vector2 to, Vector2 axis) => Vector2.SignedAngle(from, to);
        public static Vector2 slerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);
        public static Vector2 slerpUnclamped(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);

        public static Vector2 down => Vector2.down;
        public static Vector2 left => Vector2.left;
        public static Vector2 negativeInfinity => Vector2.negativeInfinity;
        public static Vector2 one => Vector2.one;
        public static Vector2 positiveInfinity => Vector2.positiveInfinity;
        public static Vector2 right => Vector2.right;
        public static Vector2 up => Vector2.up;
        public static Vector2 zero => Vector2.zero;

        // Operators
        public static Vector2 add(Vector2 a, Vector2 b) => a + b;
        public static Vector2 subtract(Vector2 a, Vector2 b) => a - b;
        public static Vector2 multiply(Vector2 a, float b) => a * b;
        public static Vector2 divide(Vector2 a, float b) => a / b;
        public static bool equals(Vector2 a, Vector2 b) => a == b;
        public static bool notEquals(Vector2 a, Vector2 b) => a != b;
    }
}
