using MoonSharp.Interpreter;
using OpenBrush.Multiplayer;

namespace TiltBrush
{
    [LuaDocsDescription("Controls multiplayer features local to this player")]
    [MoonSharpUserData]
    public static class MultiplayerApiWrapper
    {
        [LuaDocsDescription("Whether remote player avatars are hidden from this player's player and spectator cameras")]
        public static bool hideAllForMe
        {
            get => MultiplayerManager.m_Instance != null &&
                   MultiplayerManager.m_Instance.ArePlayerAvatarsHiddenForMe;
            set => MultiplayerManager.m_Instance?.SetPlayerAvatarsHiddenForMe(value);
        }
    }
}
