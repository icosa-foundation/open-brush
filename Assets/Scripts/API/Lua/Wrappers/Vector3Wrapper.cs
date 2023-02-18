using MoonSharp.Interpreter;
using UnityEngine;

[MoonSharpUserData]
public static class Vector3Wrapper
{
    public static float Angle(Vector3 a, Vector3 b) => Vector3.Angle(a, b);
    public static Vector3 ClampMagnitude(Vector3 v, float maxLength) => Vector3.ClampMagnitude(v, maxLength);
    public static Vector3 Cross(Vector3 a, Vector3 b) => Vector3.Cross(a, b);
    public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);
    public static float Dot(Vector3 a, Vector3 b) => Vector3.Dot(a, b);
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
    public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => Vector3.LerpUnclamped(a, b, t);
    public static Vector3 Max(Vector3 a, Vector3 b) => Vector3.Max(a, b);
    public static Vector3 Min(Vector3 a, Vector3 b) => Vector3.Min(a, b);
    public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) => Vector3.MoveTowards(current, target, maxDistanceDelta);
    public static Vector3 Normalize(Vector3 a) => Vector3.Normalize(a);
    // public static Vector3 OrthoNormalize(Vector3 normal, Vector3 tangent) => Vector3.OrthoNormalize(ref normal, ref tangent);
    public static Vector3 Project(Vector3 a, Vector3 b) => Vector3.Project(a, b);
    public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal) => Vector3.ProjectOnPlane(vector, planeNormal);
    public static Vector3 Reflect(Vector3 a, Vector3 b) => Vector3.Reflect(a, b);
    public static Vector3 RotateTowards(Vector3 current, Vector3 target, float maxRadiansDelta, float maxMagnitudeDelta) =>
        Vector3.RotateTowards(current, target, maxMagnitudeDelta, maxMagnitudeDelta);
    public static Vector3 Scale(Vector3 a, Vector3 b) => Vector3.Scale(a, b);
    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) => Vector3.SignedAngle(from, to, axis);
    public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
    public static Vector3 SlerpUnclamped(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);
    // public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = Time.deltaTime) => Vector3.SmoothDamp(a);

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
}
