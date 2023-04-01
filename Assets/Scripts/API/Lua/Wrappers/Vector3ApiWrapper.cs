using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class Vector3ApiWrapper
    {
        public static float angle(Vector3 a, Vector3 b) => Vector3.Angle(a, b);
        public static Vector3 clampMagnitude(Vector3 v, float maxLength) => Vector3.ClampMagnitude(v, maxLength);
        public static Vector3 cross(Vector3 a, Vector3 b) => Vector3.Cross(a, b);
        public static float distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);
        public static float magnitude(Vector3 a) => Vector3.Magnitude(a);
        public static float sqrMagnitude(Vector3 a) => Vector3.SqrMagnitude(a);
        public static float dot(Vector3 a, Vector3 b) => Vector3.Dot(a, b);
        public static Vector3 lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
        public static Vector3 lerpUnclamped(Vector3 a, Vector3 b, float t) => Vector3.LerpUnclamped(a, b, t);
        public static Vector3 max(Vector3 a, Vector3 b) => Vector3.Max(a, b);
        public static Vector3 min(Vector3 a, Vector3 b) => Vector3.Min(a, b);
        public static Vector3 moveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) => Vector3.MoveTowards(current, target, maxDistanceDelta);
        public static Vector3 normalize(Vector3 a) => Vector3.Normalize(a);
        // public static Vector3 orthoNormalize(Vector3 normal, Vector3 tangent) => Vector3.OrthoNormalize(ref normal, ref tangent);
        public static Vector3 project(Vector3 a, Vector3 b) => Vector3.Project(a, b);
        public static Vector3 projectOnPlane(Vector3 vector, Vector3 planeNormal) => Vector3.ProjectOnPlane(vector, planeNormal);
        public static Vector3 reflect(Vector3 a, Vector3 b) => Vector3.Reflect(a, b);
        public static Vector3 rotateTowards(Vector3 current, Vector3 target, float maxRadiansDelta, float maxMagnitudeDelta) =>
            Vector3.RotateTowards(current, target, maxMagnitudeDelta, maxMagnitudeDelta);
        public static Vector3 scale(Vector3 a, Vector3 b) => Vector3.Scale(a, b);
        public static float signedAngle(Vector3 from, Vector3 to, Vector3 axis) => Vector3.SignedAngle(from, to, axis);
        public static Vector3 slerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
        public static Vector3 slerpUnclamped(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
        // public static Vector3 smoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = Time.deltaTime) => Vector3.SmoothDamp(a);

        public static Vector3 back => Vector3.back;
        public static Vector3 down => Vector3.down;
        public static Vector3 forward => Vector3.forward;
        public static Vector3 left => Vector3.left;
        public static Vector3 negativeInfinity => Vector3.negativeInfinity;
        public static Vector3 one => Vector3.one;
        public static Vector3 positiveInfinity => Vector3.positiveInfinity;
        public static Vector3 right => Vector3.right;
        public static Vector3 up => Vector3.up;
        public static Vector3 zero => Vector3.zero;

        // Operators
        public static Vector3 add(Vector3 a, Vector3 b) => a + b;
        public static Vector3 subtract(Vector3 a, Vector3 b) => a - b;
        public static Vector3 multiply(Vector3 a, float b) => a * b;
        public static Vector3 divide(Vector3 a, float b) => a / b;
        public static bool equals(Vector3 a, Vector3 b) => a == b;
        public static bool notEquals(Vector3 a, Vector3 b) => a != b;
    }
}
