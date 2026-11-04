// Copyright 2020 The Tilt Brush Authors
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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using UnityEngine.XR.Management;
using UnityEngine.Rendering;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace TiltBrush
{

    public class LoadingScene : MonoBehaviour
    {
        [SerializeField] private GvrOverlay m_Overlay;
        [SerializeField] private Camera m_VrCamera;
        [SerializeField] private float m_MaximumLoadingTime;
        // Amount of the progress bar taken up by the scene load
        [SerializeField] private float m_SceneLoadRatio;

        [SerializeField] private LocalizedString m_LoadingText;
        [SerializeField] private LocalizedString m_RequestAndroidFolderPermissions;

        // We have a slightly faked loading position that will always increase
        // The fake loading rate is the minimum amount it will increase in one second, the reciprocal of
        // m_MaximumLoadingTime
        private float m_FakeLoadingRate;
        private float m_CurrentLoadingPosition;
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
        private bool m_OpenedFolderPicker;
        private XRManagerSettings m_StartupXr;
        private string m_StartupXrMode = "handoff";
        private int m_LoggedRenderFrames;
        private float m_NextStartupReport;
        private AsyncOperation m_MainLoad;

        public static void LogStartup(string message)
        {
            var manager = XRGeneralSettings.Instance?.Manager;
            var display = manager?.activeLoader?.GetLoadedSubsystem<UnityEngine.XR.XRDisplaySubsystem>();
            Debug.Log($"OBDS_TRACE t={Time.realtimeSinceStartup:F3} frame={Time.frameCount} " +
                $"focused={Application.isFocused} loader={manager?.activeLoader?.name ?? "none"} " +
                $"initialized={manager?.isInitializationComplete} displayRunning={display?.running} " +
                $"pipeline={GraphicsSettings.currentRenderPipeline?.name ?? "none"} {message}");
        }

        private void TraceRenderedFrame(ScriptableRenderContext context, Camera[] cameras)
        {
            if (m_LoggedRenderFrames++ >= 3) return;
            LogStartup($"render completed cameras={cameras.Length} scene={SceneManager.GetActiveScene().name}");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup < m_NextStartupReport) return;
            m_NextStartupReport = Time.realtimeSinceStartup + 5;
            LogStartup($"heartbeat mode={m_StartupXrMode} storage={AndroidDirectStorage.StartupReady} " +
                $"load={m_MainLoad?.progress} done={m_MainLoad?.isDone} rendered={m_LoggedRenderFrames}");
        }

        private void OnDestroy()
        {
            RenderPipelineManager.endFrameRendering -= TraceRenderedFrame;
        }
#endif
#if UNITY_ANDROID
        private bool m_FolderPermissionOverride = false;
        private bool m_WaitingForPermission = false;
#endif

        private IEnumerator Start()
        {
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            // Select comparison paths per launch with am start --es obdsStartupXr MODE.
            if (Application.platform == RuntimePlatform.Android)
            {
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var intent = activity.Call<AndroidJavaObject>("getIntent");
                    string mode = intent.Call<string>("getStringExtra", "obdsStartupXr");
                    if (mode == "disabled" || mode == "retained") m_StartupXrMode = mode;
                }
                catch (System.Exception error) { Debug.LogWarning($"OBDS_TRACE intent: {error.Message}"); }
            }
            RenderPipelineManager.endFrameRendering += TraceRenderedFrame;
            LogStartup($"loading started mode={m_StartupXrMode}");
#endif
            m_FakeLoadingRate = 1f / m_MaximumLoadingTime;
            m_CurrentLoadingPosition = 0;

            // Position screen overlay in front of the camera.
            m_Overlay.transform.parent = m_VrCamera.transform;
            m_Overlay.transform.localPosition = Vector3.zero;
            m_Overlay.transform.localRotation = Quaternion.identity;
            float scale = 0.5f * m_VrCamera.farClipPlane / m_VrCamera.transform.lossyScale.z;
            m_Overlay.transform.localScale = Vector3.one * scale;

            // Reparent the overlay so that it doesn't move with the headset.
            m_Overlay.transform.parent = transform;

            // Reset the rotation so that it's level and centered on the horizon.
            Vector3 eulerAngles = m_Overlay.transform.localRotation.eulerAngles;
            m_Overlay.transform.localRotation = Quaternion.Euler(new Vector3(0, eulerAngles.y, 0));

            m_Overlay.gameObject.SetActive(true);

            UpdateProgress(0, 0, 0);

            DontDestroyOnLoad(gameObject);

#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE && !OPEN_BRUSH_VIEWER
            if (Application.platform == RuntimePlatform.Android)
            {
                m_Overlay.MessageStatus = m_RequestAndroidFolderPermissions.GetLocalizedStringAsync().Result;
                yield return AndroidDirectStorage.Initialize(App.kAppFolderName, ShowFolderExplanation);
                if (AndroidDirectStorage.StartupCanceled) yield break;
                LogStartup($"storage ready picker={m_OpenedFolderPicker}");
                m_Overlay.MessageStatus = m_LoadingText.GetLocalizedStringAsync().Result;
                if (m_StartupXrMode == "retained" ||
                    (m_OpenedFolderPicker && m_StartupXrMode != "disabled")) RestoreStartupXr();
            }
#endif

#if UNITY_ANDROID && !OPEN_BRUSH_SCOPED_STORAGE && !OPEN_BRUSH_VIEWER
            if (Application.platform == RuntimePlatform.Android)
            {
                if (!UserHasManageExternalStoragePermission())
                {
                    m_Overlay.MessageStatus = m_RequestAndroidFolderPermissions.GetLocalizedStringAsync().Result;
                    m_WaitingForPermission = true;
                    AskForManageStoragePermission();
                    while (!UserHasManageExternalStoragePermission())
                    {
                        yield return new WaitForEndOfFrame();
                    }

                    m_WaitingForPermission = false;
                    m_Overlay.MessageStatus = m_LoadingText.GetLocalizedStringAsync().Result;
                }
            }
#endif

            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync("Main");
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            m_MainLoad = asyncLoad;
            LogStartup("Main load requested");
            if (m_StartupXr != null && m_StartupXrMode != "retained")
            {
                // Keep rendering the loading scene until Main is ready, but do not carry
                // its temporary XR session into VrSdk.Awake's render-buffer setup.
                asyncLoad.allowSceneActivation = false;
                while (asyncLoad.progress < 0.9f)
                {
                    UpdateProgress(0, m_SceneLoadRatio, asyncLoad.progress);
                    yield return null;
                }
                LogStartup("Main ready for activation; waiting for end of frame");
                yield return new WaitForEndOfFrame();
                Debug.Log("OBDS_STARTUP releasing loading XR before Main activation");
                if (m_StartupXr.isInitializationComplete) m_StartupXr.DeinitializeLoader();
                m_StartupXr = null;
                m_LoggedRenderFrames = 0;
                LogStartup("loading XR released; allowing Main activation");
                asyncLoad.allowSceneActivation = true;
            }
#endif
            while (!asyncLoad.isDone)
            {
                UpdateProgress(0, m_SceneLoadRatio, asyncLoad.progress);
                yield return null;
            }

            // Skip a frame to allow app to get out of Standard and in to LoadingBrushesAndLighting state.
#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
            m_LoggedRenderFrames = 0;
            LogStartup("Main activation completed");
#endif
            yield return null;

            // We can't check against app state until main scene loading is done.
            while (App.CurrentState == App.AppState.LoadingBrushesAndLighting)
            {
                UpdateProgress(m_SceneLoadRatio, 1f - m_SceneLoadRatio, ShaderWarmup.Instance.Progress);
                yield return null;
            }

            yield return null;

            Destroy(gameObject);
        }

