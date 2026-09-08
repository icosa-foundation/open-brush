using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The spectator camera")]
    [MoonSharpUserData]
    public static class SpectatorApiWrapper
    {
        [LuaDocsDescription("Changes the rotation of the spectator camera to look towards a specific point")]
        [LuaDocsExample("Spectator:LookAt(5, -4, 10)")]
        [LuaDocsParameter("position", "The point in the scene to look towards")]
        public static void LookAt(Vector3 position) => ApiMethods.SpectatorLookAt(position);

        [LuaDocsDescription("Sets the spectator camera's movement mode to stationary")]
        [LuaDocsExample("Spectator:Stationary()")]
        public static void Stationary()
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.SetMode(DropCamWidget.Mode.Stationary);
        }

        [LuaDocsDescription("Sets the spectator camera's movement mode to slowFollow")]
        [LuaDocsExample("Spectator:SlowFollow()")]
        public static void SlowFollow()
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.SetMode(DropCamWidget.Mode.SlowFollow);
        }

        [LuaDocsDescription("Sets the spectator camera's movement mode to wobble")]
        [LuaDocsExample("Spectator:Wobble()")]
        public static void Wobble()
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.SetMode(DropCamWidget.Mode.Wobble);
        }

        [LuaDocsDescription("Sets the spectator camera's movement mode to circular")]
        [LuaDocsExample("Spectator:Circular()")]
        public static void Circular()
        {
            var cam = SketchControlsScript.m_Instance.GetDropCampWidget();
            cam.SetMode(DropCamWidget.Mode.Circular);
        }

        [LuaDocsDescription("Sets whether Widgets are visible to the spectator camera")]
        public static bool canSeeWidgets
        {
            get => ApiMethods._GetSpectatorLayerState("widgets");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("widgets", value);
        }

        [LuaDocsDescription("Sets whether Strokes are visible to the spectator camera")]
        public static bool canSeeStrokes
        {
            get => ApiMethods._GetSpectatorLayerState("strokes");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("strokes", value);
        }

        [LuaDocsDescription("Sets whether Selection are visible to the spectator camera")]
        public static bool canSeeSelection
        {
            get => ApiMethods._GetSpectatorLayerState("selection");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("selection", value);
        }

        [LuaDocsDescription("Sets whether Headset are visible to the spectator camera")]
        public static bool canSeeHeadset
        {
            get => ApiMethods._GetSpectatorLayerState("headset");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("headset", value);
        }

        [LuaDocsDescription("Sets whether Panels are visible to the spectator camera")]
        public static bool canSeePanels
        {
            get => ApiMethods._GetSpectatorLayerState("panels");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("panels", value);
        }

        [LuaDocsDescription("Sets whether Ui are visible to the spectator camera")]
        public static bool canSeeUi
        {
            get => ApiMethods._GetSpectatorLayerState("ui");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("ui", value);
        }

        [LuaDocsDescription("Sets whether Usertools are visible to the spectator camera")]
        public static bool canSeeUsertools
        {
            get => ApiMethods._GetSpectatorLayerState("usertools");
            set => ApiMethods._SpectatorShowHideFromFriendlyName("usertools", value);
        }

        [LuaDocsDescription("Is the spectator camera currently active?")]
        public static bool active
        {
            get => SketchControlsScript.m_Instance.GetDropCampWidget().isVisible;
            set
            {
                if (value) ApiMethods.EnableSpectator();
                else ApiMethods.DisableSpectator();
            }
        }

        [LuaDocsDescription("The 3D position of the Spectator Camera Widget")]
        public static Vector3 position
        {
            get => SketchControlsScript.m_Instance.GetDropCampWidget().transform.position;
            set => SketchControlsScript.m_Instance.GetDropCampWidget().transform.position = value;
        }

        [LuaDocsDescription("The 3D orientation of the Spectator Camera")]
        public static Quaternion rotation
        {
            get => SketchControlsScript.m_Instance.GetDropCampWidget().transform.rotation;
            set => SketchControlsScript.m_Instance.GetDropCampWidget().transform.rotation = value;
        }

        [LuaDocsDescription("Sets whether the spectator camera moves with the scene or with the user")]
        public static bool lockedToScene
        {
            get
            {
                var tr = SketchControlsScript.m_Instance.GetDropCampWidget().transform;
                return tr.parent == App.Scene.transform;
            }
            set
            {
                var tr = SketchControlsScript.m_Instance.GetDropCampWidget().transform;
                if (value)
                {
                    tr.SetParent(App.Scene.transform, true);
                }
                else
                {
                    tr.SetParent(SketchControlsScript.m_Instance.transform, true);
                }
            }
        }
    }
}
