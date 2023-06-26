using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The spectator camera")]
    [MoonSharpUserData]
    public static class SpectatorApiWrapper
    {
        public static void Turn(float angle) => ApiMethods.SpectatorYaw(angle);
        public static void TurnX(float angle) => ApiMethods.SpectatorPitch(angle);
        public static void TurnZ(float angle) => ApiMethods.SpectatorRoll(angle);
        public static void Direction(Vector3 direction) => ApiMethods.SpectatorDirection(direction);
        public static void LookAt(Vector3 position) => ApiMethods.SpectatorLookAt(position);
        public static void Mode(string mode) => ApiMethods.SpectatorMode(mode);
        public static void Show(string type) => ApiMethods.SpectatorShow(type);
        public static void Hide(string type) => ApiMethods.SpectatorHide(type);
        public static void Toggle() => ApiMethods.ToggleSpectator();
        public static void On() => ApiMethods.EnableSpectator();
        public static void Off() => ApiMethods.DisableSpectator();
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

        public static void LockToScene(bool locked)
        {
            var tr = SketchControlsScript.m_Instance.GetDropCampWidget().transform;
            if (locked)
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
