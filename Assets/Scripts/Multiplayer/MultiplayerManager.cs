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

        public ITransientData<PlayerRigData> m_LocalPlayer;
        public List<ITransientData<PlayerRigData>> m_RemotePlayers;

        public Action<int, ITransientData<PlayerRigData>> localPlayerJoined;
        public Action<int, ITransientData<PlayerRigData>> remotePlayerJoined;
        public Action<int> playerLeft;
        public Action<List<RoomData>> roomDataRefreshed;

        public event Action<ConnectionState> StateUpdated;
        public event Action<bool> RoomOwnershipUpdated;
        public event Action<ConnectionUserInfo> UserInfoStateUpdated;

        private List<RoomData> m_RoomData = new List<RoomData>();
        private double? m_NetworkOffsetTimestamp = null;

        ulong myOculusUserId;

        List<ulong> oculusPlayerIds;
        internal string UserId;
        [HideInInspector] public string CurrentRoomName;

        private ConnectionState _state;

        public ConnectionState State
        {
            get => _state;
            private set
            {
                if (_state != value)
                {
                    _state = value;
                    StateUpdated?.Invoke(_state);
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
        private string m_oldNickName = null;

        [HideInInspector] public RoomCreateData data;

        private bool _isUserRoomOwner = false;
        private bool isUserRoomOwner
        {
            get => _isUserRoomOwner;
            set
            {
                _isUserRoomOwner = value;
                RoomOwnershipUpdated?.Invoke(value);
            }
        }

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

            State = ConnectionState.INITIALIZING;
            switch (m_MultiplayerType)
            {
                case MultiplayerType.Photon:
#if MP_PHOTON
                    m_Manager = new PhotonManager(this);
                    m_Manager.Disconnected += OnConnectionHandlerDisconnected;
                    if (m_Manager != null) ControllerConsoleScript.m_Instance.AddNewLine("PhotonManager Loaded");
                    else ControllerConsoleScript.m_Instance.AddNewLine("PhotonManager Not Loaded");
#endif
#if MP_PHOTON
                    m_VoiceManager = new PhotonVoiceManager(this);
                    if (m_VoiceManager != null) ControllerConsoleScript.m_Instance.AddNewLine("PhotonVoiceManager Loaded");
                    else ControllerConsoleScript.m_Instance.AddNewLine("PhotonVoiceManager Not Loaded");
#endif 
                    break;
                default:
                    return;
            }
            if (m_VoiceManager != null && m_Manager != null) State = ConnectionState.INITIALIZED;

            roomDataRefreshed += OnRoomDataRefreshed;
            localPlayerJoined += OnLocalPlayerJoined;
            remotePlayerJoined += OnRemotePlayerJoined;
            playerLeft += OnPlayerLeft;
            StateUpdated += UpdateSketchMemoryScriptTimeOffset;

            SketchMemoryScript.m_Instance.CommandPerformed += OnCommandPerformed;
            SketchMemoryScript.m_Instance.CommandUndo += OnCommandUndo;
            SketchMemoryScript.m_Instance.CommandRedo += OnCommandRedo;
        }

        void OnDestroy()
        {
            roomDataRefreshed -= OnRoomDataRefreshed;
            localPlayerJoined -= OnLocalPlayerJoined;
            remotePlayerJoined -= OnRemotePlayerJoined;
            playerLeft -= OnPlayerLeft;
            StateUpdated -= UpdateSketchMemoryScriptTimeOffset;

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

            // check if room exist to determine if user is room owner
            DoesRoomNameExist(RoomData.roomName);
            if (!isUserRoomOwner) SketchMemoryScript.m_Instance.ClearMemory();

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

            bool roomExist = m_RoomData.Any(room => room.roomName == roomName);

            // Room does not exist
            if (!roomExist)
            {
                isUserRoomOwner = true;
                return false;
            }

            // Find the room with the given name
            RoomData? room = m_RoomData.FirstOrDefault(r => r.roomName == roomName);

            // Room exists 
            RoomData r = (RoomData)room;
            if (r.numPlayers == 0) isUserRoomOwner = true;// and is empty user becomes room owner
            else isUserRoomOwner = false; // not empty user is not the room owner

            return true;
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

            if (State != ConnectionState.IN_ROOM)
            {
                m_oldNickName = null;
                return;
            }

            m_Manager.Update();
            m_VoiceManager.Update();

            // Transmit local player data relative to scene origin
            var headRelativeToScene = App.Scene.AsScene[App.VrSdk.GetVrCamera().transform];
            var pointerRelativeToScene = App.Scene.AsScene[PointerManager.m_Instance.MainPointer.transform];
            var headScale = App.VrSdk.GetVrCamera().transform.localScale;
            var leftController = InputManager.m_Instance.GetController(InputManager.ControllerName.Brush).transform;
            var rightController = InputManager.m_Instance.GetController(InputManager.ControllerName.Wand).transform;
            var leftHandRelativeToScene = App.Scene.AsScene[leftController];
            var rightHandRelativeToScene = App.Scene.AsScene[rightController];

            var data = new PlayerRigData
            {
                HeadPosition = headRelativeToScene.translation,
                HeadRotation = headRelativeToScene.rotation,
                ToolPosition = pointerRelativeToScene.translation,
                ToolRotation = pointerRelativeToScene.rotation,
                LeftHandPosition = leftHandRelativeToScene.translation,
                LeftHandRotation = leftHandRelativeToScene.rotation,
                RightHandPosition = rightHandRelativeToScene.translation,
                RightHandRotation = rightHandRelativeToScene.rotation,

                BrushData = new BrushData
                {
                    Color = PointerManager.m_Instance.MainPointer.GetCurrentColor(),
                    Size = PointerManager.m_Instance.MainPointer.BrushSize01,
                    Guid = BrushController.m_Instance.ActiveBrush.m_Guid.ToString(),
                },
                ExtraData = new ExtraData
                {
                    OculusPlayerId = myOculusUserId,
                },
                IsRoomOwner = isUserRoomOwner,
                SceneScale = App.Scene.Pose.scale,
                isReceivingVoiceTransmission = m_VoiceManager.isTransmitting,
                Nickname = UserInfo.Nickname //TODO: remove from PlayerRigData or encode it and use photon to retrieve the string
            };



            if (m_LocalPlayer != null)
            {
                m_LocalPlayer.TransmitData(data);
            }


            // Update remote user refs, and send Anchors if new player joins.
            bool newUser = false;
            foreach (var player in m_RemotePlayers)
            {
                if (!player.IsSpawned) continue;

                data = player.ReceiveData();
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
            // the user is the room owner if is the firt to get in 
            isUserRoomOwner = m_Manager.GetPlayerCount() == 1 ? true : false;
            // if not room owner clear scene 
            if (!isUserRoomOwner) SketchMemoryScript.m_Instance.ClearMemory();

            m_LocalPlayer = playerData;
            m_LocalPlayer.PlayerId = id;

        }

        void OnRemotePlayerJoined(int id, ITransientData<PlayerRigData> playerData)
        {
            playerData.PlayerId = id;
            m_RemotePlayers.Add(playerData);

            if (isUserRoomOwner)
            {
                MultiplayerSceneSync.m_Instance.StartSyncronizationForUser(id);
            }
        }

        public void SendLargeDataToPlayer(int playerId, byte[] Data)
        {
            m_Manager.SendLargeDataToPlayer(playerId, Data);
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

            // Reassign Ownership if needed 
            // Check if any remaining player is the room owner
            bool anyRoomOwner = m_RemotePlayers.Any(player => m_Manager.GetPlayerRoomOwnershipStatus(player.PlayerId))
                                || isUserRoomOwner;

            // If there's still a room owner, no reassignment is needed
            if (anyRoomOwner) return;

            // If there are no other players left, the local player becomes the room owner
            if (m_RemotePlayers.Count == 0)
            {
                isUserRoomOwner = true;
                return;
            }

            // Since There are other players left
            // Determine the new room owner by the lowest PlayerId
            var allPlayers = new List<ITransientData<PlayerRigData>> { m_LocalPlayer };
            allPlayers.AddRange(m_RemotePlayers);

            // Find the player with the lowest PlayerId
            var newOwner = allPlayers.OrderBy(player => player.PlayerId).First();

            // If the new owner is the local player, set the flag
            if (m_LocalPlayer.PlayerId == newOwner.PlayerId) isUserRoomOwner = true;

        }

        public async void OnCommandPerformed(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                await m_Manager.PerformCommand(command);
            }
        }

        public async void SendCommandToPlayer(BaseCommand command, int playerID)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                await m_Manager.SendCommandToPlayer(command, playerID);
            }
        }

        public async Task<bool> CheckCommandReception(BaseCommand command, int id)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                return await m_Manager.CheckCommandReception(command, id);
            }

            return false;
        }

        public async Task<bool> CheckStrokeReception(Stroke stroke, int id)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                return await m_Manager.CheckStrokeReception(stroke, id);
            }

            return false;
        }

        public void OnCommandUndo(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                m_Manager.UndoCommand(command);
            }
        }

        public void OnCommandRedo(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                m_Manager.RedoCommand(command);
            }
        }

        public async void StartSynchHistory(int id)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                await m_Manager.RpcStartSyncHistory(id);
            }
        }

        public async void SynchHistoryPercentage(int id, int expected, int sent)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                await m_Manager.RpcSyncHistoryPercentage(id, expected, sent);
            }
        }

        public async void SynchHistoryComplete(int id)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                await m_Manager.RpcHistorySyncComplete(id);
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
            m_LocalPlayer = null;// Clean up local player reference
            m_RemotePlayers.Clear();// Clean up remote player references
            LastError = null;
            State = ConnectionState.DISCONNECTED;
            StateUpdated?.Invoke(State);
            Disconnected?.Invoke();// Invoke the Disconnected event
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

        public bool IsUserRoomOwner()
        {
            return isUserRoomOwner;
        }

        public bool IsRemotePlayerStillConnected(int playerId)
        {
            if (m_RemotePlayers.Any(player => player.PlayerId == playerId)) return true;
            return false;
        }

        public int? GetNetworkedTimestampMilliseconds()
        {
            if (State == ConnectionState.IN_ROOM)
            {
                if (m_Manager != null) return m_Manager.GetNetworkedTimestampMilliseconds();
            }

            return null;
        }

        // this only needs to be done once when the room is created
        private void UpdateSketchMemoryScriptTimeOffset(ConnectionState state)
        {
            // Ensure the offset is set only once upon connecting as room owner
            if (state == ConnectionState.IN_ROOM
                && isUserRoomOwner
                && m_NetworkOffsetTimestamp == null)
            {
                // Capture the current sketch time as the base offset for network synchronization
                m_NetworkOffsetTimestamp = (int)(App.Instance.CurrentSketchTime * 1000);
                SketchMemoryScript.m_Instance.SetTimeOffsetToAllStacks((int)m_NetworkOffsetTimestamp);
            }

        }


    }
}

