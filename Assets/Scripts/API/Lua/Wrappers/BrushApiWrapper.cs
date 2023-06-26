using MoonSharp.Interpreter;
using UnityEngine;

namespace TiltBrush
{
    [LuaDocsDescription("The user's brush")]
    [MoonSharpUserData]
    public static class BrushApiWrapper
    {
        [LuaDocsDescription("Time in seconds since the brush trigger was last pressed")]
        public static float timeSincePressed => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameActive;

        [LuaDocsDescription("Time in seconds since the brush trigger was last released")]
        public static float timeSinceReleased => Time.realtimeSinceStartup - SketchSurfacePanel.m_Instance.ActiveTool.TimeBecameInactive;

        [LuaDocsDescription("Check whether the brush trigger is currently pressed")]
        public static bool triggerIsPressed => SketchSurfacePanel.m_Instance.ActiveTool.IsActive;

        [LuaDocsDescription("Check whether the brush trigger was pressed in the current frame")]
        public static bool triggerIsPressedThisFrame => SketchSurfacePanel.m_Instance.ActiveTool.IsActiveThisFrame;

        [LuaDocsDescription("The distance moved by the brush")]
        public static float distanceMoved => SketchSurfacePanel.m_Instance.ActiveTool.DistanceMoved_CS;

        [LuaDocsDescription("The distance drawn by the brush (i.e. distance since the trigger was last pressed)")]
        public static float distanceDrawn => SketchSurfacePanel.m_Instance.ActiveTool.DistanceDrawn_CS;

        [LuaDocsDescription("The 3D position of the Brush Controller's tip")]
        public static Vector3 position => LuaManager.Instance.GetPastBrushPos(0);

        [LuaDocsDescription("The 3D orientation of the Brush Controller's tip")]
        public static Quaternion rotation => LuaManager.Instance.GetPastBrushRot(0);

        [LuaDocsDescription("The vector representing the forward direction of the brush")]
        public static Vector3 direction => LuaManager.Instance.GetPastBrushRot(0) * Vector3.back;

        [LuaDocsDescription("The current brush size")]
        public static float size
        {
            get => PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
            set => ApiMethods.BrushSizeSet(value);
        }

        [LuaDocsDescription("Brush pressure is determined by how far the trigger is pushed in")]
        public static float pressure => PointerManager.m_Instance.MainPointer.GetPressure();

        [LuaDocsDescription("The current brush type")]
        public static string type
        {
            get => PointerManager.m_Instance.MainPointer.CurrentBrush.Description!;
            set => ApiMethods.Brush(value);
        }

        [LuaDocsDescription("How fast the brush is currently moving")]
        public static float speed => PointerManager.m_Instance.MainPointer.MovementSpeed;

        [LuaDocsDescription("Gets or set brush color")]
        public static Color colorRgb
        {
            get => PointerManager.m_Instance.PointerColor;
            set => App.BrushColor.CurrentColor = value;
        }

        [LuaDocsDescription("Gets or set brush color using a Vector3 representing hue, saturation and brightness")]
        public static Vector3 colorHsv
        {
            get
            {
                Color.RGBToHSV(App.BrushColor.CurrentColor, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
            set => App.BrushColor.CurrentColor = Color.HSVToRGB(value.x, value.y, value.z);
        }

        [LuaDocsDescription("The color of the brush as a valid HTML color string (either hex values or a color name)")]
        public static string colorHtml
        {
            get => ColorUtility.ToHtmlStringRGB(PointerManager.m_Instance.PointerColor);
            set => ApiMethods.SetColorHTML(value);
        }

        [LuaDocsDescription("Applies the current jitter settings to the brush color")]
        public static void JitterColor() => LuaApiMethods.JitterColor();

        [LuaDocsDescription("The last color picked by the brush.")]
        public static Color lastColorPicked => PointerManager.m_Instance.m_lastChosenColor;

        [LuaDocsDescription("The last color picked by the brush in HSV.")]
        public static Vector3 LastColorPickedHsv
        {
            get
            {
                Color.RGBToHSV(lastColorPicked, out float h, out float s, out float v);
                return new Vector3(h, s, v);
            }
        }

        [LuaDocsDescription("Clears the history and sets it's size")]
        [LuaDocsExample("Brush:ResizeHistory(10)")]
        [LuaDocsParameter("size", "How many frames of position/rotation to remember")]
        public static void ResizeHistory(int size) => LuaManager.Instance.ResizeBrushBuffer(size);

        [LuaDocsDescription("Sets the size of the history. Only clears it if the size has changed")]
        [LuaDocsExample("Brush:SetHistorySize(10)")]
        [LuaDocsParameter("size", "How many frames of position/rotation to remember")]
        public static void SetHistorySize(int size) => LuaManager.Instance.SetBrushBufferSize(size);

        [LuaDocsDescription("Recalls previous positions of the Brush from the history buffer")]
        [LuaDocsExample("Brush:GetPastPosition(3)")]
        [LuaDocsParameter("back", "How many frames back in the history to look")]
        public static Vector3 GetPastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);

        [LuaDocsDescription("Recalls previous orientations of the Brush from the history buffer")]
        [LuaDocsExample("Brush:GetPastRotation(3)")]
        [LuaDocsParameter("back", "How many frames back in the history to look")]
        public static Quaternion GetPastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);

        [LuaDocsDescription("If set to true then the brush will draw strokes even if the trigger isn't being pressed.")]
        [LuaDocsExample("Brush:ForcePaintingOn(true)")]
        [LuaDocsParameter("active", "True means forced painting, false is normal behaviour")]
        public static void ForcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);

        [LuaDocsDescription("If set to true then the brush will stop drawing strokes even if the trigger is still pressed.")]
        [LuaDocsExample("Brush:ForcePaintingOff(true)")]
        [LuaDocsParameter("active", "True means painting is forced off, false is normal behaviour")]
        public static void ForcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);

        [LuaDocsDescription("Forces the start of a new stroke - will stop painting this frame and start again the next.")]
        public static void ForceNewStroke() => ApiMethods.ForceNewStroke();

        [LuaDocsDescription("Gets or sets the current path of the brush. Assumes a stroke is in progress.")]
        public static PathApiWrapper currentPath
        {
            get => new (PointerManager.m_Instance.MainPointer.CurrentPath);
            set => PointerManager.m_Instance.MainPointer.CurrentPath = value._Path;
        }

    }
}
