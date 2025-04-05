// MultiplayerManagerInspector.cs
using UnityEditor;
using UnityEngine;
using OpenBrush.Multiplayer;
using System.Threading.Tasks;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using OpenBrush;

#if UNITY_EDITOR
[CustomEditor(typeof(MultiplayerManager))]
public class MultiplayerManagerInspector : Editor
{
    private MultiplayerManager multiplayerManager;
    private string roomName = "1234";
    private string nickname = "PlayerNickname";
    private string oldNickname = "PlayerNickname";
    private bool isPrivate = false;
    private int maxPlayers = 4;
    private bool voiceDisabled = false;
    private Dictionary<int, bool> muteStates = new Dictionary<int, bool>();
    private Dictionary<int, bool> viewOnlyStates = new Dictionary<int, bool>();

    public override void OnInspectorGUI()
    {
        multiplayerManager = (MultiplayerManager)target;

        DrawDefaultInspector();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        GUILayout.Space(10);


        roomName = EditorGUILayout.TextField("Room Name", roomName);
        nickname = EditorGUILayout.TextField("Nickname", nickname);
        if (nickname != oldNickname)
        {
            SetNickname();
            oldNickname = nickname;
            EditorUtility.SetDirty(target);
        }
        maxPlayers = EditorGUILayout.IntField("MaxPlayers", maxPlayers);

        //State
        string connectionState = "";
        if (multiplayerManager != null) connectionState = multiplayerManager.State.ToString();
        else connectionState = "Not Assigned";

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Connection State: ", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{connectionState}");
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(5);

        if (GUILayout.Button("Connect"))
        {
            ConnectToLobby();
            EditorUtility.SetDirty(target);
        }


        if (GUILayout.Button("Join Room"))
        {
            ConnectToRoom();
            EditorUtility.SetDirty(target);
        }


        if (GUILayout.Button("Exit Room"))
        {
            DisconnectFromRoom();
            EditorUtility.SetDirty(target);
        }


        if (GUILayout.Button("Disconnect"))
        {
            Disconnect();
            EditorUtility.SetDirty(target);
        }

        if (GUILayout.Button("Refresh Room List"))
        {
            CheckIfRoomExists();
            EditorUtility.SetDirty(target);
        }

        //Local Player Id
        string localPlayerId = "";
        if (multiplayerManager.m_LocalPlayer != null) localPlayerId = multiplayerManager.m_LocalPlayer.PlayerId.ToString();
        else localPlayerId = "Not Assigned";

        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Local Player ID: ", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{localPlayerId}");
        EditorGUILayout.EndHorizontal();

        //Room Ownership
        string ownership = "";
        if (multiplayerManager != null && multiplayerManager.IsUserRoomOwner()) ownership = "Yes";
        else if (multiplayerManager != null && !multiplayerManager.IsUserRoomOwner()) ownership = "No";
        else ownership = "Not Assigned";

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Is Local Player Room Owner:", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"{ownership}");
        EditorGUILayout.EndHorizontal();

        // Show the remote players
        GUILayout.Space(10);
        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        EditorGUILayout.LabelField("Remote Players", EditorStyles.boldLabel);
        if (multiplayerManager.m_RemotePlayers != null &&
            multiplayerManager.m_RemotePlayers.List.Count > 0)
        {
            // Then when iterating:
            foreach (var remotePlayer in multiplayerManager.m_RemotePlayers.List)
            {
                int playerId = remotePlayer.PlayerId; // now an int

                // Ensure our dictionaries have entries for this playerId
                if (!muteStates.ContainsKey(playerId))
                {
                    muteStates[playerId] = false;
                }
                if (!viewOnlyStates.ContainsKey(playerId))
                {
                    viewOnlyStates[playerId] = false;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Player: {playerId}");

                //mute/unmute
                bool currentMuteState = muteStates[playerId];
                string muteButtonLabel = currentMuteState ? "Unmute" : "Mute";
                if (GUILayout.Button(muteButtonLabel))
                {
                    bool newMuteState = !currentMuteState;
                    multiplayerManager.MutePlayerForAll(newMuteState, playerId);
                    muteStates[playerId] = newMuteState;
                    EditorUtility.SetDirty(target);
                }

                //viewOnly/edit
                bool currentViewState = viewOnlyStates[playerId];
                string viewButtonLabel = currentViewState ? "Disable ViewOnly" : "Enable ViewOnly";
                if (GUILayout.Button(viewButtonLabel))
                {
                    bool newViewState = !currentViewState;
                    multiplayerManager.ToggleUserViewOnlyMode(newViewState, playerId);
                    viewOnlyStates[playerId] = newViewState;
                    EditorUtility.SetDirty(target);
                }

                // ** Kick Out button (only if room owner) **
                if (multiplayerManager.IsUserRoomOwner())
                {
                    if (GUILayout.Button("Kick Out"))
                    {
                        multiplayerManager.KickPlayerOut(playerId);
                        EditorUtility.SetDirty(target);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("No remote players found.");
        }

        Repaint();

    }

    private async void ConnectToLobby()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.Connect();
        }
    }

    private async void ConnectToRoom()
    {
        if (multiplayerManager != null)
        {
            RoomCreateData roomData = new RoomCreateData
            {
                roomName = roomName,
                @private = isPrivate,
                maxPlayers = maxPlayers,
                voiceDisabled = voiceDisabled
            };

            bool success = await multiplayerManager.JoinRoom(roomData);

        }
    }

    private async void SetNickname()
    {
        if (multiplayerManager != null)
        {

            ConnectionUserInfo ui = new ConnectionUserInfo
            {
                Nickname = nickname,
                UserId = MultiplayerManager.m_Instance.UserInfo.UserId,
                Role = MultiplayerManager.m_Instance.UserInfo.Role
            };
            MultiplayerManager.m_Instance.UserInfo = ui;
        }
    }

    private async void DisconnectFromRoom()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.LeaveRoom();

        }
    }

    private async void Disconnect()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.Disconnect();

        }
    }

    private void CheckIfRoomExists()
    {
        if (multiplayerManager != null)
        {
            bool roomExists = multiplayerManager.DoesRoomNameExist(roomName);
        }
    }
}
#endif
