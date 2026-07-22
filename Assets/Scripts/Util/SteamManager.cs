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
using System.Collections.Generic;
using System.Linq;
using System.Net;
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Runtime.InteropServices;
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
using Steamworks;
#endif
using UnityEngine;

namespace TiltBrush
{
    /// Owns the Steamworks client lifecycle used by Android builds running under Lepton.
    /// Steamworks is also available in the Editor so the overlay URL path can be forced for testing.
    public sealed class SteamManager : MonoBehaviour
    {
        private static SteamManager m_Instance;
        private static bool m_Initialized;
        private static bool? m_RunningUnderSteam;
        private static IPAddress[] m_LeptonHostGatewayAddresses = Array.Empty<IPAddress>();
        private static string m_LastLeptonGatewayDiagnostic;
        private const float kLeptonGatewayRetrySeconds = 1.0f;
        private const float kLeptonGatewayRefreshSeconds = 5.0f;
        private float m_NextLeptonGatewayRefreshTime;

        public static bool RunningUnderLepton =>
            Application.platform == RuntimePlatform.Android && RunningUnderSteam;

        public static bool RunningUnderSteam
        {
            get
            {
                DetectSteam();
                return m_RunningUnderSteam ?? false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            m_Instance = null;
            m_Initialized = false;
            m_RunningUnderSteam = null;
            m_LeptonHostGatewayAddresses = Array.Empty<IPAddress>();
            m_LastLeptonGatewayDiagnostic = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnAndroid()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                EnsureInstance();
            }
        }

        public static bool TryOpenOverlayUrl(string url)
        {
            EnsureInstance();
            if (RunningUnderLepton)
            {
                DetectLeptonHostGateways();
                m_Instance.ScheduleLeptonGatewayRefresh();
            }

            if (!m_Initialized)
            {
                Debug.LogWarning("[STEAM_BROWSER] Steamworks is not initialized");
                return false;
            }

            try
            {
                if (!SteamClientApi.IsOverlayEnabled())
                {
                    Debug.LogWarning("[STEAM_BROWSER] Steam overlay is not enabled");
                    return false;
                }

                SteamClientApi.OpenOverlayUrl(url);
                Debug.Log($"[STEAM_BROWSER] Requested overlay URL: {url}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[STEAM_BROWSER] Steam overlay call failed: {ex.Message}");
                return false;
            }
        }

        private static void EnsureInstance()
        {
            if (m_Instance != null)
            {
                return;
            }

            var gameObject = new GameObject("SteamManager");
            DontDestroyOnLoad(gameObject);
            m_Instance = gameObject.AddComponent<SteamManager>();
        }

        private void Awake()
        {
            if (m_Instance != null && m_Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            m_Instance = this;
            DontDestroyOnLoad(gameObject);
            DetectSteam();
            InitializeSteamworks();
            DetectLeptonHostGateways();
            ScheduleLeptonGatewayRefresh();
        }

        public static bool IsLeptonHostAddress(IPAddress address)
        {
            if (address == null || !RunningUnderLepton)
            {
                return false;
            }

            var normalizedAddress = address.IsIPv4MappedToIPv6
                ? address.MapToIPv4()
                : address;
            var gatewaySnapshot = m_LeptonHostGatewayAddresses;
            return gatewaySnapshot.Contains(normalizedAddress);
        }

        private static void DetectLeptonHostGateways()
        {
            if (!RunningUnderLepton)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var detectedGateways = new HashSet<IPAddress>();
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var connectivityManager =
                    activity.Call<AndroidJavaObject>("getSystemService", "connectivity");
                using var activeNetwork =
                    connectivityManager.Call<AndroidJavaObject>("getActiveNetwork");
                if (activeNetwork == null)
                {
                    SetLeptonHostGateways(
                        Array.Empty<IPAddress>(), "Android has no active network");
                    return;
                }

                using var linkProperties = connectivityManager.Call<AndroidJavaObject>(
                    "getLinkProperties", activeNetwork);
                if (linkProperties == null)
                {
                    SetLeptonHostGateways(
                        Array.Empty<IPAddress>(), "No link properties for the active network");
                    return;
                }

                var interfaceName = linkProperties.Call<string>("getInterfaceName");
                using var routes = linkProperties.Call<AndroidJavaObject>("getRoutes");
                var routeCount = routes.Call<int>("size");
                for (var i = 0; i < routeCount; ++i)
                {
                    using var route = routes.Call<AndroidJavaObject>("get", i);
                    using var destination = route.Call<AndroidJavaObject>("getDestination");
                    if (destination.Call<int>("getPrefixLength") != 0)
                    {
                        continue;
                    }

                    using var gateway = route.Call<AndroidJavaObject>("getGateway");
                    var gatewayAddress = gateway?.Call<string>("getHostAddress");
                    if (IPAddress.TryParse(gatewayAddress, out var parsedGateway))
                    {
                        detectedGateways.Add(parsedGateway.IsIPv4MappedToIPv6
                            ? parsedGateway.MapToIPv4()
                            : parsedGateway);
                    }
                }

                SetLeptonHostGateways(
                    detectedGateways.ToArray(), $"Active interface: {interfaceName}");
            }
            catch (Exception ex)
            {
                SetLeptonHostGateways(
                    Array.Empty<IPAddress>(), $"Failed to detect host gateways: {ex.Message}");
            }
#endif
        }

