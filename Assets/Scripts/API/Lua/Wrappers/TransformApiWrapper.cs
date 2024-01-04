using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Represents a position, rotation and scale in one object")]
    [MoonSharpUserData]
    public class TransformApiWrapper
    {
        public TrTransform _TrTransform;

        public TransformApiWrapper(Vector3 translation, Quaternion rotation, float scale = 1)
        {
            _TrTransform = TrTransform.TRS(translation, rotation, scale);
        }

        public TransformApiWrapper(Vector3 translation, float scale = 1)
        {
            _TrTransform = TrTransform.TRS(translation, Quaternion.identity, scale);
        }

        public TransformApiWrapper(float x, float y, float z)
        {
            _TrTransform = TrTransform.TRS(new Vector3(x, y, z), Quaternion.identity, 1f);
        }

        [LuaDocsDescription("The inverse of this transform")]
        public TrTransform inverse => _TrTransform.inverse;

        [LuaDocsDescription("A translation of 1 in the y axis")]
        public Vector3 up => _TrTransform.up;

        [LuaDocsDescription("A translation of -1 in the y axis")]
        public Vector3 down => -_TrTransform.up;

        [LuaDocsDescription("A translation of 1 in the x axis")]
        public Vector3 right => _TrTransform.right;

        [LuaDocsDescription("A translation of -1 in the x axis")]
        public Vector3 left => -_TrTransform.right;

        [LuaDocsDescription("A translation of 1 in the z axis")]
        public Vector3 forward => _TrTransform.forward;

        [LuaDocsDescription("A translation of -1 in the z axis")]
        public Vector3 back => -_TrTransform.forward;


        [LuaDocsDescription("Applies another transform to this transform")]
        [LuaDocsExample("newTransform = myTransform:TransformBy(myOtherTransform)")]
        [LuaDocsParameter("transform", "The transform to apply")]
        public TrTransform TransformBy(TrTransform transform) => _TrTransform * transform;

        [LuaDocsDescription("Applies a translation to this transform")]
        [LuaDocsExample("newTransform = myTransform:TranslateBy(Vector3.up * 3)")]
        [LuaDocsParameter("translation", "The translation to apply")]
        public TrTransform TranslateBy(Vector3 translation) => _TrTransform * TrTransform.T(translation);

        [LuaDocsDescription("Applies a translation to this transform")]
        [LuaDocsExample("newTransform = myTransform:TranslateBy(1, 2, 3)")]
        [LuaDocsParameter("x", "The x translation to apply")]
        [LuaDocsParameter("y", "The y translation to apply")]
        [LuaDocsParameter("z", "The z translation to apply")]
        public TrTransform TranslateBy(float x, float y, float z) => TranslateBy(new Vector3(x, y, z));

        [LuaDocsDescription("Applies a rotation to this transform")]
        [LuaDocsExample("newTransform = myTransform:RotateBy(Rotation.left)")]
        [LuaDocsParameter("rotation", "The rotation to apply")]
        public TrTransform RotateBy(Quaternion rotation) => _TrTransform * TrTransform.R(rotation);

        [LuaDocsDescription("Applies a rotation to this transform")]
        [LuaDocsExample("newTransform = myTransform:RotateBy(45, 0, 0)")]
        [LuaDocsParameter("x", "The x rotation to apply")]
        [LuaDocsParameter("y", "The y rotation to apply")]
        [LuaDocsParameter("z", "The z rotation to apply")]
        public TrTransform RotateBy(float x, float y, float z) => RotateBy(Quaternion.Euler(x, y, z));

        [LuaDocsDescription("Applies a scale to this transform")]
        [LuaDocsExample("newTransform = myTransform:ScaleBy(2)")]
        [LuaDocsParameter("scale", "The scale value to apply")]
        public TrTransform ScaleBy(float scale) => _TrTransform * TrTransform.S(scale);

        public TransformApiWrapper(TrTransform tr)
        {
            _TrTransform = tr;
        }

        [LuaDocsDescription("Creates a new translation, rotation and scale transform")]
        [LuaDocsExample("myTransform = Transform:New(Vector3(1, 2, 3), Rotation.identity, 2)")]
        [LuaDocsParameter("translation", "The translation amount")]
        [LuaDocsParameter("rotation", "The rotation amount")]
        [LuaDocsParameter("scale", "The scale amount")]
        public static TransformApiWrapper New(Vector3 translation, Quaternion rotation, float scale)
        {
            var instance = new TransformApiWrapper(translation, rotation, scale);
            return instance;
        }

        [LuaDocsDescription("Creates a new translation and rotation transform")]
        [LuaDocsExample("myTransform = Transform:New(Vector3(1, 2, 3), Rotation.identity)")]
        [LuaDocsParameter("translation", "The translation amount")]
        [LuaDocsParameter("rotation", "The rotation amount")]
        public static TransformApiWrapper New(Vector3 translation, Quaternion rotation)
        {
            var instance = new TransformApiWrapper(translation, rotation);
            return instance;
        }

        [LuaDocsDescription("Creates a new translation transform")]
        [LuaDocsExample("myTransform = Transform:New(Vector3(1, 2, 3))")]
        [LuaDocsParameter("translation", "The translation amount")]
        public static TransformApiWrapper New(Vector3 translation)
        {
            var instance = new TransformApiWrapper(translation, Quaternion.identity);
            return instance;
        }

        [LuaDocsDescription("Creates a new translation and scale transform")]
        [LuaDocsExample("myTransform = Transform:New(Vector3(1, 2, 3), 2)")]
        [LuaDocsParameter("translation", "The translation amount")]
        [LuaDocsParameter("scale", "The scale amount")]
        public static TransformApiWrapper New(Vector3 translation, float scale)
        {
            var instance = new TransformApiWrapper(translation, Quaternion.identity, scale);
            return instance;
        }

        [LuaDocsDescription("Creates a new translation transform")]
        [LuaDocsExample("myTransform = Transform:Position(1, 2, 3)")]
        [LuaDocsParameter("x", "The x translation amount")]
        [LuaDocsParameter("y", "The y translation amount")]
        [LuaDocsParameter("z", "The z translation amount")]
        public static TransformApiWrapper Position(float x, float y, float z)
        {
            var instance = new TransformApiWrapper(TrTransform.T(new Vector3(x, y, z)));
            return instance;
        }

        [LuaDocsDescription("Creates a new translation transform")]
        [LuaDocsExample("myTransform = Transform:Position(myVector3)")]
        [LuaDocsParameter("position", "The Vector3 position")]
        public static TransformApiWrapper Position(Vector3ApiWrapper position)
        {
            var instance = new TransformApiWrapper(TrTransform.T(position._Vector3));
            return instance;
        }

        [LuaDocsDescription("Creates a new rotation transform")]
        [LuaDocsExample("myTransform = Transform:Rotation(1, 2, 3)")]
        [LuaDocsParameter("x", "The x rotation amount")]
        [LuaDocsParameter("y", "The y rotation amount")]
        [LuaDocsParameter("z", "The z rotation amount")]
        public static TransformApiWrapper Rotation(float x, float y, float z)
        {
            var instance = new TransformApiWrapper(TrTransform.R(Quaternion.Euler(x, y, z)));
            return instance;
        }

        [LuaDocsDescription("Creates a new rotation transform")]
        [LuaDocsExample("myTransform = Transform:Rotation(myRotation)")]
        [LuaDocsParameter("rotation", "The rotation")]
        public static TransformApiWrapper Rotation(RotationApiWrapper rotation)
        {
            var instance = new TransformApiWrapper(TrTransform.R(rotation._Quaternion));
            return instance;
        }

        [LuaDocsDescription("Creates a new scale transform")]
        [LuaDocsExample("myTransform = Transform:Scale(2)")]
        [LuaDocsParameter("amount", "The scale amount")]
        public static TransformApiWrapper Scale(float amount)
        {
            var instance = new TransformApiWrapper(TrTransform.S(amount));
            return instance;
        }

        public override string ToString()
        {
            return $"TrTransform({_TrTransform.translation}, {_TrTransform.rotation}, {_TrTransform.scale})";
        }

        [LuaDocsDescription("Get or set the position of this transform")]
        public Vector3ApiWrapper position
        {
            get => new Vector3ApiWrapper(_TrTransform.translation);
            set => _TrTransform.translation = value._Vector3;
        }

        [LuaDocsDescription("Get or set the rotation of this transform")]
        public RotationApiWrapper rotation
        {
            get => new RotationApiWrapper(_TrTransform.rotation);
            set => _TrTransform.rotation = value._Quaternion;
        }

        [LuaDocsDescription("Get or set the scale of this transform")]
        public float scale
        {
            get => _TrTransform.scale;
            set => _TrTransform.scale = value;
        }

        [LuaDocsDescription("A transform that does nothing. No translation, rotation or scaling")]
        public static TransformApiWrapper identity => new(TrTransform.identity);

        // Operators

        public static TrTransform operator *(TransformApiWrapper a, TransformApiWrapper b) => a._TrTransform * b._TrTransform;

        [LuaDocsDescription(@"Combines another transform with this one (Does the same as ""TransformBy"")")]
        [LuaDocsExample("newTransform = myTransform:Multiply(Transform.up)")]
        [LuaDocsParameter("other", "The Transform to apply to this one")]
        public TrTransform Multiply(TrTransform other) => _TrTransform * other;

        [LuaDocsDescription("Is this transform equal to another?")]
        [LuaDocsExample(@"if myTransform:Equals(Transform.up) then print(""Equal to Transform.up"")")]
        [LuaDocsParameter("other", "The Transform to compare to this one")]
        public bool Equals(TransformApiWrapper other) => Equals(other._TrTransform);

        public override bool Equals(System.Object obj)
        {
            var other = obj as TransformApiWrapper;
            return other != null && _TrTransform == other._TrTransform;
        }
        public override int GetHashCode() => 0; // Always return 0. Lookups will have to use Equals to compare

        [LuaDocsDescription("Interpolates between two transforms")]
        [LuaDocsExample("newTransform = Transform:Lerp(transformA, transformB, 0.25)")]
        [LuaDocsParameter("a", "The first transform")]
        [LuaDocsParameter("b", "The second transform")]
        [LuaDocsParameter("t", "The value between 0 and 1 that controls how far between a and b the new transform is")]
        [LuaDocsReturnValue("A transform that blends between a and b based on the value of t")]
        public static TrTransform Lerp(TrTransform a, TrTransform b, float t) => TrTransform.TRS(
            Vector3.Lerp(a.translation, b.translation, t),
            Quaternion.Slerp(a.rotation, b.rotation, t),
            Mathf.Lerp(a.scale, b.scale, t)
        );
    }
}
