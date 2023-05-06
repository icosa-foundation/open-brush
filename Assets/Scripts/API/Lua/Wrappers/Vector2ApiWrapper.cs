using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class Vector2ApiWrapper
    {
        public Vector2 _Vector2;

        public Vector2ApiWrapper(float x = 0, float y = 0)
        {
            _Vector2 = new Vector2(x, y);
        }

        public Vector2ApiWrapper(Vector2 vector)
        {
            _Vector2 = vector;
        }

        public static Vector2ApiWrapper New(float x = 0, float y = 0)
        {
            var instance = new Vector2ApiWrapper(x, y);
            return instance;
        }

        public override string ToString()
        {
            return $"Vector2({_Vector2.x}, {_Vector2.y})";
        }

        public float this[int index]
        {
            get => _Vector2[index];
            set => _Vector2[index] = value;
        }

        public float x => _Vector2.x;
        public float y => _Vector2.y;
        public static float Angle(Vector2 a, Vector2 b) => Vector2.Angle(a, b);
        public static Vector2 ClampMagnitude(Vector2 v, float maxLength) => Vector2.ClampMagnitude(v, maxLength);
        public static float Distance(Vector2 a, Vector2 b) => Vector2.Distance(a, b);
        public static float Magnitude(Vector2 a) => a.magnitude;
        public static float SqrMagnitude(Vector2 a) => Vector2.SqrMagnitude(a);
        public static float Dot(Vector2 a, Vector2 b) => Vector2.Dot(a, b);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => Vector2.LerpUnclamped(a, b, t);
        public static Vector2 Max(Vector2 a, Vector2 b) => Vector2.Max(a, b);
        public static Vector2 Min(Vector2 a, Vector2 b) => Vector2.Min(a, b);
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta) => Vector2.MoveTowards(current, target, maxDistanceDelta);
        public static Vector2 Normalized(Vector2 a) => a.normalized;
        public static Vector2 Reflect(Vector2 a, Vector2 b) => Vector2.Reflect(a, b);
        public static Vector2 Scale(Vector2 a, Vector2 b) => Vector2.Scale(a, b);
        public static float SignedAngle(Vector2 from, Vector2 to, Vector2 axis) => Vector2.SignedAngle(from, to);
        public static Vector2 Slerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);
        public static Vector2 SlerpUnclamped(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);
        public static Vector2 PointOnCircle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public Vector3 OnX() => new Vector3(0, _Vector2.x, _Vector2.y);
        public Vector3 OnY() => new Vector3(_Vector2.x, 0, _Vector2.y);
        public Vector3 OnZ() => new Vector3(_Vector2.x, _Vector2.y, 0);

        public static Vector2 down => Vector2.down;
        public static Vector2 left => Vector2.left;
        public static Vector2 negativeInfinity => Vector2.negativeInfinity;
        public static Vector2 one => Vector2.one;
        public static Vector2 positiveInfinity => Vector2.positiveInfinity;
        public static Vector2 right => Vector2.right;
        public static Vector2 up => Vector2.up;
        public static Vector2 zero => Vector2.zero;

        // Operators
        public Vector2 Add(Vector2 b) => _Vector2 + b;
        public Vector2 Add(float x, float y) => _Vector2 + new Vector2(x, y);
        public Vector2 Subtract(Vector2 b) => _Vector2 - b;
        public Vector2 Subtract(float x, float y) => _Vector2 - new Vector2(x, y);
        public Vector2 Multiply(float b) => _Vector2 * b;
        public Vector2 Multiply(float x, float y) => _Vector2 * new Vector2(x, y);
        public Vector2 Divide(float b) => _Vector2 / b;
        public Vector2 Divide(float x, float y) => _Vector2 / new Vector2(x, y);
        public bool Equals(Vector2 b) => _Vector2 == b;
        public bool Equals(float x, float y) => _Vector2 == new Vector2(x, y);
        public bool NotEquals(Vector2 b) => _Vector2 != b;
        public bool NotEquals(float x, float y) => _Vector2 != new Vector2(x, y);

        // Static Operators
        public static Vector2 Add(Vector2 a, Vector2 b) => a + b;
        public static Vector2 Subtract(Vector2 a, Vector2 b) => a - b;
        public static Vector2 Multiply(Vector2 a, float b) => a * b;
        public static Vector2 Divide(Vector2 a, float b) => a / b;
        public static bool Equals(Vector2 a, Vector2 b) => a == b;
        public static bool NotEquals(Vector2 a, Vector2 b) => a != b;
    }
}