        private static void SetLeptonHostGateways(IPAddress[] gateways, string context)
        {
            m_LeptonHostGatewayAddresses = gateways;

            // LEPTON_HTTP_DIAGNOSTICS_BEGIN: Remove this diagnostic state and logging once the
            // Lepton host gateway behavior has been confirmed on hardware.
            var gatewayList = gateways.Length == 0
                ? "none"
                : string.Join(", ", gateways.Select(x => x.ToString()));
            var diagnostic = $"{context}; default gateways: {gatewayList}";
            if (diagnostic != m_LastLeptonGatewayDiagnostic)
            {
                m_LastLeptonGatewayDiagnostic = diagnostic;
                Debug.Log($"[LEPTON_HTTP] {diagnostic}");
            }
            // LEPTON_HTTP_DIAGNOSTICS_END
        }

        private void ScheduleLeptonGatewayRefresh()
        {
            var refreshDelay = m_LeptonHostGatewayAddresses.Length == 0
                ? kLeptonGatewayRetrySeconds
                : kLeptonGatewayRefreshSeconds;
            m_NextLeptonGatewayRefreshTime = Time.realtimeSinceStartup + refreshDelay;
        }

        private void Update()
        {
            if (RunningUnderLepton &&
                Time.realtimeSinceStartup >= m_NextLeptonGatewayRefreshTime)
            {
                DetectLeptonHostGateways();
                ScheduleLeptonGatewayRefresh();
            }

            if (m_Initialized)
            {
                SteamClientApi.RunCallbacks();
            }
        }

        private void OnDestroy()
        {
            if (m_Instance != this)
            {
                return;
            }

            if (m_Initialized)
            {
                SteamClientApi.Shutdown();
            }

            m_Initialized = false;
            m_Instance = null;
        }

        private static void DetectSteam()
        {
            if (m_RunningUnderSteam.HasValue)
            {
                return;
            }

            try
            {
                m_RunningUnderSteam = SteamClientApi.IsSteamRunning();
                Debug.Log($"[STEAM_BROWSER] Steam client running: {m_RunningUnderSteam.Value}");
            }
            catch (Exception ex)
            {
                m_RunningUnderSteam = false;
                Debug.LogWarning($"[STEAM_BROWSER] Unable to detect Steam client: {ex.Message}");
            }
        }

