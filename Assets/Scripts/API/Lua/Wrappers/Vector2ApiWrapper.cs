using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("A position or offset in 2D space")]
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

        [LuaDocsDescription("Gets or sets the x coordinate")]
        public float x
        {
            get => _Vector2.x;
            set => _Vector2.x = value;
        }

        [LuaDocsDescription("Gets or sets the y coordinate")]
        public float y
        {
            get => _Vector2.y;
            set => _Vector2.y = value;
        }

        [LuaDocsDescription("Returns the angle between two points and the origin")]
        public static float Angle(Vector2 a, Vector2 b) => Vector2.Angle(a, b);

        [LuaDocsDescription("Returns a vector with the same direction but with it's length clamped to a maximum")]
        [LuaDocsExample("newVector = myVector:ClampMagnitude")]
        [LuaDocsParameter("maxLength", "The maximum length of the new vector")]
        public Vector2 ClampMagnitude(float maxLength) => Vector2.ClampMagnitude(_Vector2, maxLength);

        [LuaDocsDescription("The distance between two points")]
        [LuaDocsExample("distance = Vector2:Distance(firstPoint, secondPoint)")]
        [LuaDocsParameter("a", "The first vector")]
        [LuaDocsParameter("b", "The second vector")]
        public static float Distance(Vector2 a, Vector2 b) => Vector2.Distance(a, b);

        [LuaDocsDescription("The length of this vector")]
        public float magnitude => _Vector2.magnitude;

        [LuaDocsDescription("The square of the length of this vector (faster to calculate if you're just comparing two lengths)")]
        public float sqrMagnitude => _Vector2.sqrMagnitude;

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
        [LuaDocsParameter("current", "The current point")]
        [LuaDocsParameter("target", "The target point")]
        [LuaDocsParameter("maxDistanceDelta", "The maximum distance to move")]
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta) => Vector2.MoveTowards(current, target, maxDistanceDelta);

        [LuaDocsDescription("Returns a vector with the same distance but witha length of 1")]
        public Vector2 normalized => _Vector2.normalized;

        [LuaDocsDescription("Reflects a vector off the vector defined by a normal")]
        [LuaDocsExample("")]
        [LuaDocsParameter("", "")]
        public static Vector2 Reflect(Vector2 a, Vector2 b) => Vector2.Reflect(a, b);

        [LuaDocsDescription("Scales a vector by multiplying it's components by the components of another vector")]
        [LuaDocsExample("")]
        [LuaDocsParameter("", "")]
        public Vector2 Scale(Vector2 other) => Vector2.Scale(_Vector2, other);

        [LuaDocsDescription("Returns the signed angle between this vector and another")]
        [LuaDocsExample("")]
        [LuaDocsParameter("", "")]
        public float SignedAngle(Vector2 other, Vector2 axis) => Vector2.SignedAngle(_Vector2, other);

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
        public static Vector2 PointOnCircle(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        [LuaDocsDescription("Converts this 2D vector to a 3D vector on the YZ plane (i.e. with all x values set to 0)")]
        [LuaDocsExample("myVector3 = myVector2:OnX()")]
        public Vector3 OnX() => new Vector3(0, _Vector2.x, _Vector2.y);

        [LuaDocsDescription("Converts this 2D vector to a 3D vector on the XZ plane (i.e. with all y values set to 0)")]
        [LuaDocsExample("myVector3 = myVector2:OnY()")]
        public Vector3 OnY() => new Vector3(_Vector2.x, 0, _Vector2.y);

        [LuaDocsDescription("Converts this 2D vector to a 3D vector on the XY plane (i.e. with all z values set to 0)")]
        [LuaDocsExample("myVector3 = myVector2:OnZ()")]
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

        [LuaDocsDescription("Adds this vector to another")]
        public Vector2 Add(Vector2 other) => _Vector2 + other;

        [LuaDocsDescription("Adds the given x and y values to this vector")]
        public Vector2 Add(float x, float y) => _Vector2 + new Vector2(x, y);

        [LuaDocsDescription("Subtracts another vector from this one")]
        public Vector2 Subtract(Vector2 other) => _Vector2 - other;

        [LuaDocsDescription("Subtracts the given x and y values from this vector")]
        public Vector2 Subtract(float x, float y) => _Vector2 - new Vector2(x, y);

        [LuaDocsDescription("Multiplies a vector by a scalar value")]
        public Vector2 Multiply(float value) => _Vector2 * value;

        [LuaDocsDescription("Multiplies this vector by another component-wise")]
        public Vector2 ScaleBy(Vector2 other) => Vector2.Scale(_Vector2, other);

        [LuaDocsDescription("Multiplies each component of this vector by the given x and y values")]
        public Vector2 ScaleBy(float x, float y) => _Vector2 * new Vector2(x, y);

        [LuaDocsDescription("Divides this vector by another")]
        public Vector2 Divide(float value) => _Vector2 / value;

        [LuaDocsDescription("Is this vector equal to another?")]
        public bool Equals(Vector2 other) => _Vector2 == other;

        [LuaDocsDescription("Is this vector equal to the given x and y values?")]
        public bool Equals(float x, float y) => _Vector2 == new Vector2(x, y);

        [LuaDocsDescription("Is this vector not equal to another?")]
        public bool NotEquals(Vector2 other) => _Vector2 != other;

        [LuaDocsDescription("Is this vector not equal to the given x and y values?")]
        public bool NotEquals(float x, float y) => _Vector2 != new Vector2(x, y);
    }

}
