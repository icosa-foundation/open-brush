using OpenBrush.Multiplayer;
using UnityEngine;
namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("multiplayer.join", "Joins a multiplayer room, creating it if it does not exist")]
        public static void MultiplayerJoin(
            string nickname, string roomName, bool isPrivate, int maxPlayers,
            bool silentRoom, bool viewOnlyRoom, bool liveStrokeStreaming = false)
        {
            ConnectionUserInfo userInfo = new ConnectionUserInfo
            {
                Nickname = nickname,
                UserId = MultiplayerManager.m_Instance.UserInfo.UserId,
                Role = MultiplayerManager.m_Instance.UserInfo.Role
            };
            MultiplayerManager.m_Instance.UserInfo = userInfo;

            RoomCreateData roomData = new RoomCreateData
            {
                roomName = roomName,
                @private = isPrivate,
                maxPlayers = maxPlayers,
                silentRoom = silentRoom,
                viewOnlyRoom = viewOnlyRoom,
                liveStrokeStreaming = liveStrokeStreaming,
                liveStrokeProtocolVersion = liveStrokeStreaming
                    ? MultiplayerManager.LiveStrokeProtocolVersion
                    : 0
            };
            var joinRoomTask = MultiplayerManager.m_Instance.JoinRoom(roomData);
            AsyncHelpers.RunSync(() => joinRoomTask);
            if (joinRoomTask.Result)
            {
                // TODO - we do this when using the non-VR UI
                // Should we also do it here?
                // var cameraPos = App.VrSdk.GetVrCamera().transform.position;
                // cameraPos.y += 12;
                // App.VrSdk.GetVrCamera().transform.position = cameraPos;
            }
            else
            {
                Debug.LogError(
                    $"[MultiplayerHttpJoin] Failed to join or create room '{roomName}': {MultiplayerManager.m_Instance.LastError}");
            }
        }

        [ApiEndpoint("multiplayer.leave", "Leaves a multiplayer room")]
        public static void MultiplayerLeave()
        {
            var leaveRoomTask = MultiplayerManager.m_Instance.LeaveRoom();
            AsyncHelpers.RunSync(() => leaveRoomTask);
            if (!leaveRoomTask.Result)
            {
                Debug.LogError("Failed to leave room");
            }
        }

        [ApiEndpoint(
            "multiplayer.voice",
            "Enables or disables multiplayer voice. Disabling disconnects the voice client to stop both incoming and outgoing voice bandwidth.",
            "false")]
        public static void MultiplayerVoice(bool enabled)
        {
            var voiceTask = MultiplayerManager.m_Instance.SetVoiceEnabled(enabled);
            AsyncHelpers.RunSync(() => voiceTask);
            if (!voiceTask.Result)
            {
                Debug.LogError(
                    $"[MultiplayerVoiceApi] Failed to set multiplayer voice enabled state to {enabled}.");
            }
        }

        [ApiEndpoint(
            "multiplayer.voiceall",
            "Allows or disables multiplayer voice for the whole room. Only the room owner can change this setting, and disabling it disconnects every participant's voice client.",
            "false")]
        public static void MultiplayerVoiceAll(bool enabled)
        {
            var voiceTask = MultiplayerManager.m_Instance.SetRoomVoiceEnabled(enabled);
            AsyncHelpers.RunSync(() => voiceTask);
            if (!voiceTask.Result)
            {
                Debug.LogError(
                    $"[MultiplayerVoiceAll] Failed to set room voice enabled state to {enabled}. Only the room owner can change it.");
            }
        }

        [ApiEndpoint(
            "multiplayer.livestrokes",
            "Enables or disables live multiplayer stroke previews for the room. Only the room owner can change this setting. Completed-stroke delivery remains the fallback.",
            "true")]
        public static void MultiplayerLiveStrokes(bool enabled)
        {
            var settingTask = MultiplayerManager.m_Instance
                .SetLiveStrokeStreamingEnabled(enabled);
            AsyncHelpers.RunSync(() => settingTask);
            if (!settingTask.Result)
            {
                Debug.LogError(
                    $"[LiveStrokeStreaming] Failed to set room live stroke state to {enabled}. Only the room owner can change it.");
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
