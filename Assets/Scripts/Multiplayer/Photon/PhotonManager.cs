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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using TiltBrush;
using UnityEngine.SceneManagement;


namespace OpenBrush.Multiplayer
{
    public class PhotonManager : IDataConnectionHandler, INetworkRunnerCallbacks
    {
        private NetworkRunner m_Runner;
        private MultiplayerManager m_Manager;
        private List<PlayerRef> m_PlayersSpawning;
        private PhotonPlayerRig m_LocalPlayer;
        private FusionAppSettings m_PhotonAppSettings;
        private int sequenceNumber = 0;
        public event Action Disconnected;

        public ConnectionUserInfo UserInfo { get; set; }
        public ConnectionState State { get; private set; }
        public string LastError { get; private set; }

        public PhotonManager(MultiplayerManager manager)
        {
            m_Manager = manager;
            m_PlayersSpawning = new List<PlayerRef>();

            Init();

            m_PhotonAppSettings = new FusionAppSettings
            {
                AppIdFusion = App.Config.PhotonFusionSecrets.ClientId,
                FixedRegion = "",
            };
        }

        public async Task<bool> Init()
        {
            try
            {
                State = ConnectionState.INITIALIZING;
                var runnerGO = new GameObject("Photon Network Components");
                m_Runner = runnerGO.AddComponent<NetworkRunner>();
                m_Runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                m_Runner.ProvideInput = true;
                m_Runner.AddCallbacks(this);

                Log.LogLevel = Fusion.LogType.Error;

            }
            catch (Exception ex)
            {
                State = ConnectionState.ERROR;
                LastError = $"[PhotonManager] Failed to Initialize lobby: {ex.Message}";
                ControllerConsoleScript.m_Instance.AddNewLine(LastError);
                return false;
            }

            ControllerConsoleScript.m_Instance.AddNewLine("[PhotonManager] Runner Initialized");
            State = ConnectionState.INITIALIZED;
            return true;
        }

        public void Update()
        {
            List<PlayerRef> copy = m_PlayersSpawning.ToList();
            foreach (var player in copy)
            {
                NetworkObject newPlayer = m_Runner.GetPlayerObject(player);
                if (newPlayer != null)
                {
                    RemotePlayer newRemotePlayer = new RemotePlayer
                    {
                        PlayerId = player.RawEncoded,
                        Nickname = GetPlayerNickname(player.RawEncoded),
                        TransientData = newPlayer.GetComponent<PhotonPlayerRig>(),
                        PlayerGameObject = newPlayer.gameObject
                    };

                    m_Manager.remotePlayerJoined?.Invoke(newRemotePlayer);
                    m_PlayersSpawning.Remove(player);
                }
            }
        }

        #region IConnectionHandler Methods

        public async Task<bool> Connect()
        {
            State = ConnectionState.CONNECTING;

            await Task.Yield();

            var result = await m_Runner.JoinSessionLobby(
                SessionLobby.Shared,
                customAppSettings: m_PhotonAppSettings,
                useDefaultCloudPorts: App.UserConfig.Flags.UseDefaultPhotonCloudPorts);

            if (result.Ok)
            {
                State = ConnectionState.IN_LOBBY;
                ControllerConsoleScript.m_Instance.AddNewLine("[PhotonManager] Connected to lobby");
            }
            else
            {
                State = ConnectionState.ERROR;
                LastError = $"[PhotonManager] Failed to join lobby: {result.ErrorMessage}";
                ControllerConsoleScript.m_Instance.AddNewLine(LastError);
            }

            return result.Ok;
        }

        public async Task<bool> JoinRoom(RoomCreateData roomCreateData)
        {

            if (m_Runner == null) Init();

            State = ConnectionState.JOINING_ROOM;

            var sceneRef = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(sceneRef, LoadSceneMode.Single);

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = roomCreateData.roomName,
                CustomPhotonAppSettings = m_PhotonAppSettings,
                PlayerCount = roomCreateData.maxPlayers != 0 ? roomCreateData.maxPlayers : null,
                SceneManager = m_Runner.gameObject.GetComponent<NetworkSceneManagerDefault>(),
                Scene = sceneInfo, // Pass the configured NetworkSceneInfo
                IsOpen = true,
                IsVisible = !roomCreateData.@private,
                UseDefaultPhotonCloudPorts = App.UserConfig.Flags.UseDefaultPhotonCloudPorts,
            };

