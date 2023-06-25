using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("A video widget")]
    [MoonSharpUserData]
    public class VideoApiWrapper
    {

        public VideoWidget _VideoWidget;

        public VideoApiWrapper(VideoWidget widget)
        {
            _VideoWidget = widget;
        }

        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_VideoWidget);

        public override string ToString()
        {
            return $"CameraPath({_VideoWidget})";
        }

        public TrTransform transform
        {
            get =>  App.Scene.MainCanvas.AsCanvas[_VideoWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_VideoWidget.transform] = value;
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

        public static VideoApiWrapper Import(string location) => new (ApiMethods.ImportVideo(location));
        public void Select() => ApiMethods.SelectWidget(_VideoWidget);
        public void Delete() => ApiMethods.DeleteWidget(_VideoWidget);
    }
}
