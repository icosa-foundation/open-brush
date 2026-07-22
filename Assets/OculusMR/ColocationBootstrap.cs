// Copyright 2026 The Open Brush Authors
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

using UnityEngine;
using OpenBrush.Multiplayer;
#if OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
using OVRPlatform = Oculus.Platform;
#endif

namespace TiltBrush
{
    public class ColocationBootstrap : MonoBehaviour
    {
        private static ColocationBootstrap m_Instance;

        [SerializeField] private GameObject m_OculusMrPrefab;

        public static bool IsSupported
        {
            get
            {
#if OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
                return m_Instance != null && OculusMRController.m_Instance != null;
#else
                return false;
#endif
            }
        }

        private void Awake()
        {
            m_Instance = this;

#if OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
            Debug.Log($"[Colocation] Bootstrap awake. Prefab assigned: {m_OculusMrPrefab != null}. Existing controller: {OculusMRController.m_Instance != null}.");
            if (OculusMRController.m_Instance == null)
            {
                if (m_OculusMrPrefab == null)
                {
                    Debug.LogError("[Colocation] Oculus MR prefab is not assigned to the bootstrap.");
                    return;
                }

                var instance = Instantiate(m_OculusMrPrefab);
                Debug.Log($"[Colocation] Instantiated Oculus MR prefab: {instance.name}.");
            }
#endif
        }

        public static bool TryStart(bool isHosting)
        {
#if OCULUS_COLOCATION_SUPPORTED && UNITY_ANDROID
            string buildStamp = App.Config != null ? App.Config.m_BuildStamp : "unavailable";
            Debug.Log(
                $"[ColocationDiag] Start context. Role: {(isHosting ? "host" : "joiner")}. " +
                $"Application version: {Application.version}. Build stamp: {buildStamp}. " +
                $"Device: {SystemInfo.deviceModel}. OS: {SystemInfo.operatingSystem}. " +
                $"Meta Platform initialized: {OVRPlatform.Core.IsInitialized()}. " +
                $"Bootstrap: {m_Instance != null}. Controller: {OculusMRController.m_Instance != null}. " +
                $"Multiplayer: {MultiplayerManager.m_Instance != null}.");
            Debug.Log($"[Colocation] Start requested. Role: {(isHosting ? "host" : "joiner")}. Bootstrap: {m_Instance != null}. Controller: {OculusMRController.m_Instance != null}. Multiplayer: {MultiplayerManager.m_Instance != null}. Multiplayer state: {(MultiplayerManager.m_Instance != null ? MultiplayerManager.m_Instance.State.ToString() : "unavailable")}.");
            if (!IsSupported || MultiplayerManager.m_Instance == null)
            {
                Debug.LogWarning($"[Colocation] Start prerequisites unavailable. IsSupported: {IsSupported}. Multiplayer: {MultiplayerManager.m_Instance != null}.");
                return false;
            }

            OculusMRController.m_Instance.StartMRExperience(isHosting);
            return true;
#else
            Debug.LogWarning($"[Colocation] Start requested for role {(isHosting ? "host" : "joiner")} in a build without Android colocation support.");
            return false;
#endif
        }
    }
}