            var result = await m_Runner.StartGame(args);
            //m_Runner.ReliableDataSendRate = 60;
            //m_Runner.Config.Network.ReliableDataTransferModes = NetworkConfiguration.ReliableDataTransfers.ClientToClientWithServerProxy;

            if (result.Ok)
            {
                // Verify if the room is actually full
                int currentPlayerCount = m_Runner.SessionInfo.PlayerCount;
                int? maxPlayerCount = m_Runner.SessionInfo.MaxPlayers;
                maxPlayerCount = maxPlayerCount == null ? int.MaxValue : maxPlayerCount;

                if (currentPlayerCount > maxPlayerCount)
                {
                    State = ConnectionState.ERROR;
                    LastError = "[PhotonManager] Room is full.";
                    ControllerConsoleScript.m_Instance.AddNewLine(LastError);
                    Disconnect();
                    return false;
                }

                State = ConnectionState.IN_ROOM;
                ControllerConsoleScript.m_Instance.AddNewLine("[PhotonManager] Joined Room");
                UserInfo = new ConnectionUserInfo { 
                    Nickname = UserInfo.Nickname,
                    UserId = m_Runner.UserId,
                    Role = UserInfo.Role,
                };
            }
            else
            {
                State = ConnectionState.ERROR;
                LastError = $"[PhotonManager] Failed to join Room: {result.ErrorMessage}";
                ControllerConsoleScript.m_Instance.AddNewLine(LastError);
            }

            return result.Ok;

        }

        public async Task<bool> Disconnect()
        {
            State = ConnectionState.DISCONNECTING;

            if (m_Runner != null)
            {

                if (m_LocalPlayer != null)
                {
                    m_Runner.Despawn(m_LocalPlayer.Object);
                    m_LocalPlayer = null;
                }
                m_PlayersSpawning.Clear();

                await m_Runner.Shutdown(forceShutdownProcedure: false);
                GameObject.Destroy(m_Runner.gameObject);

                if (m_Runner.IsShutdown)
                {
                    State = ConnectionState.DISCONNECTED;
                    ControllerConsoleScript.m_Instance.AddNewLine("[PhotonManager] Disconnected successfully");
                    UserInfo = new ConnectionUserInfo
                    {
                        Nickname = UserInfo.Nickname,
                        UserId = m_Runner.UserId,
                        Role = UserInfo.Role,
                    };
                }
                else
                {
                    State = ConnectionState.ERROR;
                    LastError = $"[PhotonManager] Failed to disconnect";
                    ControllerConsoleScript.m_Instance.AddNewLine(LastError);
                }

                return m_Runner.IsShutdown;
            }
            return true;
        }

        public async Task<bool> LeaveRoom(bool force)
        {

            if (m_Runner != null)
            {
                bool success = await Disconnect();
                if (!success) return false;
                success = await Connect();
                if (!success) return false;
                return true;
            }
            return false;

        }

        #endregion

        #region IDataConnectionHandler Methods

        public int GetPlayerCount()
        {
            if (m_Runner != null)
            {
                return m_Runner.SessionInfo.PlayerCount;
            }
            return 0;
        }

        public bool IsLocalPlayerRoomOwner()
        {
            return m_Runner != null &&
                m_Runner.IsRunning &&
                m_Runner.IsSharedModeMasterClient;
        }

        public int GetNetworkedTimestampMilliseconds()
        {
            int tickRate = m_Runner.TickRate; // Access TickRate from Config directly
            int networkTimeMilliseconds = (int)((m_Runner.Tick * 1000) / (double)tickRate); // Use m_Runner.Tick directly
            return networkTimeMilliseconds;
        }

        public bool GetPlayerRoomOwnershipStatus(int playerId)
        {
            if (m_Runner == null)
            {
                return false;
            }

            PlayerRef player = PlayerRef.FromEncoded(playerId);
            PhotonPlayerRig playerRig =
                m_Runner.GetPlayerObject(player)?.GetComponent<PhotonPlayerRig>();
            return playerRig != null &&
                   playerRig.Object != null &&
                   playerRig.Object.IsValid &&
                   playerRig.IsRoomOwner;
        }

        public string GetPlayerNickname(int playerId)
        {
            var remotePlayer = m_PlayersSpawning
                .Select(playerRef => m_Runner.GetPlayerObject(playerRef)?.GetComponent<PhotonPlayerRig>())
                .FirstOrDefault(playerRig => playerRig != null && playerRig.PlayerId == playerId);

            if (remotePlayer != null && remotePlayer.Object != null && remotePlayer.Object.IsValid)
                return remotePlayer.PersistentNickname;
            else return "";
        }

