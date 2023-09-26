using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Photon.Realtime;
using Fusion.Sockets;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public class PhotonManager : IConnectionHandler, INetworkRunnerCallbacks
    {
        private NetworkRunner m_Runner;
        public bool Connect()
        {
            if(m_Runner != null)
            {
                GameObject.Destroy(m_Runner);
            }

            var runnerGO = new GameObject("Photon Network Components");

            m_Runner = runnerGO.AddComponent<NetworkRunner>();
            m_Runner.ProvideInput = true;
            m_Runner.AddCallbacks(this);
            StartSession();

            return true;
        }

        private async void StartSession()
        {
            if(m_Runner != null)
            {
                var appSettings = new AppSettings
                {
                    AppIdFusion = App.Config.PhotonFusionSecrets.ClientId,
                    // Need this set for some reason
                    FixedRegion = "",
                };

                var args = new StartGameArgs()
                {
                GameMode = GameMode.Shared,
                SessionName = "testRoom",
                CustomPhotonAppSettings = appSettings,
                SceneManager = m_Runner.gameObject.AddComponent<NetworkSceneManagerDefault>(),
                Scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex,
                };

                await m_Runner.StartGame(args);
            }
            else
            {
                Debug.LogWarning("Runner not assigned!");
            }
        }

        public bool Disconnect(bool force)
        {
            throw new System.NotImplementedException();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log("Joined!");
        }

        #region Unused Photon Callbacks 
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnConnectedToServer(NetworkRunner runner) { Debug.Log("joined server!"); }
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

