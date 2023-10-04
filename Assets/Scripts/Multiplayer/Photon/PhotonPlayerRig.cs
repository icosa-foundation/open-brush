using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public class PhotonPlayerRig : NetworkBehaviour, ITransientData<PlayerRigData>
    {
        // Only used for transferring data - don't actually use these transforms without offsetting
        public NetworkTransform m_PlayArea;
        public NetworkTransform m_PlayerHead;
        public NetworkTransform m_Left;
        public NetworkTransform m_Right;

        // The offset transforms.
        [SerializeField] private Transform headTransform;

        [Networked] public ulong oculusPlayerId { get; set; } 

        private PlayerRigData transmitData;

        public bool inverseScene;
        public bool inverseHead;
        public bool swapOrder;

        public void TransmitData(PlayerRigData data)
        {
            transmitData = data;
            oculusPlayerId = data.ExtraData.OculusPlayerId;
        }

        public PlayerRigData RecieveData()
        {
            // We construct this dynamically to get the most up-to-date interpolation
            // TODO: needed?
            var data = new PlayerRigData
            {
                HeadPosition = m_PlayerHead.InterpolationTarget.position,
                HeadRotation = m_PlayerHead.InterpolationTarget.rotation,
                ExtraData = new ExtraData
                {
                    OculusPlayerId = this.oculusPlayerId
                }
            };
            return data;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            if(Object.HasStateAuthority)
            {
                m_PlayerHead.transform.position = transmitData.HeadPosition;
                m_PlayerHead.transform.rotation = transmitData.HeadRotation;
            }

            // TODO: learn maths.
            //headTransform.position = App.Scene.transform.position + transmitData.HeadPosition;
            //headTransform.position = App.Scene.transform.position + m_PlayerHead.InterpolationTarget.position;

            headTransform.position = m_PlayerHead.InterpolationTarget.position;
            headTransform.rotation = m_PlayerHead.InterpolationTarget.rotation;
        }

#region RPCs
        [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
        public void RPC_SyncToSharedAnchor(string uuid)
        {
            Debug.Log("just about to run sync command!");
            OculusMRController.m_Instance.RemoteSyncToAnchor(uuid);
        }
#endregion
    }
}
