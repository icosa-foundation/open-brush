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

        public async Task<bool> RpcSyncToSharedAnchor(string uuid)
        {
            PhotonRPC.RPC_SyncToSharedAnchor(m_Runner, uuid);
            await Task.Yield();
            return true;
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if(player == m_Runner.LocalPlayer)
            {
                var playerPrefab = Resources.Load("Multiplayer/Photon/PhotonPlayerRig") as GameObject;
                var playerObj = m_Runner.Spawn(playerPrefab, inputAuthority: m_Runner.LocalPlayer);
                var playerPhoton = playerObj.GetComponent<PhotonPlayerRig>();
                m_Runner.SetPlayerObject(m_Runner.LocalPlayer, playerObj);

                m_Manager.localPlayerJoined?.Invoke(playerPhoton);
            }
            else
            {
                m_PlayersSpawning.Add(player);
            }
        }

        #region Unused Photon Callbacks 
        public void OnConnectedToServer(NetworkRunner runner) { }
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

