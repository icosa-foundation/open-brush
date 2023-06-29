using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Represents a rotation or orientation in 3D space")]
    [MoonSharpUserData]
    public class RotationApiWrapper
    {
        public Quaternion _Quaternion;

        public RotationApiWrapper(float x = 0, float y = 0, float z = 0)
        {
            _Quaternion = Quaternion.Euler(x, y, z);
        }

        public RotationApiWrapper(Quaternion rotation)
        {
            _Quaternion = rotation;
        }

        public static RotationApiWrapper New(float x = 0, float y = 0, float z = 0)
        {
            var instance = new RotationApiWrapper(x, y, z);
            return instance;
        }

        public override string ToString()
        {
            return $"Rotation({_Quaternion.eulerAngles.x}, {_Quaternion.eulerAngles.y}, {_Quaternion.eulerAngles.z})";
        }

        public float this[int index]
        {
            get => _Quaternion.eulerAngles[index];
            set
            {
                var euler = _Quaternion.eulerAngles;
                euler[index] = value;
                _Quaternion = Quaternion.Euler(euler);
            }
        }

        public float x => _Quaternion.eulerAngles.x;
        public float y => _Quaternion.eulerAngles.y;
        public float z => _Quaternion.eulerAngles.z;

        public static Quaternion zero => Quaternion.identity;
        public static Quaternion left => Quaternion.Euler(0, -90, 0);
        public static Quaternion right => Quaternion.Euler(0, 90, 0);
        public static Quaternion up => Quaternion.Euler(90, 0, 0);
        public static Quaternion down => Quaternion.Euler(-90, 0, 0);
        public static Quaternion anticlockwise => Quaternion.Euler(0, 0, -90);
        public static Quaternion clockwise => Quaternion.Euler(0, 0, 90);

        public Quaternion normalized => _Quaternion.normalized;


        public Quaternion SetFromToRotation(Vector3 fromDirection, Vector3 toDirection)
        {
            _Quaternion.SetFromToRotation(fromDirection, toDirection);
            return _Quaternion;
        }
        public Quaternion SetLookRotation(Vector3 view)
        {
            _Quaternion.SetLookRotation(view, Vector3.up);
            return _Quaternion;
        }
        public Quaternion SetLookRotation(Vector3 view, Vector3 up)
        {
            _Quaternion.SetLookRotation(view, up);
            return _Quaternion;
        }
        
        public (float angle, Vector3 axis) ToAngleAxis()
        {
            _Quaternion.ToAngleAxis(out float angle, out Vector3 axis);
            return (angle, axis);
        }

        public static float kEpsilon => Quaternion.kEpsilon;
        public static float Angle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);
        public static Quaternion AngleAxis(float angle, Vector3 axis) => Quaternion.AngleAxis(angle, axis);
        public static float Dot(Quaternion a, Quaternion b) => Quaternion.Dot(a, b);
        public static Quaternion FromToRotation(Vector3 from, Vector3 to) => Quaternion.FromToRotation(from, to);
        public static Quaternion Inverse(Quaternion a) => Quaternion.Inverse(a);
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => Quaternion.Lerp(a, b, t);
        public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t) => Quaternion.LerpUnclamped(a, b, t);
        public static Quaternion LookRotation(Vector3 forward) => Quaternion.LookRotation(forward, Vector3.up);
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => Quaternion.LookRotation(forward, up);
        public static Quaternion Normalize(Quaternion a) => Quaternion.Normalize(a);
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => Quaternion.RotateTowards(from, to, maxDegreesDelta);
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => Quaternion.Slerp(a, b, t);
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => Quaternion.SlerpUnclamped(a, b, t);

        // Operators
        public Quaternion Multiply(Quaternion other) => _Quaternion * other;
        public Quaternion Multiply(float x, float y, float z) => _Quaternion * Quaternion.Euler(x, y, z);
        public Quaternion Scale(float amount) => _Quaternion * Quaternion.Euler(x * amount, y * amount, z * amount);
        public bool Equals(Quaternion other) => _Quaternion == other;
        public bool Equals(float x, float y, float z) => _Quaternion ==  Quaternion.Euler(x, y, z);
    }
}
