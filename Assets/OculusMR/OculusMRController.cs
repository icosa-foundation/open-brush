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
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenBrush.Multiplayer;
using UnityEngine;

namespace TiltBrush
{
    public class OculusMRController : MonoBehaviour
    {
#if OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
        public static OculusMRController m_Instance;

        public OVRSceneManager ovrSceneManager;
        public SpatialAnchorManager m_SpatialAnchorManager;

        private bool loadedScene;

        private bool host;
        private bool started;

        public bool IsHosting => host;
        public bool IsStarted => started;

        void Awake()
        {
            m_Instance = this;

            ovrSceneManager = GetComponent<OVRSceneManager>();
            m_SpatialAnchorManager = GetComponent<SpatialAnchorManager>();
            Debug.Log($"[Colocation] Oculus MR controller awake. Scene manager: {ovrSceneManager != null}. Spatial anchor manager: {m_SpatialAnchorManager != null}.");
        }

        Task<bool> RequestScenePermissionAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            const string permissionString = "com.oculus.permission.USE_SCENE";
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permissionString))
            {
                Debug.Log("[Colocation] Scene permission is already granted.");
                return Task.FromResult(true);
            }

            Debug.Log("[Colocation] Requesting Meta scene permission.");
            var completion = new TaskCompletionSource<bool>();
            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ =>
            {
                Debug.Log("[Colocation] Scene permission granted.");
                completion.TrySetResult(true);
            };
            callbacks.PermissionDenied += _ =>
            {
                Debug.LogWarning("[Colocation] Scene permission denied.");
                completion.TrySetResult(false);
            };
            callbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                Debug.LogWarning("[Colocation] Scene permission denied with do-not-ask-again.");
                completion.TrySetResult(false);
            };
            UnityEngine.Android.Permission.RequestUserPermission(permissionString, callbacks);
            return completion.Task;
#else
            Debug.Log("[Colocation] Scene permission check bypassed outside an Android player.");
            return Task.FromResult(true);
#endif
        }

        public async void StartMRExperience(bool isHosting)
        {
            float operationStartedAt = Time.realtimeSinceStartup;
            string role = isHosting ? "host" : "joiner";
            try
            {
                Debug.Log($"[Colocation] Starting MR experience as {role}. Already started: {started}.");
                if (started)
                {
                    Debug.LogWarning($"[Colocation] Start ignored because the MR experience has already started as {(host ? "host" : "joiner")}.");
                    return;
                }

                float permissionStartedAt = Time.realtimeSinceStartup;
                bool scenePermissionGranted = await RequestScenePermissionAsync();
                Debug.Log(
                    $"[ColocationDiag] Scene permission operation completed. Role: {role}. " +
                    $"Granted: {scenePermissionGranted}. DurationSeconds: {Time.realtimeSinceStartup - permissionStartedAt:F3}.");
                if (!scenePermissionGranted)
                {
                    Debug.LogWarning("[Colocation] Scene permission is required to start the MR experience.");
                    return;
                }

                started = true;
                host = isHosting;
                Debug.Log($"[Colocation] Scene permission ready. Role set to {(host ? "host" : "joiner")}.");

                if (host)
                {
                    Debug.Log("[Colocation] Host is creating the origin spatial anchor.");
                    float anchorStartedAt = Time.realtimeSinceStartup;
                    bool anchorCreated = await m_SpatialAnchorManager.CreateSpatialAnchor();
                    Debug.Log(
                        $"[ColocationDiag] Host anchor creation and save completed. Success: {anchorCreated}. " +
                        $"DurationSeconds: {Time.realtimeSinceStartup - anchorStartedAt:F3}.");
                    if (!anchorCreated)
                    {
                        Debug.LogError("[Colocation] Host failed to create and save the colocation spatial anchor.");
                        started = false;
                        return;
                    }

                    Debug.Log($"[Colocation] Host spatial anchor ready. UUID: {m_SpatialAnchorManager.AnchorUuid}.");
                    if (!m_SpatialAnchorManager.SceneLocalizeToAnchor())
                    {
                        Debug.LogError("[Colocation] Host failed to localize the scene to the colocation spatial anchor.");
                        started = false;
                        return;
                    }

                    LoadSceneModel();
                    Debug.Log("[Colocation] Host is joining Photon room OculusMRRoom.");
                    float roomJoinStartedAt = Time.realtimeSinceStartup;
                    bool joinedRoom = await MultiplayerManager.m_Instance.JoinRoom(new RoomCreateData()
                    {
                        roomName = "OculusMRRoom",
                        maxPlayers = 12
                    });
                    Debug.Log(
                        $"[ColocationDiag] Photon room join duration. Role: host. " +
                        $"DurationSeconds: {Time.realtimeSinceStartup - roomJoinStartedAt:F3}.");
                    Debug.Log($"[Colocation] Host room join completed. Success: {joinedRoom}. Multiplayer state: {MultiplayerManager.m_Instance.State}. Error: {MultiplayerManager.m_Instance.LastError ?? "none"}.");
                }
                else
                {
                    Debug.Log("[Colocation] Joiner is joining Photon room OculusMRRoom.");
                    float roomJoinStartedAt = Time.realtimeSinceStartup;
                    bool joinedRoom = await MultiplayerManager.m_Instance.JoinRoom(new RoomCreateData()
                    {
                        roomName = "OculusMRRoom",
                        maxPlayers = 12
                    });
                    Debug.Log(
                        $"[ColocationDiag] Photon room join duration. Role: joiner. " +
                        $"DurationSeconds: {Time.realtimeSinceStartup - roomJoinStartedAt:F3}.");
                    Debug.Log($"[Colocation] Joiner room join completed. Success: {joinedRoom}. Multiplayer state: {MultiplayerManager.m_Instance.State}. Error: {MultiplayerManager.m_Instance.LastError ?? "none"}.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ColocationDiag] Unhandled exception at the MR experience entry boundary. " +
                    $"Role: {role}. Started: {started}. DurationSeconds: {Time.realtimeSinceStartup - operationStartedAt:F3}. " +
                    $"Exception: {exception}");
            }
        }

        public async void RemoteSyncToAnchor(string uuid)
        {
            float operationStartedAt = Time.realtimeSinceStartup;
            try
            {
                Debug.Log($"[Colocation] Received request to synchronize to remote anchor UUID {uuid}.");
                bool success = await m_SpatialAnchorManager.SyncToRemoteAnchor(
                    uuid, OVRSpace.StorageLocation.Cloud);

                Debug.Log(
                    $"[ColocationDiag] Remote anchor synchronization completed. Success: {success}. " +
                    $"DurationSeconds: {Time.realtimeSinceStartup - operationStartedAt:F3}.");
                if (!success)
                {
                    Debug.LogError($"[Colocation] Failed to synchronize to remote colocation anchor UUID {uuid}.");
                    return;
                }

                Debug.Log($"[Colocation] Successfully localized to remote anchor UUID {uuid}.");
                LoadSceneModel();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ColocationDiag] Unhandled exception at the remote anchor synchronization boundary. " +
                    $"UUID: {uuid}. DurationSeconds: {Time.realtimeSinceStartup - operationStartedAt:F3}. " +
                    $"Exception: {exception}");
            }
        }

        private void LoadSceneModel()
        {
            if (loadedScene)
            {
                Debug.Log("[Colocation] Scene model load skipped because it was already requested.");
                return;
            }

            Debug.Log("[Colocation] Requesting Meta scene model load.");
            ovrSceneManager.LoadSceneModel();
            loadedScene = true;
        }
#endif // OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
    }
}
