using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Platform;
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
        private List<ITransientData<PlayerRigData>> m_Players;

        public Action<ITransientData<PlayerRigData>> localPlayerJoined;
        public Action<ITransientData<PlayerRigData>> remotePlayerJoined;

        ulong oculusUserId;

        List<ulong> oculusPlayerIds;

        void Awake()
        {
            m_Instance = this;
            oculusPlayerIds = new List<ulong>();
            m_Players = new List<ITransientData<PlayerRigData>>();
        }

        async void Start()
        {
            //Get Oculus ID
            var appId = App.Config.OculusSecrets.ClientId;
#if UNITY_ANDROID
            appId = App.Config.OculusMobileSecrets.ClientId;
#endif
            Core.Initialize(appId);
            Users.GetLoggedInUser().OnComplete((msg) => {

                if (!msg.IsError)
                {
                    oculusUserId = msg.GetUser().ID;
                    Debug.Log(oculusUserId);
                    oculusPlayerIds.Add(oculusUserId);
                }
                else
                {
                    Debug.LogError(msg.GetError());
                }
            });

            localPlayerJoined += OnLocalPlayerJoined;
            remotePlayerJoined += OnRemotePlayerJoined;
            switch(m_MultiplayerType)
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
            m_Players.Add(playerData);
        }

        // Update is called once per frame
        void Update()
        {
            m_Manager.Update();
            // Transmit local player data.
            var headTransform = App.VrSdk.GetVrCamera().transform;

            var data = new PlayerRigData
            {
                HeadPosition = App.Scene.transform.InverseTransformPoint(headTransform.position),
                HeadRotation = headTransform.localRotation,
                ExtraData = new ExtraData
                {
                    OculusPlayerId = oculusUserId
                }
            };

            if (m_LocalPlayer != null)
            {
                m_LocalPlayer.TransmitData(data);
            }

            bool newUser = false;
            foreach (var player in m_Players)
            {
                data = player.RecieveData();
                // New user, share the anchor with them
                if (data.ExtraData.OculusPlayerId != 0 && !oculusPlayerIds.Contains(data.ExtraData.OculusPlayerId))
                {
                    Debug.Log("detected new user!");
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
            var success = await SpatialAnchorManager.m_Instance.ShareAnchors(oculusPlayerIds);

            if (success)
            {
                if(SpatialAnchorManager.m_Instance != null && !SpatialAnchorManager.m_Instance.AnchorUuid.Equals(String.Empty))
                {
                    await m_Manager.RpcSyncToSharedAnchor(SpatialAnchorManager.m_Instance.AnchorUuid);
                }
            }
        }
    }
}
