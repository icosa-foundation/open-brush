
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("A position or offset in 3D space. See https://docs.unity3d.com/ScriptReference/Vector4.html for more detail on many of these methods or properties")]
    [MoonSharpUserData]
    public class Vector4ApiWrapper
    {
        public Vector4 _Vector4;

        public Vector4ApiWrapper(float x, float y, float z, float w)
        {
            _Vector4 = new Vector4(x, y, z, w);
        }

        public Vector4ApiWrapper(float x, float y, float z)
        {
            _Vector4 = new Vector4(x, y, z);
        }

        public Vector4ApiWrapper(Vector4 vector)
        {
            _Vector4 = vector;
        }

        public Vector4ApiWrapper(Vector3 vector)
        {
            _Vector4 = vector;
        }

        public Vector4ApiWrapper(Vector2 vector)
        {
            _Vector4 = vector;
        }

        [LuaDocsDescription("Creates a new vector based on x, y, z and w position")]
        [LuaDocsExample("newVector = Vector4:New(1, 2, 3, 4)")]
        [LuaDocsParameter("x", "The x coordinate")]
        [LuaDocsParameter("y", "The y coordinate")]
        [LuaDocsParameter("z", "The z coordinate")]
        [LuaDocsParameter("z", "The w coordinate")]
        public static Vector4ApiWrapper New(float x, float y, float z, float w)
        {
            var instance = new Vector4ApiWrapper(x, y, z);
            return instance;
        }

        public override string ToString()
        {
            return $"Vector4({_Vector4.x}, {_Vector4.y}, {_Vector4.z}, {_Vector4.w})";
        }

        [LuaDocsDescription("The component at the specified index")]
        public float this[int index]
        {
            get => _Vector4[index];
            set => _Vector4[index] = value;
        }

        [LuaDocsDescription("The x coordinate")]
        public float x
        {
            get => _Vector4.x;
            set => _Vector4.x = value;
        }

        [LuaDocsDescription("The y coordinate")]
        public float y
        {
            get => _Vector4.y;
            set => _Vector4.y = value;
        }

        [LuaDocsDescription("The z coordinate")]
        public float z
        {
            get => _Vector4.z;
            set => _Vector4.z = value;
        }

        [LuaDocsDescription("The w coordinate")]
        public float w
        {
            get => _Vector4.w;
            set => _Vector4.w = value;
        }

        [LuaDocsDescription("Returns the length of this vector")]
        public float magnitude
        {
            get => _Vector4.magnitude;
            set => _Vector4 = _Vector4.normalized * value;
        }

        [LuaDocsDescription("Returns the squared length of this vector")]
        public float sqrMagnitude
        {
            get => _Vector4.sqrMagnitude;
            set => _Vector4 = _Vector4.normalized * Mathf.Sqrt(value);
        }

        [LuaDocsDescription("Returns a vector with the same direction but with a length of 1")]
        public Vector4 normalized => _Vector4.normalized;

        [LuaDocsDescription("Returns the distance between two points")]
        [LuaDocsExample("distance = Vector4:Distance(firstPoint, secondPoint)")]
        [LuaDocsParameter("other", "The other vector")]
        public float Distance(Vector4 other) => Vector4.Distance(_Vector4, other);

        [LuaDocsDescription("Linearly interpolates between two points")]
        [LuaDocsExample("newPoint = Vector4:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value between 0 and 1 that controls how far between a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector4 Lerp(Vector4 a, Vector4 b, float t) => Vector4.Lerp(a, b, t);

        [LuaDocsDescription("Linearly interpolates (or extrapolates) between two points")]
        [LuaDocsExample("newPoint = Vector4:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t) => Vector4.LerpUnclamped(a, b, t);

        [LuaDocsDescription("Creates a vector made from the largest components of the inputs")]
        [LuaDocsExample("result = Vector4:Max(firstVector, secondVector")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector4 Max(Vector4 a, Vector4 b) => Vector4.Max(a, b);

        [LuaDocsDescription("Creates a vector made from the smallest components of the inputs")]
        [LuaDocsExample("result = Vector4:Min(firstVector, secondVector")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector4 Min(Vector4 a, Vector4 b) => Vector4.Min(a, b);

        [LuaDocsDescription("Moves a point towards a target point")]
        [LuaDocsExample("position = position:MoveTowards(PointB, 0.25)")]
        [LuaDocsParameter("target", "The target point")]
        [LuaDocsParameter("maxDistanceDelta", "The maximum distance to move towards the target point")]
        public Vector4 MoveTowards(Vector4 target, float maxDistanceDelta) => Vector4.MoveTowards(_Vector4, target, maxDistanceDelta);

        [LuaDocsDescription("Projects this vector onto another")]
        [LuaDocsExample("newVector = myVector:Project(otherVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector4 Project(Vector4 other) => Vector4.Project(_Vector4, other);

        [LuaDocsDescription("Multiplies two vectors component-wise")]
        [LuaDocsExample("result = myVector:Scale(secondVector")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector4 ScaleBy(Vector4 other) => Vector4.Scale(_Vector4, other);

        [LuaDocsDescription("Spherically interpolates between two vectors")]
        [LuaDocsExample("newPoint = Vector4:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector4 Slerp(Vector4 a, Vector4 b, float t) => Vector4.Lerp(a, b, t);

        [LuaDocsDescription("Spherically interpolates (or extrapolates) between two vectors")]
        [LuaDocsExample("newPoint = Vector4:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector4 SlerpUnclamped(Vector4 a, Vector4 b, float t) => Vector4.Lerp(a, b, t);

        // public static Vector4 SmoothDamp(Vector4 current, Vector4 target, ref Vector4 currentVelocity, float smoothTime, float maxSpeed = Mathf.Infinity, float deltaTime = Time.deltaTime) => Vector4.SmoothDamp(a);

        [LuaDocsDescription("A vector of -infinity in all axes")]
        public static Vector4 negativeInfinity => Vector4.negativeInfinity;

        [LuaDocsDescription("A vector of 1 in all axes")]
        public static Vector4 one => Vector4.one;

        [LuaDocsDescription("A vector of infinity in all axes")]
        public static Vector4 positiveInfinity => Vector4.positiveInfinity;

        [LuaDocsDescription("A vector of 0 in all axes")]
        public static Vector4 zero => Vector4.zero;

        // Operators

        public static Vector4 operator +(Vector4ApiWrapper a, Vector4ApiWrapper b) => a._Vector4 + b._Vector4;
        public static Vector4 operator -(Vector4ApiWrapper a, Vector4ApiWrapper b) => a._Vector4 - b._Vector4;
        public static Vector4 operator *(Vector4ApiWrapper a, float b) => a._Vector4 * b;
        public static Vector4 operator /(Vector4ApiWrapper a, float b) => a._Vector4 / b;

        [LuaDocsDescription("Adds two vectors")]
        [LuaDocsExample("result = myVector:Add(secondVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector4 Add(Vector4 other) => _Vector4 + other;

        [LuaDocsDescription("Adds x, y and z values to this vector")]
        [LuaDocsExample("result = myVector:Add(1, 2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public Vector4 Add(float x, float y, float z) => _Vector4 + new Vector4(x, y, z);

        [LuaDocsDescription("Subtracts a Vector4 from this vector")]
        [LuaDocsExample("result = myVector:Subtract(otherVector)")]
        [LuaDocsParameter("other", "The vector to subtract")]
        public Vector4 Subtract(Vector4 other) => _Vector4 - other;

        [LuaDocsDescription("Subtracts x, y and z values from this vector")]
        [LuaDocsExample("result = myVector:Subtract(1, 2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public Vector4 Subtract(float x, float y, float z) => _Vector4 - new Vector4(x, y, z);

        [LuaDocsDescription("Multiplies this vector by a scalar value")]
        [LuaDocsExample("result = myVector:Multiply(2)")]
        [LuaDocsParameter("value", "The scalar value")]
        public Vector4 Multiply(float value) => _Vector4 * value;

        [LuaDocsDescription("Multiplies this vector by x, y and z values component-wise")]
        [LuaDocsExample("result = myVector:Multiply(2, 3, 4)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public Vector4 ScaleBy(float x, float y, float z) => Vector4.Scale(_Vector4, new Vector4(x, y, z));

        [LuaDocsDescription("Divides this vector by a scalar value")]
        [LuaDocsExample("result = myVector:Divide(2)")]
        [LuaDocsParameter("value", "The scalar value")]
        public Vector4 Divide(float value) => _Vector4 / value;

        [LuaDocsDescription("Is this vector equal to another?")]
        [LuaDocsExample(@"if myVector:Equals(Vector4.zero) then print(""Vector is zero"") end")]
        [LuaDocsParameter("other", "The other vector")]
        public bool Equals(Vector4ApiWrapper other) => Equals(other._Vector4);

        public override bool Equals(System.Object obj)
        {
            var other = obj as Vector4ApiWrapper;
            return other != null && _Vector4 == other._Vector4;
        }
        public override int GetHashCode() => 0; // Always return 0. Lookups will have to use Equals to compare

        [LuaDocsDescription("Is this vector equal these x, y and z values?")]
        [LuaDocsExample(@"if myVector:Equals(1, 2, 3) then print(""Vector is 1,2,3"") end")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public bool Equals(float x, float y, float z) => _Vector4 == new Vector4(x, y, z);

        [LuaDocsDescription("Is this vector not equal to another?")]
        [LuaDocsExample(@"if myVector:NotEquals(Vector4.zero) then print(""Vector is not zero"") end")]
        [LuaDocsParameter("other", "The other vector")]
        public bool NotEquals(Vector4ApiWrapper other) => _Vector4 != other._Vector4;

        [LuaDocsDescription("Is this vector not equal to these x, y and z values?")]
        [LuaDocsExample(@"if myVector:NotEquals(1, 2, 3) then print(""Vector is not 1,2,3"") end")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        [LuaDocsParameter("z", "The z value")]
        public bool NotEquals(float x, float y, float z) => _Vector4 != new Vector4(x, y, z);
    }
}
