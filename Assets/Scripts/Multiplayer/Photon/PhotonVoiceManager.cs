// Copyright 2023 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#if MP_PHOTON

using OpenBrush.Multiplayer;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.Unity;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TiltBrush;
using UnityEngine;

public class PhotonVoiceManager : IVoiceConnectionHandler, IConnectionCallbacks, IMatchmakingCallbacks
{
    public ConnectionUserInfo UserInfo { get; set; }
    public ConnectionState State { get; private set; }
    public string LastError { get; private set; }
    public bool isTransmitting { get; private set; }

    private VoiceConnection m_VoiceConnection;
    private MultiplayerManager m_Manager;
    private AppSettings m_PhotonVoiceAppSettings;
    private Recorder m_Recorder;
    private bool ConnectedToMaster = false;
    private bool wasTransmitting = false;



    public PhotonVoiceManager(MultiplayerManager manager)
    {
        m_Manager = manager;
        Init();
        isTransmitting = false;
    }

    public async Task<bool> Init()
    {

        try
        {
            State = ConnectionState.INITIALIZING;
            m_VoiceConnection = GameObject.FindFirstObjectByType<VoiceConnection>();
            if (m_VoiceConnection == null) throw new Exception("[PhotonVoiceManager] VoiceConnection component not found in scene");
            PhotonNetwork.LogLevel = PunLogLevel.ErrorsOnly;
            m_VoiceConnection.VoiceLogger.LogLevel = Photon.Voice.LogLevel.Error;

            m_VoiceConnection.Settings = new AppSettings
            {
                AppIdVoice = App.Config.PhotonVoiceSecrets.ClientId,
                FixedRegion = "",
            };

            m_VoiceConnection.Client.AddCallbackTarget(this);

        }
        catch (Exception ex)
        {
            State = ConnectionState.ERROR;
            LastError = $"[PhotonVoiceManager] Failed to Initialize lobby: {ex.Message}";
            ControllerConsoleScript.m_Instance.AddNewLine(LastError);
            return false;
        }

        ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Succesfully initialized");
        State = ConnectionState.INITIALIZED;
        return true;

    }

    public async Task<bool> Connect()
    {
        State = ConnectionState.CONNECTING;

        m_VoiceConnection.Client.UserId = m_Manager.UserInfo.UserId;

        if (!m_VoiceConnection.Client.IsConnected)
        {
            //ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Attempting to connect Voice Server...");
            m_VoiceConnection.ConnectUsingSettings();
            while (!ConnectedToMaster)
            {
                //ControllerConsoleScript.m_Instance.AddNewLine("Waiting for Voice Connection to establish...");
                await Task.Delay(100);
            }
        }

        if (ConnectedToMaster)
        {
            State = ConnectionState.IN_LOBBY;
            ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Connection successfully established.");
        }
        else
        {
            State = ConnectionState.ERROR;
            LastError = $"[PhotonVoiceManager] Failed to connect.";
            ControllerConsoleScript.m_Instance.AddNewLine(LastError);
        }

        return ConnectedToMaster;
    }

    public async Task<bool> JoinRoom(RoomCreateData RoomData)
    {
        State = ConnectionState.JOINING_ROOM;

        if (!m_VoiceConnection.Client.IsConnected)
        {
            bool connected = await Connect();
            if (!connected)
            {
                return false;
            }
        }

        var RoomParameters = new EnterRoomParams { RoomName = RoomData.roomName };
        bool roomJoined = m_VoiceConnection.Client.OpJoinOrCreateRoom(RoomParameters);

        if (roomJoined)
        {
            State = ConnectionState.IN_ROOM;
            ControllerConsoleScript.m_Instance.AddNewLine($"[PhotonVoiceManager] Successfully joined room");
        }
        else
        {
            State = ConnectionState.ERROR;
            LastError = $"[PhotonVoiceManager] Failed to join or create room";
            ControllerConsoleScript.m_Instance.AddNewLine(LastError);
        }

        return roomJoined;
    }

