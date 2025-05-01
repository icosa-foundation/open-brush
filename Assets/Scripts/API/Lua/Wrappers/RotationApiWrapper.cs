using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Represents a rotation or orientation in 3D space. See https://docs.unity3d.com/ScriptReference/Quaternion.html for further documentation")]
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

        [LuaDocsDescription("Creates a new Rotation")]
        [LuaDocsExample("myRotation = Rotation:New(45, -90, 0)")]
        [LuaDocsParameter("x", "The angle of rotation on the x axis in degrees")]
        [LuaDocsParameter("y", "The angle of rotation on the y axis in degrees")]
        [LuaDocsParameter("z", "The angle of rotation on the z axis in degrees")]
        public static RotationApiWrapper New(float x = 0, float y = 0, float z = 0)
        {
            var instance = new RotationApiWrapper(x, y, z);
            return instance;
        }

        public override string ToString()
        {
            return $"Rotation({_Quaternion.eulerAngles.x}, {_Quaternion.eulerAngles.y}, {_Quaternion.eulerAngles.z})";
        }

        [LuaDocsDescription(@"The amount of rotation in degrees around a single axis (0=x, 1=y, 2=z)")]
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

        [LuaDocsDescription(@"The amount of rotation around the x axis in degrees")]
        public float x
        {
            get => _Quaternion.eulerAngles.x;
            set => Quaternion.Euler(value, y, z);
        }

        [LuaDocsDescription(@"The amount of rotation around the y axis in degrees")]
        public float y
        {
            get => _Quaternion.eulerAngles.y;
            set => Quaternion.Euler(x, value, z);
        }

        [LuaDocsDescription(@"The amount of rotation around the z axis in degrees")]
        public float z
        {
            get => _Quaternion.eulerAngles.z;
            set => Quaternion.Euler(x, y, value);
        }

        [LuaDocsDescription(@"A rotation of zero in all axes")]
        public static Quaternion zero => Quaternion.identity;

        [LuaDocsDescription(@"A 90 degree anti-clockwise rotation in the y axis (yaw)")]
        public static Quaternion left => Quaternion.Euler(0, -90, 0);

        [LuaDocsDescription(@"A 90 degree clockwise rotation in the y axis (yaw)")]
        public static Quaternion right => Quaternion.Euler(0, 90, 0);

        [LuaDocsDescription(@"A 90 degree clockwise rotation in the x axis (pitch)")]
        public static Quaternion up => Quaternion.Euler(90, 0, 0);

        [LuaDocsDescription(@"A 90 degree anti-clockwise rotation in the x axis (pitch)")]
        public static Quaternion down => Quaternion.Euler(-90, 0, 0);

        [LuaDocsDescription(@"Converts this rotation to one with the same orientation but with a magnitude of 1")]
        public Quaternion normalized => _Quaternion.normalized;

        [LuaDocsDescription(@"Returns the Inverse of this rotation")]
        public Quaternion inverse => Quaternion.Inverse(_Quaternion);

        private (float, Vector3) _GetAngleAxis()
        {
            _Quaternion.ToAngleAxis(out float angle, out Vector3 axis);
            return new(angle, axis);
        }

        [LuaDocsDescription(@"The angle in degrees of the angle-axis representation of this rotation")]
        public float angle
        {
            get
            {
                var (angle, _) = _GetAngleAxis();
                return angle;
            }
            set
            {
                var (_, axis) = _GetAngleAxis();
                _Quaternion = Quaternion.AngleAxis(value, axis);
            }
        }

        [LuaDocsDescription(@"The axis part of the angle-axis representation of this rotation")]
        public Vector3 axis
        {
            get
            {
                var (_, axis) = _GetAngleAxis();
                return axis;
            }
            set
            {
                var (angle, _) = _GetAngleAxis();
                _Quaternion = Quaternion.AngleAxis(angle, value);
            }
        }

        [LuaDocsDescription(@"Creates a rotation which rotates from one direction to another")]
        [LuaDocsExample(@"newRot = myRotation:SetFromToRotation(Vector3:New(0, 5, 5), Vector3:New(5, 5, 0))")]
        [LuaDocsParameter("fromDirection", "The starting direction")]
        [LuaDocsParameter("toDirection", "The target direction")]
        [LuaDocsReturnValue(@"A rotation that would change one direction to the other")]
        public Quaternion SetFromToRotation(Vector3 fromDirection, Vector3 toDirection)
        {
            _Quaternion.SetFromToRotation(fromDirection, toDirection);
            return _Quaternion;
        }

        [LuaDocsDescription(@"Creates a rotation with the specified forward directions")]
        [LuaDocsExample(@"result = myRotation:SetLookRotation(view)")]
        [LuaDocsParameter("view", "The direction to look in")]
        [LuaDocsReturnValue(@"The new Rotation")]
        public Quaternion SetLookRotation(Vector3 view)
        {
            _Quaternion.SetLookRotation(view, Vector3.up);
            return _Quaternion;
        }

        [LuaDocsDescription(@"Creates a rotation with the specified forward and upwards directions")]
        [LuaDocsExample(@"result = myRotation:SetLookRotation(view, up)")]
        [LuaDocsParameter("view", "The direction to look in")]
        [LuaDocsParameter("up", "The vector that defines in which direction is up")]
        [LuaDocsReturnValue(@"The new Rotation")]
        public Quaternion SetLookRotation(Vector3 view, Vector3 up)
        {
            _Quaternion.SetLookRotation(view, up);
            return _Quaternion;
        }

        [LuaDocsDescription(@"Returns the angle in degrees between two rotations")]
        [LuaDocsExample(@"result = Rotation:Angle(a, b)")]
        [LuaDocsParameter("a", "The first rotation angle")]
        [LuaDocsParameter("b", "The second rotation angle")]
        [LuaDocsReturnValue(@"Returns the angle in degrees between two rotations")]
        public static float Angle(Quaternion a, Quaternion b) => Quaternion.Angle(a, b);

        [LuaDocsDescription(@"Creates a rotation which rotates angle degrees around axis")]
        [LuaDocsExample(@"result = Rotation:AngleAxis(angle, axis)")]
        [LuaDocsParameter("angle", "The angle in degrees")]
        [LuaDocsParameter("axis", "The axis of rotation")]
        [LuaDocsReturnValue(@"Returns a Quaternion that represents the rotation")]
        public static Quaternion AngleAxis(float angle, Vector3 axis) => Quaternion.AngleAxis(angle, axis);

        [LuaDocsDescription(@"The dot product between two rotations")]
        [LuaDocsExample(@"result = Rotation:Dot(a, b)")]
        [LuaDocsParameter("a", "The first rotation")]
        [LuaDocsParameter("b", "The second rotation")]
        [LuaDocsReturnValue(@"Returns the dot product between two rotations")]
        public static float Dot(Quaternion a, Quaternion b) => Quaternion.Dot(a, b);

        [LuaDocsDescription(@"Creates a rotation which rotates from fromDirection to toDirection")]
        [LuaDocsExample(@"result = Rotation:FromToRotation(from, to)")]
        [LuaDocsParameter("from", "The initial direction vector")]
        [LuaDocsParameter("to", "The target direction vector")]
        [LuaDocsReturnValue(@"Returns a Quaternion that represents the rotation")]
        public static Quaternion FromToRotation(Vector3 from, Vector3 to) => Quaternion.FromToRotation(from, to);

        [LuaDocsDescription(@"Interpolates between a and b by t and normalizes the result afterwards. The parameter t is clamped to the range [0, 1]")]
        [LuaDocsExample(@"result = Rotation:Lerp(a, b, t)")]
        [LuaDocsParameter("a", "The first rotation")]
        [LuaDocsParameter("b", "The second rotation")]
        [LuaDocsParameter("t", "A ratio between 0 and 1")]
        [LuaDocsReturnValue(@"Interpolated rotation")]
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => Quaternion.Lerp(a, b, t);

        [LuaDocsDescription(@"Interpolates between a and b by t and normalizes the result afterwards. The parameter t is not clamped")]
        [LuaDocsExample(@"result = Rotation:LerpUnclamped(a, b, t)")]
        [LuaDocsParameter("a", "The first rotation")]
        [LuaDocsParameter("b", "The second rotation")]
        [LuaDocsParameter("t", "A ratio between 0 and 1")]
        [LuaDocsReturnValue(@"Interpolated rotation")]
        public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t) => Quaternion.LerpUnclamped(a, b, t);

        [LuaDocsDescription(@"Creates a rotation with the specified forward and upwards directions")]
        [LuaDocsExample(@"result = Rotation:LookRotation(forward)")]
        [LuaDocsParameter("forward", "Vector3 forward direction")]
        [LuaDocsReturnValue(@"Rotation with specified forward direction")]
        public static Quaternion LookRotation(Vector3 forward) => Quaternion.LookRotation(forward, Vector3.up);

        [LuaDocsDescription(@"Creates a rotation with the specified forward and upwards directions")]
        [LuaDocsExample(@"result = Rotation:LookRotation(forward, up)")]
        [LuaDocsParameter("forward", "Vector3 forward direction")]
        [LuaDocsParameter("up", "Vector3 up direction")]
        [LuaDocsReturnValue(@"Rotation with specified forward and up directions")]
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => Quaternion.LookRotation(forward, up);

        [LuaDocsDescription(@"Converts this quaternion to one with the same orientation but with a magnitude of 1")]
        [LuaDocsExample(@"result = Rotation:Normalize(a)")]
        [LuaDocsParameter("a", "The input rotation")]
        [LuaDocsReturnValue(@"Normalized rotation")]
        public static Quaternion Normalize(Quaternion a) => Quaternion.Normalize(a);

        [LuaDocsDescription(@"Rotates a rotation from towards to")]
        [LuaDocsExample(@"result = Rotation:RotateTowards(to, maxDegreesDelta)")]
        [LuaDocsParameter("from", "Rotation from")]
        [LuaDocsParameter("to", "Rotation to")]
        [LuaDocsParameter("maxDegreesDelta", "Max degrees delta")]
        [LuaDocsReturnValue(@"Rotation rotated from towards to")]
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta) => Quaternion.RotateTowards(from, to, maxDegreesDelta);

        [LuaDocsDescription(@"Spherically interpolates between quaternions a and b by ratio t. The parameter t is clamped to the range [0, 1]")]
        [LuaDocsExample(@"result = Rotation:Slerp(a, b, t)")]
        [LuaDocsParameter("a", "The first rotation")]
        [LuaDocsParameter("b", "The second rotation")]
        [LuaDocsParameter("t", "A ratio between 0 and 1")]
        [LuaDocsReturnValue(@"Spherically interpolated rotation")]
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => Quaternion.Slerp(a, b, t);

        [LuaDocsDescription(@"Spherically interpolates between a and b by t. The parameter t is not clamped")]
        [LuaDocsExample(@"result = Rotation:SlerpUnclamped(a, b, t)")]
        [LuaDocsParameter("a", "The first rotation")]
        [LuaDocsParameter("b", "The second rotation")]
        [LuaDocsParameter("t", "A ratio")]
        [LuaDocsReturnValue(@"Spherically interpolated rotation")]
        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t) => Quaternion.SlerpUnclamped(a, b, t);

        // Operators

        public static Quaternion operator *(RotationApiWrapper a, RotationApiWrapper b) => a._Quaternion * b._Quaternion;

        [LuaDocsDescription(@"Combines two rotations")]
        [LuaDocsExample(@"result = myRotation:Multiply(anotherRotation)")]
        [LuaDocsParameter("other", "The other rotation")]
        [LuaDocsReturnValue(@"The rotation that represents applying both rotations in turn")]
        public Quaternion Multiply(Quaternion other) => _Quaternion * other;

        [LuaDocsDescription(@"Are two rotations the same?")]
        [LuaDocsExample(@"if myRotation:Equals(otherRotation) then print(""Equal!"") end")]
        [LuaDocsParameter("other", "The rotation to comapre to")]
        [LuaDocsReturnValue(@"True if they are the same as each other")]
        public bool Equals(RotationApiWrapper other) => Equals(other._Quaternion);

        [LuaDocsDescription("Determines whether this rotation is not equal to the specified rotation")]
        [LuaDocsExample("if myRotation:NotEquals(rot2) then print(\"rotations are different\") end")]
        [LuaDocsParameter("other", "The rotation to compare")]
        [LuaDocsReturnValue("true if this rotation is not equal to the specified rotation; otherwise, false")]
        public bool NotEquals(RotationApiWrapper other) => !Equals(other);

        [LuaDocsDescription("Determines whether this rotation is equal to the specified xyz values")]
        [LuaDocsExample("if myRotation:Equals(0, 90, 0) then print(\"90 degree turn to the right\") end")]
        [LuaDocsParameter("x", "The x value to compare")]
        [LuaDocsParameter("y", "The y value to compare")]
        [LuaDocsParameter("z", "The z value to compare")]
        [LuaDocsReturnValue("true if this rotation is equal to the specified xyz values; otherwise, false")]
        public bool Equals(float x, float y, float z) => _Quaternion == Quaternion.Euler(x, y, z);

        public override bool Equals(System.Object obj)
        {
            var other = obj as RotationApiWrapper;
            return other != null && _Quaternion == other._Quaternion;
        }
        public override int GetHashCode() => 0; // Always return 0. Lookups will have to use Equals to compare
    }
}
