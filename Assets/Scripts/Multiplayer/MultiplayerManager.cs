using System.Collections;
using System.Collections.Generic;
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

        public IConnectionHandler m_Manager;

        // Start is called before the first frame update
        void Start()
        {
            switch(m_MultiplayerType)
            {
                case MultiplayerType.None:
                    return;
                case MultiplayerType.Photon:
                    m_Manager = new PhotonManager();
                    break;
            }

            m_Manager.Connect();
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}

