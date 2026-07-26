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

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    public class OculusMRController : MonoBehaviour
    {
#if OCULUS_SUPPORTED
        public static OculusMRController m_Instance;

        public OVRSceneManager ovrSceneManager;
        public SpatialAnchorManager m_SpatialAnchorManager;

        private bool loadedScene;

        private bool host;

        void Awake()
        {
            m_Instance = this;

            ovrSceneManager = GetComponent<OVRSceneManager>();
            m_SpatialAnchorManager = GetComponent<SpatialAnchorManager>();
        }

        Task<bool> RequestScenePermissionAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permissionString = "com.oculus.permission.USE_SCENE";
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permissionString))
            {
                return Task.FromResult(true);
            }

            var completion = new TaskCompletionSource<bool>();
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ => completion.TrySetResult(true);
            callbacks.PermissionDenied += _ => completion.TrySetResult(false);
            callbacks.PermissionDeniedAndDontAskAgain += _ => completion.TrySetResult(false);
            UnityEngine.Android.Permission.RequestUserPermission(permissionString, callbacks);
            return completion.Task;
#else
            return Task.FromResult(true);
#endif
        }

        public async void StartMRExperience(bool isHosting)
        {
            if (!await RequestScenePermissionAsync())
            {
                Debug.LogWarning("Oculus scene permission is required to start MR experience.");
                return;
            }

            host = isHosting;

            if (host)
            {
                await m_SpatialAnchorManager.CreateSpatialAnchor();
                m_SpatialAnchorManager.SceneLocalizeToAnchor();
                MultiplayerManager.m_Instance.JoinRoom(new RoomCreateData()
                {
                    roomName = "OculusMRRoom",
                    maxPlayers = 12
                });
            }
            else
            {
                MultiplayerManager.m_Instance.JoinRoom(new RoomCreateData()
                {
                    roomName = "OculusMRRoom",
                    maxPlayers = 12
                });
            }
        }

        public async void RemoteSyncToAnchor(string uuid)
        {
            await m_SpatialAnchorManager.SyncToRemoteAnchor(uuid, OVRSpace.StorageLocation.Cloud);

            if (!loadedScene)
            {
                ovrSceneManager.LoadSceneModel();
                loadedScene = true;
            }
        }
#endif // OCULUS_SUPPORTED
    }
}
