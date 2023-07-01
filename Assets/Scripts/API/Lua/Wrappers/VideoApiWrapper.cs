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

        [LuaDocsDescription("Gets or sets the Transform (position, rotation, scale) of the Video Widget")]
        public TrTransform transform
        {
            get =>  App.Scene.MainCanvas.AsCanvas[_VideoWidget.transform];
            set
            {
                value = App.Scene.Pose * value;
                App.Scene.ActiveCanvas.AsCanvas[_VideoWidget.transform] = value;
            }
        }

        [LuaDocsDescription("The 3D position of the Video Widget")]
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

        [LuaDocsDescription("The 3D orientation of the Video Widget")]
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

        [LuaDocsDescription("Gets or sets the scale of the Video Widget")]
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

        [LuaDocsDescription("Imports a video file from the user's MediaLibrary/Videos folder")]
        public static VideoApiWrapper Import(string location) => new (ApiMethods.ImportVideo(location));

        [LuaDocsDescription("Adds this Video Widget to the current selection")]
        public void Select() => ApiMethods.SelectWidget(_VideoWidget);

        [LuaDocsDescription("Deletes this Video Widget")]
        public void Delete() => ApiMethods.DeleteWidget(_VideoWidget);
    }
}
