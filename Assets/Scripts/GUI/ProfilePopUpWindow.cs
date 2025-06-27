﻿// Copyright 2020 The Tilt Brush Authors
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
using Org.OpenAPITools.Api;
using Org.OpenAPITools.Client;
using Org.OpenAPITools.Model;
using UnityEngine;
using TMPro;

namespace TiltBrush
{

    public class ProfilePopUpWindow : OptionsPopUpWindow
    {
        public enum Mode
        {
            Accounts,
            TakeOffHeadset,
            GoogleHelp,
            DriveHelp,
            SketchfabHelp,
            IcosaHelp,
            ConfirmLogin,
            Unavailable,
        }

        [SerializeField] private GameObject m_GoogleSignedInElements;
        [SerializeField] private GameObject m_GoogleSignedOutElements;
        [SerializeField] private GameObject m_GoogleConfirmSignOutElements;
        [SerializeField] private GameObject m_SketchfabSignedInElements;
        [SerializeField] private GameObject m_SketchfabSignedOutElements;
        [SerializeField] private GameObject m_SketchfabConfirmSignOutElements;
        [SerializeField] private GameObject m_IcosaSignedInElements;
        [SerializeField] private GameObject m_IcosaSignedOutElements;
        [SerializeField] private GameObject m_IcosaConfirmSignOutElements;
        [SerializeField] private GameObject m_IcosaLoginElements;
        [SerializeField] private Renderer m_GooglePhoto;
        [SerializeField] private Renderer m_SketchfabPhoto;
        [SerializeField] private Renderer m_IcosaPhoto;
        [SerializeField] private TextMeshPro m_GoogleNameText;
        [SerializeField] private TextMeshPro m_SketchfabNameText;
        [SerializeField] private TextMeshPro m_IcosaNameText;
        [SerializeField] private Texture2D m_GenericPhoto;

        [SerializeField] private GameObject m_Accounts;
        [SerializeField] private GameObject m_TakeOffHeadset;
        [SerializeField] private GameObject m_GoogleInfoElements;
        [SerializeField] private GameObject m_DriveInfoElements;
        [SerializeField] private GameObject m_SketchfabInfoElements;
        [SerializeField] private GameObject m_IcosaInfoElements;
        [SerializeField] private GameObject m_UnavailableElements;
        [SerializeField] private GameObject m_DriveSyncEnabledElements;
        [SerializeField] private GameObject m_DriveSyncDisabledElements;
        [SerializeField] private GameObject m_DriveFullElements;

        [SerializeField] private GameObject m_DriveSyncIconEnabled;
        [SerializeField] private GameObject m_DriveSyncIconDisabled;
        [SerializeField] private GameObject m_DriveSyncIconDriveFull;

        [SerializeField] private GameObject m_BackupCompleteElements;
        [SerializeField] private GameObject m_BackingUpElements;
        [SerializeField] private TextMeshPro m_BackingUpProgress;

        [Header("Mobile State Members")]
        [SerializeField] private GameObject m_ConfirmLoginElements;
        [SerializeField] private SaveAndOptionButton m_SaveAndProceedButton;

        private Mode m_CurrentMode;
        private bool m_DriveSyncing = false;

        private void Start()
        {
            App.DriveSync.SyncEnabledChanged += RefreshObjects;
        }

        override public void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);
            OAuth2Identity.ProfileUpdated += OnProfileUpdated;
            RefreshObjects();
            m_IcosaLoginElements.SetActive(false);
            if (App.IcosaIsLoggedIn)
            {
                StartCoroutine(FetchUserDataCoroutine(userData =>
                {
                    App.IcosaUserName = userData.Displayname;
                    App.IcosaUserId = userData.Id;
                    App.IcosaUserIcon = m_GenericPhoto; // TODO: Get icon from API
                    RefreshIcosaUserInfoUi();
                }));
            }

            App.DriveAccess.RefreshFreeSpaceAsync().AsAsyncVoid();