    public async Task<bool> LeaveRoom(bool force)
    {
        State = ConnectionState.DISCONNECTING;

        if (!m_VoiceConnection.Client.InRoom) return false;

        bool leftRoom = m_VoiceConnection.Client.OpLeaveRoom(false);

        if (!leftRoom)
        {
            //ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Failed to initiate leaving the room.");
            return false;
        }

        //ControllerConsoleScript.m_Instance.AddNewLine("Initiated leaving the room...");

        while (m_VoiceConnection.ClientState != ClientState.ConnectedToMasterServer)
        {
            await Task.Delay(100);
        }

        if (m_VoiceConnection.ClientState == ClientState.ConnectedToMasterServer)
        {
            State = ConnectionState.DISCONNECTED;
            ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Successfully left the room.");
            return true;
        }
        else
        {
            State = ConnectionState.ERROR;
            ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Failed to leave the room.");
            return false;
        }
    }

    public async Task<bool> Disconnect()
    {

        State = ConnectionState.DISCONNECTING;

        if (!m_VoiceConnection.Client.IsConnected)
        {
            //ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Voice Server is already disconnected.");
            return true;
        }

        m_VoiceConnection.Client.Disconnect();

        while (m_VoiceConnection.ClientState != ClientState.Disconnected)
        {
            await Task.Delay(100);
            //ControllerConsoleScript.m_Instance.AddNewLine("Disconnecting.");
        }

        if (m_VoiceConnection.ClientState == ClientState.Disconnected)
        {
            State = ConnectionState.DISCONNECTED;
            ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Successfully disconnected from Server.");
            return true;
        }
        else
        {
            State = ConnectionState.ERROR;
            ControllerConsoleScript.m_Instance.AddNewLine("[PhotonVoiceManager] Failed to disconnect from Server.");
            return false;
        }
    }

    public bool StartSpeaking()
    {
        m_Recorder = m_VoiceConnection.PrimaryRecorder;
        if (m_Recorder == null)
        {
            ControllerConsoleScript.m_Instance.AddNewLine("Recorder not found! Ensure it's attached to a GameObject.");
            return false;
        }

        // m_Recorder.DebugEchoMode = true;
        m_Recorder.TransmitEnabled = true;
        return true;
    }

    public bool StopSpeaking()
    {
        if (m_Recorder != null)
        {
            m_Recorder.TransmitEnabled = false;
            return true;
        }
        return false;
    }

    public void Update()
    {
        UpdateSpeechDetection();
    }

    private void UpdateSpeechDetection()
    {
        if (m_Recorder == null)
        {
           return;
        }

        isTransmitting = m_Recorder.IsCurrentlyTransmitting;

        if (isTransmitting && !wasTransmitting)
        {;
            Debug.Log("[PhotonVoiceManager] Speech started.");
        }
        else if (!isTransmitting && wasTransmitting)
        {
            Debug.Log("[PhotonVoiceManager] Speech stopped.");
        }

        wasTransmitting = isTransmitting;
    }


    #region MatchmakingCallbacks

    public void OnCreatedRoom()
    {

    }

    public void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogErrorFormat("OnCreateRoomFailed errorCode={0} errorMessage={1}", returnCode, message);
    }

    public void OnFriendListUpdate(List<FriendInfo> friendList)
    {

    }

    public void OnJoinedRoom()
    {
    }

    public void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.LogErrorFormat("OnJoinRandomFailed errorCode={0} errorMessage={1}", returnCode, message);
    }

    public void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogErrorFormat("OnJoinRoomFailed  errorCode={1} errorMessage={2}", returnCode, message);
    }

    public void OnLeftRoom()
    {

    }

    #endregion

    #region ConnectionCallbacks

    public void OnConnected()
    {

    }

    public void OnConnectedToMaster()
    {
        ConnectedToMaster = true;
    }

    public void OnDisconnected(DisconnectCause cause)
    {
        if (cause == DisconnectCause.None || cause == DisconnectCause.DisconnectByClientLogic || cause == DisconnectCause.ApplicationQuit)
        {
            return;
        }
        Debug.LogErrorFormat("OnDisconnected cause={0}", cause);
    }

    public void OnRegionListReceived(RegionHandler regionHandler)
    {

    }

    public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
    {

    }

    public void OnCustomAuthenticationFailed(string debugMessage)
    {

    }


    #endregion

}
#endif