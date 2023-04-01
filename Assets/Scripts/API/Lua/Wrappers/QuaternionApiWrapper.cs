using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public static class QuaternionApiWrapper
    {
        public static Quaternion identity => Quaternion.identity;
        public static float kEpsilon => Quaternion.kEpsilon;
        public static float angle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);
        public static Quaternion angleAxis(float angle, Vector3 axis) => Quaternion.AngleAxis(angle, axis);
        public static float dot(Quaternion a, Quaternion b) => Quaternion.Dot(a, b);
        public static Quaternion fromToRotation(Vector3 from, Vector3 to) => Quaternion.FromToRotation(from, to);
        public static Quaternion inverse(Quaternion a) => Quaternion.Inverse(a);
        public static Quaternion lerp(Quaternion a, Quaternion b, float t) => Quaternion.Lerp(a, b, t);
        public static Quaternion lerpUnclamped(Quaternion a, Quaternion b, float t) => Quaternion.LerpUnclamped(a, b, t);
        public static Quaternion lookRotation(Vector3 forward, Vector3 up) => Quaternion.LookRotation(forward, up);
        public static Quaternion normalize(Quaternion a) => Quaternion.Normalize(a);
        public static Quaternion rotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => Quaternion.RotateTowards(from, to, maxDegreesDelta);
        public static Quaternion slerp(Quaternion a, Quaternion b, float t) => Quaternion.Slerp(a, b, t);
        public static Quaternion slerpUnclamped(Quaternion a, Quaternion b, float t) => Quaternion.SlerpUnclamped(a, b, t);

        // Operators
        public static Quaternion multiply(Quaternion a, Quaternion b) => a * b;
        public static bool equals(Quaternion a, Quaternion b) => a == b;
    }
}