        public GameObject GetPlayerPrefab(int playerId)
        {
            if (m_Runner == null) return null;

            PlayerRef player = PlayerRef.FromEncoded(playerId);

            NetworkObject playerNetworkObject = m_Runner.GetPlayerObject(player);
            if (playerNetworkObject != null) return playerNetworkObject.gameObject;

            else
            {
                Debug.LogWarning($"No NetworkObject found for PlayerRef: {player.RawEncoded}");
                return null;
            }
        }

        public async Task<bool> PerformCommand(BaseCommand command)
        {
            await Task.Yield();
            if (m_Runner == null || !m_Runner.IsRunning)
            {
                return false;
            }

            bool success = true;
            foreach (PlayerRef playerRef in m_Runner.ActivePlayers
                .Where(player => player != m_Runner.LocalPlayer))
            {
                bool useEnrichedStrokeTransport =
                    m_Manager.IsPlayerLiveStrokeCompatible(playerRef.RawEncoded);
                success &= ProcessCommand(
                    command, playerRef, rebaseTimestamps: true,
                    useEnrichedStrokeTransport);
            }
            return success;
        }

        public async Task<bool> SendCommandToPlayer(BaseCommand command, int playerId)
        {
            await Task.Yield();
            PlayerRef playerRef = PlayerRef.FromEncoded(playerId);
            bool useEnrichedStrokeTransport =
                m_Manager.IsPlayerLiveStrokeCompatible(playerId);
            return ProcessCommand(
                command, playerRef, rebaseTimestamps: false,
                useEnrichedStrokeTransport);
        }

        public bool RpcSyncSketchTimeToPlayer(uint sketchTimeMs, int playerId)
        {
            if (m_Runner == null || !m_Runner.IsRunning)
            {
                return false;
            }

            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            PhotonRPCBatcher.EnqueueRPC(() =>
            {
                PhotonRPC.RPC_SyncSketchTime(m_Runner, sketchTimeMs, targetPlayer);
            });
            return true;
        }

