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


        if (GUILayout.Button("Connect to Room"))
        {
            ConnectToRoom();
        }

        // Draw default inspector below
        DrawDefaultInspector();
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

            bool success = await multiplayerManager.Connect(roomData);
            if (success)
            {
                Debug.Log($"Successfully connected to room: {roomName}");
            }
            else
            {
                Debug.LogError($"Failed to connect to room: {roomName}");
            }
        }
    }
}
#endif
