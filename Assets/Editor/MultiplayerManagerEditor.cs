// MultiplayerManagerInspector.cs
using UnityEditor;
using UnityEngine;
using OpenBrush.Multiplayer;
using System.Threading.Tasks;

#if UNITY_EDITOR
[CustomEditor(typeof(MultiplayerManager))]
public class MultiplayerManagerInspector : Editor
{
    private MultiplayerManager multiplayerManager;
    private string roomName = "1234";
    private bool isPrivate = false;
    private int maxPlayers = 4;
    private bool voiceDisabled = false;

    public override void OnInspectorGUI()
    {
        // Get the target object (MultiplayerManager)
        multiplayerManager = (MultiplayerManager)target;

        GUILayout.Label("Multiplayer Manager Controls", EditorStyles.boldLabel);

        // Room data input fields
        roomName = EditorGUILayout.TextField("Room Name", roomName);

        // Button to join the lobby
        if (GUILayout.Button("Join Lobby") && multiplayerManager.State == ConnectionState.INITIALIZED)
        {
            ConnectToLobby();
            EditorUtility.SetDirty(target); // Mark the object as dirty to recognize state changes
        }

        // Button to join the room
        if (GUILayout.Button("Join Room") && multiplayerManager.State == ConnectionState.IN_LOBBY)
        {
            ConnectToRoom();
            EditorUtility.SetDirty(target); // Update inspector on state change
        }

        // Button to exit the room
        if (GUILayout.Button("Exit Room") && multiplayerManager.State == ConnectionState.IN_ROOM)
        {
            DisconnectFromRoom();
            EditorUtility.SetDirty(target); // Update inspector on state change
        }

        // Force the inspector to repaint to reflect the latest state
        Repaint();

        // Draw default inspector below
        DrawDefaultInspector();
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

    private async void DisconnectFromRoom()
    {
        if (multiplayerManager != null)
        {
            bool success = await multiplayerManager.LeaveRoom();
          
        }
    }
}
#endif
