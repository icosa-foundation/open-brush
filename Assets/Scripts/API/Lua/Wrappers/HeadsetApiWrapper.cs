using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The user's headset")]
    [MoonSharpUserData]
    public static class HeadsetApiWrapper
    {
        [LuaDocsDescription("Clears the history and sets it's size")]
        public static void ResizeHistory(int size) => LuaManager.Instance.ResizeHeadBuffer(size);

        [LuaDocsDescription("Sets the size of the history. Only clears it if the size has changed")]
        public static void SetHistorySize(int size) => LuaManager.Instance.SetHeadBufferSize(size);

        [LuaDocsDescription("Recalls previous positions of the Headset from the history buffer")]
        public static Vector3 PastPosition(int count) => LuaManager.Instance.GetPastHeadPos(count);

        [LuaDocsDescription("Recalls previous orientations of the Headset from the history buffer")]
        public static Quaternion PastRotation(int count) => LuaManager.Instance.GetPastHeadRot(count);
    }
}
