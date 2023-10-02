using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public class PhotonPlayerRig : NetworkBehaviour, ITransientData<PlayerRigData>
    {
        public NetworkTransform m_PlayArea;
        public NetworkTransform m_PlayerHead;
        public NetworkTransform m_Left;
        public NetworkTransform m_Right;

        [Networked] public ulong oculusPlayerId { get; set; } 

        private PlayerRigData transmitData;

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
        }

#region RPCs
        [Rpc]
        public void Rpc_SyncToSharedAnchor(string uuid)
        {
            Debug.Log("just about to run sync command!");
            SpatialAnchorManager.m_Instance.SyncToAnchor(uuid);
        }
#endregion
    }
}
