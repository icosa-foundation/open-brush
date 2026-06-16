using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID
using Steamworks;
#endif

namespace TiltBrush
{
    public sealed class SteamManager : MonoBehaviour
    {
        private static SteamManager m_Instance;
        private static bool m_Initialized;

        public static bool Initialized => m_Initialized;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstance()
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
            InitializeSteam();
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            if (m_Initialized)
            {
                SteamAPI.RunCallbacks();
            }
#endif
        }

        private void OnDestroy()
        {
            if (m_Instance != this)
            {
                return;
            }

#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            if (m_Initialized)
            {
                SteamAPI.Shutdown();
            }
#endif
            m_Initialized = false;
            m_Instance = null;
        }

        private static void InitializeSteam()
        {
#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || UNITY_ANDROID
            if (m_Initialized)
            {
                return;
            }

            try
            {
                string errorMessage;
                var result = SteamAPI.InitEx(out errorMessage);
                if (result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                {
                    m_Initialized = true;
                    return;
                }

                Debug.LogWarning($"[OBSteamworks] SteamAPI_Init failed: {result} {errorMessage}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[OBSteamworks] SteamAPI_Init unavailable: {ex.Message}");
            }
#endif
        }
    }
}
