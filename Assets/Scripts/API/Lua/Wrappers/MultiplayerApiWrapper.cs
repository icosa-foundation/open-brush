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

        [LuaDocsDescription("Hides or shows one remote player's avatar on this player's player and spectator cameras")]
        [LuaDocsExample("Multiplayer:HidePlayer(2, true)")]
        [LuaDocsParameter("playerId", "The remote player's numeric ID")]
        [LuaDocsParameter("hidden", "True to hide the avatar; false to show it")]
        [LuaDocsReturnValue("Whether the player's visibility was changed")]
        public static bool HidePlayer(int playerId, bool hidden)
        {
            return ApiMethods.MultiplayerHidePlayer(playerId, hidden);
        }
    }
}
