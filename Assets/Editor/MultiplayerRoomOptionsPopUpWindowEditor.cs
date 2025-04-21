#if UNITY_EDITOR
using OpenBrush.Multiplayer;
using TiltBrush;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MultiplayerRoomOptionsPopUpWindow))]
public class MultiplayerRoomOptionsPopUpWindowEditor : Editor
{
    RemotePlayers remotePlayers = new RemotePlayers { };

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        MultiplayerRoomOptionsPopUpWindow popupWindow = (MultiplayerRoomOptionsPopUpWindow)target;

        if (GUILayout.Button("Add Player"))
            if (!Application.isPlaying) 
                AddPlayer(popupWindow);

        if (GUILayout.Button("Remove Player"))
           if (!Application.isPlaying) 
                RemovePlayer(popupWindow);

        if (GUILayout.Button("Populate Players list"))
            if (!Application.isPlaying)
                RegeneratePlayerList(popupWindow);

    }

    private void AddPlayer(MultiplayerRoomOptionsPopUpWindow popupWindow)
    {
        if (popupWindow.m_playerGuiPrefab == null)
        {
            Debug.LogWarning("Player GUI Prefab is not assigned!");
            return;
        }

        int newIndex = remotePlayers.List.Count > 0 
            ? remotePlayers.List[remotePlayers.List.Count - 1].PlayerId + 1 
            : 0; 

        remotePlayers.AddPlayer(new RemotePlayer
        {
            PlayerId = newIndex,
            Nickname = $"testPlayer{newIndex}",
        });

    }

    private void RemovePlayer(MultiplayerRoomOptionsPopUpWindow popupWindow)
    {
        if (popupWindow.m_playerGuiPrefab == null)
        {
            Debug.LogWarning("Player GUI Prefab is not assigned!");
            return;
        }

        int RemoveIndex = remotePlayers.List[remotePlayers.List.Count-1].PlayerId;

        remotePlayers.RemovePlayerById(RemoveIndex);
 
    }



    private void RegeneratePlayerList(MultiplayerRoomOptionsPopUpWindow popupWindow)
    {

        if (popupWindow.m_playerGuiPrefab == null)
        {
            Debug.LogWarning("Player GUI Prefab is not assigned!");
            return;
        }

        popupWindow.GeneratePlayerList(remotePlayers.List);
    }
}
#endif
