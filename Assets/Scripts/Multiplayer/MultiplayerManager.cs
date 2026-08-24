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
#if MP_FUSION
using Fusion;
#endif
using UnityEngine;
#if OCULUS_SUPPORTED
using OVRPlatform = Oculus.Platform;
#endif
using TiltBrush;
using UnityEngine.Serialization;

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
        private bool m_IsLocalVoiceEnabled = true;
        private bool m_IsRoomVoiceEnabled = true;
        private bool m_IsVoiceEnabled = true;

        public bool IsLocalVoiceEnabled => m_IsLocalVoiceEnabled;
        public bool IsRoomVoiceEnabled => m_IsRoomVoiceEnabled;
        public bool IsVoiceEnabled => m_IsVoiceEnabled;

        public ITransientData<PlayerRigData> m_LocalPlayer;
        [HideInInspector] public RemotePlayers m_RemotePlayers;
        public int LocalPlayerId => m_LocalPlayer?.PlayerId ?? -1;

        public Action<int, ITransientData<PlayerRigData>> localPlayerJoined;
        public Action<RemotePlayer> remotePlayerJoined;
        public Action<int, GameObject> remoteVoiceAdded;
        public Action<int> playerLeft;
        public Action<List<RoomData>> roomDataRefreshed;

        public event Action<ConnectionState> StateUpdated;
        public event Action<bool> RoomOwnershipUpdated;
        public event Action<ConnectionUserInfo> UserInfoStateUpdated;

        private List<RoomData> m_RoomData = new List<RoomData>();
        private double? m_NetworkOffsetTimestamp = null;
        private readonly Dictionary<Guid, CanvasScript> m_ContributorLayers =
            new Dictionary<Guid, CanvasScript>();
        private readonly HashSet<int> m_LiveStrokeCapablePlayers =
            new HashSet<int>();
        private readonly HashSet<int> m_PendingSceneSyncPlayerIds =
            new HashSet<int>();
        private readonly Dictionary<int, int> m_LiveStrokePointerCapacities =
            new Dictionary<int, int>();

        private sealed class OutgoingLiveStroke
        {
            public Guid StreamId;
            public PointerScript Pointer;
            public int Seed;
            public StrokeTimeSessionMetadata SourceTimeSession;
            public HashSet<int> Recipients;
            public int SentConfirmedPointCount;
            public uint NextProvisionalSequence;
            public float NextUpdateTime;
            public bool StartSent;
        }

        private sealed class RetainedLiveStrokeCommand
        {
            public BrushStrokeCommand Command;
            public float ExpiresAt;
        }

        private readonly Dictionary<PointerScript, OutgoingLiveStroke> m_OutgoingLiveStrokes =
            new Dictionary<PointerScript, OutgoingLiveStroke>();
        private readonly Dictionary<int, OutgoingLiveStroke> m_OutgoingLiveStrokesBySeed =
            new Dictionary<int, OutgoingLiveStroke>();
        private readonly Dictionary<Guid, OutgoingLiveStroke> m_OutgoingLiveStrokesById =
            new Dictionary<Guid, OutgoingLiveStroke>();
        private readonly Dictionary<Guid, RetainedLiveStrokeCommand> m_RetainedLiveStrokeCommands =
            new Dictionary<Guid, RetainedLiveStrokeCommand>();

        private const int k_MaxLiveStrokeControlPoints = 32768;
        private const float k_LiveStrokeUpdateIntervalSeconds = 0.1f;
        private const float k_LiveStrokeRepairRetentionSeconds = 30f;
        private const int k_CapabilityDiscoveryTimeoutMs = 2000;

        public bool IsLiveStrokeStreamingEnabled { get; private set; }
        public bool IsLiveStrokeRoomStateReady { get; private set; }
        public int MaxStreamedPointers => App.UserConfig.Multiplayer.MaxStreamedPointers;
        public event Action<bool> LiveStrokeStreamingUpdated;

        public Guid LocalContributorId { get; private set; }

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

        [HideInInspector] public RoomCreateData CurrentRoomData;

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

        private bool _isViewOnly;

        [NonSerialized] public bool m_IsAllMutedForMe;
        [NonSerialized] public bool m_IsAllMutedForAll;
        public bool ArePlayerAvatarsHiddenForMe { get; private set; }

        public bool IsViewOnly
        {
            get
            {
                // If the user is not in a room, then they can't be view only
                if (State != ConnectionState.IN_ROOM) return false;
                // Room owners are never in view-only mode
                if (isUserRoomOwner) return false;
                return _isViewOnly;
            }
            set => _isViewOnly = value;
        }

        void Awake()
        {
            m_Instance = this;
            LocalContributorId = Guid.NewGuid();
            oculusPlayerIds = new List<ulong>();
            if (GetComponent<ManualColocationManager>() == null)
            {
                gameObject.AddComponent<ManualColocationManager>();
            }
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
            remoteVoiceAdded += OnRemoteVoiceConnected;
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
            remoteVoiceAdded -= OnRemoteVoiceConnected;
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

            bool successVoice = true;
            if (m_IsVoiceEnabled && m_VoiceManager != null)
            {
                successVoice = await m_VoiceManager.Connect();
            }

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
            m_IsRoomVoiceEnabled = !RoomData.voiceDisabled;
            m_IsVoiceEnabled = m_IsLocalVoiceEnabled && m_IsRoomVoiceEnabled;

            if (State == ConnectionState.INITIALIZED || State == ConnectionState.DISCONNECTED)
            {
                if (!await Connect())
                {
                    return false;
                }
            }

            if (State != ConnectionState.IN_LOBBY)
            {
                LastError = $"Cannot join room while multiplayer is in state {State}.";
                Debug.LogError($"[MultiplayerHttpJoin] {LastError}");
                return false;
            }

            State = ConnectionState.JOINING_ROOM;

            bool successData = false;
            if (m_Manager != null) successData = await m_Manager.JoinRoom(RoomData);

            bool successVoice = true;
            if (m_IsVoiceEnabled && m_VoiceManager != null)
            {
                successVoice = await m_VoiceManager.JoinRoom(RoomData);
                if (successVoice)
                {
                    m_VoiceManager.StartSpeaking();
                }
            }

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

            CurrentRoomData = RoomData;
            if (successData)
            {
                if (isUserRoomOwner)
                {
                    ApplyLiveStrokeRoomStateLocally(RoomData.liveStrokeStreaming);
                }
                else
                {
                    IsLiveStrokeStreamingEnabled = false;
                    IsLiveStrokeRoomStateReady = false;
                }
                await m_Manager.RpcAdvertiseLiveStrokeSupport(MaxStreamedPointers);
            }

            return successData & successVoice;
        }

        public async Task<bool> LeaveRoom(bool force = false)
        {
            State = ConnectionState.LEAVING_ROOM;

            bool successData = false;
            if (m_Manager != null) successData = await m_Manager.LeaveRoom();

            bool successVoice = true;
            if (m_VoiceManager != null && m_VoiceManager.State == ConnectionState.IN_ROOM)
            {
                m_VoiceManager.StopSpeaking();
                successVoice = await m_VoiceManager.LeaveRoom();
            }

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
                LastError = m_Manager?.LastError;
            }
            else if (!successVoice)
            {
                State = ConnectionState.ERROR;
                LastError = m_VoiceManager?.LastError;
            }
            else State = ConnectionState.DISCONNECTED;

            return successData && successVoice;
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

        public void RoomOwnershipReceived(RemotePlayerSettings[] playerSettings, RoomCreateData roomData)
        {
            roomData.voiceDisabled = !IsRoomVoiceEnabled;
            roomData.liveStrokeStreaming = CurrentRoomData.liveStrokeStreaming;
            CurrentRoomData = roomData;

            foreach (var p in playerSettings)
            {
                var PlayerId = p.m_PlayerId;
                var mplayer = m_Instance.GetPlayerById(PlayerId);
                if (mplayer == null) continue;
                mplayer.m_IsMutedForAll = p.m_IsMutedForAll;
                mplayer.m_IsViewOnly = p.m_IsViewOnly;
            }
            // TODO Refresh GUI

            isUserRoomOwner = true;
        }

        public void RoomOwnershipTransferToUser(int playerId)
        {
            if (!isUserRoomOwner) return;

            var playerSettings = new RemotePlayerSettings[m_RemotePlayers.List.Count];

            for (var i = 0; i < m_RemotePlayers.List.Count; i++)
            {
                var player = m_RemotePlayers.List[i];
                playerSettings[i] = new RemotePlayerSettings(player.PlayerId, player.m_IsMutedForAll, player.m_IsViewOnly);
            }
            m_Manager.RpcTransferRoomOwnership(playerId, playerSettings, CurrentRoomData);
            isUserRoomOwner = false;
        }

        // Not really a multiplayer function but placing it here for consistency with other methods
        public void MutePlayerForMe(bool muted, int playerId)
        {
            GetPlayerById(playerId).m_IsMutedForMe = muted;
            MultiplayerAudioSourcesManager.m_Instance.SetMuteForPlayer(playerId, muted);
        }

        public bool SetPlayerAvatarsHiddenForMe(bool hidden)
        {
            int rendererCount = 0;
            foreach (RemotePlayer player in m_RemotePlayers.List)
            {
                if (!hidden)
                {
                    player.m_IsHiddenForMe = false;
                }
                rendererCount += SetAvatarRenderersHidden(player, hidden);
            }

            ArePlayerAvatarsHiddenForMe = hidden;
            Debug.Log(
                $"[MultiplayerAvatarVisibility] hidden={hidden}, rendererCount={rendererCount}.");
            return true;
        }

        public bool SetPlayerAvatarHiddenForMe(bool hidden, int playerId)
        {
            RemotePlayer player = GetPlayerById(playerId);
            if (player == null) return false;

            player.m_IsHiddenForMe = hidden;
            bool effectivelyHidden = hidden || ArePlayerAvatarsHiddenForMe;
            int rendererCount = SetAvatarRenderersHidden(player, effectivelyHidden);
            Debug.Log(
                $"[MultiplayerAvatarVisibility] playerId={playerId}, hidden={hidden}, " +
                $"effectivelyHidden={effectivelyHidden}, rendererCount={rendererCount}.");
            return true;
        }

        private static void InitializeAvatarVisibility(RemotePlayer player, bool hidden)
        {
            if (player?.PlayerGameObject == null) return;

            int rendererCount = SetAvatarRenderersHidden(player, hidden);
            Debug.Log(
                $"[MultiplayerAvatarVisibility] initialized playerId={player.PlayerId}, " +
                $"hidden={hidden}, rendererCount={rendererCount}.");
        }

        private static int SetAvatarRenderersHidden(RemotePlayer player, bool hidden)
        {
            if (player?.PlayerGameObject == null) return 0;

            Renderer[] renderers =
                player.PlayerGameObject.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                renderer.forceRenderingOff = hidden;
            }
            return renderers.Length;
        }

        public void MutePlayerForAll(bool muted, int playerId)
        {
            if (!isUserRoomOwner) return;
            GetPlayerById(playerId).m_IsMutedForAll = muted;
            MultiplayerAudioSourcesManager.m_Instance.SetMuteForPlayer(playerId, muted);
            m_Manager.RpcMutePlayer(muted, playerId);
        }

        public void SetUserViewOnlyMode(bool isViewOnly, int playerId)
        {
            if (!isUserRoomOwner) return;
            GetPlayerById(playerId).m_IsViewOnly = isViewOnly;
            m_Manager.RpcSetUserViewOnlyMode(isViewOnly, playerId);
        }

        public void KickPlayerOut(int playerId)
        {
            if (!isUserRoomOwner) return;
            m_Manager.RpcKickPlayerOut(playerId);
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
            UpdateOutgoingLiveStrokes();
            ExpireRetainedLiveStrokeCommands();

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
                    Guid = BrushController.m_Instance.ActiveBrush?.m_Guid.ToString(),
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
            foreach (var playerData in m_RemotePlayers.List)
            {
                ITransientData<PlayerRigData> player = playerData.TransientData;

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
            // SessionInfo.PlayerCount can lag during the local join callback. Fusion's shared
            // mode master-client state is the authoritative ownership signal.
            isUserRoomOwner = m_Manager.IsLocalPlayerRoomOwner();
            Debug.Log(
                $"[MultiplayerJoinConsistency] Local player {id} joined; roomOwner={isUserRoomOwner}.");
            if (!isUserRoomOwner) SketchMemoryScript.m_Instance.ClearMemory();

            m_LocalPlayer = playerData;
            m_LocalPlayer.PlayerId = id;
            ManualColocationManager.m_Instance?.OnLocalPlayerJoinedRoom();

        }

        void OnRemotePlayerJoined(RemotePlayer newRemotePlayer)
        {
            InitializeAvatarVisibility(newRemotePlayer, ArePlayerAvatarsHiddenForMe);
            m_RemotePlayers.AddPlayer(newRemotePlayer);

            if (!isUserRoomOwner) return;  //below this line is only room owner responsability 

            if (ManualColocationManager.m_Instance != null &&
                ManualColocationManager.m_Instance.HasReference)
            {
                _ = SendManualColocationReferenceToPlayer(
                    ManualColocationManager.m_Instance.CurrentReference,
                    newRemotePlayer.PlayerId);
            }
            if (CurrentRoomData.silentRoom == true) MutePlayerForAll(true, newRemotePlayer.PlayerId);
            if (CurrentRoomData.viewOnlyRoom == true) SetUserViewOnlyMode(true, newRemotePlayer.PlayerId);
            if (m_LiveStrokeCapablePlayers.Contains(newRemotePlayer.PlayerId))
            {
                _ = m_Manager.RpcSetRoomVoiceEnabled(
                    !CurrentRoomData.voiceDisabled, newRemotePlayer.PlayerId);
                _ = SendLiveStrokeRoomStateToPlayer(newRemotePlayer.PlayerId);
            }
            ScheduleInitialSceneSync(newRemotePlayer.PlayerId);
        }

        public void ReceiveLiveStrokeCapability(
            int playerId, int maxStreamedPointers)
        {
            if (playerId == LocalPlayerId ||
                maxStreamedPointers <= 0)
            {
                return;
            }

            bool isFirstAdvertisement =
                m_LiveStrokeCapablePlayers.Add(playerId);
            m_LiveStrokePointerCapacities[playerId] = maxStreamedPointers;
            Debug.Log(
                $"[LiveStrokeCapacity] Player {playerId} advertised capacity " +
                $"{maxStreamedPointers}.");
            if (isFirstAdvertisement && m_Manager != null)
            {
                _ = m_Manager.RpcAdvertiseLiveStrokeSupport(
                    MaxStreamedPointers);
            }
            if (isUserRoomOwner)
            {
                _ = m_Manager.RpcSetRoomVoiceEnabled(
                    !CurrentRoomData.voiceDisabled, playerId);
                _ = SendLiveStrokeRoomStateToPlayer(playerId);
                CompletePendingSceneSync(playerId, "capability advertisement");
            }
        }

        private void ScheduleInitialSceneSync(int playerId)
        {
            m_PendingSceneSyncPlayerIds.Add(playerId);
            if (IsPlayerLiveStrokeCompatible(playerId))
            {
                CompletePendingSceneSync(playerId, "known capability");
                return;
            }
            _ = CompletePendingSceneSyncAfterTimeout(playerId);
        }

        private async Task CompletePendingSceneSyncAfterTimeout(int playerId)
        {
            await Task.Delay(k_CapabilityDiscoveryTimeoutMs);
            CompletePendingSceneSync(playerId, "compatibility timeout");
        }

        private void CompletePendingSceneSync(int playerId, string reason)
        {
            if (!m_PendingSceneSyncPlayerIds.Remove(playerId) ||
                !isUserRoomOwner ||
                !IsRemotePlayerStillConnected(playerId))
            {
                return;
            }

            Debug.Log(
                $"[LiveStrokeCapabilitySync] Starting scene sync for player {playerId}; " +
                $"reason={reason}, enriched={IsPlayerLiveStrokeCompatible(playerId)}.");
            MultiplayerSceneSync.m_Instance.StartSyncronizationForUser(playerId);
        }

        public bool IsPlayerLiveStrokeCompatible(int playerId)
        {
            return m_LiveStrokeCapablePlayers.Contains(playerId);
        }

        public IReadOnlyList<int> GetLiveStrokeCompatiblePlayerIds()
        {
            return m_RemotePlayers.List
                .Where(player => IsPlayerLiveStrokeCompatible(player.PlayerId))
                .Select(player => player.PlayerId)
                .ToList();
        }

        private bool CanPlayerReceiveLiveStroke(int playerId, int pointerCount)
        {
            return IsPlayerLiveStrokeCompatible(playerId) &&
                m_LiveStrokePointerCapacities.TryGetValue(
                    playerId, out int capacity) &&
                capacity >= pointerCount;
        }

        public async Task<bool> SetLiveStrokeStreamingEnabled(bool enabled)
        {
            if (!isUserRoomOwner || State != ConnectionState.IN_ROOM || m_Manager == null)
            {
                return false;
            }

            ApplyLiveStrokeRoomStateLocally(enabled);

            bool success = true;
            foreach (int playerId in GetLiveStrokeCompatiblePlayerIds())
            {
                success &= await m_Manager.RpcSetLiveStrokeRoomState(
                    enabled, playerId);
            }
            return success;
        }

        public void ApplyLiveStrokeRoomState(
            bool enabled, int sourcePlayerId)
        {
            if (!IsPlayerRoomOwner(sourcePlayerId))
            {
                Debug.LogWarning(
                    $"[LiveStrokeStreaming] Rejected room state from non-owner player {sourcePlayerId}.");
                return;
            }

            ApplyLiveStrokeRoomStateLocally(enabled);
        }

        private void ApplyLiveStrokeRoomStateLocally(bool enabled)
        {
            IsLiveStrokeStreamingEnabled = enabled;
            IsLiveStrokeRoomStateReady = true;
            CurrentRoomData.liveStrokeStreaming = enabled;
            LiveStrokeStreamingUpdated?.Invoke(IsLiveStrokeStreamingEnabled);
            Debug.Log(
                $"[LiveStrokeStreaming] Room state applied: enabled={enabled}.");
        }

        private Task<bool> SendLiveStrokeRoomStateToPlayer(int playerId)
        {
            return m_Manager.RpcSetLiveStrokeRoomState(
                CurrentRoomData.liveStrokeStreaming, playerId);
        }

        public void SetRoomSilent(bool isSilent)
        {
            if (!isUserRoomOwner) return;
            CurrentRoomData.silentRoom = isSilent;
            for (int i = 0; i < m_RemotePlayers.List.Count; i++)
            {
                var player = m_RemotePlayers.List[i];
                player.m_IsMutedForAll = CurrentRoomData.silentRoom;
                MutePlayerForAll(player.m_IsMutedForAll, player.PlayerId);
            }
        }

        public void SetRoomViewOnly(bool isViewOnly)
        {
            if (!isUserRoomOwner) return;
            CurrentRoomData.viewOnlyRoom = isViewOnly;
            for (int i = 0; i < m_RemotePlayers.List.Count; i++)
            {
                var player = m_RemotePlayers.List[i];
                player.m_IsViewOnly = CurrentRoomData.viewOnlyRoom;
                SetUserViewOnlyMode(player.m_IsViewOnly, player.PlayerId);
            }
        }

        public RemotePlayer GetPlayerById(int id)
        {
            RemotePlayer playerData = m_RemotePlayers.List.FirstOrDefault(x => x.PlayerId == id);
            if (playerData == null)
            {
                Debug.LogWarning($"PlayerRigData with ID {id} not found");
                return null;
            }

            if (playerData.PlayerGameObject == null)
            {
                Debug.LogWarning($"RemotePlayerGameObject with ID {id} not found");
                return null;
            }

            return playerData;
        }

        public void OnRemoteVoiceConnected(int id, GameObject voicePrefab)
        {
            var playerData = GetPlayerById(id);
            if (playerData == null) return;

            Transform headTransform = playerData.PlayerGameObject.transform.Find("HeadTransform");
            if (headTransform != null)
            {
                voicePrefab.transform.SetParent(headTransform, false);
                playerData.VoiceGameObject = voicePrefab;
            }
            else
            {
                Debug.LogWarning($"HeadTransform not found in {playerData.PlayerGameObject.name}");
            }

            AudioSource audioSource = voicePrefab.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                Debug.LogWarning($"VoicePrefab with ID {id} lack AudioSource :S ");
                return;
            }
            MultiplayerAudioSourcesManager.m_Instance.AddAudioSource(id, audioSource);
        }

        public void SendLargeDataToPlayer(int playerId, byte[] Data, int percentage)
        {
            m_Manager.SendLargeDataToPlayer(playerId, Data, percentage);
        }

        public void TagStrokeWithLocalContributor(Stroke stroke)
        {
            if (stroke == null || stroke.m_MultiplayerContributorId != Guid.Empty)
            {
                return;
            }

            stroke.m_MultiplayerContributorId = LocalContributorId;
            stroke.m_MultiplayerContributorNickname = UserInfo.Nickname;
        }

        public CanvasScript GetOrCreateContributorLayer(Guid contributorId, string nickname)
        {
            if (contributorId == Guid.Empty)
            {
                return App.Scene.MainCanvas;
            }

            if (m_ContributorLayers.TryGetValue(contributorId, out var layer) &&
                layer != null && !App.Scene.IsLayerDeleted(layer))
            {
                return layer;
            }

            layer = App.Scene.AddLayerNow();
            string displayName = string.IsNullOrWhiteSpace(nickname) ? "Player" : nickname.Trim();
            App.Scene.RenameLayer(layer, $"Multiplayer - {displayName}");
            m_ContributorLayers[contributorId] = layer;
            return layer;
        }

        public void TryBeginLocalLiveStroke(
            PointerScript pointer, CanvasScript canvas, ParametricStrokeCreator creator)
        {
            if (pointer == null || canvas == null || creator != null ||
                State != ConnectionState.IN_ROOM ||
                !IsLiveStrokeRoomStateReady ||
                !IsLiveStrokeStreamingEnabled ||
                m_Manager == null ||
                PointerManager.m_Instance == null ||
                !PointerManager.m_Instance.IsActiveUserPointer(pointer) ||
                PointerManager.m_Instance.StraightEdgeModeEnabled ||
                PointerManager.m_Instance.ActiveUserPointerCount > MaxStreamedPointers ||
                m_OutgoingLiveStrokes.Count >= MaxStreamedPointers ||
                pointer.CurrentBrushScript == null ||
                !pointer.CurrentBrushScript.m_bCanBatch)
            {
                return;
            }

            int pointerCount = PointerManager.m_Instance.ActiveUserPointerCount;
            var recipients = GetLiveStrokeCompatiblePlayerIds()
                .Where(playerId => CanPlayerReceiveLiveStroke(playerId, pointerCount))
                .Where(IsRemotePlayerStillConnected)
                .ToHashSet();
            if (recipients.Count == 0 ||
                !SketchMemoryScript.m_Instance.TryGetActiveStrokeTimeSession(
                    out StrokeTimeSessionMetadata sourceTimeSession))
            {
                return;
            }

            var stream = new OutgoingLiveStroke
            {
                StreamId = Guid.NewGuid(),
                Pointer = pointer,
                Seed = pointer.CurrentBrushScript.RandomSeed,
                SourceTimeSession = new StrokeTimeSessionMetadata
                {
                    StartUtcMs = sourceTimeSession.StartUtcMs,
                    StartSketchTimeMs = sourceTimeSession.StartSketchTimeMs,
                    EndSketchTimeMs = sourceTimeSession.EndSketchTimeMs,
                },
                Recipients = recipients,
                NextUpdateTime = Time.realtimeSinceStartup,
            };
            m_OutgoingLiveStrokes[pointer] = stream;
            m_OutgoingLiveStrokesBySeed[stream.Seed] = stream;
            m_OutgoingLiveStrokesById[stream.StreamId] = stream;
        }

        public void NotifyLocalLiveStrokeChanged(PointerScript pointer)
        {
            if (!m_OutgoingLiveStrokes.TryGetValue(pointer, out OutgoingLiveStroke stream) ||
                stream.StartSent)
            {
                return;
            }

            List<PointerManager.ControlPoint> points = pointer.GetControlPoints();
            if (points.Count == 0 || !pointer.LastControlPointIsKeeper)
            {
                return;
            }

            var line = pointer.CurrentBrushScript;
            if (line == null)
            {
                CancelLocalLiveStroke(pointer);
                return;
            }

            var stroke = new Stroke
            {
                m_Type = Stroke.Type.NotCreated,
                m_IntendedCanvas = App.Scene.MainCanvas,
                m_BrushGuid = line.Descriptor.m_Guid,
                m_BrushScale = line.StrokeScale,
                m_BrushSize = line.BaseSize_PS,
                m_Color = line.CurrentColor,
                m_Seed = line.RandomSeed,
                m_ControlPoints = new[] { points[0] },
                m_ControlPointsToDrop = new[] { false },
            };

            bool sent = true;
            foreach (int playerId in stream.Recipients)
            {
                sent &= m_Manager.RpcLiveStrokeStart(
                    stream.StreamId, stroke, stream.SourceTimeSession,
                    LocalContributorId, UserInfo.Nickname, playerId);
            }
            if (!sent)
            {
                CancelLocalLiveStroke(pointer);
                return;
            }

            stream.StartSent = true;
            stream.SentConfirmedPointCount = 1;
            stream.NextUpdateTime = Time.realtimeSinceStartup +
                k_LiveStrokeUpdateIntervalSeconds;
        }

        public void CancelLocalLiveStroke(PointerScript pointer)
        {
            if (!m_OutgoingLiveStrokes.TryGetValue(pointer, out OutgoingLiveStroke stream))
            {
                return;
            }

            if (stream.StartSent)
            {
                foreach (int playerId in stream.Recipients.Where(IsRemotePlayerStillConnected))
                {
                    m_Manager.RpcLiveStrokeCancel(stream.StreamId, playerId);
                }
            }
            RemoveOutgoingLiveStroke(stream);
        }

        public void FinishLocalLiveStroke(PointerScript pointer)
        {
            // Batched symmetry strokes only raise CommandPerformed after the last pointer has
            // detached. Keep earlier pointer streams alive until that complete command tree is
            // available. UpdateOutgoingLiveStrokes removes a stream if no command was produced.
        }

        private void UpdateOutgoingLiveStrokes()
        {
            if (m_OutgoingLiveStrokes.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            foreach (OutgoingLiveStroke stream in m_OutgoingLiveStrokes.Values.ToList())
            {
                if (!stream.StartSent || now < stream.NextUpdateTime)
                {
                    continue;
                }

                List<PointerManager.ControlPoint> points = stream.Pointer.GetControlPoints();
                if (stream.Pointer.CurrentBrushScript == null ||
                    points.Count == 0 ||
                    points.Count > k_MaxLiveStrokeControlPoints)
                {
                    CancelLocalLiveStroke(stream.Pointer);
                    continue;
                }

                int confirmedCount = stream.Pointer.LastControlPointIsKeeper
                    ? points.Count
                    : Math.Max(0, points.Count - 1);
                bool hasTail = !stream.Pointer.LastControlPointIsKeeper && points.Count > 0;
                PointerManager.ControlPoint tail = hasTail ? points[points.Count - 1] : default;
                int nextIndex = stream.SentConfirmedPointCount;
                while (nextIndex < confirmedCount)
                {
                    int count = Math.Min(
                        NetworkingConstants.MaxControlPointsPerChunk,
                        confirmedCount - nextIndex);
                    var confirmed = points.Skip(nextIndex).Take(count).ToArray();
                    foreach (int playerId in stream.Recipients.Where(IsRemotePlayerStillConnected))
                    {
                        m_Manager.RpcLiveStrokeConfirmed(
                            stream.StreamId, nextIndex, confirmed, playerId);
                    }
                    nextIndex += count;
                }
                if (hasTail)
                {
                    uint sequence = ++stream.NextProvisionalSequence;
                    foreach (int playerId in stream.Recipients.Where(IsRemotePlayerStillConnected))
                    {
                        m_Manager.RpcLiveStrokeProvisionalTail(
                            stream.StreamId, sequence, confirmedCount, tail, playerId);
                    }
                }
                stream.SentConfirmedPointCount = confirmedCount;
                stream.NextUpdateTime = now + k_LiveStrokeUpdateIntervalSeconds;
            }
        }

        private async Task<bool> TryCompleteOutgoingLiveStrokeTree(BaseCommand rootCommand)
        {
            List<BrushStrokeCommand> allBrushCommands =
                EnumerateBrushStrokeCommands(rootCommand).ToList();
            List<BrushStrokeCommand> brushCommands = allBrushCommands.Where(
                command => command.m_Stroke != null &&
                    m_OutgoingLiveStrokesBySeed.ContainsKey(command.m_Stroke.m_Seed))
                .ToList();
            if (brushCommands.Count == 0)
            {
                return false;
            }

            var deltaCommandGuids = brushCommands
                .Select(command => command.Guid)
                .ToHashSet();
            bool deltaTreeIsClosed = brushCommands.All(command =>
                command.Children.All(child => deltaCommandGuids.Contains(child.Guid)));
            if (!deltaTreeIsClosed)
            {
                foreach (BrushStrokeCommand command in brushCommands)
                {
                    if (m_OutgoingLiveStrokesBySeed.TryGetValue(
                            command.m_Stroke.m_Seed, out OutgoingLiveStroke activeStream))
                    {
                        CancelLocalLiveStroke(activeStream.Pointer);
                    }
                }
                return false;
            }

            List<BrushStrokeCommand> deltaRoots = brushCommands
                .Where(command => command.ParentGuid == Guid.Empty ||
                    !deltaCommandGuids.Contains(command.ParentGuid))
                .ToList();
            Debug.Log(
                $"[LiveStrokeCommand] Completing tree root={rootCommand.Guid} " +
                $"commands={brushCommands.Count} deltaRoots={deltaRoots.Count}.");

            var streams = new List<(BrushStrokeCommand Command, OutgoingLiveStroke Stream)>();
            foreach (BrushStrokeCommand command in brushCommands)
            {
                Stroke stroke = command.m_Stroke;
                if (stroke == null ||
                    !m_OutgoingLiveStrokesBySeed.TryGetValue(
                        stroke.m_Seed, out OutgoingLiveStroke stream) ||
                    !stream.StartSent ||
                    stroke.m_ControlPoints == null ||
                    stroke.m_ControlPoints.Length == 0 ||
                    stroke.m_ControlPoints.Length > k_MaxLiveStrokeControlPoints)
                {
                    foreach (BrushStrokeCommand brushCommand in brushCommands)
                    {
                        if (brushCommand.m_Stroke != null &&
                            m_OutgoingLiveStrokesBySeed.TryGetValue(
                                brushCommand.m_Stroke.m_Seed,
                                out OutgoingLiveStroke activeStream))
                        {
                            CancelLocalLiveStroke(activeStream.Pointer);
                        }
                    }
                    return false;
                }
                streams.Add((command, stream));
            }

            var fullyStreamedRecipients = new HashSet<int>(streams[0].Stream.Recipients);
            foreach (var item in streams.Skip(1))
            {
                fullyStreamedRecipients.IntersectWith(item.Stream.Recipients);
            }
            if (streams.Any(item =>
                    item.Command.m_Stroke.m_ControlPointsToDrop != null &&
                    item.Command.m_Stroke.m_ControlPointsToDrop.Any(drop => drop)))
            {
                fullyStreamedRecipients.Clear();
            }

            foreach (var item in streams)
            {
                TagStrokeWithLocalContributor(item.Command.m_Stroke);
                CompleteOutgoingLiveStroke(
                    item.Command, item.Stream, fullyStreamedRecipients);
            }

            foreach (RemotePlayer player in m_RemotePlayers.List)
            {
                if (!fullyStreamedRecipients.Contains(player.PlayerId))
                {
                    foreach (BrushStrokeCommand deltaRoot in deltaRoots)
                    {
                        await m_Manager.SendCommandToPlayer(deltaRoot, player.PlayerId);
                    }
                }
            }
            return true;
        }

        private void CompleteOutgoingLiveStroke(
            BrushStrokeCommand command, OutgoingLiveStroke stream,
            HashSet<int> fullyStreamedRecipients)
        {
            Stroke stroke = command.m_Stroke;
            foreach (int playerId in stream.Recipients
                .Where(IsRemotePlayerStillConnected)
                .Where(playerId => !fullyStreamedRecipients.Contains(playerId)))
            {
                m_Manager.RpcLiveStrokeCancel(stream.StreamId, playerId);
            }

            int nextIndex = stream.SentConfirmedPointCount;
            while (nextIndex < stroke.m_ControlPoints.Length)
            {
                int count = Math.Min(
                    NetworkingConstants.MaxControlPointsPerChunk,
                    stroke.m_ControlPoints.Length - nextIndex);
                var points = stroke.m_ControlPoints.Skip(nextIndex).Take(count).ToArray();
                foreach (int playerId in fullyStreamedRecipients
                    .Where(IsRemotePlayerStillConnected))
                {
                    m_Manager.RpcLiveStrokeConfirmed(
                        stream.StreamId, nextIndex, points, playerId);
                }
                nextIndex += count;
            }

            TagStrokeWithLocalContributor(stroke);
            foreach (int playerId in fullyStreamedRecipients
                .Where(IsRemotePlayerStillConnected))
            {
                Debug.Log(
                    $"[LiveStrokeCommand] Send complete stream={stream.StreamId} " +
                    $"command={command.Guid} parent={command.ParentGuid} " +
                    $"children={command.ChildrenCount} seed={stroke.m_Seed} " +
                    $"points={stroke.m_ControlPoints.Length} player={playerId}.");
                m_Manager.RpcLiveStrokeComplete(
                    stream.StreamId, stroke.m_ControlPoints.Length,
                    stroke.m_Flags,
                    command.Guid, (int)command.NetworkTimestamp,
                    command.ParentGuid, command.ChildrenCount, playerId);
            }

            RemoveOutgoingLiveStroke(stream);
            m_RetainedLiveStrokeCommands[command.Guid] = new RetainedLiveStrokeCommand
            {
                Command = command,
                ExpiresAt = Time.realtimeSinceStartup + k_LiveStrokeRepairRetentionSeconds,
            };
        }

        private static IEnumerable<BrushStrokeCommand> EnumerateBrushStrokeCommands(
            BaseCommand command)
        {
            if (command is BrushStrokeCommand brushStroke)
            {
                yield return brushStroke;
            }
            foreach (BaseCommand child in command.Children)
            {
                foreach (BrushStrokeCommand descendant in
                    EnumerateBrushStrokeCommands(child))
                {
                    yield return descendant;
                }
            }
        }

        public void SendLiveStrokeRepair(Guid streamId, Guid commandGuid, int playerId)
        {
            if (!IsRemotePlayerStillConnected(playerId) ||
                !m_RetainedLiveStrokeCommands.TryGetValue(
                    commandGuid, out RetainedLiveStrokeCommand retained))
            {
                Debug.LogWarning(
                    $"[LiveStrokeStreaming] Repair unavailable for stream {streamId}, command {commandGuid}.");
                return;
            }
            SendCommandToPlayer(retained.Command, playerId);
        }

        public void RequestLiveStrokeRepair(
            Guid streamId, Guid commandGuid, int sourcePlayerId)
        {
            if (State == ConnectionState.IN_ROOM && m_Manager != null)
            {
                m_Manager.RpcRequestLiveStrokeRepair(
                    streamId, commandGuid, sourcePlayerId);
            }
        }

        public void DeclineLiveStroke(Guid streamId, int sourcePlayerId)
        {
            if (State == ConnectionState.IN_ROOM && m_Manager != null)
            {
                m_Manager.RpcLiveStrokeDeclined(streamId, sourcePlayerId);
            }
        }

        public void ReceiveLiveStrokeDeclined(Guid streamId, int playerId)
        {
            if (m_OutgoingLiveStrokesById.TryGetValue(
                    streamId, out OutgoingLiveStroke stream) &&
                stream.Recipients.Remove(playerId))
            {
                Debug.LogWarning(
                    $"[LiveStrokeCapacity] Player {playerId} declined stream " +
                    $"{streamId}; the completed stroke group will be sent instead.");
            }
        }

        private void RemoveOutgoingLiveStroke(OutgoingLiveStroke stream)
        {
            m_OutgoingLiveStrokes.Remove(stream.Pointer);
            m_OutgoingLiveStrokesBySeed.Remove(stream.Seed);
            m_OutgoingLiveStrokesById.Remove(stream.StreamId);
        }

        private void ExpireRetainedLiveStrokeCommands()
        {
            float now = Time.realtimeSinceStartup;
            foreach (Guid commandGuid in m_RetainedLiveStrokeCommands
                .Where(pair => pair.Value.ExpiresAt <= now)
                .Select(pair => pair.Key)
                .ToList())
            {
                m_RetainedLiveStrokeCommands.Remove(commandGuid);
            }
        }

        public void PlaceStrokeOnContributorLayer(Stroke stroke)
        {
            if (stroke == null || stroke.m_MultiplayerContributorId == Guid.Empty)
            {
                return;
            }

            stroke.m_IntendedCanvas = GetOrCreateContributorLayer(
                stroke.m_MultiplayerContributorId,
                stroke.m_MultiplayerContributorNickname);
        }

        void OnPlayerLeft(int id)
        {
            m_LiveStrokeCapablePlayers.Remove(id);
            m_PendingSceneSyncPlayerIds.Remove(id);
            m_LiveStrokePointerCapacities.Remove(id);
            m_Manager?.RemoveLiveStrokePreviewsForPlayer(id);
            foreach (OutgoingLiveStroke stream in m_OutgoingLiveStrokes.Values)
            {
                stream.Recipients.Remove(id);
            }
            if (m_LocalPlayer.PlayerId == id)
            {
                m_LocalPlayer = null;
                Debug.Log("Possible to get here!");
                return;
            }

            m_RemotePlayers.RemovePlayerById(id);

            // Reassign Ownership if needed 
            // Check if any remaining player is the room owner
            bool anyRoomOwner = m_RemotePlayers.List.Any(player => m_Manager.GetPlayerRoomOwnershipStatus(player.PlayerId))
                                || isUserRoomOwner;

            // If there's still a room owner, no reassignment is needed
            if (anyRoomOwner) return;

            // If there are no other players left, the local player becomes the room owner
            if (m_RemotePlayers.List.Count == 0)
            {
                isUserRoomOwner = true;
                return;
            }

            // Since There are other players left
            // Determine the new room owner by the lowest PlayerId
            var allPlayers = new List<RemotePlayer> { new RemotePlayer { PlayerId = m_LocalPlayer.PlayerId } };
            allPlayers.AddRange(m_RemotePlayers.List);

            // Find the player with the lowest PlayerId
            var newOwner = allPlayers.OrderBy(player => player.PlayerId).First();

            // If the new owner is the local player, set the flag
            if (m_LocalPlayer.PlayerId == newOwner.PlayerId) isUserRoomOwner = true;

        }

        public async void OnCommandPerformed(BaseCommand command)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                foreach (BrushStrokeCommand brushCommand in
                    EnumerateBrushStrokeCommands(command))
                {
                    TagStrokeWithLocalContributor(brushCommand.m_Stroke);
                }
                if (command is BrushStrokeCommand brushStrokeCommand)
                {
                    if (await TryCompleteOutgoingLiveStrokeTree(brushStrokeCommand))
                    {
                        return;
                    }
                }
                await m_Manager.PerformCommand(command);
            }
        }

        public async Task<bool> PublishManualColocationReference(
            ManualColocationReference reference)
        {
            if (State != ConnectionState.IN_ROOM ||
                !isUserRoomOwner ||
                m_Manager == null)
            {
                Debug.LogWarning(
                    "[ManualColocation] Only the room owner can publish a reference.");
                return false;
            }

            return await m_Manager.RpcPublishManualColocationReference(reference);
        }

        public async Task<bool> SendManualColocationReferenceToPlayer(
            ManualColocationReference reference,
            int playerId)
        {
            if (State != ConnectionState.IN_ROOM ||
                !isUserRoomOwner ||
                m_Manager == null)
            {
                return false;
            }

            bool sent =
                await m_Manager.RpcSendManualColocationReferenceToPlayer(
                    reference, playerId);
            if (sent)
            {
                Debug.Log(
                    $"[ManualColocation] Sent revision {reference.Revision} to late joiner {playerId}.");
            }
            return sent;
        }

        public void ReceiveManualColocationReference(
            ManualColocationReference reference)
        {
            ManualColocationManager.m_Instance?.ApplyReceivedReference(reference);
        }

        public async void SendCommandToPlayer(BaseCommand command, int playerID)
        {
            if (State == ConnectionState.IN_ROOM)
            {
                await m_Manager.SendCommandToPlayer(command, playerID);
            }
        }

        public bool SyncSketchTimeToPlayer(uint sketchTimeMs, int playerId)
        {
            return State == ConnectionState.IN_ROOM &&
                m_Manager != null &&
                IsPlayerLiveStrokeCompatible(playerId) &&
                m_Manager.RpcSyncSketchTimeToPlayer(sketchTimeMs, playerId);
        }

        internal static double CalculateSynchronizedSketchTime(
            double localSketchTime, uint sourceSketchTimeMs)
        {
            return Math.Max(localSketchTime, sourceSketchTimeMs / 1000.0);
        }

        public void ApplySketchTimeSync(uint sourceSketchTimeMs)
        {
            App.Instance.CurrentSketchTime = CalculateSynchronizedSketchTime(
                App.Instance.CurrentSketchTime, sourceSketchTimeMs);
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
            foreach (RemotePlayer player in m_RemotePlayers.List.ToList())
            {
                m_Manager?.RemoveLiveStrokePreviewsForPlayer(player.PlayerId);
            }
            m_OutgoingLiveStrokes.Clear();
            m_OutgoingLiveStrokesBySeed.Clear();
            m_OutgoingLiveStrokesById.Clear();
            m_RetainedLiveStrokeCommands.Clear();
            m_LocalPlayer = null;// Clean up local player reference
            m_RemotePlayers.ClearList();// Clean up remote player references
            m_LiveStrokeCapablePlayers.Clear();
            m_PendingSceneSyncPlayerIds.Clear();
            m_LiveStrokePointerCapacities.Clear();
            IsLiveStrokeStreamingEnabled = false;
            IsLiveStrokeRoomStateReady = false;
            LastError = null;
            State = ConnectionState.DISCONNECTED;
            StateUpdated?.Invoke(State);
            Disconnected?.Invoke();// Invoke the Disconnected event
        }

        public void StartSpeaking()
        {
            if (m_IsVoiceEnabled)
            {
                m_VoiceManager?.StartSpeaking();
            }
        }

        public void StopSpeaking()
        {
            m_VoiceManager?.StopSpeaking();
        }

        public Task<bool> SetVoiceEnabled(bool enabled)
        {
            m_IsLocalVoiceEnabled = enabled;
            return ApplyVoiceEnabled(m_IsLocalVoiceEnabled && m_IsRoomVoiceEnabled);
        }

        public async Task<bool> SetRoomVoiceEnabled(bool enabled)
        {
            if (!isUserRoomOwner || State != ConnectionState.IN_ROOM)
            {
                return false;
            }

            CurrentRoomData.voiceDisabled = !enabled;
            m_IsRoomVoiceEnabled = enabled;
            bool appliedLocally = await ApplyVoiceEnabled(
                m_IsLocalVoiceEnabled && m_IsRoomVoiceEnabled);
            bool sentToRoom = true;
            foreach (int playerId in GetLiveStrokeCompatiblePlayerIds())
            {
                sentToRoom &= await m_Manager.RpcSetRoomVoiceEnabled(enabled, playerId);
            }
            return appliedLocally && sentToRoom;
        }

        public async void ApplyRoomVoiceEnabled(bool enabled)
        {
            CurrentRoomData.voiceDisabled = !enabled;
            m_IsRoomVoiceEnabled = enabled;
            if (!await ApplyVoiceEnabled(m_IsLocalVoiceEnabled && m_IsRoomVoiceEnabled))
            {
                Debug.LogError(
                    $"[MultiplayerVoiceAll] Failed to apply room voice enabled state {enabled}.");
            }
        }

        private async Task<bool> ApplyVoiceEnabled(bool enabled)
        {
            if (m_IsVoiceEnabled == enabled)
            {
                return true;
            }

            m_IsVoiceEnabled = enabled;
            if (m_VoiceManager == null)
            {
                return !enabled;
            }

            if (!enabled)
            {
                m_VoiceManager.StopSpeaking();
                return await m_VoiceManager.Disconnect();
            }

            if (State != ConnectionState.IN_LOBBY && State != ConnectionState.IN_ROOM)
            {
                return true;
            }

            if (!await m_VoiceManager.Connect())
            {
                m_IsVoiceEnabled = false;
                return false;
            }

            if (State == ConnectionState.IN_ROOM)
            {
                if (!await m_VoiceManager.JoinRoom(CurrentRoomData))
                {
                    m_IsVoiceEnabled = false;
                    await m_VoiceManager.Disconnect();
                    return false;
                }
                m_VoiceManager.StartSpeaking();
            }

            return true;
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

        public bool HasRemotePlayersInRoom()
        {
            return State == ConnectionState.IN_ROOM && m_RemotePlayers != null && m_RemotePlayers.List.Count > 0;
        }

        public bool IsUserRoomOwner()
        {
            return isUserRoomOwner;
        }

        public bool IsPlayerRoomOwner(int playerId)
        {
            if (playerId == LocalPlayerId)
            {
                return isUserRoomOwner;
            }
            return m_Manager?.GetPlayerRoomOwnershipStatus(playerId) ?? false;
        }

        public bool IsRemotePlayerStillConnected(int playerId)
        {
            if (m_RemotePlayers.List.Any(player => player.PlayerId == playerId)) return true;
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
