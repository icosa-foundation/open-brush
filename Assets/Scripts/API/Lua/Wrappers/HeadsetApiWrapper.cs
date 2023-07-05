using MoonSharp.Interpreter;
using UnityEngine;
namespace TiltBrush
{
    [LuaDocsDescription("The user's headset")]
    [MoonSharpUserData]
    public static class HeadsetApiWrapper
    {
        [LuaDocsDescription("Clears the history and sets it's size")]
        [LuaDocsExample("Headset:ResizeHistory(4)")]
        [LuaDocsParameter("size", "How many frames of position/rotation to remember")]
        public static void ResizeHistory(int size) => LuaManager.Instance.ResizeHeadBuffer(size);

        [LuaDocsDescription("Sets the size of the history. Only clears it if the size has changed")]
        [LuaDocsExample("Headset:SetHistorySize(4)")]
        [LuaDocsParameter("size", "How many frames of position/rotation to remember")]
        public static void SetHistorySize(int size) => LuaManager.Instance.SetHeadBufferSize(size);

        [LuaDocsDescription("Recalls previous positions of the Headset from the history buffer")]
        [LuaDocsExample("Headset:PastPosition(4)")]
        [LuaDocsParameter("back", "How many frames back in the history to look")]
        public static Vector3 PastPosition(int back) => LuaManager.Instance.GetPastHeadPos(back);

        [LuaDocsDescription("Recalls previous orientations of the Headset from the history buffer")]
        [LuaDocsExample("Headset:PastRotation(4)")]
        [LuaDocsParameter("back", "How many frames back in the history to look")]
        public static Quaternion PastRotation(int back) => LuaManager.Instance.GetPastHeadRot(back);
    }
}