        private static void InitializeSteamworks()
        {
            if (m_Initialized)
            {
                return;
            }

#if !UNITY_ANDROID || UNITY_EDITOR
            if (!RunningUnderSteam)
            {
                return;
            }
#endif

            try
            {
                m_Initialized = SteamClientApi.Initialize(out string errorMessage);
                if (m_Initialized)
                {
                    // SteamAPI_IsSteamRunning currently reports false in Lepton even when the
                    // process was launched by Steam with a valid SteamAppId. A successful
                    // SteamAPI_InitFlat call is authoritative here.
                    m_RunningUnderSteam = true;
                    Debug.Log("[STEAM_BROWSER] Steamworks initialized");
                }
                else
                {
                    Debug.LogWarning($"[STEAM_BROWSER] Steamworks initialization failed: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[STEAM_BROWSER] Steamworks initialization unavailable: {ex.Message}");
            }
        }

        private static class SteamClientApi
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            private const string NativeLibrary = "steam_api";
            private const int SteamErrorMessageSize = 1024;
            private const int InitResultOk = 0;
            // Steam Frame currently retains a modal browser session across Android app
            // restarts and ignores later navigation requests. Default mode allows Steam to
            // open or select a normal overlay browser page for each request.
            private const int OverlayPageModeDefault = 0;
            private const string SteamClientVersion = "SteamClient023";
            private const string SteamFriendsVersion = "SteamFriends018";
            private const string SteamUtilsVersion = "SteamUtils010";

            private static IntPtr m_SteamFriends;
            private static IntPtr m_SteamUtils;

            public static bool IsSteamRunning() => NativeIsSteamRunning();

            public static bool Initialize(out string errorMessage)
            {
                var errorBuffer = Marshal.AllocHGlobal(SteamErrorMessageSize);
                try
                {
                    var emptyBuffer = new byte[SteamErrorMessageSize];
                    Marshal.Copy(emptyBuffer, 0, errorBuffer, emptyBuffer.Length);
                    var result = NativeInitFlat(errorBuffer);
                    errorMessage = Marshal.PtrToStringAnsi(errorBuffer) ?? string.Empty;
                    if (result != InitResultOk)
                    {
                        return false;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(errorBuffer);
                }

                var steamClient = NativeCreateInterface(SteamClientVersion);
                if (steamClient == IntPtr.Zero)
                {
                    errorMessage = "Steam client interface was unavailable";
                    NativeShutdown();
                    return false;
                }

                var steamUser = NativeGetHSteamUser();
                var steamPipe = NativeGetHSteamPipe();
                m_SteamFriends = NativeGetSteamFriends(
                    steamClient, steamUser, steamPipe, SteamFriendsVersion);
                m_SteamUtils = NativeGetSteamUtils(steamClient, steamPipe, SteamUtilsVersion);
                if (m_SteamFriends != IntPtr.Zero && m_SteamUtils != IntPtr.Zero)
                {
                    return true;
                }

                errorMessage = "Steam client interfaces were unavailable";
                NativeShutdown();
                return false;
            }

            public static bool IsOverlayEnabled() => NativeIsOverlayEnabled(m_SteamUtils);

            public static void OpenOverlayUrl(string url)
            {
                NativeActivateGameOverlayToWebPage(m_SteamFriends, url, OverlayPageModeDefault);
            }

            public static void RunCallbacks() => NativeRunCallbacks();

            public static void Shutdown()
            {
                NativeShutdown();
                m_SteamFriends = IntPtr.Zero;
                m_SteamUtils = IntPtr.Zero;
            }

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_IsSteamRunning",
                CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private static extern bool NativeIsSteamRunning();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_InitFlat",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern int NativeInitFlat(IntPtr errorMessage);

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_Shutdown",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern void NativeShutdown();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_RunCallbacks",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern void NativeRunCallbacks();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_GetHSteamPipe",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern int NativeGetHSteamPipe();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_GetHSteamUser",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern int NativeGetHSteamUser();

            [DllImport(NativeLibrary, EntryPoint = "SteamInternal_CreateInterface",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr NativeCreateInterface(
                [MarshalAs(UnmanagedType.LPUTF8Str)] string version);

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_ISteamClient_GetISteamFriends",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr NativeGetSteamFriends(
                IntPtr steamClient,
                int steamUser,
                int steamPipe,
                [MarshalAs(UnmanagedType.LPUTF8Str)] string version);

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_ISteamClient_GetISteamUtils",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr NativeGetSteamUtils(
                IntPtr steamClient,
                int steamPipe,
                [MarshalAs(UnmanagedType.LPUTF8Str)] string version);

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_ISteamUtils_IsOverlayEnabled",
                CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private static extern bool NativeIsOverlayEnabled(IntPtr steamUtils);

            [DllImport(NativeLibrary,
                EntryPoint = "SteamAPI_ISteamFriends_ActivateGameOverlayToWebPage",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern void NativeActivateGameOverlayToWebPage(
                IntPtr steamFriends,
                [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
                int mode);
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX
            public static bool IsSteamRunning() => SteamAPI.IsSteamRunning();

            public static bool Initialize(out string errorMessage)
            {
                var result = SteamAPI.InitEx(out errorMessage);
                return result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
            }

            public static bool IsOverlayEnabled() => SteamUtils.IsOverlayEnabled();

            public static void OpenOverlayUrl(string url)
            {
                SteamFriends.ActivateGameOverlayToWebPage(
                    url,
                    EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Modal);
            }

            public static void RunCallbacks() => SteamAPI.RunCallbacks();

            public static void Shutdown() => SteamAPI.Shutdown();
#else
            public static bool IsSteamRunning() => false;

            public static bool Initialize(out string errorMessage)
            {
                errorMessage = "Steamworks is not supported on this platform";
                return false;
            }

            public static bool IsOverlayEnabled() => false;

            public static void OpenOverlayUrl(string url) { }

            public static void RunCallbacks() { }

            public static void Shutdown() { }
#endif
        }
    }
}
