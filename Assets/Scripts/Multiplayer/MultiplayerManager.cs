using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;
using OVRPlatform = Oculus.Platform;
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

        private Transform m_trans;

        void Awake()
        {
            m_Instance = this;
            oculusPlayerIds = new List<ulong>();
            m_RemotePlayers = new List<ITransientData<PlayerRigData>>();

            m_trans = new GameObject("HMDRelativeToScene").transform;
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
                    m_Manager = new PhotonManager(this);
                    break;
                default:
                    return;
            }

            localPlayerJoined += OnLocalPlayerJoined;
            remotePlayerJoined += OnRemotePlayerJoined;
        }

        public async void Connect()
        {
            var result = await m_Manager.Connect();
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

            // Stupid, so stupid.
            m_trans.parent = headTransform;
            m_trans.localPosition = Vector3.zero;
            m_trans.localRotation = Quaternion.identity;
            m_trans.localScale = Vector3.one;
            m_trans.parent = App.Scene.transform;

            // TODO: Learn maths
            // var inversePose = App.Scene.transform.InverseTransformPose(headTransform.GetWorldPose());
            var data = new PlayerRigData
            {
                HeadPosition = m_trans.position,
                HeadRotation = m_trans.rotation,
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

        void OnLocalPlayerJoined(ITransientData<PlayerRigData> playerData)
        {
            m_LocalPlayer = playerData;
        }

        void OnRemotePlayerJoined(ITransientData<PlayerRigData> playerData)
        {
            Debug.Log("Adding new player to track.");
            m_RemotePlayers.Add(playerData);
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
