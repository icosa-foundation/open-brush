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

#if FUSION_WEAVER

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using TiltBrush;
using System.Linq;

namespace OpenBrush.Multiplayer
{
    public class PhotonManager : IConnectionHandler, INetworkRunnerCallbacks
    {
        private NetworkRunner m_Runner;

        MultiplayerManager m_Manager;

        List<PlayerRef> m_PlayersSpawning;

        PhotonPlayerRig m_LocalPlayer;

        public PhotonManager(MultiplayerManager manager)
        {
            m_Manager = manager;
            m_PlayersSpawning = new List<PlayerRef>();
        }

        public async Task<bool> Connect()
        {
            if(m_Runner != null)
            {
                GameObject.Destroy(m_Runner);
            }

            var runnerGO = new GameObject("Photon Network Components");

            m_Runner = runnerGO.AddComponent<NetworkRunner>();
            m_Runner.ProvideInput = true;
            m_Runner.AddCallbacks(this);

            var appSettings = new AppSettings
            {
                AppIdFusion = App.Config.PhotonFusionSecrets.ClientId,
                // Need this set for some reason
                FixedRegion = "",
            };

            var args = new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = "OpenBrushMultiplayerTest",
                CustomPhotonAppSettings = appSettings,
                SceneManager = m_Runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
            };

            var result = await m_Runner.StartGame(args);

            return result.Ok;
            
        }

        public bool IsConnected()
        {
            if(m_Runner == null)
            {
                return false;
            }
            return m_Runner.IsRunning;
        }

        public async Task<bool> Disconnect(bool force)
        {
            if(m_Runner != null)
            {
                await m_Runner.Shutdown(forceShutdownProcedure: force);
                return m_Runner.IsShutdown;
            }
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
                    m_Manager.remotePlayerJoined?.Invoke(newPlayer.GetComponent<PhotonPlayerRig>());
                    m_PlayersSpawning.Remove(player);
                }
            }
        }

#region IConnectionHandler Methods
        public async Task<bool> PerformCommand(BaseCommand command)
        {
            await Task.Yield();
            return ProcessCommand(command);;
        }

        public async Task<bool> UndoCommand(BaseCommand command)
        {
            PhotonRPC.RPC_Undo(m_Runner, command.GetType().ToString());
            await Task.Yield();
            return true;
        }

        public async Task<bool> RedoCommand(BaseCommand command)
        {
            PhotonRPC.RPC_Redo(m_Runner, command.GetType().ToString());
            await Task.Yield();
            return true;
        }

        public async Task<bool> RpcSyncToSharedAnchor(string uuid)
        {
            PhotonRPC.RPC_SyncToSharedAnchor(m_Runner, uuid);
            await Task.Yield();
            return true;
        }
#endregion

#region Command Methods
        private bool ProcessCommand(BaseCommand command)
        {
            bool success = true;
            switch(command)
            {
                case BrushStrokeCommand:
                    success = CommandBrushStroke(command as BrushStrokeCommand);
                    break;
                case DeleteStrokeCommand:
                    success = CommandDeleteStroke(command as DeleteStrokeCommand);
                    break;
                case BaseCommand:
                    success = CommandBase(command);
                    break;
                default:
                    // Don't know how to process this command
                    success = false;
                    break;
            }

            if(command.ChildrenCount > 0)
            {
                foreach(var child in command.Children)
                {
                    success &= ProcessCommand(child);
                }
            }

            return success;
        }

        private bool CommandBrushStroke(BrushStrokeCommand command)
        {
            var stroke = command.m_Stroke;

            if (stroke.m_ControlPoints.Length > 128)
            {
                // Split and Send
                int numSplits = stroke.m_ControlPoints.Length / 128;

                var firstStroke = new Stroke(stroke)
                {
                    m_ControlPoints = stroke.m_ControlPoints.Take(128).ToArray(),
                    m_ControlPointsToDrop = stroke.m_ControlPointsToDrop.Take(128).ToArray()
                };

                var netStroke = new NetworkedStroke().Init(firstStroke);

                var strokeGuid = Guid.NewGuid();

                // First Stroke
                PhotonRPC.RPC_BrushStrokeBegin(m_Runner, strokeGuid, netStroke, stroke.m_ControlPoints.Length);

                // Middle
                for (int rounds = 1; rounds < numSplits + 1; ++rounds)
                {
                    var controlPoints = stroke.m_ControlPoints.Skip(rounds*128).Take(128).ToArray();
                    var dropPoints = stroke.m_ControlPointsToDrop.Skip(rounds*128).Take(128).ToArray();

                    var netControlPoints = new NetworkedControlPoint[controlPoints.Length];

                    for (int point = 0; point < controlPoints.Length; ++ point)
                    {
                        netControlPoints[point] = new NetworkedControlPoint().Init(controlPoints[point]);
                    }

                    PhotonRPC.RPC_BrushStrokeContinue(m_Runner, strokeGuid, rounds * 128, netControlPoints, dropPoints);
                }

                // End
                PhotonRPC.RPC_BrushStrokeComplete(m_Runner, strokeGuid, command.Guid, command.ParentGuid, command.ChildrenCount);
            }
            else
            {
                // Can send in one.
                PhotonRPC.RPC_BrushStrokeFull(m_Runner, new NetworkedStroke().Init(command.m_Stroke), command.Guid, command.ParentGuid, command.ChildrenCount);
            }
            return true;
        }

        private bool CommandBase(BaseCommand command)
        {
            PhotonRPC.RPC_BaseCommand(m_Runner, command.Guid, command.ParentGuid, command.ChildrenCount);
            return true;
        }

        private bool CommandDeleteStroke(DeleteStrokeCommand command)
        {
            PhotonRPC.RPC_DeleteStroke(m_Runner, command.m_TargetStroke.m_Seed, command.Guid, command.ParentGuid, command.ChildrenCount);
            return true;
        }
#endregion

#region Photon Callbacks
        public void OnConnectedToServer(NetworkRunner runner)
        {
            var rpc = m_Runner.gameObject.AddComponent<PhotonRPC>();
            m_Runner.AddSimulationBehaviour(rpc);
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if(player == m_Runner.LocalPlayer)
            {
                var playerPrefab = Resources.Load("Multiplayer/Photon/PhotonPlayerRig") as GameObject;
                var playerObj = m_Runner.Spawn(playerPrefab, inputAuthority: m_Runner.LocalPlayer);
                m_LocalPlayer = playerObj.GetComponent<PhotonPlayerRig>();
                m_Runner.SetPlayerObject(m_Runner.LocalPlayer, playerObj);
                

                m_Manager.localPlayerJoined?.Invoke(m_LocalPlayer);
            }
            else
            {
                m_PlayersSpawning.Add(player);
            }
        }
#endregion

#region Unused Photon Callbacks 
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
#endregion
    }
}

#endif // FUSION_WEAVER
