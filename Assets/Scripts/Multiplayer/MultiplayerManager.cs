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
        public Action<ITransientData<PlayerRigData>> otherPlayerJoined;

        ulong oculusUserId;

        List<ulong> oculusPlayerIds;

        void Awake()
        {
            m_Instance = this;
            oculusPlayerIds = new List<ulong>();
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

        void RemotePlayerJoined(ITransientData<PlayerRigData> playerData)
        {
            m_Players.Add(playerData);
        }

        // Update is called once per frame
        void Update()
        {
            // Transmit local player data.
            var data = new PlayerRigData();
            data.HeadPosition = App.VrSdk.GetVrCamera().transform.position;
            data.HeadRotation = App.VrSdk.GetVrCamera().transform.localRotation;
            if(m_LocalPlayer != null)
            {
                m_LocalPlayer.TransmitData(data);
            }

            foreach (var player in m_Players)
            {
                
            }
        }
    }
}
