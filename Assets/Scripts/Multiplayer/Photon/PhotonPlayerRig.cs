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
        [Networked] public bool IsRoomOwner { get; set; }
        [Networked] public float SceneScale { get; set; }

        PointerScript transientPointer;
        // The offset transforms.
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;

        private PlayerRigData transmitData;

        public int m_PlayerId;

        public int PlayerId
        {
            get { return m_PlayerId; }
            set { m_PlayerId = value; }
        }

        public GameObject m_LeftControllerModel;
        public GameObject m_RightControllerModel;


        public void TransmitData(PlayerRigData data)
        {
            transmitData = data;
            oculusPlayerId = data.ExtraData.OculusPlayerId;
            brushColor = data.BrushData.Color;
            brushSize = data.BrushData.Size;
            brushGuid = data.BrushData.Guid;
            IsRoomOwner = data.IsRoomOwner;
            SceneScale = data.SceneScale;
        }

        public PlayerRigData RecieveData()
        {
            var data = new PlayerRigData();

            if (m_PlayerHead?.InterpolationTarget != null)
            {
                data.HeadPosition = m_PlayerHead.InterpolationTarget.position;
                data.HeadRotation = m_PlayerHead.InterpolationTarget.rotation;
            }

            if (m_Tool?.InterpolationTarget != null)
            {
                data.ToolPosition = m_Tool.InterpolationTarget.position;
                data.ToolRotation = m_Tool.InterpolationTarget.rotation;
            }

            if (m_Left?.InterpolationTarget != null)
            {
                data.LeftHandPosition = m_Left.InterpolationTarget.position;
                data.LeftHandRotation = m_Left.InterpolationTarget.rotation;
            }

            if (m_Right?.InterpolationTarget != null)
            {
                data.RightHandPosition = m_Right.InterpolationTarget.position;
                data.RightHandRotation = m_Right.InterpolationTarget.rotation;
            }

            data.IsRoomOwner = this.IsRoomOwner;
            data.ExtraData = new ExtraData { OculusPlayerId = this.oculusPlayerId };
            data.SceneScale = this.SceneScale;

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

            UpdateControllerVisibility();
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

                m_Left.transform.position = transmitData.LeftHandPosition;
                m_Left.transform.rotation = transmitData.LeftHandRotation;

                m_Right.transform.position = transmitData.RightHandPosition;
                m_Right.transform.rotation = transmitData.RightHandRotation;
            }
        }

        public override void Render()
        {
            base.Render();

            if (Object.HasStateAuthority)
            {
                var remoteTR = TrTransform.TR(
                    m_PlayerHead.InterpolationTarget.position,
                    m_PlayerHead.InterpolationTarget.rotation
                );
                    App.Scene.AsScene[headTransform] = remoteTR;

            }
            
            else
            {
                // Remote pointer
                var toolTR = TrTransform.TR(
                    m_Tool.InterpolationTarget.position,
                    m_Tool.InterpolationTarget.rotation
                    );
                App.Scene.AsScene[transientPointer.transform] = toolTR;

                transientPointer.SetColor(brushColor);
                if(brushGuid.ToString() != string.Empty)
                {
                    transientPointer.SetBrush(BrushCatalog.m_Instance.GetBrush(new System.Guid(brushGuid.ToString())));
                }
                transientPointer.BrushSize01 = brushSize;

                // Calculate the scale based on the scene scale
                float clampedSceneScale = Mathf.Clamp(SceneScale, 0.01f, float.MaxValue);
                float Scale = 1 / clampedSceneScale;

                // Remote head
                var remoteTR = TrTransform.TRS(
                    m_PlayerHead.InterpolationTarget.position,
                    m_PlayerHead.InterpolationTarget.rotation,
                    Scale
                );
                App.Scene.AsScene[headTransform] = remoteTR;

                // Remote left hand
                var remoteLeftTR = TrTransform.TRS(
                    m_Left.InterpolationTarget.position,
                    m_Left.InterpolationTarget.rotation,
                    Scale
                );
                App.Scene.AsScene[leftHandTransform] = remoteLeftTR;

                // Remote right hand
                var remoteRightTR = TrTransform.TRS(
                    m_Right.InterpolationTarget.position,
                    m_Right.InterpolationTarget.rotation,
                    Scale
                );
                App.Scene.AsScene[rightHandTransform] = remoteRightTR;

            }
        }

        public void UpdateControllerVisibility()
        {
            if (Object.HasStateAuthority)
            {
                m_LeftControllerModel.SetActive(false);
                m_RightControllerModel.SetActive(false);
            }
            else
            {
                m_LeftControllerModel.SetActive(true);
                m_RightControllerModel.SetActive(true);
            }
        }

        void OnDestroy()
        {
            if (transientPointer != null)
            {
                PointerManager.m_Instance.RemoveRemotePointer(transientPointer);
                transientPointer = null;
            }

            m_PlayerHead = null;
            m_Tool = null;
            m_Left = null;
            m_Right = null;
        }
    }
}

#endif // FUSION_WEAVER
