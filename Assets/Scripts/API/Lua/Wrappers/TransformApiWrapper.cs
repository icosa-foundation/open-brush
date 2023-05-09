using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
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

        // Convenient shorthand
        public TransformApiWrapper(float x, float y, float z)
        {
            _TrTransform = TrTransform.T(new Vector3(x, y, z));
        }

        public TransformApiWrapper(TrTransform tr)
        {
            _TrTransform = tr;
        }

        public static TransformApiWrapper New(Vector3 translation, Quaternion rotation, float scale = 1)
        {
            var instance = new TransformApiWrapper(translation, rotation, scale);
            return instance;
        }

        public static TransformApiWrapper New(Vector3 translation, float scale = 1)
        {
            var instance = new TransformApiWrapper(translation, Quaternion.identity, scale);
            return instance;
        }

        public static TransformApiWrapper New(float scale = 1)
        {
            var instance = new TransformApiWrapper(Vector3.zero, Quaternion.identity, scale);
            return instance;
        }

        public override string ToString()
        {
            return $"TrTransform({_TrTransform.translation}, {_TrTransform.rotation}, {_TrTransform.scale})";
        }

        public static TrTransform zero => TrTransform.identity;

        // Operators
        public TrTransform Multiply(TrTransform b) => _TrTransform * b;
        public bool Equals(TrTransform b) => _TrTransform == b;

        // Static Operators
        public static TrTransform Multiply(TrTransform a, TrTransform b) => a * b;
        public static bool Equals(TrTransform a, TrTransform b) => a == b;
    }
}
