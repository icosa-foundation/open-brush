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

using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
#if OCULUS_SUPPORTED
using OVRPlatform = Oculus.Platform;
#endif
using TiltBrush;

namespace OpenBrush.Multiplayer
{
    public enum MultiplayerType
    {
        None,
        Colyseus = 1,
        Photon = 2,
    }

    public class MultiplayerManager : MonoBehaviour
    {
        public static MultiplayerManager m_Instance;
        public MultiplayerType m_MultiplayerType;

        private IConnectionHandler m_Manager;

        private ITransientData<PlayerRigData> m_LocalPlayer;
        private List<ITransientData<PlayerRigData>> m_RemotePlayers;

        public Action<ITransientData<PlayerRigData>> localPlayerJoined;
        public Action<ITransientData<PlayerRigData>> remotePlayerJoined;

        ulong myOculusUserId;

        List<ulong> oculusPlayerIds;

        private bool IsConnected { get { return m_Manager != null && m_Manager.IsConnected(); } }

        void Awake()
        {
            m_Instance = this;
            oculusPlayerIds = new List<ulong>();
            m_RemotePlayers = new List<ITransientData<PlayerRigData>>();
        }

        void Start()
        {

#if OCULUS_SUPPORTED
            OVRPlatform.Users.GetLoggedInUser().OnComplete((msg) => {
                if (!msg.IsError)
                {
                    myOculusUserId = msg.GetUser().ID;
                    Debug.Log($"OculusID: {myOculusUserId}");
                    oculusPlayerIds.Add(myOculusUserId);
                }
                else
                {
                    Debug.LogError(msg.GetError());
                }
            });
#endif
            switch (m_MultiplayerType)
            {
                case MultiplayerType.Photon:
#if FUSION_WEAVER                
                    m_Manager = new PhotonManager(this);
#endif // FUSION_WEAVER
                    break;
                default:
                    return;
            }

            localPlayerJoined += OnLocalPlayerJoined;
            remotePlayerJoined += OnRemotePlayerJoined;
            SketchMemoryScript.m_Instance.CommandPerformed += OnCommandPerformed;
            SketchMemoryScript.m_Instance.CommandUndo += OnCommandUndo;
            SketchMemoryScript.m_Instance.CommandRedo += OnCommandRedo;
        }

        void OnDestroy()
        {
            localPlayerJoined -= OnLocalPlayerJoined;
            remotePlayerJoined -= OnRemotePlayerJoined;
            SketchMemoryScript.m_Instance.CommandPerformed -= OnCommandPerformed;
            SketchMemoryScript.m_Instance.CommandUndo -= OnCommandUndo;
            SketchMemoryScript.m_Instance.CommandRedo -= OnCommandRedo;
        }

        public async void Connect()
        {
            var result = await m_Manager.Connect();
        }

        void Update()
        {
            if (App.CurrentState != App.AppState.Standard || m_Manager == null)
            {
                return;
            }

            m_Manager.Update();

            // Transmit local player data relative to scene origin
            var headRelativeToScene = App.Scene.AsScene[App.VrSdk.GetVrCamera().transform];
            var pointerRelativeToScene = App.Scene.AsScene[PointerManager.m_Instance.MainPointer.transform];

            var data = new PlayerRigData
            {
                HeadPosition = headRelativeToScene.translation,
                HeadRotation = headRelativeToScene.rotation,
                ToolPosition = pointerRelativeToScene.translation,
                ToolRotation = pointerRelativeToScene.rotation,
                BrushData = new BrushData
                {
                    Color = PointerManager.m_Instance.MainPointer.GetCurrentColor(),
                    Size = PointerManager.m_Instance.MainPointer.BrushSize01,
                    Guid = BrushController.m_Instance.ActiveBrush.m_Guid.ToString(),
                },
                ExtraData = new ExtraData
                {
                    OculusPlayerId = myOculusUserId,
                }
            };

            if (m_LocalPlayer != null)
            {
                m_LocalPlayer.TransmitData(data);
            }


            // Update remote user refs, and send Anchors if new player joins.
            bool newUser = false;
            foreach (var player in m_RemotePlayers)
            {
                data = player.RecieveData();
                // New user, share the anchor with them
                if (data.ExtraData.OculusPlayerId != 0 && !oculusPlayerIds.Contains(data.ExtraData.OculusPlayerId))
                {
                    Debug.Log("detected new user!");
                    Debug.Log(data.ExtraData.OculusPlayerId);
                    oculusPlayerIds.Add(data.ExtraData.OculusPlayerId);
                    newUser = true;
                }
            }

            if (newUser)
            {
                ShareAnchors();
            }
        }

        void OnLocalPlayerJoined(ITransientData<PlayerRigData> playerData)
        {
            m_LocalPlayer = playerData;
        }

        void OnRemotePlayerJoined(ITransientData<PlayerRigData> playerData)
        {
            Debug.Log("Adding new player to track.");
            m_RemotePlayers.Add(playerData);
        }

        private async void OnCommandPerformed(BaseCommand command)
        {
            if (!IsConnected)
            {
                return;
            }

            var success = await m_Manager.PerformCommand(command);

            // TODO: Proper rollback if command not possible right now.
            // Commented so it doesn't interfere with general use.
            // Link actions to connect/disconnect, not Unity lifecycle.

            // if (!success)
            // {
            //     OutputWindowScript.m_Instance.CreateInfoCardAtController(InputManager.ControllerName.Brush, "Don't know how to network this action yet.");
            //     SketchMemoryScript.m_Instance.StepBack(false);
            // }
        }

        private void OnCommandUndo(BaseCommand command)
        {
            if (IsConnected)
            {
                m_Manager.UndoCommand(command);
            }
        }

        private void OnCommandRedo(BaseCommand command)
        {
            if (IsConnected)
            {
                m_Manager.RedoCommand(command);
            }
        }

        async void ShareAnchors()
        {
#if OCULUS_SUPPORTED
            Debug.Log($"sharing to {oculusPlayerIds.Count} Ids");
            var success = await OculusMRController.m_Instance.m_SpatialAnchorManager.ShareAnchors(oculusPlayerIds);

            if (success)
            {
                if (!OculusMRController.m_Instance.m_SpatialAnchorManager.AnchorUuid.Equals(String.Empty))
                {
                    await m_Manager.RpcSyncToSharedAnchor(OculusMRController.m_Instance.m_SpatialAnchorManager.AnchorUuid);
                }
            }
#endif // OCULUS_SUPPORTED
        }
    }
}
