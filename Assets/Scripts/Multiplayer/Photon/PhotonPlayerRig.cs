using System;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Sockets;

namespace OpenBrush.Multiplayer
{
    public class PhotonPlayerRig : NetworkBehaviour, ITransientData<PlayerRigData>
    {
        public NetworkTransform m_PlayArea;
        public NetworkTransform m_PlayerHead;
        public NetworkTransform m_Left;
        public NetworkTransform m_Right;

        private PlayerRigData transmitData;

        public void TransmitData(PlayerRigData data)
        {
            transmitData = data;
        }

        public PlayerRigData RecieveData()
        {
            // We construct this dynamically to get the most up-to-date interpolation
            // TODO: needed?
            var data = new PlayerRigData
            {
                HeadPosition = m_PlayerHead.InterpolationTarget.position,
                HeadRotation = m_PlayerHead.InterpolationTarget.rotation
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
    }
}

