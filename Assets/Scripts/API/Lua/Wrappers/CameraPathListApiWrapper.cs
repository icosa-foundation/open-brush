using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("Wrapper class for CameraPathListApi")]
    [MoonSharpUserData]
    public class CameraPathListApiWrapper
    {
        [MoonSharpHidden]
        public List<CameraPathWidget> _CameraPaths;

        [LuaDocsDescription("Returns the last CameraPathApiWrapper in the list")]
        public CameraPathApiWrapper last => (_CameraPaths == null || _CameraPaths.Count == 0) ? null : new CameraPathApiWrapper(_CameraPaths[^1]);

        public CameraPathListApiWrapper()
        {
            _CameraPaths = new List<CameraPathWidget>();
        }

        public CameraPathListApiWrapper(List<CameraPathWidget> cameraPaths)
        {
            _CameraPaths = cameraPaths;
        }

        [LuaDocsDescription("Gets the CameraPathApiWrapper at the specified index")]
        public CameraPathApiWrapper this[int index] => new CameraPathApiWrapper(_CameraPaths[index]);

        [LuaDocsDescription("Gets the number of CameraPathWidgets in the list")]
        public int count => _CameraPaths?.Count ?? 0;


        [LuaDocsDescription("Gets or sets the active CameraPathWidget")]
        public CameraPathWidget active
        {
            get => WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript;
            set => WidgetManager.m_Instance.SetCurrentCameraPath(value);
        }

        [LuaDocsDescription("Shows all CameraPaths in the list")]
        public void ShowAll() => WidgetManager.m_Instance.CameraPathsVisible = true;

        [LuaDocsDescription("Hides all CameraPaths in the list")]
        public void HideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;

        [LuaDocsDescription("Previews the active path")]
        [LuaDocsParameter("active", "A boolean value indicating whether to preview the active path or not")]
        public void PreviewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;
    }
}
