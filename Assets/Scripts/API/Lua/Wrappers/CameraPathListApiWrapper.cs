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

        public CameraPathApiWrapper last => new CameraPathApiWrapper(_CameraPaths[count - 1]);

        public CameraPathListApiWrapper()
        {
            _CameraPaths = new List<CameraPathWidget>();
        }

        public CameraPathListApiWrapper(List<CameraPathWidget> cameraPaths)
        {
            _CameraPaths = cameraPaths;
        }

        public CameraPathApiWrapper this[int index] => new CameraPathApiWrapper(_CameraPaths[index]);
        public int count => _CameraPaths.Count;


        public CameraPathWidget active
        {
            get => WidgetManager.m_Instance.GetCurrentCameraPath().WidgetScript;
            set => WidgetManager.m_Instance.SetCurrentCameraPath(value);
        }

        public static void ShowAll() => WidgetManager.m_Instance.CameraPathsVisible = true;
        public static void HideAll() => WidgetManager.m_Instance.CameraPathsVisible = false;
        public static void PreviewActivePath(bool active) => WidgetManager.m_Instance.FollowingPath = active;
    }
}


