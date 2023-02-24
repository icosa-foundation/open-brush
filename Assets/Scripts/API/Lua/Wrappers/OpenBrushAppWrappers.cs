using System.Collections.Generic;
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
        public static float size => PointerManager.m_Instance.MainPointer.BrushSizeAbsolute;
        public static float size01 => PointerManager.m_Instance.MainPointer.BrushSize01;
        public static float pressure => PointerManager.m_Instance.MainPointer.GetPressure();
        public static string name => PointerManager.m_Instance.MainPointer.CurrentBrush?.m_Description;
        public static void type(string brushName) => ApiMethods.Brush(brushName);
        public static float speed => PointerManager.m_Instance.MainPointer.MovementSpeed;
        public static Color color => PointerManager.m_Instance.PointerColor;
        public static Color lastColorPicked => PointerManager.m_Instance.m_lastChosenColor;
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastBrushPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastBrushRot(back);
        public static void sizeSet(float size) => ApiMethods.BrushSizeSet(size);
        public static void sizeAdd(float amount) => ApiMethods.BrushSizeAdd(amount);
        public static void forcePaintingOn(bool active) => ApiMethods.ForcePaintingOn(active);
        public static void forcePaintingOff(bool active) => ApiMethods.ForcePaintingOff(active);

        public static Vector3 colorHsv
        {
            get
            {
                float h, s, v;
                Color.RGBToHSV(color, out h, out s, out v);
                return new Vector3(h, s, v);
            }
        }

        public static Vector3 lastColorPickedHsv
        {
            get
            {
                float h, s, v;
                Color.RGBToHSV(lastColorPicked, out h, out s, out v);
                return new Vector3(h, s, v);
            }
        }
    }

    [MoonSharpUserData]
    public static class WandApiWrapper
    {
        public static Vector3 position => LuaManager.Instance.GetPastWandPos(0);
        public static Quaternion rotation => LuaManager.Instance.GetPastWandRot(0);
        public static Vector3 direction => LuaManager.Instance.GetPastWandRot(0) * Vector3.forward;
        public static float pressure => InputManager.Wand.GetTriggerValue();
        public static Vector3 speed => InputManager.Wand.m_Velocity;
        public static Vector3 pastPosition(int back) => LuaManager.Instance.GetPastWandPos(back);
        public static Quaternion pastRotation(int back) => LuaManager.Instance.GetPastWandRot(back);
    }

    [MoonSharpUserData]
    public static class WidgetApiWrapper
    {
        public static Vector3 position => PointerManager.m_Instance.SymmetryWidget.position;
        public static Quaternion rotation => PointerManager.m_Instance.SymmetryWidget.rotation;
        public static Vector3 direction => PointerManager.m_Instance.SymmetryWidget.rotation * Vector3.forward;
        public static void spin(Vector3 rot) => PointerManager.m_Instance.SymmetryWidget.GetComponent<SymmetryWidget>().Spin(rot);
    }

    [MoonSharpUserData]
    public static class AppApiWrapper
    {
        public static float time => Time.realtimeSinceStartup;
        public static float frames => Time.frameCount;
        public static List<TrTransform> lastSelectedStroke => SelectionManager.m_Instance.LastSelectedStrokeCP;
        public static List<TrTransform> lastStroke => SelectionManager.m_Instance.LastStrokeCP;

        public static void undo() => ApiMethods.Undo();
        public static void redo() => ApiMethods.Redo();
        public static void addListener(string a) => ApiMethods.AddListener(a);
        public static void resetPanels() => ApiMethods.ResetAllPanels();
        public static void showScriptsFolder() => ApiMethods.OpenUserScriptsFolder();
        public static void showExportFolder() => ApiMethods.OpenExportFolder();
        public static void showSketchesFolder(int a) => ApiMethods.ShowSketchFolder(a);
        public static void straightEdge(bool a) => LuaApiMethods.StraightEdge(a);
        public static void autoOrient(bool a) => LuaApiMethods.AutoOrient(a);
        public static void viewOnly(bool a) => LuaApiMethods.ViewOnly(a);
        public static void autoSimplify(bool a) => LuaApiMethods.AutoSimplify(a);
        public static void disco(bool a) => LuaApiMethods.Disco(a);
        public static void profiling(bool a) => LuaApiMethods.Profiling(a);
        public static void postProcessing(bool a) => LuaApiMethods.PostProcessing(a);

        public static void setEnvironment(string environmentName) => ApiMethods.SetEnvironment(environmentName);

        public static void watermark(bool a) => LuaApiMethods.Watermark(a);
        // TODO Unified API for tools and panels
        // public static void SettingsPanel(bool a) => )LuaApiMethods.SettingsPanel)(a);
        // public static void SketchOrigin(bool a) => )LuaApiMethods.SketchOrigin)(a);
    }

    [MoonSharpUserData]
    public static class CanvasApiWrapper
    {
        public static float scale => App.ActiveCanvas.Pose.scale;
        public static float strokeCount => SketchMemoryScript.m_Instance.StrokeCount;
    }
}