            // TODO: Make configurable by secrets/login data available at runtime.
            if (App.Config.DisableAccountLogins)
            {
                UpdateMode(Mode.Unavailable);
            }
        }

        void OnDestroy()
        {
            OAuth2Identity.ProfileUpdated -= OnProfileUpdated;
            App.DriveSync.SyncEnabledChanged -= RefreshObjects;
        }

        override protected void BaseUpdate()
        {
            base.BaseUpdate();
            if (App.DriveSync.Syncing != m_DriveSyncing)
            {
                RefreshObjects();
            }
            RefreshBackupProgressText();
        }

        void RefreshObjects()
        {
            // Google.
            bool driveFull = App.DriveSync.DriveIsLowOnSpace;
            bool driveSyncEnabled = App.DriveSync.SyncEnabled;
            bool driveSyncing = App.DriveSync.Syncing;

            OAuth2Identity.UserInfo googleInfo = App.GoogleIdentity.Profile;
            bool googleInfoValid = googleInfo != null;
            m_GoogleSignedInElements.SetActive(googleInfoValid);
            m_GoogleSignedOutElements.SetActive(!googleInfoValid);
            m_GoogleConfirmSignOutElements.SetActive(false);
            if (googleInfoValid)
            {
                m_GoogleNameText.text = googleInfo.name;
                m_GooglePhoto.material.mainTexture = googleInfo.icon;

                m_DriveSyncIconDriveFull.SetActive(driveFull && driveSyncEnabled);
                m_DriveSyncIconEnabled.SetActive(!driveFull && driveSyncEnabled);
                m_DriveSyncIconDisabled.SetActive(!driveSyncEnabled);
            }

            // Sketchfab.
            OAuth2Identity.UserInfo sketchfabInfo = App.SketchfabIdentity.Profile;
            bool sketchfabInfoValid = sketchfabInfo != null;
            m_SketchfabSignedInElements.SetActive(sketchfabInfoValid);
            m_SketchfabSignedOutElements.SetActive(!sketchfabInfoValid);
            m_SketchfabConfirmSignOutElements.SetActive(false);
            if (sketchfabInfoValid)
            {
                m_SketchfabNameText.text = sketchfabInfo.name;
                m_SketchfabPhoto.material.mainTexture = sketchfabInfo.icon;
            }

            // Icosa.
            m_IcosaSignedInElements.SetActive(App.IcosaIsLoggedIn);
            m_IcosaSignedOutElements.SetActive(!App.IcosaIsLoggedIn);
            m_IcosaConfirmSignOutElements.SetActive(false);
            RefreshIcosaUserInfoUi();

            m_DriveFullElements.SetActive(driveFull && driveSyncEnabled);
            m_DriveSyncEnabledElements.SetActive(!driveFull && driveSyncEnabled);
            m_DriveSyncDisabledElements.SetActive(!driveSyncEnabled);
            m_BackupCompleteElements.SetActive(!driveSyncing);
            m_BackingUpElements.SetActive(driveSyncing);
            m_DriveSyncing = driveSyncing;
            RefreshBackupProgressText();
        }

        private void RefreshIcosaUserInfoUi()
        {
            m_IcosaNameText.text = App.IcosaUserName;
            m_IcosaPhoto.material.mainTexture = App.IcosaUserIcon;
        }

        public void HideIcosaLogin()
        {
            m_IcosaLoginElements.SetActive(false);
            m_IcosaSignedInElements.SetActive(true);
            m_IcosaSignedOutElements.SetActive(true);
            m_SketchfabSignedOutElements.SetActive(true);
            m_SketchfabSignedInElements.SetActive(true);
            m_GoogleSignedInElements.SetActive(true);
            m_GoogleSignedOutElements.SetActive(true);
            RefreshObjects();
        }

        public void ShowIcosaLogin()
        {
            m_IcosaLoginElements.SetActive(true);
            m_IcosaLoginElements.GetComponent<IcosaLoginKeyboardController>().Clear();
            m_IcosaSignedInElements.SetActive(false);
            m_IcosaSignedOutElements.SetActive(false);
            m_SketchfabSignedOutElements.SetActive(false);
            m_SketchfabSignedInElements.SetActive(false);
            m_GoogleSignedInElements.SetActive(false);
            m_GoogleSignedOutElements.SetActive(false);
        }

        public void HandleIcosaLoginSubmit(string code)
        {
            if (App.IcosaIsLoggedIn) return;
            StartCoroutine(LoginCoroutine(code));
        }

        private IEnumerator LoginCoroutine(string code)
        {
            var config = new Configuration();
            var loginApi = new LoginApi(VrAssetService.m_Instance.IcosaApiRoot);
            config.BasePath = VrAssetService.m_Instance.IcosaApiRoot;
            loginApi.Configuration = config;
            var loginTask = loginApi.DeviceLoginLoginDeviceLoginPostAsync(code);
            yield return new WaitUntil(() => loginTask.IsCompleted);

            if (loginTask.Exception != null)
            {
                if (loginTask.Exception.Message.Contains("401 Unauthorized"))
                {
                    // TODO: Show error message.
                    LoginFailure();
                    AudioManager.m_Instance.PlayTrashSound(transform.position);
                }
                yield break;
            }

            if (loginTask.Result?.AccessToken == null)
            {
                // TODO: Show error message.
                LoginFailure();
                AudioManager.m_Instance.PlayPinSound(transform.position, AudioManager.PinSoundType.Wobble);
                yield break;
            }
            App.Instance.IcosaToken = loginTask.Result.AccessToken;
            StartCoroutine(FetchUserDataCoroutine(userData => LoginSuccess(userData)));
        }

        private IEnumerator FetchUserDataCoroutine(Action<FullUser> onSuccess)
        {
            var usersApi = new UsersApi(VrAssetService.m_Instance.IcosaApiRoot);
            var config = new Configuration { AccessToken = App.Instance.IcosaToken };
            config.BasePath = VrAssetService.m_Instance.IcosaApiRoot;
            usersApi.Configuration = config;
            var getUserTask = usersApi.GetUsersMeUsersMeGetAsync();
            yield return new WaitUntil(() => getUserTask.IsCompleted);

            if (getUserTask.Exception != null)
            {
                if (getUserTask.Exception.Message.Contains("401 Unauthorized"))
                {
                    // Clear user token
                    LoginFailure();
                }
                Debug.Log($"GetUser failed with exception: {getUserTask.Exception}");
                yield break;
            }

            var userData = getUserTask.Result;
            if (userData == null)
            {
                Debug.Log($"Failure - no user data received");
                // TODO should we logout? Clear username/icon?
                yield break;
            }
            onSuccess?.Invoke(userData);
        }

        private void LoginSuccess(FullUser userData)
        {
            // Call the callback delegate if it's provided (which means this was called from the first coroutine)
            App.IcosaUserName = userData.Displayname;
            App.IcosaUserId = userData.Id;
            App.IcosaUserIcon = m_GenericPhoto; // TODO: Get icon from API
            RefreshIcosaUserInfoUi();
            HideIcosaLogin();
        }

        private void LoginFailure()
        {
            HideIcosaLogin();
            App.Instance.LogoutIcosa();
        }

        void RefreshBackupProgressText()
        {
            if (m_BackingUpElements.activeSelf)
            {
                m_BackingUpProgress.text = string.Format("Backing Up... {0}",
                    Mathf.Clamp(App.DriveSync.Progress, 0.01f, 0.99f).ToString("P0"));
            }
        }

        void UpdateMode(Mode newMode)
        {
            m_CurrentMode = newMode;
            m_Accounts.SetActive(m_CurrentMode == Mode.Accounts);
            m_TakeOffHeadset.SetActive(m_CurrentMode == Mode.TakeOffHeadset);
            m_GoogleInfoElements.SetActive(m_CurrentMode == Mode.GoogleHelp);
            m_DriveInfoElements.SetActive(m_CurrentMode == Mode.DriveHelp);
            m_SketchfabInfoElements.SetActive(m_CurrentMode == Mode.SketchfabHelp);
            m_IcosaInfoElements.SetActive(m_CurrentMode == Mode.IcosaHelp);
            m_UnavailableElements.SetActive(m_CurrentMode == Mode.Unavailable);
            if (m_ConfirmLoginElements != null)
            {
                m_ConfirmLoginElements.SetActive(m_CurrentMode == Mode.ConfirmLogin);
            }
        }

        void OnProfileUpdated(OAuth2Identity _)
        {
            // If we're currently telling the user to take of the headset to signin,
            // and they've done so correctly, switch back to the accounts view.
            if (m_CurrentMode == Mode.TakeOffHeadset)
            {
                Debug.Log($"OnProfileUpdated set AccountMode");
                UpdateMode(Mode.Accounts);
            }
            RefreshObjects();
        }

        // This function serves as a callback from ProfilePopUpButtons that want to
        // change the mode of the popup on click.
        public void OnProfilePopUpButtonPressed(ProfilePopUpButton button)
        {
            switch (button.m_Command)
            {
                // Identifier for signaling we understand the info message.
                case SketchControlsScript.GlobalCommands.Null:
                case SketchControlsScript.GlobalCommands.GoogleDriveSync:
                    UpdateMode(Mode.Accounts);
                    RefreshObjects();
                    break;
                case SketchControlsScript.GlobalCommands.LoginToGenericCloud:
                    // m_CommandParam 1 is Google.  m_CommandParam 2 is Sketchfab.
                    if (button.m_CommandParam == 1 || button.m_CommandParam == 2)
                    {
                        if (App.Config.IsMobileHardware && m_SaveAndProceedButton != null)
                        {
                            m_SaveAndProceedButton.SetCommandParameters(button.m_CommandParam, 0);
                            UpdateMode(Mode.ConfirmLogin);
                        }
                        else
                        {
                            OAuth2Identity.UserInfo userInfo = (button.m_CommandParam == 1) ?
                                App.GoogleIdentity.Profile : App.SketchfabIdentity.Profile;
                            if (userInfo == null)
                            {
                                UpdateMode(Mode.TakeOffHeadset);
                            }
                        }
                    }
                    break;
                case SketchControlsScript.GlobalCommands.LoginToIcosa:
                    if (!App.Config.IsMobileHardware)
                    {
                        OutputWindowScript.m_Instance.CreateInfoCardAtController(
                            InputManager.ControllerName.Brush,
                            SketchControlsScript.kRemoveHeadsetFyi,
                            fPopScalar: 0.5f
                        );
                    }
                    string secret = VrAssetService.m_Instance.GenerateDeviceCodeSecret();
                    App.OpenURL($"{VrAssetService.m_Instance.IcosaHomePage}/device?{secret}");
                    ShowIcosaLogin();
                    break;
                case SketchControlsScript.GlobalCommands.AccountInfo:
                    // Identifier for triggering an info message.
                    switch (button.m_CommandParam)
                    {
                        case 0:
                            UpdateMode(Mode.DriveHelp);
                            break;
                        case 1:
                            UpdateMode(Mode.GoogleHelp);
                            break;
                        case 2:
                            UpdateMode(Mode.SketchfabHelp);
                            break;
                        case 3:
                            UpdateMode(Mode.IcosaHelp);
                            break;
                    }
                    break;
                case SketchControlsScript.GlobalCommands.SignOutConfirm:
                    switch ((Cloud)button.m_CommandParam)
                    {
                        case Cloud.Sketchfab:
                            m_SketchfabSignedInElements.SetActive(false);
                            m_SketchfabSignedOutElements.SetActive(false);
                            m_SketchfabConfirmSignOutElements.SetActive(true);
                            break;
                        case Cloud.Icosa:
                            m_IcosaSignedInElements.SetActive(false);
                            m_IcosaSignedOutElements.SetActive(false);
                            m_IcosaConfirmSignOutElements.SetActive(true);
                            break;
                        case Cloud.None: break;
                    }
                    break;
            }
        }

        public void CloseProfilePopup()
        {
            RequestClose(true);
        }
    }
} // namespace TiltBrush
