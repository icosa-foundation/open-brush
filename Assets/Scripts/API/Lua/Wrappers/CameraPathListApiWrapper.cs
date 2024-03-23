using System.Collections.Generic;
using MoonSharp.Interpreter;

namespace TiltBrush
{
    [LuaDocsDescription("The list of Camera Paths in the scene. (You don't instantiate this yourself. Access this via Sketch.cameraPaths)")]
    [MoonSharpUserData]
    public class CameraPathListApiWrapper
    {
        [MoonSharpHidden]
        public List<CameraPathWidget> _CameraPaths;

        [LuaDocsDescription("Returns the last Camera Path")]
        public CameraPathApiWrapper last => (_CameraPaths == null || _CameraPaths.Count == 0) ? null : new CameraPathApiWrapper(_CameraPaths[^1]);

        public CameraPathListApiWrapper()
        {
            _CameraPaths = new List<CameraPathWidget>();
        }

        public CameraPathListApiWrapper(List<CameraPathWidget> cameraPaths)
        {
            _CameraPaths = cameraPaths;
        }

        [LuaDocsDescription("Gets a Camera Path by it's index")]
        public CameraPathApiWrapper this[int index]
        {
            get => new(_CameraPaths[index]);
            set => _CameraPaths[index] = value._CameraPathWidget;
        }

        [LuaDocsDescription("The number of Camera Paths")]
        public int count => _CameraPaths?.Count ?? 0;


        [LuaDocsDescription("The active Camera Path")]
        public CameraPathApiWrapper active
        {
            get => new CameraPathApiWrapper(WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript);
            set => WidgetManager.m_Instance.SetCurrentCameraPath(value._CameraPathWidget);
        }

        [LuaDocsDescription("Makes all Camera Paths visible")]
        [LuaDocsExample("Sketch.cameraPaths:ShowAll()")]
        public void ShowAll() => WidgetManager.m_Instance.CameraPathsVisible = true;

        [LuaDocsDescription("Hides all Camera Paths")]
        [LuaDocsExample("Sketch:cameraPaths:HideAll()")]
        public void HideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;

        [LuaDocsDescription("Sets whether to preview the active path or not")]
        [LuaDocsExample("Sketch.cameraPaths:PreviewActivePath(true)")]
        [LuaDocsParameter("active", "A boolean value indicating whether to preview the active path or not")]
        public void PreviewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;
    }
}
