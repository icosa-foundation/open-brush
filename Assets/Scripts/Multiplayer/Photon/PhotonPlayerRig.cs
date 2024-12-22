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
using System;
using System.Collections;
using TMPro;

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
        [Networked] public bool isReceivingVoiceTransmission { get; set; }
        [Networked] public string Nickname { get; set; }

        PointerScript transientPointer;
        // The offset transforms.
        [SerializeField] private Transform headTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private Transform leftHandTransform;
        // The 3D model of the headset
        [SerializeField] private Renderer HMDMeshRenderer;
        // The Nickname of the player
        [SerializeField] private TextMeshPro NicknameText;

        private PlayerRigData transmitData;
        private Color originalColor;
        private Coroutine fadeCoroutine;

        private bool m_IsSpawned = false;
        public bool IsSpawned => m_IsSpawned;

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
            isReceivingVoiceTransmission = data.isReceivingVoiceTransmission;
            Nickname = data.Nickname;
        }

        private void Awake()
        {
            if (HMDMeshRenderer != null && HMDMeshRenderer.material.HasProperty("_EmissionColor"))
            {
                originalColor = HMDMeshRenderer.material.GetColor("_EmissionColor");
            } 
        }

        public PlayerRigData ReceiveData()
        {
            if (!m_IsSpawned) return default;

            var data = new PlayerRigData();

            if (m_PlayerHead?.transform != null)
            {
                data.HeadPosition = m_PlayerHead.transform.position;
                data.HeadRotation = m_PlayerHead.transform.rotation;
            }

            if (m_Tool?.transform != null)
            {
                data.ToolPosition = m_Tool.transform.position;
                data.ToolRotation = m_Tool.transform.rotation;
            }

            if (m_Left?.transform != null)
            {
                data.LeftHandPosition = m_Left.transform.position;
                data.LeftHandRotation = m_Left.transform.rotation;
            }

            if (m_Right?.transform != null)
            {
                data.RightHandPosition = m_Right.transform.position;
                data.RightHandRotation = m_Right.transform.rotation;
            }

            try
            {
                data.IsRoomOwner = this.IsRoomOwner;
                data.ExtraData = new ExtraData { OculusPlayerId = this.oculusPlayerId };
                data.SceneScale = this.SceneScale;
                data.isReceivingVoiceTransmission = this.isReceivingVoiceTransmission;
                if (!string.IsNullOrEmpty(this.Nickname))
                {
                    data.Nickname = this.Nickname;
                    NicknameText.text = this.Nickname;
                }
                
            }
            catch (InvalidOperationException ex)
            {
                return default;
            }

            return data;
        }

        public override void Spawned()
        {
            base.Spawned();

            brushGuid = BrushCatalog.m_Instance.DefaultBrush.m_Guid.ToString();

            if (!Object.HasStateAuthority)
            {
                transientPointer = PointerManager.m_Instance.CreateRemotePointer();
                transientPointer.SetBrush(BrushCatalog.m_Instance.DefaultBrush);
                transientPointer.SetColor(App.BrushColor.CurrentColor);
            }
            else 
            {
                NicknameText.text = "";
                NicknameText.gameObject.SetActive(false);
            }

            UpdateControllerVisibility();

            m_IsSpawned = true;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            if (Object.HasStateAuthority)
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
                    m_PlayerHead.transform.position,
                    m_PlayerHead.transform.rotation
                );
                App.Scene.AsScene[headTransform] = remoteTR;

            }

            else
            {
                // Remote pointer
                var toolTR = TrTransform.TR(
                    m_Tool.transform.position,
                    m_Tool.transform.rotation
                    );
                App.Scene.AsScene[transientPointer.transform] = toolTR;

                transientPointer.SetColor(brushColor);
                if (brushGuid.ToString() != string.Empty)
                {
                    transientPointer.SetBrush(BrushCatalog.m_Instance.GetBrush(new System.Guid(brushGuid.ToString())));
                }
                transientPointer.BrushSize01 = brushSize;

                // Calculate the scale based on the scene scale
                float clampedSceneScale = Mathf.Clamp(SceneScale, 0.1f, 9.8f);
                float Scale = 1 / clampedSceneScale;

                // Remote head
                var remoteTR = TrTransform.TRS(
                    m_PlayerHead.transform.position,
                    m_PlayerHead.transform.rotation,
                    Scale
                );
                App.Scene.AsScene[headTransform] = remoteTR;

                // Remote left hand
                var remoteLeftTR = TrTransform.TRS(
                    m_Left.transform.position,
                    m_Left.transform.rotation,
                    Scale
                );
                App.Scene.AsScene[leftHandTransform] = remoteLeftTR;

                // Remote right hand
                var remoteRightTR = TrTransform.TRS(
                    m_Right.transform.position,
                    m_Right.transform.rotation,
                    Scale
                );
                App.Scene.AsScene[rightHandTransform] = remoteRightTR;

                //HMD color
                if (isReceivingVoiceTransmission) FadeHMDMeshColor(Color.red);
                else FadeHMDMeshColor(originalColor);
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

            m_IsSpawned = false;
        }

        public void UpdateColor(Color color)
        {

            if (HMDMeshRenderer != null && HMDMeshRenderer.material.HasProperty("_EmissionColor"))
            {
                HMDMeshRenderer.material.SetColor("_EmissionColor", color);
            }

            if (NicknameText != null)
            {
                NicknameText.outlineColor = color;
            }

        }

        public void FadeHMDMeshColor(Color targetColor)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeColorRoutine(targetColor));
        }

        private IEnumerator FadeColorRoutine(Color targetColor)
        {
            
            if (HMDMeshRenderer == null || 
                !HMDMeshRenderer.material.HasProperty("_EmissionColor") ||
                NicknameText == null) yield break;

            Color startColor = HMDMeshRenderer.material.GetColor("_EmissionColor");
            float elapsedTime = 0f;

            while (elapsedTime < 0.2f)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / 0.2f);
                Color currentColor = Color.Lerp(startColor, targetColor, t);
                UpdateColor(currentColor);
                yield return null;
            }

            HMDMeshRenderer.material.SetColor("_EmissionColor", targetColor);
        }
    }

}

#endif // FUSION_WEAVER
