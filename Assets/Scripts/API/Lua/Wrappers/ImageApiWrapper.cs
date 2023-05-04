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
            return $"CameraPath({_ImageWidget})";
        }

        public ImageWidget this[int index] => WidgetManager.m_Instance.ActiveImageWidgets[index].WidgetScript;
        public ImageWidget last => this[count - 1];

        public static int count => WidgetManager.m_Instance.ActiveImageWidgets.Count;

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

        public ImageWidget Import(string location) => ApiMethods.ImportImage(location);
        public void Select() => ApiMethods.SelectImage(index);
        public string FormEncode() => ApiMethods.FormEncodeImage(index);
        public string SaveBase64(string base64, string filename) => ApiMethods.SaveBase64(base64, filename);
    }
}
