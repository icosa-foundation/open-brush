using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("A position or offset in 2D space. See https://docs.unity3d.com/ScriptReference/Vector2.html for more detail on many of these methods or properties")]
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

        [LuaDocsDescription("Creates a new vector")]
        [LuaDocsExample("newVector = Vector2(1, 2)")]
        [LuaDocsParameter("x", "The x coordinate")]
        [LuaDocsParameter("y", "The y coordinate")]
        public static Vector2ApiWrapper New(float x = 0, float y = 0)
        {
            var instance = new Vector2ApiWrapper(x, y);
            return instance;
        }

        public override string ToString()
        {
            return $"Vector2({_Vector2.x}, {_Vector2.y})";
        }

        [LuaDocsDescription("The component at the specified index")]
        public float this[int index]
        {
            get => _Vector2[index];
            set => _Vector2[index] = value;
        }

        [LuaDocsDescription("The x coordinate")]
        public float x
        {
            get => _Vector2.x;
            set => _Vector2.x = value;
        }

        [LuaDocsDescription("The y coordinate")]
        public float y
        {
            get => _Vector2.y;
            set => _Vector2.y = value;
        }

        [LuaDocsDescription("The unsigned angle in degrees between this vector and another")]
        [LuaDocsExample("angle = myVector:Angle(otherVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public float Angle(Vector2 other) => Vector2.Angle(_Vector2, other);

        [LuaDocsDescription("Returns a vector with the same direction but with it's length clamped to a maximum")]
        [LuaDocsExample("newVector = myVector:ClampMagnitude")]
        [LuaDocsParameter("maxLength", "The maximum length of the new vector")]
        public Vector2 ClampMagnitude(float maxLength) => Vector2.ClampMagnitude(_Vector2, maxLength);

        [LuaDocsDescription("The distance between two points")]
        [LuaDocsExample("distance = Vector2:Distance(firstPoint, secondPoint)")]
        [LuaDocsParameter("other", "The other vector")]
        public float Distance(Vector2 other) => Vector2.Distance(_Vector2, other);

        [LuaDocsDescription("The length of this vector")]
        public float magnitude
        {
            get => _Vector2.magnitude;
            set => _Vector2 = _Vector2.normalized * value;
        }

        [LuaDocsDescription("The square of the length of this vector (faster to calculate if you're just comparing two lengths)")]
        public float sqrMagnitude
        {
            get => _Vector2.sqrMagnitude;
            set => _Vector2 = _Vector2.normalized * Mathf.Sqrt(value);
        }

        [LuaDocsDescription("The dot product of two vectors")]
        [LuaDocsExample("result = Vector3:Dot(myVector, otherVector")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static float Dot(Vector2 a, Vector2 b) => Vector2.Dot(a, b);

        [LuaDocsDescription("Linearly interpolates between two points")]
        [LuaDocsExample("newPoint = Vector2:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value between 0 and 1 that controls how far between a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);

        [LuaDocsDescription("Linearly interpolates (or extrapolates) between two points")]
        [LuaDocsExample("newPoint = Vector2:Lerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => Vector2.LerpUnclamped(a, b, t);

        [LuaDocsDescription("Creates a vector made from the largest components of the inputs")]
        [LuaDocsExample("result = Vector2:Max(firstVector, secondVector")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector2 Max(Vector2 a, Vector2 b) => Vector2.Max(a, b);

        [LuaDocsDescription("Creates a vector made from the largest components of the inputs")]
        [LuaDocsExample("result = Vector2:Min(firstVector, secondVector")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static Vector2 Min(Vector2 a, Vector2 b) => Vector2.Min(a, b);

        [LuaDocsDescription("Moves a point towards a target point")]
        [LuaDocsExample("newPoint = Vector2:MoveTowards(currentPoint, targetPoint, 0.25)")]
        [LuaDocsParameter("target", "The target point")]
        [LuaDocsParameter("maxDistanceDelta", "The maximum distance to move")]
        public Vector2 MoveTowards(Vector2 target, float maxDistanceDelta) => Vector2.MoveTowards(_Vector2, target, maxDistanceDelta);

        [LuaDocsDescription("Returns a vector with the same distance but witha length of 1")]
        public Vector2 normalized => _Vector2.normalized;

        [LuaDocsDescription("Reflects a vector off the vector defined by a normal")]
        [LuaDocsExample("newVector = myVector:Reflect(normalVector)")]
        [LuaDocsParameter("normal", "The normal vector")]
        public Vector2 Reflect(Vector2 normal) => Vector2.Reflect(_Vector2, normal);

        [LuaDocsDescription("Scales a vector by multiplying it's components by the components of another vector")]
        [LuaDocsExample("newVector = myVector:Scale(otherVector)")]
        [LuaDocsParameter("other", "The vector to scale by")]
        public Vector2 Scale(Vector2 other) => Vector2.Scale(_Vector2, other);

        [LuaDocsDescription("Returns the signed angle in degrees between this vector and another")]
        [LuaDocsExample("result = myVector:SignedAngle(otherVector")]
        [LuaDocsParameter("other", "The other vector")]
        public float SignedAngle(Vector2 other) => Vector2.SignedAngle(_Vector2, other);

        [LuaDocsDescription("Spherically interpolates between two vectors")]
        [LuaDocsExample("newPoint = Vector3:Slerp(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector2 Slerp(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);

        [LuaDocsDescription("Spherically interpolates (or extrapolates) between two vectors")]
        [LuaDocsExample("newPoint = Vector3:SlerpUnclamped(pointA, PointB, 0.25)")]
        [LuaDocsParameter("a", "The first point")]
        [LuaDocsParameter("b", "The second point")]
        [LuaDocsParameter("t", "The value that controls how far between (or beyond) a and b the new point is")]
        [LuaDocsReturnValue("A point somewhere between a and b based on the value of t")]
        public static Vector2 SlerpUnclamped(Vector2 a, Vector2 b, float t) => Vector2.Lerp(a, b, t);

        [LuaDocsDescription("Returns the point the given number of degrees around a circle with radius 1")]
        [LuaDocsExample("result = Vector2:PointOnCircle(45)")]
        [LuaDocsParameter("degrees", "The angle in degrees")]
        public static Vector2 PointOnCircle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        [LuaDocsDescription("Converts this 2D vector to a 3D vector on the YZ plane)")]
        [LuaDocsExample("myVector3 = myVector2:OnX()")]
        [LuaDocsReturnValue("A 3D Vector based on the input but with x as 0: (0, inX, inY)")]
        public Vector3 OnX() => new Vector3(0, _Vector2.x, _Vector2.y);

        [LuaDocsDescription("Converts this 2D vector to a 3D vector on the XZ plane (i.e. with all y values set to 0)")]
        [LuaDocsExample("myVector3 = myVector2:OnY()")]
        [LuaDocsReturnValue("A 3D Vector based on the input but with y as 0: (inX, 0, inY)")]
        public Vector3 OnY() => new Vector3(_Vector2.x, 0, _Vector2.y);

        [LuaDocsDescription("Converts this 2D vector to a 3D vector on the XY plane (i.e. with all z values set to 0)")]
        [LuaDocsExample("myVector3 = myVector2:OnZ()")]
        [LuaDocsReturnValue("A 3D Vector based on the input but with z as 0: (inX, inX, 0)")]
        public Vector3 OnZ() => new Vector3(_Vector2.x, _Vector2.y, 0);

        [LuaDocsDescription("A vector of -1 in the y axis")]
        public static Vector2 down => Vector2.down;

        [LuaDocsDescription("A vector of -1 in the x axis")]
        public static Vector2 left => Vector2.left;

        [LuaDocsDescription("A vector of negative infinity in all axes")]
        public static Vector2 negativeInfinity => Vector2.negativeInfinity;

        [LuaDocsDescription("A vector of 1 in all axes")]
        public static Vector2 one => Vector2.one;

        [LuaDocsDescription("A vector of positive infinity in all axes")]
        public static Vector2 positiveInfinity => Vector2.positiveInfinity;

        [LuaDocsDescription("A vector of 1 in the x axis")]
        public static Vector2 right => Vector2.right;

        [LuaDocsDescription("A vector of 1 in the y axis")]
        public static Vector2 up => Vector2.up;

        [LuaDocsDescription("A vector of 0 in all axes")]
        public static Vector2 zero => Vector2.zero;

        // Operators

        public static Vector2 operator +(Vector2ApiWrapper a, Vector2ApiWrapper b) => a._Vector2 + b._Vector2;
        public static Vector2 operator -(Vector2ApiWrapper a, Vector2ApiWrapper b) => a._Vector2 - b._Vector2;
        public static Vector2 operator *(Vector2ApiWrapper a, float b) => a._Vector2 * b;
        public static Vector2 operator /(Vector2ApiWrapper a, float b) => a._Vector2 / b;


        [LuaDocsDescription("Adds this vector to another")]
        [LuaDocsExample("result = myVector:Add(otherVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector2 Add(Vector2 other) => _Vector2 + other;

        [LuaDocsDescription("Adds the given x and y values to this vector")]
        [LuaDocsExample("result = myVector:Add(2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        public Vector2 Add(float x, float y) => _Vector2 + new Vector2(x, y);

        [LuaDocsDescription("Subtracts another vector from this one")]
        [LuaDocsExample("result = myVector:Subtract(otherVector)")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector2 Subtract(Vector2 other) => _Vector2 - other;


        [LuaDocsDescription("Subtracts the given x and y values from this vector")]
        [LuaDocsExample("result = myVector:Subtract(2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        public Vector2 Subtract(float x, float y) => _Vector2 - new Vector2(x, y);

        [LuaDocsDescription("Multiplies a vector by a scalar value")]
        [LuaDocsExample("result = myVector:Multiply(2)")]
        [LuaDocsParameter("value", "The value to multiply by")]
        public Vector2 Multiply(float value) => _Vector2 * value;

        [LuaDocsDescription("Multiplies this vector by another component-wise")]
        [LuaDocsExample("result = myVector:ScaleBy(Vector2:New(2, 3)))")]
        [LuaDocsParameter("other", "The other vector")]
        public Vector2 ScaleBy(Vector2 other) => Vector2.Scale(_Vector2, other);

        [LuaDocsDescription("Multiplies each component of this vector by the given x and y values")]
        [LuaDocsExample("result = myVector:ScaleBy(2, 3)")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        public Vector2 ScaleBy(float x, float y) => _Vector2 * new Vector2(x, y);

        [LuaDocsDescription("Divides this vector by another")]
        [LuaDocsExample("result = myVector:Divide(2)")]
        [LuaDocsParameter("value", "The value to divide by")]
        public Vector2 Divide(float value) => _Vector2 / value;

        [LuaDocsDescription("Is this vector equal to another?")]
        [LuaDocsExample(@"if myVector:Equals(Vector2.zero) then print(""Vector is zero"") end")]
        [LuaDocsParameter("other", "The other vector")]
        public bool Equals(Vector2ApiWrapper other) => Equals(other._Vector2);

        public override bool Equals(System.Object obj)
        {
            var other = obj as Vector2ApiWrapper;
            return other != null && _Vector2 == other._Vector2;
        }
        public override int GetHashCode() => 0; // Always return 0. Lookups will have to use Equals to compare

        [LuaDocsDescription("Is this vector equal to the given x and y values?")]
        [LuaDocsExample(@"if myVector:Equals(1, 2) then print(""Vector is 1,2"") end")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        public bool Equals(float x, float y) => _Vector2 == new Vector2(x, y);

        [LuaDocsDescription("Is this vector not equal to another?")]
        [LuaDocsExample(@"if myVector:NotEquals(Vector2.zero) then print(""Vector is not zero"") end")]
        [LuaDocsParameter("other", "The other vector")]
        public bool NotEquals(Vector2 other) => _Vector2 != other;

        [LuaDocsDescription("Is this vector not equal to the given x and y values?")]
        [LuaDocsExample(@"if myVector:NotEquals(1, 2) then print(""Vector is not 1,2"") end")]
        [LuaDocsParameter("x", "The x value")]
        [LuaDocsParameter("y", "The y value")]
        public bool NotEquals(float x, float y) => _Vector2 != new Vector2(x, y);

    }

}
