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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace TiltBrush
{
    /// <summary>
    /// User-facing Android XR hand-tracking permission flow.
    ///
    /// Important:
    /// - Hand Interaction Profile (pinch / poke / grasp) can continue to drive
    ///   AndroidXRHandBridge independently.
    /// - This permission is only for XRHandSubsystem joint data / articulated
    ///   hand visualisation.
    /// - No custom scripting define is required.
    /// </summary>
    public class AndroidXRHandTrackingPermission : MonoBehaviour
    {
        public const string HandTrackingPermission =
            "android.permission.HAND_TRACKING";

        [Header("Startup")]
        [SerializeField]
        [Tooltip("Request HAND_TRACKING automatically when this component starts.")]
        private bool requestOnStart = true;

        [SerializeField]
        [Min(0f)]
        [Tooltip("Small delay so the Android XR activity/OpenXR session is fully visible before the permission sheet is requested.")]
        private float startupDelay = 0.5f;

        [Header("Hand subsystem")]
        [SerializeField]
        [Tooltip("Restart XRHandSubsystem after permission is granted so joint delivery begins immediately.")]
        private bool restartHandSubsystemAfterGrant = true;

        [Header("Debug")]
        [SerializeField]
        private bool debugLogging = true;

        private bool m_RequestInFlight;
        private bool m_WasFocused;

        private readonly List<XRHandSubsystem> m_HandSubsystems = new();

        public bool IsPermissionGranted
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return Permission.HasUserAuthorizedPermission(
                    HandTrackingPermission);
#else
                return true;
#endif
            }
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Log(
                "startup " +
                $"permissionGranted={IsPermissionGranted}");

            if (IsPermissionGranted)
            {
                if (restartHandSubsystemAfterGrant)
                {
                    StartCoroutine(
                        RestartHandSubsystemWhenAvailable());
                }

                return;
            }

            if (requestOnStart)
            {
                StartCoroutine(
                    RequestAfterStartup());
            }
#else
            Log("non-Android/editor build; no permission request required");
#endif
        }

        private IEnumerator RequestAfterStartup()
        {
            if (startupDelay > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    startupDelay);
            }

            // Android permission UI is much more reliable once the app is the
            // focused foreground activity.
            while (!Application.isFocused)
            {
                yield return null;
            }

            RequestPermission();
        }

        /// <summary>
        /// Can also be wired to an Open Brush UI button so the user can retry
        /// after initially denying the permission.
        /// </summary>
        public void RequestPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_RequestInFlight)
            {
                Log("request already in flight");
                return;
            }

            if (IsPermissionGranted)
            {
                Log("permission already granted");

                if (restartHandSubsystemAfterGrant)
                {
                    StartCoroutine(
                        RestartHandSubsystemWhenAvailable());
                }

                return;
            }

            m_RequestInFlight = true;

            Log(
                "requesting " +
                HandTrackingPermission);

            var callbacks =
                new PermissionCallbacks();

            callbacks.PermissionGranted +=
                OnPermissionGranted;

            callbacks.PermissionDenied +=
                OnPermissionDenied;

            callbacks.PermissionDeniedAndDontAskAgain +=
                OnPermissionDeniedAndDontAskAgain;

            Permission.RequestUserPermission(
                HandTrackingPermission,
                callbacks);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnPermissionGranted(
            string permission)
        {
            m_RequestInFlight = false;

            Log(
                "GRANTED " +
                permission);

            if (restartHandSubsystemAfterGrant)
            {
                StartCoroutine(
                    RestartHandSubsystemWhenAvailable());
            }
        }

        private void OnPermissionDenied(
            string permission)
        {
            m_RequestInFlight = false;

            Debug.LogWarning(
                "ANDROID_XR_HAND_PERMISSION: DENIED " +
                permission,
                this);
        }

        private void OnPermissionDeniedAndDontAskAgain(
            string permission)
        {
            m_RequestInFlight = false;

            Debug.LogWarning(
                "ANDROID_XR_HAND_PERMISSION: PERMANENTLY DENIED " +
                permission +
                ". User must enable Hand Tracking in Android app settings.",
                this);
        }
#endif

        private void OnApplicationFocus(
            bool hasFocus)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Permission sheets temporarily remove focus. On return, verify the
            // real Android permission state rather than trusting only callbacks.
            if (hasFocus && !m_WasFocused)
            {
                bool granted =
                    IsPermissionGranted;

                Log(
                    "focus returned " +
                    $"permissionGranted={granted}");

                if (granted &&
                    restartHandSubsystemAfterGrant)
                {
                    StartCoroutine(
                        RestartHandSubsystemWhenAvailable());
                }
            }

            m_WasFocused = hasFocus;
#endif
        }

        private IEnumerator RestartHandSubsystemWhenAvailable()
        {
            // XRHandSubsystem can be created slightly after this permission
            // component depending on OpenXR startup order.
            const float timeout = 5f;
            float startedAt =
                Time.realtimeSinceStartup;

            while (
                Time.realtimeSinceStartup - startedAt <
                timeout)
            {
                m_HandSubsystems.Clear();

                SubsystemManager.GetSubsystems(
                    m_HandSubsystems);

                if (m_HandSubsystems.Count > 0)
                {
                    foreach (
                        XRHandSubsystem subsystem in
                        m_HandSubsystems)
                    {
                        if (subsystem == null)
                            continue;

                        Log(
                            "restarting XRHandSubsystem " +
                            $"wasRunning={subsystem.running}");

                        if (subsystem.running)
                        {
                            subsystem.Stop();
                        }

                        subsystem.Start();

                        Log(
                            "XRHandSubsystem after restart " +
                            $"running={subsystem.running} " +
                            $"L={subsystem.leftHand.isTracked} " +
                            $"R={subsystem.rightHand.isTracked}");
                    }

                    yield break;
                }

                yield return null;
            }

            Debug.LogWarning(
                "ANDROID_XR_HAND_PERMISSION: permission granted but no " +
                "XRHandSubsystem appeared within 5 seconds. Check that " +
                "'Hand Tracking Subsystem' is enabled in OpenXR features.",
                this);
        }

        /// <summary>
        /// Call this from UI if the user selected "Don't ask again".
        /// Opens Android's App Info page where permissions can be changed.
        /// </summary>
        public void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var unityPlayer =
                    new AndroidJavaClass(
                        "com.unity3d.player.UnityPlayer");

                using AndroidJavaObject activity =
                    unityPlayer.GetStatic<AndroidJavaObject>(
                        "currentActivity");

                using var intent =
                    new AndroidJavaObject(
                        "android.content.Intent",
                        "android.settings.APPLICATION_DETAILS_SETTINGS");

                using var uriClass =
                    new AndroidJavaClass(
                        "android.net.Uri");

                using AndroidJavaObject uri =
                    uriClass.CallStatic<AndroidJavaObject>(
                        "parse",
                        "package:" +
                        Application.identifier);

                intent.Call<AndroidJavaObject>(
                    "setData",
                    uri);

                activity.Call(
                    "startActivity",
                    intent);

                Log(
                    "opened Android app settings");
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    "ANDROID_XR_HAND_PERMISSION: failed to open app settings: " +
                    e,
                    this);
            }
#endif
        }

        private void Log(
            string message)
        {
            if (!debugLogging)
                return;

            Debug.Log(
                "ANDROID_XR_HAND_PERMISSION: " +
                message,
                this);
        }
    }
}