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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.XR.CoreUtils;
using System.ComponentModel.Composition;

#if OCULUS_SUPPORTED
using OVRPlatform = Oculus.Platform;
#endif
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public enum MultiplayerType
    {
        None,
        Colyseus = 1,
        Photon = 2,
    }

    public class MultiplayerManager : MonoBehaviour
    {
        public static MultiplayerManager m_Instance;
        public MultiplayerType m_MultiplayerType;
        public event Action Disconnected;

        private IDataConnectionHandler m_Manager;
        private IVoiceConnectionHandler m_VoiceManager;

        private ITransientData<PlayerRigData> m_LocalPlayer;
        private List<ITransientData<PlayerRigData>> m_RemotePlayers;

        public Action<int, ITransientData<PlayerRigData>> localPlayerJoined;
        public Action<int, ITransientData<PlayerRigData>> remotePlayerJoined;
        public Action<int> playerLeft;
        public Action<List<RoomData>> roomDataRefreshed;
        public event Action<ConnectionState> StateUpdated;
        private List<RoomData> m_RoomData = new List<RoomData>();

        ulong myOculusUserId;

        List<ulong> oculusPlayerIds;
        internal string UserId;
        [HideInInspector] public string CurrentRoomName;

        //public ConnectionState State => m_Manager?.State ?? ConnectionState.DISCONNECTED;
        private ConnectionState _state;
        public ConnectionState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateUpdated?.Invoke(_state);  // Trigger the event when the state changes
                }
            }
        }
        public string LastError { get; private set; }

        public ConnectionUserInfo UserInfo
        {
            get => m_Manager?.UserInfo ?? default;
            set
            {
                if (m_Manager != null)
                {
                    m_Manager.UserInfo = value;
                }
            }
        }

        public RoomCreateData data;

        void Awake()
        {
            m_Instance = this;
            oculusPlayerIds = new List<ulong>();
            m_RemotePlayers = new List<ITransientData<PlayerRigData>>();
        }

        void Start()
        {
#if OCULUS_SUPPORTED
            OVRPlatform.Users.GetLoggedInUser().OnComplete((msg) => {
                if (!msg.IsError)
                {
                    myOculusUserId = msg.GetUser().ID;
                    Debug.Log($"OculusID: {myOculusUserId}");
                    oculusPlayerIds.Add(myOculusUserId);
                }
                else
                {
                    Debug.LogError(msg.GetError());
                }
            });
#endif

            State = ConnectionState.INITIALISING;
            switch (m_MultiplayerType)
            {
                case MultiplayerType.Photon:
#if FUSION_WEAVER
                    m_Manager = new PhotonManager(this);
                    m_Manager.Disconnected += OnConnectionHandlerDisconnected;
                    if (m_Manager != null) ControllerConsoleScript.m_Instance.AddNewLine("PhotonManager Loaded");
                    else ControllerConsoleScript.m_Instance.AddNewLine("PhotonManager Not Loaded");
#endif
#if PHOTON_UNITY_NETWORKING && PHOTON_VOICE_DEFINED
                    m_VoiceManager = new PhotonVoiceManager(this);
                    if (m_VoiceManager != null) ControllerConsoleScript.m_Instance.AddNewLine("PhotonVoiceManager Loaded");
                    else ControllerConsoleScript.m_Instance.AddNewLine("PhotonVoiceManager Not Loaded");
#endif 
                    break;
                default:
                    return;
            }
            if (m_VoiceManager != null && m_Manager != null) State = ConnectionState.INITIALIZED;

            localPlayerJoined += OnLocalPlayerJoined;
            remotePlayerJoined += OnRemotePlayerJoined;
            playerLeft += OnPlayerLeft;
            SketchMemoryScript.m_Instance.CommandPerformed += OnCommandPerformed;
            SketchMemoryScript.m_Instance.CommandUndo += OnCommandUndo;
            SketchMemoryScript.m_Instance.CommandRedo += OnCommandRedo;
        }

        void OnDestroy()
        {
            localPlayerJoined -= OnLocalPlayerJoined;
            remotePlayerJoined -= OnRemotePlayerJoined;
            playerLeft -= OnPlayerLeft;
            SketchMemoryScript.m_Instance.CommandPerformed -= OnCommandPerformed;
            SketchMemoryScript.m_Instance.CommandUndo -= OnCommandUndo;
            SketchMemoryScript.m_Instance.CommandRedo -= OnCommandRedo;
        }

        public async Task<bool> Connect()
        {
            State = ConnectionState.CONNECTING;

            var successData = false;
            if (m_Manager != null) successData = await m_Manager.Connect();

            var successVoice = false;
            if (m_VoiceManager != null) successVoice = await m_VoiceManager.Connect();

            if (!successData)
            {
                State = ConnectionState.ERROR;
                LastError = m_Manager.LastError;
            }
            else if (!successVoice)
            {
                State = ConnectionState.ERROR;
                LastError = m_VoiceManager.LastError;
            }
            else State = ConnectionState.IN_LOBBY;


            return successData & successVoice;
        }

        public async Task<bool> JoinRoom(RoomCreateData RoomData)
        {
            State = ConnectionState.JOINING_ROOM;

            bool successData = false;
            if (m_Manager != null) successData = await m_Manager.JoinRoom(RoomData);

            bool successVoice = false;
            if (m_VoiceManager != null) successVoice = await m_VoiceManager.JoinRoom(RoomData);
            m_VoiceManager?.StartSpeaking();

            if (!successData)
            {
                State = ConnectionState.ERROR;
                LastError = m_Manager.LastError;
            }
            else if (!successVoice)
            {
                State = ConnectionState.ERROR;
                LastError = m_VoiceManager.LastError;
            }
            else State = ConnectionState.IN_ROOM;

            return successData & successVoice;
        }

        public async Task<bool> LeaveRoom(bool force = false)
        {
            State = ConnectionState.LEAVING_ROOM;

            bool successData = false;
            if (m_Manager != null) successData = await m_Manager.LeaveRoom();

            bool successVoice = false;
            m_VoiceManager?.StopSpeaking();
            if (m_VoiceManager != null) successVoice = await m_VoiceManager.LeaveRoom();

            if (!successData)
            {
                State = ConnectionState.ERROR;
                LastError = m_Manager.LastError;
            }
            else if (!successVoice)
            {
                State = ConnectionState.ERROR;
                LastError = m_VoiceManager.LastError;
            }
            else State = ConnectionState.IN_LOBBY;

            return successData & successVoice;
        }

        public async Task<bool> Disconnect()
        {
            State = ConnectionState.DISCONNECTING;

            bool successData = false;
            if (m_Manager != null) successData = await m_Manager.Disconnect();

            bool successVoice = false;
            if (m_VoiceManager != null) successVoice = await m_VoiceManager.Disconnect();

            if (!successData)
            {
                State = ConnectionState.ERROR;
                LastError = m_Manager.LastError;
            }
            else if (!successVoice)
            {
                State = ConnectionState.ERROR;
                LastError = m_VoiceManager.LastError;
            }
            else State = ConnectionState.DISCONNECTED;

            return successData & successVoice;
        }

        public bool DoesRoomNameExist(string roomName)
        {
            return m_RoomData.Any(room => room.roomName == roomName);
        }

        void OnRoomDataRefreshed(List<RoomData> rooms)
        {
            m_RoomData = rooms;
        }

        void Update()
        {
            if (App.CurrentState != App.AppState.Standard || m_Manager == null)
            {
                return;
            }

            m_Manager.Update();

            // Transmit local player data relative to scene origin
            var headRelativeToScene = App.Scene.AsScene[App.VrSdk.GetVrCamera().transform];
            var pointerRelativeToScene = App.Scene.AsScene[PointerManager.m_Instance.MainPointer.transform];

            var data = new PlayerRigData
            {
                HeadPosition = headRelativeToScene.translation,
                HeadRotation = headRelativeToScene.rotation,
                ToolPosition = pointerRelativeToScene.translation,
                ToolRotation = pointerRelativeToScene.rotation,
                BrushData = new BrushData
                {
                    Color = PointerManager.m_Instance.MainPointer.GetCurrentColor(),
                    Size = PointerManager.m_Instance.MainPointer.BrushSize01,
                    Guid = BrushController.m_Instance.ActiveBrush.m_Guid.ToString(),
                },
                ExtraData = new ExtraData
                {
                    OculusPlayerId = myOculusUserId,
                }
            };

            if (m_LocalPlayer != null)
            {
                m_LocalPlayer.TransmitData(data);
            }


            // Update remote user refs, and send Anchors if new player joins.
            bool newUser = false;
            foreach (var player in m_RemotePlayers)
            {
                data = player.RecieveData();
#if OCULUS_SUPPORTED
                // New user, share the anchor with them
                if (data.ExtraData.OculusPlayerId != 0 && !oculusPlayerIds.Contains(data.ExtraData.OculusPlayerId))
                {
                    Debug.Log("detected new user!");
                    Debug.Log(data.ExtraData.OculusPlayerId);
                    oculusPlayerIds.Add(data.ExtraData.OculusPlayerId);
                    newUser = true;
                }
#endif // OCULUS_SUPPORTED
            }

            if (newUser)
            {
                ShareAnchors();
            }
        }

        void OnLocalPlayerJoined(int id, ITransientData<PlayerRigData> playerData)
        {
            m_LocalPlayer = playerData;
        }

        void OnRemotePlayerJoined(int id, ITransientData<PlayerRigData> playerData)
        {
            Debug.Log("Adding new player to track.");
            playerData.PlayerId = id;
            m_RemotePlayers.Add(playerData);
        }

        void OnPlayerLeft(int id)
        {
            if (m_LocalPlayer.PlayerId == id)
            {
                m_LocalPlayer = null;
                Debug.Log("Possible to get here!");
                return;
            }
            var copy = m_RemotePlayers.ToList();
            foreach (var player in copy)
            {
                if (player.PlayerId == id)
                {
                    m_RemotePlayers.Remove(player);
                }
            }
        }

        private async void OnCommandPerformed(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                return;
            }

            var success = await m_Manager.PerformCommand(command);

            // TODO: Proper rollback if command not possible right now.
            // Commented so it doesn't interfere with general use.
            // Link actions to connect/disconnect, not Unity lifecycle.

            // if (!success)
            // {
            //     OutputWindowScript.m_Instance.CreateInfoCardAtController(InputManager.ControllerName.Brush, "Don't know how to network this action yet.");
            //     SketchMemoryScript.m_Instance.StepBack(false);
            // }
        }

        private void OnCommandUndo(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                m_Manager.UndoCommand(command);
            }
        }

        private void OnCommandRedo(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                m_Manager.RedoCommand(command);
            }
        }

        async void ShareAnchors()
        {
#if OCULUS_SUPPORTED
            Debug.Log($"sharing to {oculusPlayerIds.Count} Ids");
            var success = await OculusMRController.m_Instance.m_SpatialAnchorManager.ShareAnchors(oculusPlayerIds);

            if (success)
            {
                if (!OculusMRController.m_Instance.m_SpatialAnchorManager.AnchorUuid.Equals(String.Empty))
                {
                    await m_Manager.RpcSyncToSharedAnchor(OculusMRController.m_Instance.m_SpatialAnchorManager.AnchorUuid);
                }
            }
#endif // OCULUS_SUPPORTED
        }

        private void OnConnectionHandlerDisconnected()
        {
            // Clean up local player reference
            m_LocalPlayer = null;

            // Invoke the Disconnected event
            Disconnected?.Invoke();
        }

        public void StartSpeaking()
        {
            m_VoiceManager?.StartSpeaking();
        }

        public void StopSpeaking()
        {
            m_VoiceManager?.StopSpeaking();
        }

        public bool IsDisconnectable()
        {

            return State == ConnectionState.IN_ROOM || State == ConnectionState.IN_LOBBY;
        }
        public bool IsConnectable()
        {
            return State == ConnectionState.INITIALIZED || State == ConnectionState.DISCONNECTED;
        }
        public bool CanJoinRoom()
        {
            return State == ConnectionState.IN_LOBBY;
        }
        public bool CanLeaveRoom()
        {
            return State == ConnectionState.IN_ROOM;
        }
    }
}
