using System;
using System.Collections;
using OpenBrush.Multiplayer;
using TiltBrush;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class NonVrMultiplayerUi : MonoBehaviour
{
    public GameObject m_MultiplayerMenuPanel;
    public TMP_InputField m_RoomNameInput;
    public TMP_InputField m_NicknameInput;
    public TMP_InputField m_MaxPlayersInput;
    public Toggle m_PrivateToggle;
    public Toggle m_VoiceDisabledToggle;
    public Button m_JoinRoomButton;
    public Button m_LeaveRoomButton;

    private ViewModeUI m_ViewModeUi;

    void Start()
    {
        m_ViewModeUi = GetComponentInParent<ViewModeUI>();
        m_RoomNameInput.text = PlayerPrefs.GetString("roomname", "");
        m_NicknameInput.text = PlayerPrefs.GetString("nickname", "");
        m_MaxPlayersInput.text = PlayerPrefs.GetString("maxplayers", "4");
    }

    void SetJoinRoomUi()
    {
        // This probably misses some edge cases
        bool canJoin = MultiplayerManager.m_Instance.State != ConnectionState.IN_ROOM;

        m_RoomNameInput.gameObject.SetActive(canJoin);
        m_NicknameInput.gameObject.SetActive(canJoin);
        m_MaxPlayersInput.gameObject.SetActive(canJoin);
        m_PrivateToggle.gameObject.SetActive(canJoin);
        m_VoiceDisabledToggle.gameObject.SetActive(canJoin);
        m_JoinRoomButton.gameObject.SetActive(canJoin);
        m_LeaveRoomButton.gameObject.SetActive(!canJoin);
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

    public void HandleLeaveRoomButton()
    {
        StartCoroutine(HandleJoinRoomButtonCoroutine());
    }

    private IEnumerator HandleJoinRoomButtonCoroutine()
    {
        yield return StartCoroutine(HandleJoinRoomButtonAsync());
    }

    private IEnumerator HandleJoinRoomButtonAsync()
    {
        ConnectionUserInfo userInfo = new ConnectionUserInfo
        {
            Nickname = m_NicknameInput.text,
            UserId = MultiplayerManager.m_Instance.UserInfo.UserId,
            Role = MultiplayerManager.m_Instance.UserInfo.Role
        };
        MultiplayerManager.m_Instance.UserInfo = userInfo;

        RoomCreateData roomData = new RoomCreateData
        {
            roomName = m_RoomNameInput.text,
            @private = m_PrivateToggle.isOn,
            maxPlayers = int.Parse(m_MaxPlayersInput.text),
            voiceDisabled = m_VoiceDisabledToggle.isOn
        };
        PlayerPrefs.SetString("roomname", roomData.roomName);
        PlayerPrefs.SetString("nickname", userInfo.Nickname);
        PlayerPrefs.SetString("maxplayers", roomData.maxPlayers.ToString());

        var joinRoomTask = MultiplayerManager.m_Instance.JoinRoom(roomData);
        yield return new WaitUntil(() => joinRoomTask.IsCompleted);
        if (joinRoomTask.IsFaulted)
        {
            Debug.LogError("Failed to join room");
            yield break;
        }
        OnJoinRoomCompleted(joinRoomTask.Result);
    }

    private IEnumerator HandleLeaveRoomButtonAsync()
    {
        var leaveRoomTask = MultiplayerManager.m_Instance.LeaveRoom();
        yield return new WaitUntil(() => leaveRoomTask.IsCompleted);
        if (leaveRoomTask.IsFaulted)
        {
            Debug.LogError("Failed to leave room");
            yield break;
        }
        SetJoinRoomUi();
    }

    private void OnJoinRoomCompleted(bool success)
    {
        if (success)
        {
            var cameraPos = App.VrSdk.GetVrCamera().transform.position;
            cameraPos.y += 12;
            App.VrSdk.GetVrCamera().transform.position = cameraPos;
            m_MultiplayerMenuPanel.SetActive(false);
            SetJoinRoomUi();
        }
        else
        {
            Debug.LogError("Failed to join room");
        }
    }

    public void HandleMenuButton()
    {
        m_MultiplayerMenuPanel.SetActive(!m_MultiplayerMenuPanel.activeSelf);
        if (m_MultiplayerMenuPanel.activeSelf)
        {
            SetJoinRoomUi();
        }
    }
}
