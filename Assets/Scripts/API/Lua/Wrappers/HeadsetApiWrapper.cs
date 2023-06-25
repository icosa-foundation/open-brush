using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The user's headset")]
    [MoonSharpUserData]
    public static class HeadsetApiWrapper
    {
        public static void ResizeHistory(int size) => LuaManager.Instance.ResizeHeadBuffer(size);
        public static void SetHistorySize(int size) => LuaManager.Instance.SetHeadBufferSize(size);
        public static Vector3 PastPosition(int count) => LuaManager.Instance.GetPastHeadPos(count);
        public static Quaternion PastRotation(int count) => LuaManager.Instance.GetPastHeadRot(count);
    }
}
