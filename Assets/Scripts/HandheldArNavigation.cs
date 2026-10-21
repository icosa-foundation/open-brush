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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace TiltBrush
{
    /// Optional handheld AR navigation for the no-headset view mode on Android phones.
    ///
    /// While active, the phone's ARCore-tracked pose drives the camera's local pose, just as a
    /// headset's pose does in VR, so touch navigation keeps moving the scene rather than the
    /// camera. The ARCore loader is kept out of the automatic loader search: otherwise a phone
    /// without an OpenXR runtime would fall through to ARCore and be mistaken for a headset.
    public class HandheldArNavigation : MonoBehaviour
    {
        public enum State
        {
            Off,
            Starting,
            Active,
        }

        private const string kArCoreLoaderType = "UnityEngine.XR.ARCore.ARCoreLoader";
        // Not applied while ARCore is being installed from the Play Store.
        private const float kStartupTimeout = 20f;

        private static XRLoader sm_ArLoader;
        private static List<XRLoader> sm_HeadsetLoaders;
        private static HandheldArNavigation sm_Instance;
        private static State sm_State = State.Off;

        public static event Action StateChanged;

        /// Why the last attempt to start failed, or null.
        public static string LastError { get; private set; }

        public static State CurrentState => sm_State;
        public static bool IsActive => sm_State == State.Active;

        /// True only in the no-headset fallback of a build that includes ARCore.
        public static bool IsSupported =>
            sm_ArLoader != null &&
            App.Config.m_SdkMode == SdkMode.UnityXR &&
            !App.VrSdk.IsHmdInitialized();

        public static bool IsArLoader(XRLoader loader) => loader != null && loader == sm_ArLoader;

        private ARSession m_Session;
        private ARCameraManager m_CameraManager;
        private UnityEngine.SpatialTracking.TrackedPoseDriver m_PoseDriver;
        private bool m_PoseDriverWasEnabled;
        private InputAction m_PositionAction;
        private InputAction m_RotationAction;

        private Vector3 m_CameraStartPosition;
        private Quaternion m_CameraStartRotation;
        private bool m_Anchored;
        private Vector3 m_ArStartPosition;
        private Quaternion m_YawOffset;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ExcludeArLoaderFromHeadsetSearch()
        {
            // Editor play mode modifies the settings asset in place, and ARCore only runs on device.
            if (Application.platform != RuntimePlatform.Android)
            {
                return;
            }
            XRManagerSettings manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null)
            {
                return;
            }
            XRLoader loader = manager.activeLoaders.FirstOrDefault(
                l => l != null && l.GetType().FullName == kArCoreLoaderType);
            if (loader == null)
            {
                return;
            }
            if (!manager.TryRemoveLoader(loader))
            {
                Debug.LogWarning("HandheldArNavigation: could not remove the ARCore loader from the XR loader list");
                return;
            }
            sm_ArLoader = loader;
            sm_HeadsetLoaders = manager.activeLoaders.ToList();
        }

        public static void Toggle()
        {
            SetActive(sm_State == State.Off);
        }

        public static void SetActive(bool active)
        {
            if (active)
            {
                if (sm_State != State.Off || !IsSupported)
                {
                    return;
                }
                LastError = null;
                var go = new GameObject("Handheld AR Navigation");
                go.transform.SetParent(App.Instance.transform, false);
                sm_Instance = go.AddComponent<HandheldArNavigation>();
                SetState(State.Starting);
                sm_Instance.StartCoroutine(sm_Instance.Startup());
            }
            else if (sm_Instance != null)
            {
                sm_Instance.Shutdown(null);
            }
        }

        private static void SetState(State state)
        {
            sm_State = state;
            StateChanged?.Invoke();
        }

        private IEnumerator Startup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Ask up front so a refusal can be reported, rather than leaving a session that
            // never tracks.
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Camera))
            {
                bool? granted = null;
                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ => granted = true;
                callbacks.PermissionDenied += _ => granted = false;
                UnityEngine.Android.Permission.RequestUserPermission(
                    UnityEngine.Android.Permission.Camera, callbacks);
                while (granted == null)
                {
                    yield return null;
                }
                if (granted == false)
                {
                    Shutdown("AR navigation needs camera access");
                    yield break;
                }
            }
