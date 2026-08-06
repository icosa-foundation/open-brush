using OpenBrush.Multiplayer;
using UnityEngine;
namespace TiltBrush
{
    public static partial class ApiMethods
    {
        [ApiEndpoint("multiplayer.join", "Joins a multiplayer room")]
        public static void MultiplayerJoin(string nickname, string roomName, bool isPrivate, int maxPlayers, bool silentRoom, bool viewOnlyRoom)
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
                viewOnlyRoom = viewOnlyRoom
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
                Debug.LogError("Failed to join room");
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

    }
}
