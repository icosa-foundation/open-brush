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
using UnityEditor;
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
            var copy = m_PlayersSpawning.ToList();
            foreach (var player in copy)
            {
                var newPlayer = m_Runner.GetPlayerObject(player);
                if (newPlayer != null)
                {
                    m_Manager.remotePlayerJoined?.Invoke(player.RawEncoded, newPlayer.GetComponent<PhotonPlayerRig>());
                    m_PlayersSpawning.Remove(player);
                }
            }
        }

        #region IConnectionHandler Methods

        public async Task<bool> Connect()
        {
            State = ConnectionState.CONNECTING;

            await Task.Yield();

            var result = await m_Runner.JoinSessionLobby(SessionLobby.Shared, customAppSettings: m_PhotonAppSettings);

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

                if (currentPlayerCount >= maxPlayerCount)
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

        public int GetNetworkedTimestampMilliseconds()
        {
            int tickRate = m_Runner.TickRate; // Access TickRate from Config directly
            int networkTimeMilliseconds = (int)((m_Runner.Tick * 1000) / (double)tickRate); // Use m_Runner.Tick directly
            return networkTimeMilliseconds;
        }

        public bool GetPlayerRoomOwnershipStatus(int playerId)
        {
            var remotePlayer = m_PlayersSpawning
                .Select(playerRef => m_Runner.GetPlayerObject(playerRef)?.GetComponent<PhotonPlayerRig>())
                .FirstOrDefault(playerRig => playerRig != null && playerRig.PlayerId == playerId);

            if (remotePlayer != null && remotePlayer.Object != null && remotePlayer.Object.IsValid)
                return remotePlayer.IsRoomOwner;
            else return false;
        }

        public async Task<bool> PerformCommand(BaseCommand command)
        {
            await Task.Yield();
            return ProcessCommand(command);
        }

        public async Task<bool> SendCommandToPlayer(BaseCommand command, int playerId)
        {
            await Task.Yield();
            PlayerRef playerRef = PlayerRef.FromEncoded(playerId);
            return ProcessCommand(command, playerRef);
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

        public async Task<bool> RpcStartSyncHistory(int id)
        {
            PlayerRef playerRef = PlayerRef.FromEncoded(id);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_StartHistorySync(m_Runner, playerRef); });
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcHistorySyncComplete(int id)
        {
            PlayerRef playerRef = PlayerRef.FromEncoded(id);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_HistorySyncCompleted(m_Runner, playerRef);});
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcSyncHistoryPercentage(int id, int exp, int snt)
        {
            PlayerRef playerRef = PlayerRef.FromEncoded(id);
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.RPC_HistoryPercentageUpdate(m_Runner, playerRef, exp, snt);});
            await Task.Yield();
            return true;
        }

        public void SendLargeDataToPlayer(int playerId, byte[] largeData)
        {
            sequenceNumber++;
            PlayerRef playerRef = PlayerRef.FromEncoded(playerId);
            int dataHash = largeData.GetHashCode();
            var key = ReliableKey.FromInts(playerId, sequenceNumber, dataHash, 0);
            m_Runner.SendReliableDataToPlayer(playerRef, key, largeData);
        }

        #endregion

        #region Command Methods
        private bool ProcessCommand(BaseCommand command, PlayerRef playerRef = default)
        {
            bool success = true;

            switch (command)
            {
                case BrushStrokeCommand:
                    success &= CommandBrushStroke(command as BrushStrokeCommand, playerRef);
                    break;
                case DeleteStrokeCommand:
                    success &= CommandDeleteStroke(command as DeleteStrokeCommand, playerRef);
                    break;
                case SwitchEnvironmentCommand:
                    success &= CommandSwitchEnvironment(command as SwitchEnvironmentCommand, playerRef);
                    break;
                case BaseCommand:
                    success &= CommandBase(command);
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
                    success &= ProcessCommand(child);
                }
            }

            return success;
        }

        private bool CommandBrushStroke(BrushStrokeCommand command, PlayerRef playerRef = default)
        {
            var stroke = command.m_Stroke;
            int maxPointsPerChunk = NetworkingConstants.MaxControlPointsPerChunk;

            int totalPoints = stroke.m_ControlPoints.Length;

            // Calculate how many chunks in total we need, including the initial one.
            int numberOfChunks = (int)Math.Ceiling((double)totalPoints / maxPointsPerChunk);

            // If we can fit everything in a single message:
            if (numberOfChunks == 1)
            {
                // Send it all at once as a full stroke
                PhotonRPCBatcher.EnqueueRPC(() =>
                { PhotonRPC.Send_BrushStrokeFull( m_Runner,new NetworkedStroke().Init(stroke),command.Guid, (int)command.NetworkTimestamp, command.ParentGuid, command.ChildrenCount ); });
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
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.Send_BrushStrokeBegin( m_Runner, strokeGuid, netStroke, totalPoints);});

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
                { PhotonRPC.Send_BrushStrokeContinue(m_Runner,strokeGuid, offset,netControlPoints,dropPoints);});
            }

            // After all chunks have been sent, send the Complete call
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.Send_BrushStrokeComplete( m_Runner,strokeGuid, command.Guid, (int)command.NetworkTimestamp, command.ParentGuid, command.ChildrenCount ); });

            return true;
        }


        private bool CommandBase(BaseCommand command)
        {
            PhotonRPCBatcher.EnqueueRPC(() =>
            { PhotonRPC.Send_BaseCommand(m_Runner, command.Guid, command.ParentGuid, command.ChildrenCount); });
            return true;
        }

        private bool CommandDeleteStroke(DeleteStrokeCommand command, PlayerRef playerRef = default)
        {
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
                    @private = session.IsOpen,
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

            byte[] receivedData = data.Array;
            if (receivedData == null || receivedData.Length == 0)
            {
                Debug.LogWarning("Received data is null or empty.");
                return;
            }

            MultiplayerSceneSync.m_Instance.onLargeDataReceived?.Invoke(receivedData);
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