#endif

            XRManagerSettings manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null || manager.activeLoader != null ||
                !manager.TrySetLoaders(new List<XRLoader> { sm_ArLoader }))
            {
                Shutdown("AR could not be initialized");
                yield break;
            }
            manager.InitializeLoaderSync();
            if (manager.activeLoader != sm_ArLoader)
            {
                Shutdown("AR could not be initialized");
                yield break;
            }
            manager.StartSubsystems();

            // ARSession checks availability and offers to install ARCore itself.
            var sessionObject = new GameObject("AR Session");
            sessionObject.transform.SetParent(transform, false);
            m_Session = sessionObject.AddComponent<ARSession>();
            // Otherwise the app is limited to the camera's frame rate.
            m_Session.matchFrameRateRequested = false;

            float deadline = Time.realtimeSinceStartup + kStartupTimeout;
            bool sawInstall = false;
            while (true)
            {
                ARSessionState state = ARSession.state;
                if (state == ARSessionState.Installing)
                {
                    sawInstall = true;
                    deadline = Time.realtimeSinceStartup + kStartupTimeout;
                }
                if (state >= ARSessionState.Ready || state == ARSessionState.Unsupported ||
                    (state == ARSessionState.NeedsInstall && sawInstall) ||
                    Time.realtimeSinceStartup > deadline)
                {
                    break;
                }
                yield return null;
            }
            if (ARSession.state < ARSessionState.Ready)
            {
                Shutdown(ARSession.state == ARSessionState.Unsupported
                    ? "AR is not supported on this device"
                    : "ARCore is not available");
                yield break;
            }

            Camera camera = App.VrSdk.GetVrCamera();
            // ARCore only produces frames, and so poses, while its camera is running. The
            // camera image itself isn't drawn: there's no ARCameraBackground.
            m_CameraManager = camera.gameObject.AddComponent<ARCameraManager>();

            // Don't let the headset pose driver fight over the camera.
            m_PoseDriver = camera.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
            if (m_PoseDriver != null)
            {
                m_PoseDriverWasEnabled = m_PoseDriver.enabled;
                m_PoseDriver.enabled = false;
            }

            m_PositionAction = new InputAction("AR Device Position", InputActionType.Value,
                "<HandheldARInputDevice>/devicePosition", expectedControlType: "Vector3");
            m_RotationAction = new InputAction("AR Device Rotation", InputActionType.Value,
                "<HandheldARInputDevice>/deviceRotation", expectedControlType: "Quaternion");
            m_PositionAction.Enable();
            m_RotationAction.Enable();

            m_CameraStartPosition = camera.transform.localPosition;
            m_CameraStartRotation = camera.transform.localRotation;
            m_Anchored = false;
            Application.onBeforeRender += ApplyPose;
            SetState(State.Active);
        }

        void Update()
        {
            if (sm_State == State.Active)
            {
                ApplyPose();
            }
        }

        private void ApplyPose()
        {
            if (ARSession.state != ARSessionState.SessionTracking ||
                m_RotationAction.activeControl == null)
            {
                // Hold the last pose while tracking is lost.
                return;
            }

            Vector3 position = m_PositionAction.ReadValue<Vector3>();
            Quaternion rotation = m_RotationAction.ReadValue<Quaternion>();
            if (!m_Anchored)
            {
                // Continue from the current view. Only yaw is realigned, so the phone's
                // gravity stays the scene's gravity.
                m_YawOffset = Quaternion.Euler(0,
                    Yaw(m_CameraStartRotation) - Yaw(rotation), 0);
                m_ArStartPosition = position;
                m_Anchored = true;
            }

            Transform cameraXf = App.VrSdk.GetVrCamera().transform;
            cameraXf.localRotation = m_YawOffset * rotation;
            cameraXf.localPosition = m_CameraStartPosition +
                m_YawOffset * (position - m_ArStartPosition);
        }

        private static float Yaw(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            if (new Vector2(forward.x, forward.z).sqrMagnitude < 1e-4f)
            {
                // Looking straight up or down: the top of the screen gives the heading.
                forward = rotation * (forward.y < 0 ? Vector3.up : Vector3.down);
            }
            return Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private void Shutdown(string error)
        {
            Application.onBeforeRender -= ApplyPose;
            m_PositionAction?.Dispose();
            m_RotationAction?.Dispose();
            m_PositionAction = null;
            m_RotationAction = null;

            // Disable before the subsystems go away so the managers stop them cleanly.
            if (m_CameraManager != null)
            {
                m_CameraManager.enabled = false;
                Destroy(m_CameraManager);
            }
            if (m_Session != null)
            {
                m_Session.enabled = false;
                Destroy(m_Session.gameObject);
            }

            XRManagerSettings manager = XRGeneralSettings.Instance?.Manager;
            if (manager != null)
            {
                if (manager.activeLoader == sm_ArLoader)
                {
                    manager.StopSubsystems();
                    manager.DeinitializeLoader();
                }
                if (manager.activeLoader == null)
                {
                    manager.TrySetLoaders(sm_HeadsetLoaders);
                }
            }

            if (m_PoseDriver != null)
            {
                m_PoseDriver.enabled = m_PoseDriverWasEnabled;
            }

            if (m_Anchored)
            {
                // Hand the view back to touch navigation where AR left it, minus any roll,
                // which touch look never introduces or removes.
                Transform cameraXf = App.VrSdk.GetVrCamera().transform;
                Vector3 euler = cameraXf.localEulerAngles;
                cameraXf.localEulerAngles = new Vector3(euler.x, euler.y, 0);
                cameraXf.localPosition = m_CameraStartPosition;
            }

            if (error != null)
            {
                Debug.LogWarning($"HandheldArNavigation: {error}");
            }
            LastError = error;
            sm_Instance = null;
            Destroy(gameObject);
            SetState(State.Off);
        }

        void OnDestroy()
        {
            if (sm_Instance == this)
            {
                Application.onBeforeRender -= ApplyPose;
                m_PositionAction?.Dispose();
                m_RotationAction?.Dispose();
                sm_Instance = null;
                sm_State = State.Off;
            }
        }
    }
}
