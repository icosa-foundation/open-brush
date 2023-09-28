using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

namespace OpenBrush.Multiplayer
{
    public class PhotonPlayerRig : NetworkBehaviour, ITransientData<PlayerRigData>, INetworkRunnerCallbacks
    {
        public NetworkTransform m_PlayArea;
        public NetworkTransform m_PlayerHead;
        public NetworkTransform m_Left;
        public NetworkTransform m_Right;

        private PlayerRigData transmitData;
        private PlayerRigData recievedData;

        public override void Spawned()
        {
            base.Spawned();
        }

        public void TransmitData(PlayerRigData data)
        {
            transmitData = data;
        }

        public PlayerRigData RecieveData()
        {
            return recievedData;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            //update the rig at each network tick
            if (GetInput<RigInput>(out var input))
            {
                m_PlayerHead.transform.position = input.headPosition;
                m_PlayerHead.transform.rotation = input.headRotation;
            }
        }

        public override void Render()
        {
            base.Render();

            if (Object.HasStateAuthority)
            {
                m_PlayerHead.InterpolationTarget.position = transmitData.HeadPosition;
                m_PlayerHead.InterpolationTarget.rotation = transmitData.HeadRotation;
            }
            else
            {
                recievedData = new PlayerRigData
                {
                    HeadPosition = m_PlayerHead.InterpolationTarget.position,
                    HeadRotation = m_PlayerHead.InterpolationTarget.rotation
                };
                m_PlayerHead.transform.position = recievedData.HeadPosition;
                m_PlayerHead.transform.rotation = recievedData.HeadRotation;
            }
        }

        public virtual void OnInput(NetworkRunner runner, NetworkInput input) {
            RigInput rigInput = new RigInput();
            rigInput.headPosition = transmitData.HeadPosition;
            rigInput.headRotation = transmitData.HeadRotation;
            input.Set(rigInput);
        }

        [System.Serializable]
        public struct RigInput : INetworkInput
        {
            public Vector3 headPosition;
            public Quaternion headRotation;
        }

        #region INetworkRunnerCallbacks (unused)
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }


        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }

        public void OnConnectedToServer(NetworkRunner runner) { }

        public void OnDisconnectedFromServer(NetworkRunner runner) { }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

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

