using System;
using System.Collections;
using System.Collections.Generic;
using TiltBrush;
using UnityEngine;

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
        public MultiplayerType m_MultiplayerType;

        private IConnectionHandler m_Manager;
        private ITransientData<PlayerRigData> m_LocalPlayer;
        private List<ITransientData<PlayerRigData>> m_Players;

        public Action localPlayerJoined;

        // Start is called before the first frame update
        async void Start()
        {
            localPlayerJoined += OnPlayerJoined;
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

        void OnPlayerJoined()
        {
            m_LocalPlayer = m_Manager.SpawnPlayer();
        }

        // Update is called once per frame
        void Update()
        {
            var data = new PlayerRigData();
            data.HeadPosition = App.VrSdk.GetVrCamera().transform.position;
            data.HeadRotation = App.VrSdk.GetVrCamera().transform.localRotation;
            if(m_LocalPlayer != null)
            {
                m_LocalPlayer.TransmitData(data);
            }
        }
    }
}
