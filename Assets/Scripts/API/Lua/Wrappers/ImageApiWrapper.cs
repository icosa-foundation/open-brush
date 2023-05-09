using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class ImageApiWrapper
    {
        public ImageWidget _ImageWidget;

        public ImageApiWrapper(ImageWidget widget)
        {
            _ImageWidget = widget;
        }

        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_ImageWidget);

        public override string ToString()
        {
            return $"Image({_ImageWidget})";
        }

        public TrTransform transform
        {
            get =>  App.Scene.MainCanvas.AsCanvas[_ImageWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_ImageWidget.transform] = value;
            }
        }

        public Vector3 position
        {
            get => transform.translation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.T(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.translation = newTransform.translation;
                transform = tr_CS;
            }
        }

        public Quaternion rotation
        {
            get => transform.rotation;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.R(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.rotation = newTransform.rotation;
                transform = tr_CS;
            }
        }

        public float scale
        {
            get => transform.scale;
            set
            {
                var tr_CS = transform;
                var newTransform = TrTransform.S(value);
                newTransform = App.Scene.Pose * newTransform;
                tr_CS.scale = newTransform.scale;
                transform = tr_CS;
            }
        }

        public static ImageApiWrapper Import(string location) => new (ApiMethods.ImportImage(location));
        public void Select() => ApiMethods.SelectImage(index);
        public string FormEncode() => ApiMethods.FormEncodeImage(index);
        public string SaveBase64(string base64, string filename) => ApiMethods.SaveBase64(base64, filename);
    }
}
