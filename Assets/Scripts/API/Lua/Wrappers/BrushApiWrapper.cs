using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class BrushApiWrapper
    {
        public static float timeSincePressed => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameActive;
        public static float timeSinceReleased => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameInactive;
        public static bool triggerIsPressed => SketchSurfacePanel.m_Instance.ActiveTool.IsActive;
        public static bool triggerIsPressedThisFrame => SketchSurfacePanel.m_Instance.ActiveTool.IsActiveThisFrame;
        public static float distanceMoved => SketchSurfacePanel.m_Instance.ActiveTool.DistanceMoved_CS;
        public static float distanceDrawn => SketchSurfacePanel.m_Instance.ActiveTool.DistanceDrawn_CS;
        public static Vector3 position => LuaManager.Instance.GetPastBrushPos(0);
        public static Quaternion rotation => LuaManager.Instance.GetPastBrushRot(0);
        public static Vector3 direction => LuaManager.Instance.GetPastBrushRot(0) * Vector3.forward;
        public static float size
        {
            get => PointerManager.m_Instance.MainPointer.BrushSize01;
            set => ApiMethods.BrushSizeSet(value);
        }
        public static float pressure => PointerManager.m_Instance.MainPointer.GetPressure();
        public static string type
        {
            get => PointerManager.m_Instance.MainPointer.CurrentBrush.m_Description!;
            set => ApiMethods.Brush(value);
        }
        public static float speed => PointerManager.m_Instance.MainPointer.MovementSpeed;
        public static Color colorRgb
        {
            get => PointerManager.m_Instance.PointerColor;
            set => App.BrushColor.CurrentColor = value;
        }

        public static Vector3 colorHsv
        {
            get
            {
                Color.RGBToHSV(App.BrushColor.CurrentColor, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
            set => App.BrushColor.CurrentColor = Color.HSVToRGB(value.x, value.y, value.z);
        }

        public static string colorHtml
        {
            get => ColorUtility.ToHtmlStringRGB(PointerManager.m_Instance.PointerColor);
            set => ApiMethods.SetColorHTML(value);
        }
        public static void JitterColor() => LuaApiMethods.JitterColor();
        public static Color lastColorPicked => PointerManager.m_Instance.m_lastChosenColor;
        public static void ResizeBuffer(int size) => LuaManager.Instance.ResizeBrushBuffer(size);
        public static void SetBufferSize(int size) => LuaManager.Instance.SetBrushBufferSize(size);
        public static Vector3 GetPastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);
        public static Quaternion GetPastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);
        public static void ForcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);
        public static void ForcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);
        public static void ForceNewStroke() => ApiMethods.ForceNewStroke();

        public static Vector3 LastColorPickedHsv
        {
            get
            {
                Color.RGBToHSV(lastColorPicked, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
        }
    }
}
