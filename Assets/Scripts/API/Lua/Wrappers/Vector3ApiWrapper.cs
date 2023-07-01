using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A position or offset in 3D space")]
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

        [LuaDocsDescription("Gets or sets the x coordinate")]
        public float x
        {
            get => _Vector3.x;
            set => _Vector3.x = value;
        }

        [LuaDocsDescription("Gets or sets the y coordinate")]
        public float y
        {
            get => _Vector3.y;
            set => _Vector3.y = value;
        }

        [LuaDocsDescription("Gets or sets the z coordinate")]
        public float z
        {
            get => _Vector3.z;
            set => _Vector3.z = value;
        }

        [LuaDocsDescription("Returns the length of this vector")]
        public float magnitude => _Vector3.magnitude;

        [LuaDocsDescription("Returns the squared length of this vector")]
        public float sqrMagnitude => _Vector3.sqrMagnitude;

        [LuaDocsDescription("Returns a vector with the same direction but with a length of 1")]
        public Vector3 normalized => _Vector3.normalized;

        [LuaDocsDescription("Returns the angle in degrees between two points and the origin")]
        public static float Angle(Vector3 a, Vector3 b) => Vector3.Angle(a, b);

        [LuaDocsDescription("Returns a vector with the same direction but with it's length clamped to a maximum")]
        public Vector3 ClampMagnitude(float maxLength) => Vector3.ClampMagnitude(_Vector3, maxLength);

        [LuaDocsDescription("Returns the cross product of two vectors")]
        public static Vector3 Cross(Vector3 a, Vector3 b) => Vector3.Cross(a, b);

        [LuaDocsDescription("Returns the distance between two points")]
        public static float Distance(Vector3 a, Vector3 b) => Vector3.Distance(a, b);

        [LuaDocsDescription("Linearly interpolates between two points")]
        [LuaDocsExample("newPoint = Vector2:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value between 0 and 1 that controls how far between a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);

        [LuaDocsDescription("Linearly interpolates (or extrapolates) between two points")]
        [LuaDocsExample("newPoint = Vector3:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => Vector3.LerpUnclamped(a, b, t);

        [LuaDocsDescription("Creates a vector made from the largest components of the inputs")]
        [LuaDocsExample("result = Vector3:Max(firstVector, secondVector")]
        public static Vector3 Max(Vector3 a, Vector3 b) => Vector3.Max(a, b);

        [LuaDocsDescription("Creates a vector made from the smallest components of the inputs")]
        [LuaDocsExample("result = Vector3:Min(firstVector, secondVector")]
        public static Vector3 Min(Vector3 a, Vector3 b) => Vector3.Min(a, b);

        [LuaDocsDescription("Moves a point towards a target point")]
        public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta) => Vector3.MoveTowards(current, target, maxDistanceDelta);

        [LuaDocsDescription("Projects this vector onto another")]
        public Vector3 Project(Vector3 other) => Vector3.Project(_Vector3, other);

        [LuaDocsDescription("Projects this vector onto a plane defined by a normal orthogonal to the plane")]
        public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal) => Vector3.ProjectOnPlane(vector, planeNormal);

        [LuaDocsDescription("Reflects a vector off the plane defined by a normal")]
        public Vector3 Reflect(Vector3 other) => Vector3.Reflect(_Vector3, other);

        [LuaDocsDescription("Moves this vector towards another with a maximum change in angle")]
        public static Vector3 RotateTowards(Vector3 current, Vector3 target, float maxRadiansDelta, float maxMagnitudeDelta) =>
            Vector3.RotateTowards(current, target, maxMagnitudeDelta, maxMagnitudeDelta);

        [LuaDocsDescription("Multiplies two vectors component-wise")]
        public static Vector3 ScaleBy(Vector3 a, Vector3 b) => Vector3.Scale(a, b);

        [LuaDocsDescription("Returns the signed angle in degrees between two points and the origin")]
        public float SignedAngle(Vector3 other, Vector3 axis) => Vector3.SignedAngle(_Vector3, other, axis);

        [LuaDocsDescription("Spherically interpolates between two vectors")]
        [LuaDocsExample("newPoint = Vector3:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);

        [LuaDocsDescription("Spherically interpolates (or extrapolates) between two vectors")]
        [LuaDocsExample("newPoint = Vector3:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector3 SlerpUnclamped(Vector3 a, Vector3 b, float t) => Vector3.Lerp(a, b, t);

        // public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = Time.deltaTime) => Vector3.SmoothDamp(a);

        [LuaDocsDescription("A vector of -1 in the z axis")]
        public static Vector3 back => Vector3.back;

        [LuaDocsDescription("A vector of -1 in the y axis")]
        public static Vector3 down => Vector3.down;

        [LuaDocsDescription("A vector of 1 in the z axis")]
        public static Vector3 forward => Vector3.forward;

        [LuaDocsDescription("A vector of -1 in the x axis")]
        public static Vector3 left => Vector3.left;

        [LuaDocsDescription("A vector of -infinity in all axes")]
        public static Vector3 negativeInfinity => Vector3.negativeInfinity;

        [LuaDocsDescription("A vector of 1 in all axes")]
        public static Vector3 one => Vector3.one;

        [LuaDocsDescription("A vector of infinity in all axes")]
        public static Vector3 positiveInfinity => Vector3.positiveInfinity;

        [LuaDocsDescription("A vector of 1 in the x axis")]
        public static Vector3 right => Vector3.right;

        [LuaDocsDescription("A vector of 1 in the y axis")]
        public static Vector3 up => Vector3.up;

        [LuaDocsDescription("A vector of 0 in all axes")]
        public static Vector3 zero => Vector3.zero;

        // Operators
        [LuaDocsDescription("Adds two vectors")]
        public Vector3 Add(Vector3 other) => _Vector3 + other;

        [LuaDocsDescription("Adds x, y and z values to this vector")]
        public Vector3 Add(float x, float y, float z) => _Vector3 + new Vector3(x, y, z);

        [LuaDocsDescription("Subtracts a Vector3 from this vector")]
        public Vector3 Subtract(Vector3 other) => _Vector3 - other;

        [LuaDocsDescription("Subtracts x, y and z values from this vector")]
        public Vector3 Subtract(float x, float y, float z) => _Vector3 - new Vector3(x, y, z);

        [LuaDocsDescription("Multiplies this vector by a scalar value")]
        public Vector3 Multiply(float value) => _Vector3 * value;

        [LuaDocsDescription("Multiplies this vector by another vector component-wise")]
        public Vector3 ScaleBy(Vector3 other) => Vector3.Scale(_Vector3, other);

        [LuaDocsDescription("Multiplies this vector by x, y and z values component-wise")]
        public Vector3 ScaleBy(float x, float y, float z) => Vector3.Scale(_Vector3, new Vector3(x, y, z));

        [LuaDocsDescription("Divides this vector by a scalar value")]
        public Vector3 Divide(float value) => _Vector3 / value;

        [LuaDocsDescription("Is this vector equal to another?")]
        public bool Equals(Vector3 other) => _Vector3 == other;

        [LuaDocsDescription("Is this vector equal these x, y and z values?")]
        public bool Equals(float x, float y, float z) => _Vector3 == new Vector3(x, y, z);

        [LuaDocsDescription("Is this vector not equal to another?")]
        public bool NotEquals(Vector3 other) => _Vector3 != other;

        [LuaDocsDescription("Is this vector not equal to these x, y and z values?")]
        public bool NotEquals(float x, float y, float z) => _Vector3 != new Vector3(x, y, z);
    }
}
