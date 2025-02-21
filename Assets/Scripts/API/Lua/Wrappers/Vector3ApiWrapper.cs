using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A position or offset in 3D space. See https://docs.unity3d.com/ScriptReference/Vector3.html for more detail on many of these methods or properties")]
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

        [LuaDocsDescription("Creates a new vector based on x, y and z position")]
        [LuaDocsExample("newVector = Vector3:New(1, 2, 3)")]
        [LuaDocsParameter("x", "The x coordinate")]
        [LuaDocsParameter("y", "The y coordinate")]
        [LuaDocsParameter("z", "The z coordinate")]
        public static Vector3ApiWrapper New(float x, float y, float z)
        {
            var instance = new Vector3ApiWrapper(x, y, z);
            return instance;
        }

        public override string ToString()
        {
            return $"Vector3({_Vector3.x}, {_Vector3.y}, {_Vector3.z})";
        }

        [LuaDocsDescription("The component at the specified index")]
        public float this[int index]
        {
            get => _Vector3[index];
            set => _Vector3[index] = value;
        }

        [LuaDocsDescription("The x coordinate")]
        public float x
        {
            get => _Vector3.x;
            set => _Vector3.x = value;
        }

        [LuaDocsDescription("The y coordinate")]
        public float y
        {
            get => _Vector3.y;
            set => _Vector3.y = value;
        }

        [LuaDocsDescription("The z coordinate")]
        public float z
        {
            get => _Vector3.z;
            set => _Vector3.z = value;
        }

        [LuaDocsDescription("Returns the length of this vector")]
        public float magnitude
        {
            get => _Vector3.magnitude;
            set => _Vector3 = _Vector3.normalized * value;
        }

        [LuaDocsDescription("Returns the squared length of this vector")]
        public float sqrMagnitude
        {
            get => _Vector3.sqrMagnitude;
            set => _Vector3 = _Vector3.normalized * Mathf.Sqrt(value);
        }

        [LuaDocsDescription("Returns a vector with the same direction but with a length of 1")]
        public Vector3 normalized => _Vector3.normalized;

        [LuaDocsDescription("The unsigned angle in degrees between this vector and another")]
        [LuaDocsExample("angle = myVector:Angle(otherVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public float Angle(Vector3 other) => Vector3.Angle(_Vector3, other);

        [LuaDocsDescription("Returns a vector with the same direction but with it's length clamped to a maximum")]
        [LuaDocsExample("clampedVector = myVector:ClampMagnitude(5)")]
        [LuaDocsParameter("maxLength", "The maximum length of the returned vector")]
        public Vector3 ClampMagnitude(float maxLength) => Vector3.ClampMagnitude(_Vector3, maxLength);

        [LuaDocsDescription("Returns the cross product of two vectors")]
        [LuaDocsExample("crossProduct = Vector3:Cross(firstVector, secondVector)")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector3 Cross(Vector3 a, Vector3 b) => Vector3.Cross(a, b);

        [LuaDocsDescription("Returns the distance between two points")]
        [LuaDocsExample("distance = Vector3:Distance(firstPoint, secondPoint)")]
        [LuaDocsParameter("other", "The other vector")]
        public float Distance(Vector3 other) => Vector3.Distance(_Vector3, other);

        [LuaDocsDescription("Linearly interpolates between two points")]
        [LuaDocsExample("newPoint = Vector3:Lerp(pointA, PointB, 0.25)")]
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
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector3 Max(Vector3 a, Vector3 b) => Vector3.Max(a, b);

        [LuaDocsDescription("Creates a vector made from the smallest components of the inputs")]
        [LuaDocsExample("result = Vector3:Min(firstVector, secondVector")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector3 Min(Vector3 a, Vector3 b) => Vector3.Min(a, b);

        [LuaDocsDescription("Moves a point towards a target point")]
        [LuaDocsExample("position = position:MoveTowards(PointB, 0.25)")]
        [LuaDocsParameter("target", "The target point")]
        [LuaDocsParameter("maxDistanceDelta", "The maximum distance to move towards the target point")]
        public Vector3 MoveTowards(Vector3 target, float maxDistanceDelta) => Vector3.MoveTowards(_Vector3, target, maxDistanceDelta);

        [LuaDocsDescription("Projects this vector onto another")]
        [LuaDocsExample("newVector = myVector:Project(otherVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector3 Project(Vector3 other) => Vector3.Project(_Vector3, other);

        [LuaDocsDescription("Projects this vector onto a plane defined by a normal orthogonal to the plane")]
        [LuaDocsExample("newVector = myVector:ProjectOnPlane(planeNormal)")]
        [LuaDocsParameter("planeNormal", "The normal vector of the plane")]
        public Vector3 ProjectOnPlane(Vector3 planeNormal) => Vector3.ProjectOnPlane(_Vector3, planeNormal);

        [LuaDocsDescription("Reflects a vector off the vector defined by a normal")]
        [LuaDocsExample("newVector = myVector:Reflect(normalVector)")]
        [LuaDocsParameter("normal", "The normal vector")]
        public Vector3 Reflect(Vector3 normal) => Vector3.Reflect(_Vector3, normal);

        [LuaDocsDescription("Moves this vector towards another with a maximum change in angle")]
        [LuaDocsExample("newVector = myVector:RotateTowards(targetVector, Math.pi / 10, 0.25)")]
        [LuaDocsParameter("target", "The target vector")]
        [LuaDocsParameter("maxRadiansDelta", "The maximum change in angle")]
        [LuaDocsParameter("maxMagnitudeDelta", "The maximum allowed change in vector magnitude for this rotation")]
        public Vector3 RotateTowards(Vector3 target, float maxRadiansDelta, float maxMagnitudeDelta) =>
            Vector3.RotateTowards(_Vector3, target, maxRadiansDelta, maxMagnitudeDelta);

        [LuaDocsDescription("Multiplies two vectors component-wise")]
        [LuaDocsExample("result = myVector:Scale(secondVector")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector3 ScaleBy(Vector3 other) => Vector3.Scale(_Vector3, other);

        [LuaDocsDescription("Returns the signed angle in degrees between two points and the origin")]
        [LuaDocsExample("angle = myVector:SignedAngle(otherVector, axis)")]
        [LuaDocsParameter("other", "The other vector")]
        [LuaDocsParameter("axis", "The axis around which the vectors are rotated")]
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

        public static Vector3 operator +(Vector3ApiWrapper a, Vector3ApiWrapper b) => a._Vector3 + b._Vector3;
        public static Vector3 operator -(Vector3ApiWrapper a, Vector3ApiWrapper b) => a._Vector3 - b._Vector3;
        public static Vector3 operator *(Vector3ApiWrapper a, float b) => a._Vector3 * b;
        public static Vector3 operator /(Vector3ApiWrapper a, float b) => a._Vector3 / b;

        [LuaDocsDescription("Adds two vectors")]
        [LuaDocsExample("result = myVector:Add(secondVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector3 Add(Vector3 other) => _Vector3 + other;

        [LuaDocsDescription("Adds x, y and z values to this vector")]
        [LuaDocsExample("result = myVector:Add(1, 2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public Vector3 Add(float x, float y, float z) => _Vector3 + new Vector3(x, y, z);

        [LuaDocsDescription("Subtracts a Vector3 from this vector")]
        [LuaDocsExample("result = myVector:Subtract(otherVector)")]
        [LuaDocsParameter("other", "The vector to subtract")]
        public Vector3 Subtract(Vector3 other) => _Vector3 - other;

        [LuaDocsDescription("Subtracts x, y and z values from this vector")]
        [LuaDocsExample("result = myVector:Subtract(1, 2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public Vector3 Subtract(float x, float y, float z) => _Vector3 - new Vector3(x, y, z);

        [LuaDocsDescription("Multiplies this vector by a scalar value")]
        [LuaDocsExample("result = myVector:Multiply(2)")]
        [LuaDocsParameter("value", "The scalar value")]
        public Vector3 Multiply(float value) => _Vector3 * value;

        [LuaDocsDescription("Multiplies this vector by x, y and z values component-wise")]
        [LuaDocsExample("result = myVector:Multiply(2, 3, 4)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public Vector3 ScaleBy(float x, float y, float z) => Vector3.Scale(_Vector3, new Vector3(x, y, z));

        [LuaDocsDescription("Divides this vector by a scalar value")]
        [LuaDocsExample("result = myVector:Divide(2)")]
        [LuaDocsParameter("value", "The scalar value")]
        public Vector3 Divide(float value) => _Vector3 / value;

        [LuaDocsDescription("Is this vector equal to another?")]
        [LuaDocsExample(@"if myVector:Equals(Vector3.zero) then print(""Vector is zero"") end")]
        [LuaDocsParameter("other", "The other vector")]
        public bool Equals(Vector3ApiWrapper other) => Equals(other._Vector3);

        public override bool Equals(System.Object obj)
        {
            var other = obj as Vector3ApiWrapper;
            return other != null && _Vector3 == other._Vector3;
        }
        public override int GetHashCode() => 0; // Always return 0. Lookups will have to use Equals to compare

        [LuaDocsDescription("Is this vector equal these x, y and z values?")]
        [LuaDocsExample(@"if myVector:Equals(1, 2, 3) then print(""Vector is 1,2,3"") end")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public bool Equals(float x, float y, float z) => _Vector3 == new Vector3(x, y, z);

        [LuaDocsDescription("Is this vector not equal to another?")]
        [LuaDocsExample(@"if myVector:NotEquals(Vector3.zero) then print(""Vector is not zero"") end")]
        [LuaDocsParameter("other", "The other vector")]
        public bool NotEquals(Vector3ApiWrapper other) => _Vector3 != other._Vector3;

        [LuaDocsDescription("Is this vector not equal to these x, y and z values?")]
        [LuaDocsExample(@"if myVector:NotEquals(1, 2, 3) then print(""Vector is not 1,2,3"") end")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public bool NotEquals(float x, float y, float z) => _Vector3 != new Vector3(x, y, z);
    }
}
