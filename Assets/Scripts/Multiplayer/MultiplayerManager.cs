using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TiltBrush;
using OVRPlatform = Oculus.Platform;

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

        void Awake()
        {
            m_Instance = this;
            oculusPlayerIds = new List<ulong>();
            m_RemotePlayers = new List<ITransientData<PlayerRigData>>();
        }

        void Start()
        {
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

            localPlayerJoined += OnLocalPlayerJoined;
            remotePlayerJoined += OnRemotePlayerJoined;
        }

        public async void Connect()
        {
            switch (m_MultiplayerType)
            {
                case MultiplayerType.None:
                    return;
                case MultiplayerType.Photon:
                    m_Manager = new PhotonManager(this);
                    break;
            }

            var result = await m_Manager.Connect();
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

        void Update()
        {
            if(m_Manager == null)
            {
                return;
            }

            m_Manager.Update();

            // Transmit local player data.
            var headTransform = App.VrSdk.GetVrCamera().transform;
            var data = new PlayerRigData
            {
                HeadPosition = App.Scene.transform.InverseTransformPoint(headTransform.position),
                HeadRotation = headTransform.localRotation,
                ExtraData = new ExtraData
                {
                    OculusPlayerId = myOculusUserId
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

        async void ShareAnchors()
        {
            Debug.Log($"sharing to {oculusPlayerIds.Count} Ids");
            var success = await OculusMRController.m_Instance.m_SpatialAnchorManager.ShareAnchors(oculusPlayerIds);

            if (success)
            {
                if(!OculusMRController.m_Instance.m_SpatialAnchorManager.AnchorUuid.Equals(String.Empty))
                {
                    await m_Manager.RpcSyncToSharedAnchor(OculusMRController.m_Instance.m_SpatialAnchorManager.AnchorUuid);
                }
            }
        }
    }
}
