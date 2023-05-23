using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [MoonSharpUserData]
    public static class WandApiWrapper
    {
        public static Vector3 position => LuaManager.Instance.GetPastWandPos(0);
        public static Quaternion rotation => LuaManager.Instance.GetPastWandRot(0);
        public static Vector3 direction => LuaManager.Instance.GetPastWandRot(0) * Vector3.forward;
        public static float pressure => InputManager.Wand.GetTriggerValue();
        public static Vector3 speed => InputManager.Wand.m_Velocity;
        public static void ResizeBuffer(int size) => LuaManager.Instance.ResizeWandBuffer(size);
        public static void SetBufferSize(int size) => LuaManager.Instance.SetWandBufferSize(size);
        public static Vector3 PastPosition(int back) => LuaManager.Instance.GetPastWandPos(back);
        public static Quaternion PastRotation(int back) => LuaManager.Instance.GetPastWandRot(back);
    }
}
