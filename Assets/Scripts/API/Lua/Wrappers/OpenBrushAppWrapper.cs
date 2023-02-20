using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [MoonSharpUserData]
    public static class BrushApiWrapper
    {
        public static float TimeSincePressed => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameActive;
        public static float TimeSinceReleased => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameInactive;
        public static bool TriggerIsPressed => SketchSurfacePanel.m_Instance.ActiveTool.IsActive;
        public static bool TriggerIsPressedThisFrame => SketchSurfacePanel.m_Instance.ActiveTool.IsActiveThisFrame;
        public static float DistanceMoved => SketchSurfacePanel.m_Instance.ActiveTool.DistanceMoved_CS;
        public static float DistanceDrawn => SketchSurfacePanel.m_Instance.ActiveTool.DistanceDrawn_CS;
        public static Vector3 Position => LuaManager.Instance.GetPastBrushPos(0);
        public static Quaternion Rotation => LuaManager.Instance.GetPastBrushRot(0);
        public static Vector3 Direction => LuaManager.Instance.GetPastBrushRot(0) * Vector3.forward;
        public static float Size => PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
        public static float Size01 => PointerManager.m_Instance.MainPointer.BrushSize01;
        public static float Pressure => PointerManager.m_Instance.MainPointer.GetPressure();
        public static string Name => PointerManager.m_Instance.MainPointer.CurrentBrush?.m_Description;
        public static float Speed => PointerManager.m_Instance.MainPointer.MovementSpeed;
        public static Color Color => PointerManager.m_Instance.PointerColor;
        public static Color LastColorPicked => PointerManager.m_Instance.m_lastChosenColor;
        public static Vector3 PastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);
        public static Quaternion PastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);
        public static void Type(string brushType) => ApiMethods.Brush(brushType);
        public static void SizeSet(float size) => ApiMethods.BrushSizeSet(size);
        public static void SizeAdd(float amount) => ApiMethods.BrushSizeAdd(amount);
        public static void ForcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);
        public static void ForcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);

        public static Vector3 ColorHsv
        {
            get
            {
                float h, s, v;
                Color.RGBToHSV(Color, out h, out s, out v);
                return new Vector3(h, s, v);
            }
        }

        public static Vector3 LastColorPickedHsv
        {
            get
            {
                float h, s, v;
                Color.RGBToHSV(LastColorPicked, out h, out s, out v);
                return new Vector3(h, s, v);
            }
        }
    }

    [MoonSharpUserData]
    public static class WandApiWrapper
    {
        public static Vector3 Position => LuaManager.Instance.GetPastWandPos(0);
        public static Quaternion Rotation => LuaManager.Instance.GetPastWandRot(0);
        public static Vector3 Direction => LuaManager.Instance.GetPastWandRot(0) * Vector3.forward;
        public static float Pressure => InputManager.Wand.GetTriggerValue();
        public static Vector3 Speed => InputManager.Wand.m_Velocity;
        public static Vector3 PastPosition(int back) => LuaManager.Instance.GetPastWandPos(back);
        public static Quaternion PastRotation(int back) => LuaManager.Instance.GetPastWandRot(back);
    }

    [MoonSharpUserData]
    public static class AppApiWrapper
    {
        public static float Time => UnityEngine.Time.realtimeSinceStartup;
        public static float Frames => UnityEngine.Time.frameCount;
        public static List<TrTransform> LastSelectedStroke => SelectionManager.m_Instance.LastSelectedStrokeCP;
        public static List<TrTransform> LastStroke => SelectionManager.m_Instance.LastStrokeCP;
    }

    [MoonSharpUserData]
    public static class CanvasApiWrapper
    {
        public static float Scale => App.ActiveCanvas.Pose.scale;
        public static float StrokeCount => SketchMemoryScript.m_Instance.StrokeCount;
    }
}
