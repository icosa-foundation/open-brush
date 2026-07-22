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

        [ApiEndpoint("colocation.host", "Hosts a Meta Quest colocation session")]
        public static void HostColocation()
        {
            Debug.Log("[Colocation] Host command invoked.");
            if (!ColocationBootstrap.TryStart(true))
            {
                Debug.LogWarning("[Colocation] Host command rejected because colocation is unsupported or has not finished initializing.");
            }
        }

        [ApiEndpoint("colocation.join", "Joins a Meta Quest colocation session")]
        public static void JoinColocation()
        {
            Debug.Log("[Colocation] Join command invoked.");
            if (!ColocationBootstrap.TryStart(false))
            {
                Debug.LogWarning("[Colocation] Join command rejected because colocation is unsupported or has not finished initializing.");
            }
        }

    }
}
