using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("Represents the user's wand (the controller that isn't the brush controller)")]
    [MoonSharpUserData]
    public static class WandApiWrapper
    {
        [LuaDocsDescription("The 3D position of the Wand")]
        public static Vector3 position => LuaManager.Instance.GetPastWandPos(0);

        [LuaDocsDescription("The 3D orientation of the Wand")]
        public static Quaternion rotation => LuaManager.Instance.GetPastWandRot(0);

        public static Vector3 direction => LuaManager.Instance.GetPastWandRot(0) * Vector3.forward;
        public static float pressure => InputManager.Wand.GetTriggerValue();
        public static Vector3 speed => InputManager.Wand.m_Velocity;


        [LuaDocsDescription("Clears the history and sets it's size")]
        public static void ResizeHistory(int size) => LuaManager.Instance.ResizeWandBuffer(size);

        [LuaDocsDescription("Sets the size of the history. Only clears it if the size has changed")]
        public static void SetHistorySize(int size) => LuaManager.Instance.SetWandBufferSize(size);

        [LuaDocsDescription("Recalls previous positions of the Wand from the history buffer")]
        public static Vector3 PastPosition(int back) => LuaManager.Instance.GetPastWandPos(back);

        [LuaDocsDescription("Recalls previous orientations of the Wand from the history buffer")]
        public static Quaternion PastRotation(int back) => LuaManager.Instance.GetPastWandRot(back);
    }
}
