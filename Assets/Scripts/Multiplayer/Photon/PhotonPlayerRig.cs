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
        public NetworkTransform m_Tool;

        [Networked] private Color brushColor { get; set; }
        [Networked] private float brushSize { get; set; }
        [Networked] private NetworkString<_64> brushGuid { get; set; }
        [Networked] public ulong oculusPlayerId { get; set; }

        PointerScript transientPointer;
        // The offset transforms.
        [SerializeField] private Transform headTransform;
        private PlayerRigData transmitData;

        public void TransmitData(PlayerRigData data)
        {
            transmitData = data;
            oculusPlayerId = data.ExtraData.OculusPlayerId;

            brushColor = data.BrushData.Color;
            brushSize = data.BrushData.Size;
            brushGuid = data.BrushData.Guid;
        }

        public PlayerRigData RecieveData()
        {
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

        public override void Spawned()
        {
            base.Spawned();

            brushGuid = BrushCatalog.m_Instance.DefaultBrush.m_Guid.ToString();

            if(!Object.HasStateAuthority)
            {
                transientPointer = PointerManager.m_Instance.CreateRemotePointer();
                transientPointer.SetBrush(BrushCatalog.m_Instance.DefaultBrush);
                transientPointer.SetColor(App.BrushColor.CurrentColor);
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            if(Object.HasStateAuthority)
            {
                m_PlayerHead.transform.position = transmitData.HeadPosition;
                m_PlayerHead.transform.rotation = transmitData.HeadRotation;

                m_Tool.transform.position = transmitData.ToolPosition;
                m_Tool.transform.rotation = transmitData.ToolRotation;
            }
        }

        public override void Render()
        {
            base.Render();

            if (Object.HasStateAuthority)
            {

            }
            
            else
            {
                var toolTR = TrTransform.TR(m_Tool.InterpolationTarget.position, m_Tool.InterpolationTarget.rotation);
                App.Scene.AsScene[transientPointer.transform] = toolTR;

                transientPointer.SetColor(brushColor);
                if(brushGuid.ToString() != string.Empty)
                {
                    transientPointer.SetBrush(BrushCatalog.m_Instance.GetBrush(new System.Guid(brushGuid.ToString())));
                }
                transientPointer.BrushSize01 = brushSize;
            }

            var remoteTR = TrTransform.TR(m_PlayerHead.InterpolationTarget.position, m_PlayerHead.InterpolationTarget.rotation);
            App.Scene.AsScene[headTransform] = remoteTR;
        }
    }
}

#endif // FUSION_WEAVER
