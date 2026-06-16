using System;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID
using Steamworks;
#endif

namespace TiltBrush
{
    public static class SteamOverlayBrowser
    {
        public static void OpenUrl(string url)
        {
            try
            {
                if (SteamManager.Initialized && SteamUtils.IsOverlayEnabled())
                {
                    SteamFriends.ActivateGameOverlayToWebPage(
                        url,
                        EActivateGameOverlayToWebPageMode.k_EActivateGameOverlayToWebPageMode_Modal);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OBSteamworks] Steam overlay browser unavailable: {ex.Message}");
            }

            Application.OpenURL(url);
        }
    }
}
