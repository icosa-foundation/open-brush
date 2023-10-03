using System.Collections;
using System.Collections.Generic;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    public class OculusMRController : MonoBehaviour
    {
        public static OculusMRController m_Instance;

        public OVRSceneManager ovrSceneManager;
        public SpatialAnchorManager m_SpatialAnchorManager;

        private bool host;

        void Awake()
        {
            m_Instance = this;

            ovrSceneManager = GetComponent<OVRSceneManager>();
            m_SpatialAnchorManager = GetComponent<SpatialAnchorManager>();
        }

        void RequestScenePermission()
        {
            const string permissionString = "com.oculus.permission.USE_SCENE";
            bool hasUserAuthorizedPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(permissionString);
            if (!hasUserAuthorizedPermission) {
                UnityEngine.Android.Permission.RequestUserPermission(permissionString);
            }
        }

        public async void StartMRExperience(bool isHosting)
        {
            host = isHosting;

            if (host)
            {
                await m_SpatialAnchorManager.CreateSpatialAnchor();
                m_SpatialAnchorManager.SceneLocalizeToAnchor();
                MultiplayerManager.m_Instance.Connect();
            }
            else
            {
                MultiplayerManager.m_Instance.Connect();
            }
        }

        public async void RemoteSyncToAnchor(string uuid)
        {
            if (!host)
            {
                await m_SpatialAnchorManager.SyncToRemoteAnchor(uuid, OVRSpace.StorageLocation.Cloud);
            }
        }

        public void LoadScene()
        {
            ovrSceneManager.LoadSceneModel();
        }
    }
}

