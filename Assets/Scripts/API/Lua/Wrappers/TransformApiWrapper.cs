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
        [LuaDocsExample("newTransform = myTransform:TranslateBy(Vector3(1, 2, 3))")]
        [LuaDocsParameter("translation", "The translation to apply")]
        public TrTransform TranslateBy(Vector3 translation) => _TrTransform * TrTransform.T(translation);

        [LuaDocsDescription("Applies a rotation to this transform")]
        [LuaDocsExample("newTransform = myTransform:RotateBy(Rotation.New(0, 45, 0))")]
        [LuaDocsParameter("rotation", "The rotation to apply")]
        public TrTransform RotateBy(Quaternion rotation) => _TrTransform * TrTransform.R(rotation);

        [LuaDocsDescription("Applies a scale to this transform")]
        [LuaDocsExample("newTransform = myTransform:ScaleBy(2)")]
        [LuaDocsParameter("scale", "The scale value to apply")]
        public TrTransform ScaleBy(float scale) => _TrTransform * TrTransform.S(scale);

        // Convenient shorthand
        public TransformApiWrapper(float x, float y, float z)
        {
            _TrTransform = TrTransform.T(new Vector3(x, y, z));
        }

        public TransformApiWrapper(TrTransform tr)
        {
            _TrTransform = tr;
        }

        [LuaDocsDescription("Creates a new translation, rotation and scale transform")]
        [LuaDocsExample("myTransform = Transform:New(Vector3(1, 2, 3), Rotation.identity, 2)")]
        [LuaDocsParameter("translation", "The translation amount")]
        [LuaDocsParameter("rotation", "The rotation amount")]
        [LuaDocsParameter("scale", "The scale amount")]
        public static TransformApiWrapper New(Vector3 translation, Quaternion rotation, float scale = 1)
        {
            var instance = new TransformApiWrapper(translation, rotation, scale);
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

        [LuaDocsDescription("Creates a new translation transform based on the x, y, z values")]
        [LuaDocsExample("myTransform = Transform:New(1, 2, 3)")]
        [LuaDocsParameter("x", "The x translation amount")]
        [LuaDocsParameter("y", "The y translation amount")]
        [LuaDocsParameter("z", "The z translation amount")]
        public static TransformApiWrapper New(float x, float y, float z)
        {
            var instance = new TransformApiWrapper(x, y, z);
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
        public static TrTransform identity => TrTransform.identity;

        // Operators
        [LuaDocsDescription(@"Combines another transform with this one (Does the same as ""TransformBy"")")]
        [LuaDocsExample("newTransform = myTransform:Multiply(Transform.up)")]
        [LuaDocsParameter("other", "The Transform to apply to this one")]
        public TrTransform Multiply(TrTransform other) => _TrTransform * other;

        [LuaDocsDescription("Is this transform equal to another?")]
        [LuaDocsExample(@"if myTransform:Equals(Transform.up) then print(""Equal to Transform.up"")")]
        [LuaDocsParameter("other", "The Transform to compare to this one")]
        public bool Equals(TrTransform other) => _TrTransform == other;
    }
}
