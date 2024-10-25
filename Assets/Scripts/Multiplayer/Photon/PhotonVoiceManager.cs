#if PHOTON_UNITY_NETWORKING && PHOTON_VOICE_DEFINED
using OpenBrush.Multiplayer;
using Photon.Realtime;
using Photon.Voice.Unity;
using System.Collections.Generic;
using System.Threading.Tasks;
using TiltBrush;
using UnityEngine;

public class PhotonVoiceManager : IVoiceManager, IConnectionCallbacks, IMatchmakingCallbacks
{
    private VoiceConnection m_VoiceConnection;
    private MultiplayerManager m_Manager;
    private AppSettings m_PhotonVoiceAppSettings;
    private Recorder m_Recorder;

    public PhotonVoiceManager(MultiplayerManager manager)
    {
        m_Manager = manager;
        InitializeVoice();

    }

    public void InitializeVoice()
    {
        m_VoiceConnection = GameObject.FindFirstObjectByType<VoiceConnection>();
        if (m_VoiceConnection == null)
        {
            ControllerConsoleScript.m_Instance.AddNewLine("VoiceConnection not found! Ensure the component is attached to a GameObject.");
            return;
        }

        m_VoiceConnection.Settings = new AppSettings
        {
            AppIdVoice = App.Config.PhotonVoiceSecrets.ClientId,
            FixedRegion = "",  
        };

        

        m_VoiceConnection.Client.AddCallbackTarget(this);
    }

    public async Task<bool> ConnectToVoiceServer()
    {
        m_VoiceConnection.Client.UserId = m_Manager.Id;

        if (!m_VoiceConnection.Client.IsConnected)
        {
            ControllerConsoleScript.m_Instance.AddNewLine("Attempting to connect Voice Server...");
            m_VoiceConnection.ConnectUsingSettings();
            while (!m_VoiceConnection.Client.IsConnected && !m_VoiceConnection.Client.IsConnectedAndReady)
            {
                ControllerConsoleScript.m_Instance.AddNewLine("Waiting for Voice Connection to establish...");
                await Task.Delay(100);
            }
        }

        bool connectedAndReady = m_VoiceConnection.Client.IsConnectedAndReady;
        if (connectedAndReady) ControllerConsoleScript.m_Instance.AddNewLine("Voice Connection successfully established.");
        else ControllerConsoleScript.m_Instance.AddNewLine("Failed to connect to Voice Server.");
        return connectedAndReady;
    }


    public async Task<bool> JoinRoom(string roomName)
    {
        if (!m_VoiceConnection.Client.IsConnected)
        {
            bool connected = await ConnectToVoiceServer();
            if (!connected)
            {
                return false; 
            }
        }

        while (m_VoiceConnection.ClientState != ClientState.JoinedLobby)
        {
            ControllerConsoleScript.m_Instance.AddNewLine("Waiting to join lobby...");
            await Task.Delay(100);
        }
        ControllerConsoleScript.m_Instance.AddNewLine("Joined lobby...");

        bool roomJoined = m_VoiceConnection.Client.OpJoinOrCreateRoom(new EnterRoomParams
        {
            RoomName = roomName
        });

        if (roomJoined)  ControllerConsoleScript.m_Instance.AddNewLine($"Successfully joined room: {roomName}");
        else  ControllerConsoleScript.m_Instance.AddNewLine($"Failed to join or create room: {roomName}");
        

        return roomJoined;
    }


    public void StartSpeaking()
    {
        m_Recorder = m_VoiceConnection.PrimaryRecorder;
        if (m_Recorder == null)
        {
            ControllerConsoleScript.m_Instance.AddNewLine("Recorder not found! Ensure it's attached to a GameObject.");
            return;
        }

        // m_Recorder.DebugEchoMode = true;
        m_Recorder.TransmitEnabled = true;
    }

    public void StopSpeaking()
    {
        if (m_Recorder != null)
        {
            m_Recorder.TransmitEnabled = false;
        }
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
        m_VoiceConnection.Client.OpJoinOrCreateRoom(new EnterRoomParams
        {
            RoomName = m_Manager.CurrentRoomName,
        });
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