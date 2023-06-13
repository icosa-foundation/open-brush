using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public class Vector3ApiWrapper
    {
        public Vector3 _Vector3;

        public Vector3ApiWrapper(float x, float y, float z)
        {
            _Vector3 = new Vector3(x, y, z);
        }

        public Vector3ApiWrapper(Vector3 vector)
        {
            _Vector3 = vector;
        }

        public Vector3ApiWrapper(Vector2 vector)
        {
            _Vector3 = vector;
        }

        public static Vector3ApiWrapper New(float x, float y, float z)
        {
            var instance = new Vector3ApiWrapper(x, y, z);
            return instance;
        }

        public override string ToString()
        {
            return $"Vector3({_Vector3.x}, {_Vector3.y}, {_Vector3.z})";
        }

        public float this[int index]
        {
            get => _Vector3[index];
            set => _Vector3[index] = value;
        }

        public float x
        {
            get => _Vector3.x;
            set => _Vector3.x = value;
        }
        public float y
        {
            get => _Vector3.y;
            set => _Vector3.y = value;
        }
        public float z
        {
            get => _Vector3.z;
            set => _Vector3.z = value;
        }
        public float magnitude => _Vector3.magnitude;
        public Vector3 normalized => _Vector3.normalized;
        public float sqrMagnitude => _Vector3.sqrMagnitude;
        public static float Angle(Vector3 a, Vector3 b) => Vector3.Angle(a, b);
        public static Vector3 ClampMagnitude(Vector3 v, float maxLength) => Vector3.ClampMagnitude(v, maxLength);
        public static Vector3 Cross(Vector3 a, Vector3 b) => Vector3.Cross(a, b);
        public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);
        public static float Magnitude(Vector3 a) => Vector3.Magnitude(a);
        public static float SqrMagnitude(Vector3 a) => Vector3.SqrMagnitude(a);
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
        public static Vector3 ScaleBy(Vector3 a, Vector3 b) => Vector3.Scale(a, b);
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


        // Operators
        public Vector3 Add(Vector3 b) => _Vector3 + b;
        public Vector3 Add(float x, float y, float z) => _Vector3 + new Vector3(x, y, z);
        public Vector3 Subtract(Vector3 b) => _Vector3 - b;
        public Vector3 Subtract(float x, float y, float z) => _Vector3 - new Vector3(x, y, z);
        public Vector3 Multiply(float b) => _Vector3 * b;
        public Vector3 ScaleBy(Vector3 b) => Vector3.Scale(_Vector3, b);
        public Vector3 ScaleBy(float x, float y, float z) => Vector3.Scale(_Vector3, new Vector3(x, y, z));
        public Vector3 Divide(float b) => _Vector3 / b;
        public bool Equals(Vector3 b) => _Vector3 == b;
        public bool Equals(float x, float y, float z) => _Vector3 == new Vector3(x, y, z);
        public bool NotEquals(Vector3 b) => _Vector3 != b;
        public bool NotEquals(float x, float y, float z) => _Vector3 != new Vector3(x, y, z);


        // Static Operators
        public static Vector3 Add(Vector3 a, Vector3 b) => a + b;
        public static Vector3 Subtract(Vector3 a, Vector3 b) => a - b;
        public static Vector3 Multiply(Vector3 a, float b) => a * b;
        public static Vector3 Divide(Vector3 a, float b) => a / b;
        public static bool Equals(Vector3 a, Vector3 b) => a == b;
        public static bool NotEquals(Vector3 a, Vector3 b) => a != b;
    }
}