#if UNITY_ANDROID && OPEN_BRUSH_SCOPED_STORAGE
        private IEnumerator ShowFolderExplanation(bool retry)
        {
            bool accepted = false;
            yield return StartupFolderDialog.Show(m_VrCamera, m_Overlay, App.kAppFolderName,
                retry, result => accepted = result);
            if (accepted)
            {
                m_OpenedFolderPicker = true;
                AndroidDirectStorage.OpenFolderPicker();
            }
            else AndroidDirectStorage.CancelStartup();
        }

        private void RestoreStartupXr()
        {
            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null || manager.activeLoader != null) return;
            try
            {
                m_StartupXr = manager;
                Debug.Log("OBDS_STARTUP restoring XR loading surface");
                if (manager.activeLoader == null) manager.InitializeLoaderSync();
                if (manager.activeLoader != null) manager.StartSubsystems();
                LogStartup("loading XR start returned");
            }
            catch (System.Exception error)
            {
                Debug.LogError($"OBDS_STARTUP XR resume failed: {error}");
            }
        }
#endif

        // Updates the progress bar. Will always increment a little bit to ensure the user is always
        // told that things are progressing.
        // start and scale determine which part of the progress bar we are updating
        // progress should be from 0 - 1 for that section.
        private void UpdateProgress(float start, float scale, float progress)
        {
            m_CurrentLoadingPosition += Time.deltaTime * m_FakeLoadingRate;
            float position = start + scale * progress;
            m_CurrentLoadingPosition = Mathf.Max(m_CurrentLoadingPosition, position);
            m_Overlay.Progress = m_CurrentLoadingPosition;
        }

#if UNITY_ANDROID
        private bool UserHasManageExternalStoragePermission()
        {
            bool isExternalStorageManager = false;
            try
            {
                AndroidJavaClass environmentClass = new AndroidJavaClass("android.os.Environment");
                isExternalStorageManager = environmentClass.CallStatic<bool>("isExternalStorageManager");
            }
            catch (AndroidJavaException e)
            {
                Debug.LogError("Java Exception caught and ignored: " + e.Message);
                Debug.LogError("Assuming this means this device doesn't support isExternalStorageManager.");
            }
            return m_FolderPermissionOverride || isExternalStorageManager;
        }

        private void AskForManageStoragePermission()
        {
            try
            {
                using var unityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using AndroidJavaObject currentActivityObject = unityClass.GetStatic<AndroidJavaObject>("currentActivity");
                string packageName = currentActivityObject.Call<string>("getPackageName");
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using AndroidJavaObject uriObject = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", packageName, null);
                using var intentObject = new AndroidJavaObject("android.content.Intent", "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION", uriObject);
                intentObject.Call<AndroidJavaObject>("addCategory", "android.intent.category.DEFAULT");
                currentActivityObject.Call("startActivity", intentObject);
            }
            catch (AndroidJavaException e)
            {
                // TODO: only skip this if it's of type act=android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION
                m_FolderPermissionOverride = true;
                Debug.LogError("Java Exception caught and ignored: " + e.Message);
                Debug.LogError("Assuming this means we don't need android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION (e.g., Android SDK < 30)");
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && m_WaitingForPermission && !UserHasManageExternalStoragePermission())
            {
                // User returned to app without granting permission - show dialog again
                AskForManageStoragePermission();
            }
        }
#endif
    }
} // namespace TiltBrush
