using System;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public class CameraPathListApiWrapper
    {
        [MoonSharpHidden]
        public List<CameraPathWidget> _CameraPaths;

        public CameraPathApiWrapper last => (_CameraPaths == null || _CameraPaths.Count == 0) ? null : new CameraPathApiWrapper(_CameraPaths[^1]);

        public CameraPathListApiWrapper()
        {
            _CameraPaths = new List<CameraPathWidget>();
        }

        public CameraPathListApiWrapper(List<CameraPathWidget> cameraPaths)
        {
            _CameraPaths = cameraPaths;
        }

        public CameraPathApiWrapper this[int index] => new CameraPathApiWrapper(_CameraPaths[index]);
        public int count => _CameraPaths?.Count ?? 0;


        public CameraPathWidget active
        {
            get => WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript;
            set => WidgetManager.m_Instance.SetCurrentCameraPath(value);
        }

        public void ShowAll() => WidgetManager.m_Instance.CameraPathsVisible = true;
        public void HideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;
        public void PreviewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;
    }
}


