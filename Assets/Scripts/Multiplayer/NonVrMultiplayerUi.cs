using System;
using System.Collections;
using OpenBrush.Multiplayer;
using TiltBrush;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class NonVrMultiplayerUi : MonoBehaviour
{
    public TMP_InputField m_RoomNameInput;
    public TMP_InputField m_NicknameInput;

    private ViewModeUI m_ViewModeUi;

    void Start()
    {
        m_ViewModeUi = GetComponentInParent<ViewModeUI>();
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            m_ViewModeUi.m_CloseButton.SetActive(!m_ViewModeUi.m_CloseButton.activeSelf);
            m_ViewModeUi.m_MenuButton.SetActive(!m_ViewModeUi.m_MenuButton.activeSelf);
        }
    }

    public void HandleJoinRoomButton()
    {
        StartCoroutine(HandleJoinRoomButtonCoroutine());
    }

    private IEnumerator HandleJoinRoomButtonCoroutine()
    {
        yield return StartCoroutine(HandleJoinRoomButtonAsync());
        OnJoinRoomCompleted(true);
    }

    private IEnumerator HandleJoinRoomButtonAsync()
    {
        SetNickname();
        RoomCreateData roomData = new RoomCreateData
        {
            roomName = m_RoomNameInput.text,
            @private = false,
            maxPlayers = 4,
            voiceDisabled = false
        };
        var joinRoomTask = MultiplayerManager.m_Instance.JoinRoom(roomData);
        yield return new WaitUntil(() => joinRoomTask.IsCompleted);
        if (joinRoomTask.IsFaulted)
        {
            Debug.LogError("Failed to join room");
            yield break;
        }
        OnJoinRoomCompleted(joinRoomTask.Result);
    }

    private void OnJoinRoomCompleted(bool success)
    {
        if (success)
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y += 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            gameObject.SetActive(false);

        }
        else
        {
            Debug.LogError("Failed to join room");
        }
    }

    private void SetNickname()
    {
        ConnectionUserInfo userInfo = new ConnectionUserInfo
        {
            Nickname = m_NicknameInput.text,
            UserId = MultiplayerManager.m_Instance.UserInfo.UserId,
            Role = MultiplayerManager.m_Instance.UserInfo.Role
        };
        MultiplayerManager.m_Instance.UserInfo = userInfo;
    }
}