        public async Task<bool> CheckCommandReception(BaseCommand command, int playerId)
        {
            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_CheckCommand(m_Runner, command.Guid, m_Runner.LocalPlayer, targetPlayer); });
            return await PhotonRPC.WaitForAcknowledgment(command.Guid);
        }

        public async Task<bool> CheckStrokeReception(Stroke stroke, int playerId)
        {
            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_CheckStroke(m_Runner, stroke.m_Guid, m_Runner.LocalPlayer, targetPlayer); });
            return await PhotonRPC.WaitForAcknowledgment(stroke.m_Guid);
        }

        public async Task<bool> UndoCommand(BaseCommand command)
        {
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_Undo(m_Runner, command.GetType().ToString()); });
            await Task.Yield();
            return true;
        }

        public async Task<bool> RedoCommand(BaseCommand command)
        {
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_Redo(m_Runner, command.GetType().ToString());});
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcSyncToSharedAnchor(string uuid)
        {
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_SyncToSharedAnchor(m_Runner, uuid); });
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcPublishManualColocationReference(
            ManualColocationReference reference)
        {
            if (m_Runner == null || !m_Runner.IsRunning)
            {
                return false;
            }

            var networkReference =
                new NetworkManualColocationReference(reference);
            PhotonRPCBatcher.EnqueueRPC(() =>
            {
                PhotonRPC.RPC_ManualColocationReference(
                    m_Runner, networkReference);
            });
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcSendManualColocationReferenceToPlayer(
            ManualColocationReference reference,
            int playerId)
        {
            if (m_Runner == null || !m_Runner.IsRunning)
            {
                return false;
            }

            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            var networkReference =
                new NetworkManualColocationReference(reference);
            PhotonRPCBatcher.EnqueueRPC(() =>
            {
                PhotonRPC.RPC_ManualColocationReference(
                    m_Runner, networkReference, targetPlayer);
            });
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcTransferRoomOwnership(int playerId, RemotePlayerSettings[] playerSettings, RoomCreateData currentRoomData)
        {
            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);

            // Convert RemotePlayerSettings[] to NetworkPlayerSettings[]
            NetworkPlayerSettings[] networkSettings = new NetworkPlayerSettings[playerSettings.Length];
            for (int i = 0; i < playerSettings.Length; i++)
            {
                networkSettings[i] = new NetworkPlayerSettings(
                    playerSettings[i].m_PlayerId,
                    playerSettings[i].m_IsMutedForAll,
                    playerSettings[i].m_IsViewOnly
                );
            }

            var roomData = new NetworkRoomSettings(currentRoomData);

            PhotonRPCBatcher.EnqueueRPC(() =>
            {
                PhotonRPC.RPC_TransferRoomOwnership(m_Runner, targetPlayer, networkSettings, roomData);
            });

            return true;
        }


        public async Task<bool> RpcSetUserViewOnlyMode(bool value,int playerId)
        {
            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_SetUserViewOnlyMode(m_Runner,value, targetPlayer); });
            return true;
        }

        public async Task<bool> RpcSetRoomVoiceEnabled(bool enabled, int playerId)
        {
            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_SetRoomVoiceEnabled(m_Runner, enabled, targetPlayer); });
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcAdvertiseLiveStrokeSupport(int maxStreamedPointers)
        {
            if (m_Runner == null || !m_Runner.IsRunning)
            {
                return false;
            }

            // Capability discovery uses a pre-streaming RPC. Older clients safely ignore
            // the unknown command name instead of receiving an RPC they cannot resolve.
            PhotonRPC.Send_LiveStrokeCapability(
                m_Runner, maxStreamedPointers);
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcSetLiveStrokeRoomState(
            bool enabled, int playerId)
        {
            if (m_Runner == null || !m_Runner.IsRunning || playerId < 0)
            {
                return false;
            }

            PhotonRPC.RPC_LiveStrokeRoomState(
                m_Runner, enabled, PlayerRef.FromEncoded(playerId));
            await Task.Yield();
            return true;
        }

        public bool RpcLiveStrokeStart(
            Guid streamId, Stroke stroke, StrokeTimeSessionMetadata sourceTimeSession,
            Guid contributorId, string contributorNickname, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId) || stroke == null ||
                stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length != 1 ||
                stroke.m_ControlPointsToDrop == null ||
                stroke.m_ControlPointsToDrop.Length != 1 || sourceTimeSession == null)
            {
                return false;
            }

            PhotonRPC.RPC_LiveStrokeStart(
                m_Runner, streamId, new NetworkedLiveStrokeStart().Init(stroke),
                contributorId, contributorNickname,
                sourceTimeSession.StartUtcMs,
                sourceTimeSession.StartSketchTimeMs,
                PlayerRef.FromEncoded(playerId));
            return true;
        }

        public bool RpcLiveStrokeConfirmed(
            Guid streamId, int firstControlPointIndex,
            PointerManager.ControlPoint[] confirmedControlPoints, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId))
            {
                return false;
            }

            var networkedPoints = confirmedControlPoints
                .Select(point => new NetworkedControlPoint().Init(point))
                .ToArray();
            PhotonRPC.RPC_LiveStrokeConfirmed(
                m_Runner, streamId, firstControlPointIndex, networkedPoints,
                PlayerRef.FromEncoded(playerId));
            return true;
        }

        public bool RpcLiveStrokeProvisionalTail(
            Guid streamId, uint sequence, int confirmedControlPointCount,
            PointerManager.ControlPoint provisionalTail, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId))
            {
                return false;
            }

            PhotonRPC.RPC_LiveStrokeProvisionalTail(
                m_Runner, streamId, sequence, confirmedControlPointCount,
                new NetworkedControlPoint().Init(provisionalTail),
                PlayerRef.FromEncoded(playerId));
            return true;
        }

        public bool RpcLiveStrokeComplete(
            Guid streamId, int finalControlPointCount,
            SketchMemoryScript.StrokeFlags strokeFlags, Guid commandGuid,
            int timestamp, Guid parentGuid, int childCount, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId))
            {
                return false;
            }

            PhotonRPC.RPC_LiveStrokeComplete(
                m_Runner, streamId, finalControlPointCount,
                (uint)strokeFlags, commandGuid, timestamp, parentGuid, childCount,
                PlayerRef.FromEncoded(playerId));
            return true;
        }

        public bool RpcLiveStrokeCancel(Guid streamId, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId))
            {
                return false;
            }
            PhotonRPC.RPC_LiveStrokeCancel(
                m_Runner, streamId, PlayerRef.FromEncoded(playerId));
            return true;
        }

        public bool RpcLiveStrokeDeclined(Guid streamId, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId))
            {
                return false;
            }
            PhotonRPC.RPC_LiveStrokeDeclined(
                m_Runner, streamId, PlayerRef.FromEncoded(playerId));
            return true;
        }

        public bool RpcRequestLiveStrokeRepair(
            Guid streamId, Guid commandGuid, int playerId)
        {
            if (!CanSendLiveStrokeTo(playerId))
            {
                return false;
            }
            PhotonRPC.RPC_LiveStrokeRepairRequest(
                m_Runner, streamId, commandGuid, PlayerRef.FromEncoded(playerId));
            return true;
        }

        public void RemoveLiveStrokePreviewsForPlayer(int playerId)
        {
            PhotonRPC.RemoveLiveStrokePreviewsForPlayer(playerId);
        }

        private bool CanSendLiveStrokeTo(int playerId)
        {
            return m_Runner != null && m_Runner.IsRunning && playerId >= 0;
        }

        public async Task<bool> RpcKickPlayerOut(int playerId)
        {
            PlayerRef targetPlayer = PlayerRef.FromEncoded(playerId);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_DisconnectRemoteUser(m_Runner, targetPlayer); });
            return true;
        }

        public void SendLargeDataToPlayer(int playerId, byte[] largeData, int percentage)
        {
            sequenceNumber++;
            PlayerRef playerRef = PlayerRef.FromEncoded(playerId);
            int dataHash = largeData.GetHashCode();
            var key = ReliableKey.FromInts(playerId, sequenceNumber, dataHash, percentage);
            m_Runner.SendReliableDataToPlayer(playerRef, key, largeData);
        }

        public bool RpcMutePlayer(bool mute, int playerId)
        {
            PhotonRPCBatcher.EnqueueRPC(() =>
            {
                PhotonRPC.RPC_MutePlayer(m_Runner, mute, playerId);
            });
            return true;
        }

        #endregion

        #region Command Methods
        private bool ProcessCommand(
            BaseCommand command, PlayerRef playerRef, bool rebaseTimestamps,
            bool useEnrichedStrokeTransport)
        {
            bool success = true;

            switch (command)
            {
                case BrushStrokeCommand:
                    success &= CommandBrushStroke(
                        command as BrushStrokeCommand, playerRef, rebaseTimestamps,
                        useEnrichedStrokeTransport);
                    break;
                case DeleteStrokeCommand:
                    success &= CommandDeleteStroke(command as DeleteStrokeCommand, playerRef);
                    break;
                case SwitchEnvironmentCommand:
                    success &= CommandSwitchEnvironment(command as SwitchEnvironmentCommand, playerRef);
                    break;
                case MoveWidgetCommand moveCommand:
                    // Widget manipulation generates a MoveWidgetCommand every frame. Send_BaseCommand
                    // carries no transform data, so each one only adds an empty, unmergeable command
                    // to every remote peer's undo stack - broadcasting them per frame buries a remote
                    // user's own undo history. Send just the settled one.
                    // NOTE: this does not make widget movement replicate; it never has. See the
                    // payload of CommandBase / PhotonRPC.BaseCommand.
                    if (moveCommand.IsFinal)
                    {
                        success &= CommandBase(command, playerRef);
                    }
                    else
                    {
                        success = true;
                    }
                    break;
                case BaseCommand:
                    success &= CommandBase(command, playerRef);
                    break;
                default:
                    success = false;
                    break;
            }

            if (command.ChildrenCount > 0)
            {
                foreach (var child in command.Children)
                {
                    if (child.ParentGuid == Guid.Empty)
                    {
                        child.SetParent(command);
                    }
                    success &= ProcessCommand(
                        child, playerRef, rebaseTimestamps,
                        useEnrichedStrokeTransport);
                }
            }

            return success;
        }

        private bool CommandBrushStroke(
            BrushStrokeCommand command, PlayerRef playerRef, bool rebaseTimestamps,
            bool useEnrichedStrokeTransport)
        {
            var stroke = command.m_Stroke;
            bool hasSourceTimeSession = SketchMemoryScript.m_Instance.TryGetStrokeTimeSession(
                stroke, out StrokeTimeSessionMetadata sourceTimeSession);
            int maxPointsPerChunk = NetworkingConstants.MaxControlPointsPerChunk;

            int totalPoints = stroke.m_ControlPoints.Length;

            // Calculate how many chunks in total we need, including the initial one.
            int numberOfChunks = (int)Math.Ceiling((double)totalPoints / maxPointsPerChunk);

            // If we can fit everything in a single message:
            if (numberOfChunks == 1)
            {
                // Send it all at once as a full stroke
                if (useEnrichedStrokeTransport &&
                    stroke.m_MultiplayerContributorId != Guid.Empty)
                {
                    PhotonRPCBatcher.EnqueueRPC(() =>
                    { PhotonRPC.Send_BrushStrokeFullContributor(
                        m_Runner, new NetworkedStroke().Init(stroke), command.Guid,
                        (int)command.NetworkTimestamp, rebaseTimestamps,
                        stroke.m_MultiplayerContributorId,
                        stroke.m_MultiplayerContributorNickname,
                        hasSourceTimeSession,
                        sourceTimeSession?.StartUtcMs ?? 0,
                        sourceTimeSession?.StartSketchTimeMs ?? 0,
                        command.ParentGuid, command.ChildrenCount, playerRef); });
                }
                else if (useEnrichedStrokeTransport && hasSourceTimeSession)
                {
                    PhotonRPCBatcher.EnqueueRPC(() =>
                    { PhotonRPC.Send_BrushStrokeFullClock(
                        m_Runner, new NetworkedStroke().Init(stroke), command.Guid,
                        (int)command.NetworkTimestamp, rebaseTimestamps,
                        sourceTimeSession.StartUtcMs,
                        sourceTimeSession.StartSketchTimeMs,
                        command.ParentGuid, command.ChildrenCount, playerRef); });
                }
                else
                {
                    PhotonRPCBatcher.EnqueueRPC(() =>
                    { PhotonRPC.Send_BrushStrokeFull(
                        m_Runner, new NetworkedStroke().Init(stroke), command.Guid,
                        (int)command.NetworkTimestamp, command.ParentGuid,
                        command.ChildrenCount, playerRef); });
                }
                return true;
            }

            // More than one chunk: break it down.

            // Prepare the first chunk
            int firstChunkSize = Math.Min(maxPointsPerChunk, totalPoints);
            var firstStroke = new Stroke(stroke)
            {
                m_ControlPoints = stroke.m_ControlPoints.Take(firstChunkSize).ToArray(),
                m_ControlPointsToDrop = stroke.m_ControlPointsToDrop.Take(firstChunkSize).ToArray()
            };

            var netStroke = new NetworkedStroke().Init(firstStroke);
            var strokeGuid = Guid.NewGuid();

            // Send the initial Begin call
            if (useEnrichedStrokeTransport &&
                stroke.m_MultiplayerContributorId != Guid.Empty)
            {
                PhotonRPCBatcher.EnqueueRPC(() =>
                { PhotonRPC.Send_BrushStrokeBeginContributor(
                    m_Runner, strokeGuid, netStroke, totalPoints,
                    stroke.m_MultiplayerContributorId,
                    stroke.m_MultiplayerContributorNickname, playerRef); });
            }
            else
            {
                PhotonRPCBatcher.EnqueueRPC(() =>
                { PhotonRPC.Send_BrushStrokeBegin(
                    m_Runner, strokeGuid, netStroke, totalPoints, playerRef); });
            }

            // Send the middle "Continue" chunks (if any)
            for (int chunkIndex = 1; chunkIndex < numberOfChunks; chunkIndex++)
            {
                int offset = chunkIndex * maxPointsPerChunk;
                int chunkSize = Math.Min(maxPointsPerChunk, totalPoints - offset);

                // Extract this chunk of control points and drop flags
                var controlPoints = stroke.m_ControlPoints.Skip(offset).Take(chunkSize).ToArray();
                var dropPoints = stroke.m_ControlPointsToDrop.Skip(offset).Take(chunkSize).ToArray();

                // Convert to NetworkedControlPoint
                var netControlPoints = new NetworkedControlPoint[chunkSize];
                for (int i = 0; i < chunkSize; ++i)
                {
                    netControlPoints[i] = new NetworkedControlPoint().Init(controlPoints[i]);
                }

                PhotonRPCBatcher.EnqueueRPC(() =>
                { PhotonRPC.Send_BrushStrokeContinue(
                    m_Runner, strokeGuid, offset, netControlPoints, dropPoints, playerRef); });
            }

            // After all chunks have been sent, send the Complete call
            if (useEnrichedStrokeTransport && hasSourceTimeSession)
            {
                PhotonRPCBatcher.EnqueueRPC(() =>
                { PhotonRPC.Send_BrushStrokeCompleteClock(
                    m_Runner, strokeGuid, command.Guid, (int)command.NetworkTimestamp,
                    rebaseTimestamps, sourceTimeSession.StartUtcMs,
                    sourceTimeSession.StartSketchTimeMs,
                    command.ParentGuid, command.ChildrenCount, playerRef); });
            }
            else
            {
                PhotonRPCBatcher.EnqueueRPC(() =>
                { PhotonRPC.Send_BrushStrokeComplete(
                    m_Runner, strokeGuid, command.Guid, (int)command.NetworkTimestamp,
                    command.ParentGuid, command.ChildrenCount, playerRef); });
            }

            return true;
        }


        private bool CommandBase(BaseCommand command, PlayerRef playerRef = default)
        {
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.Send_BaseCommand(
                m_Runner, command.Guid, command.ParentGuid, command.ChildrenCount, playerRef); });
            return true;
        }

        private bool CommandDeleteStroke(DeleteStrokeCommand command, PlayerRef playerRef = default)
        {
            string target = playerRef == default
                ? "broadcast"
                : playerRef.RawEncoded.ToString();
            Debug.Log(
                $"[LiveStrokeCommand] Send delete command={command.Guid} " +
                $"parent={command.ParentGuid} children={command.ChildrenCount} " +
                $"seed={command.m_TargetStroke.m_Seed} target={target}.");
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.Send_DeleteStroke(m_Runner, command.m_TargetStroke.m_Seed, command.Guid, (int)command.NetworkTimestamp, command.ParentGuid, command.ChildrenCount, playerRef); });
            return true;
        }

        private bool CommandSwitchEnvironment(SwitchEnvironmentCommand command, PlayerRef playerRef = default)
        {
            Guid environmentGuid = command.m_NextEnvironment.m_Guid;
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.Send_SwitchEnvironment(m_Runner, environmentGuid, command.Guid, (int)command.NetworkTimestamp, command.ParentGuid, command.ChildrenCount, playerRef); });
            return true;
        }
        #endregion

        #region Photon Callbacks

        public void OnConnectedToServer(NetworkRunner runner)
        {
            var rpc = runner.gameObject.AddComponent<PhotonRPC>();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {

            try
            {

                if (player == m_Runner.LocalPlayer)
                {
                    var playerPrefab = Resources.Load("Multiplayer/Photon/PhotonPlayerRig") as GameObject;
                    var playerObj = m_Runner.Spawn(playerPrefab, inputAuthority: m_Runner.LocalPlayer);
                    m_LocalPlayer = playerObj.GetComponent<PhotonPlayerRig>();
                    m_Runner.SetPlayerObject(m_Runner.LocalPlayer, playerObj);
                    m_Manager.localPlayerJoined?.Invoke(player.RawEncoded, m_LocalPlayer);
                }
                else
                {
                    m_PlayersSpawning.Add(player);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in OnPlayerJoined: {ex.Message}");
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            m_Manager.playerLeft?.Invoke(player.RawEncoded);
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            var roomData = new List<RoomData>();
            foreach (var session in sessionList)
            {
                RoomData data = new RoomData()
                {
                    roomName = session.Name,
                    @private = !session.IsVisible,
                    numPlayers = session.PlayerCount,
                    maxPlayers = session.MaxPlayers
                };

                roomData.Add(data);
            }

            m_Manager.roomDataRefreshed?.Invoke(roomData);
        }

        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data)
        {
            //Debug.Log("Server received complete reliable data");

            int percentage;
            key.GetInts(out _, out _, out _, out percentage);
            //Debug.Log($"Data received with percentage: {percentage}%");

            byte[] receivedData = data.Array;
            if (receivedData == null || receivedData.Length == 0)
            {
                Debug.LogWarning("Received data is null or empty.");
                return;
            }

            MultiplayerSceneSync.m_Instance.onLargeDataReceived?.Invoke(receivedData,percentage);
        }


        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {

            //Debug.Log("Server received Partial reliable data");
        }

        #endregion

        #region Unused Photon Callbacks 
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            Disconnected?.Invoke();
        }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }


        #endregion
    }
}

#endif // FUSION_WEAVER
