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

        [LuaDocsDescription("Gets the index of this Video")]
        public int index => WidgetManager.m_Instance.GetActiveWidgetIndex(_VideoWidget);

        public override string ToString()
        {
            return $"CameraPath({_VideoWidget})";
        }

        [LuaDocsDescription("The layer the video is on")]
        public LayerApiWrapper layer
        {
            get => _VideoWidget != null ? new LayerApiWrapper(_VideoWidget.Canvas) : null;
            set => _VideoWidget.SetCanvas(value._CanvasScript);
        }

        [LuaDocsDescription("The group this video is part of")]
        public GroupApiWrapper group
        {
            get => _VideoWidget != null ? new GroupApiWrapper(_VideoWidget.Group, layer._CanvasScript) : null;
            set => _VideoWidget.Group = value._Group;
        }

        [LuaDocsDescription("The Transform (position, rotation, scale) of the Video Widget")]
        public TrTransform transform
        {
            get => App.Scene.MainCanvas.AsCanvas[_VideoWidget.transform];
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
            set => transform = TrTransform.TRS(value, transform.rotation, transform.scale);
        }

        [LuaDocsDescription("The 3D orientation of the Video Widget")]
        public Quaternion rotation
        {
            get => transform.rotation;
            set => transform = TrTransform.TRS(transform.translation, value, transform.scale);
        }

        [LuaDocsDescription("The scale of the Video Widget")]
        public float scale
        {
            get => transform.scale;
            set => transform = TrTransform.TRS(transform.translation, transform.rotation, value);
        }

        [LuaDocsDescription("Imports a video file from the user's MediaLibrary/Videos folder")]
        [LuaDocsExample(@"myVideo = Video.Import(""myVideo.mp4"")")]
        [LuaDocsParameter("location", "The filename of the video file to import from the user's MediaLibrary/Videos folder")]
        public static VideoApiWrapper Import(string location) => new(ApiMethods.ImportVideo(location));

        [LuaDocsDescription("Adds this Video Widget to the current selection")]
        [LuaDocsExample("myVideo:Select()")]
        public void Select() => ApiMethods.SelectWidget(_VideoWidget);

        [LuaDocsDescription("Removes this Video Widget from the current selection")]
        [LuaDocsExample("myVideo:Deselect()")]
        public void Deselect() => ApiMethods.DeselectWidget(_VideoWidget);

        [LuaDocsDescription("Deletes this Video Widget")]
        [LuaDocsExample("myVideo:Delete()")]
        public void Delete() => ApiMethods.DeleteWidget(_VideoWidget);
    }
}
