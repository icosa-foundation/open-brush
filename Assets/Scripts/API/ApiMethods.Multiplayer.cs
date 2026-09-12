using OpenBrush.Multiplayer;
using System;
using UnityEngine;
namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint(
            "multiplayer.photondefaultports",
            "Selects whether future Photon connections use Photon's default cloud ports. This changes the runtime setting without updating Open Brush.cfg.",
            "true")]
        public static bool MultiplayerPhotonDefaultPorts(bool enabled)
        {
            App.UserConfig.Flags.UseDefaultPhotonCloudPorts = enabled;
            Debug.Log(
                $"[MultiplayerPhotonDefaultPortsApi] UseDefaultPhotonCloudPorts set to {enabled}.");
            return App.UserConfig.Flags.UseDefaultPhotonCloudPorts;
        }

        [ApiEndpoint("multiplayer.join", "Joins a multiplayer room, creating it if it does not exist")]
        public static async void MultiplayerJoin(
            string nickname, string roomName, bool isPrivate, int maxPlayers,
            bool silentRoom, bool viewOnlyRoom, bool liveStrokeStreaming = false)
        {
            var manager = MultiplayerManager.m_Instance;
            ConnectionUserInfo userInfo = new ConnectionUserInfo
            {
                Nickname = nickname,
                UserId = manager.UserInfo.UserId,
                Role = manager.UserInfo.Role
            };
            manager.UserInfo = userInfo;

            RoomCreateData roomData = new RoomCreateData
            {
                roomName = roomName,
                @private = isPrivate,
                maxPlayers = maxPlayers,
                silentRoom = silentRoom,
                viewOnlyRoom = viewOnlyRoom,
                liveStrokeStreaming = liveStrokeStreaming
            };
            Debug.Log($"[MultiplayerHttpAsync] Joining or creating room '{roomName}'.");
            try
            {
                if (await manager.JoinRoom(roomData))
                {
                    Debug.Log($"[MultiplayerHttpAsync] Joined room '{roomName}'.");
                }
                else
                {
                    Debug.LogError(
                        $"[MultiplayerHttpAsync] Failed to join or create room '{roomName}': {manager.LastError}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MultiplayerHttpAsync] Unexpected error while joining room '{roomName}': {exception}");
            }
        }

        [ApiEndpoint("multiplayer.leave", "Leaves a multiplayer room")]
        public static async void MultiplayerLeave()
        {
            Debug.Log("[MultiplayerHttpAsync] Leaving multiplayer room.");
            try
            {
                if (!await MultiplayerManager.m_Instance.LeaveRoom())
                {
                    Debug.LogError("[MultiplayerHttpAsync] Failed to leave multiplayer room.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MultiplayerHttpAsync] Unexpected error while leaving the room: {exception}");
            }
        }

        [ApiEndpoint(
            "multiplayer.voice",
            "Enables or disables multiplayer voice. Disabling disconnects the voice client to stop both incoming and outgoing voice bandwidth.",
            "false")]
        public static async void MultiplayerVoice(bool enabled)
        {
            try
            {
                if (!await MultiplayerManager.m_Instance.SetVoiceEnabled(enabled))
                {
                    Debug.LogError(
                        $"[MultiplayerHttpAsync] Failed to set multiplayer voice enabled state to {enabled}.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MultiplayerHttpAsync] Unexpected error while setting local voice to {enabled}: {exception}");
            }
        }

        [ApiEndpoint(
            "multiplayer.voiceall",
            "Allows or disables multiplayer voice for the whole room. Only the room owner can change this setting, and disabling it disconnects every participant's voice client.",
            "false")]
        public static async void MultiplayerVoiceAll(bool enabled)
        {
            try
            {
                if (!await MultiplayerManager.m_Instance.SetRoomVoiceEnabled(enabled))
                {
                    Debug.LogError(
                        $"[MultiplayerHttpAsync] Failed to set room voice enabled state to {enabled}. Only the room owner can change it.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MultiplayerHttpAsync] Unexpected error while setting room voice to {enabled}: {exception}");
            }
        }

        [ApiEndpoint(
            "multiplayer.livestrokes",
            "Enables or disables live multiplayer stroke previews for the room. Only the room owner can change this setting. Completed-stroke delivery remains the fallback.",
            "true")]
        public static async void MultiplayerLiveStrokes(bool enabled)
        {
            try
            {
                if (!await MultiplayerManager.m_Instance.SetLiveStrokeStreamingEnabled(enabled))
                {
                    Debug.LogError(
                        $"[MultiplayerHttpAsync] Failed to set room live stroke state to {enabled}. Only the room owner can change it.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[MultiplayerHttpAsync] Unexpected error while setting live strokes to {enabled}: {exception}");
            }
        }

        [ApiEndpoint(
            "multiplayer.muteplayer",
            "Mutes or unmutes one remote player on this client only.",
            "0,true")]
        public static bool MultiplayerMutePlayer(int playerId, bool muted)
        {
            if (!TryGetRemotePlayer(playerId, false, out var manager))
            {
                return false;
            }

            manager.MutePlayerForMe(muted, playerId);
            return true;
        }

        [ApiEndpoint(
            "multiplayer.hideallforme",
            "Hides or shows all remote player avatars on this client's player and spectator cameras.",
            "true")]
        public static bool MultiplayerHideAllForMe(bool hidden)
        {
            var manager = MultiplayerManager.m_Instance;
            if (manager == null)
            {
                Debug.LogWarning(
                    "[MultiplayerAvatarVisibility] HTTP command ignored because the multiplayer manager is not initialized.");
                return false;
            }

            return manager.SetPlayerAvatarsHiddenForMe(hidden);
        }

        [ApiEndpoint(
            "multiplayer.hideplayer",
            "Hides or shows one remote player's avatar on this client's player and spectator cameras.",
            "0,true")]
        public static bool MultiplayerHidePlayer(int playerId, bool hidden)
        {
            if (!TryGetRemotePlayer(playerId, false, out var manager))
            {
                return false;
            }

            return manager.SetPlayerAvatarHiddenForMe(hidden, playerId);
        }

        [ApiEndpoint(
            "multiplayer.muteplayerall",
            "Mutes or unmutes one remote player for the whole room. Only the room owner can use this command.",
            "0,true")]
        public static bool MultiplayerMutePlayerForAll(int playerId, bool muted)
        {
            if (!TryGetRemotePlayer(playerId, true, out var manager))
            {
                return false;
            }

            manager.MutePlayerForAll(muted, playerId);
            return true;
        }

        [ApiEndpoint(
            "multiplayer.viewonlyplayer",
            "Enables or disables view-only mode for one remote player. Only the room owner can use this command.",
            "0,true")]
        public static bool MultiplayerViewOnlyPlayer(int playerId, bool viewOnly)
        {
            if (!TryGetRemotePlayer(playerId, true, out var manager))
            {
                return false;
            }

            manager.SetUserViewOnlyMode(viewOnly, playerId);
            return true;
        }

        [ApiEndpoint(
            "multiplayer.kickplayer",
            "Removes one remote player from the room. Only the room owner can use this command.",
            "0")]
        public static bool MultiplayerKickPlayer(int playerId)
        {
            if (!TryGetRemotePlayer(playerId, true, out var manager))
            {
                return false;
            }

            manager.KickPlayerOut(playerId);
            return true;
        }

        [ApiEndpoint(
            "multiplayer.transferowner",
            "Transfers room ownership to one remote player. Only the current room owner can use this command.",
            "0")]
        public static bool MultiplayerTransferOwner(int playerId)
        {
            if (!TryGetRemotePlayer(playerId, true, out var manager))
            {
                return false;
            }

            manager.RoomOwnershipTransferToUser(playerId);
            return true;
        }

        private static bool TryGetRemotePlayer(
            int playerId, bool requireOwner, out MultiplayerManager manager)
        {
            manager = MultiplayerManager.m_Instance;
            if (manager == null || manager.State != ConnectionState.IN_ROOM)
            {
                Debug.LogWarning(
                    "[MultiplayerHttpPlayers] Player command ignored because this client is not in a room.");
                return false;
            }

            if (requireOwner && !manager.IsUserRoomOwner())
            {
                Debug.LogWarning(
                    "[MultiplayerHttpPlayers] Owner-only player command ignored because this client is not the room owner.");
                return false;
            }

            if (manager.m_RemotePlayers == null || manager.GetPlayerById(playerId) == null)
            {
                Debug.LogWarning(
                    $"[MultiplayerHttpPlayers] Player command ignored because remote player {playerId} was not found.");
                return false;
            }

            return true;
        }

    }
}
