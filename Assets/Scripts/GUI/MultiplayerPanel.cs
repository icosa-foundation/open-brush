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

using OpenBrush.Multiplayer;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace TiltBrush
{
    public class MultiplayerPanel : BasePanel
    {

        [SerializeField] private TextMeshPro m_State;
        [SerializeField] private LocalizedString m_StatString;
        [SerializeField] private TextMeshPro m_RoomNumber;
        [SerializeField] private LocalizedString m_RoomNumberString;
        [SerializeField] private TextMeshPro m_Nickname;
        [SerializeField] private LocalizedString m_NicknameString;
        [SerializeField] private TextMeshPro m_RoomOwnership;
        [SerializeField] private LocalizedString m_RoomOwnerString;
        [SerializeField] private LocalizedString m_NotRoomOwnerString;
        [SerializeField] private TextMeshPro m_RoomAvailable;
        [SerializeField] private LocalizedString m_RoomAvailableString;
        [SerializeField] private LocalizedString m_NotRoomAvailableString;
        [SerializeField] private TextMeshPro m_RoomMaxPlayer;
        [SerializeField] private LocalizedString m_RoomMaxPlayerString;
        [SerializeField] private TextMeshPro m_AlertsErrors;
        [SerializeField] private LocalizedString m_AlertsErrorBeginnerModeActive;
        [SerializeField] private LocalizedString m_AlertsRoomAlreadyExistent;
        [SerializeField] private LocalizedString m_AlertsPassThroughAcive;
        [SerializeField] private GameObject m_RoomSettingsButton;

        private PlayerPrefsDataStore m_multiplayer;
        private bool updateDisplay = false;

        public string RoomName
        {
            get { return data.roomName; }
            set
            {
                data.roomName = value;
                UpdateDisplay();
                SaveRoomName(value);
            }
        }

        public string NickName
        {
            get
            {

                if (MultiplayerManager.m_Instance) return MultiplayerManager.m_Instance.UserInfo.Nickname;
                return "";
            }
            set
            {
                ConnectionUserInfo ui = new ConnectionUserInfo
                {
                    Nickname = value,
                    UserId = MultiplayerManager.m_Instance.UserInfo.UserId,
                    Role = MultiplayerManager.m_Instance.UserInfo.Role
                };
                MultiplayerManager.m_Instance.UserInfo = ui;
                UpdateDisplay();
                SaveNickname(value);
            }
        }

        private Tuple<int, int> MaxPlayersRange = new Tuple<int, int>(2, 8);
        public int MaxPlayers
        {
            get { return data.maxPlayers; }
            set
            {
                if (value < MaxPlayersRange.Item1) data.maxPlayers = MaxPlayersRange.Item1;
                else if (value > MaxPlayersRange.Item2) data.maxPlayers = MaxPlayersRange.Item2;
                else data.maxPlayers = value;
                UpdateDisplay();
                SaveMaxPlayerNumber(value);
            }
        }

        private RoomCreateData data;

        private List<Func<Tuple<bool, string>>> alertChecks;

        public void Awake()
        {
            data = new RoomCreateData
            {
                roomName = "default room",
                @private = false,
                maxPlayers = 4,
                silentRoom = false,
                viewOnlyRoom = false
            };

            alertChecks = new List<Func<Tuple<bool, string>>>
            {
                CheckAdvancedModeActive,
                CheckIfPassThroughEnvironment,
                CheckMultiplayerManagerErrors,
                CheckIfRoomExist,
            };

            if (MultiplayerManager.m_Instance != null)
            {
                MultiplayerManager.m_Instance.StateUpdated += OnStateUpdated;
                MultiplayerManager.m_Instance.RoomOwnershipUpdated += OnRoomOwnershipUpdated;
            }

            LocalizationSettings.SelectedLocaleChanged += OnLanguageChanged;

        }

        private void OnLanguageChanged(Locale newLocale)
        {
            updateDisplay = true;
        }

        public async void RetrieveRoomName()
        {
            var storedRoomName = await m_multiplayer.GetAsync<string>("roomname");
            RoomName = storedRoomName ?? GenerateUniqueRoomName();
        }

        private async void SaveRoomName(string roomName)
        {
            await m_multiplayer.StoreAsync("roomname", roomName);
        }

        public async void RetrieveUsername()
        {
            var storedNickname = await m_multiplayer.GetAsync<string>("nickname");
            NickName = storedNickname ?? "Unnamed";
        }

        private async void SaveNickname(string nickname)
        {
            await m_multiplayer.StoreAsync("nickname", nickname);
        }

        public async void RetrieveMaxPlayers()
        {
            try
            {
                var storedMaxPlayers = await m_multiplayer.GetAsync<int>("maxPlayers");
                MaxPlayers = storedMaxPlayers;
            }
            catch (KeyNotFoundException)
            {
                MaxPlayers = 4;
            }
        }

        private async void SaveMaxPlayerNumber(int maxPlayers)
        {
            await m_multiplayer.StoreAsync("maxPlayers", maxPlayers);
        }

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();

            m_multiplayer = new PlayerPrefsDataStore("Multiplayer");
            RetrieveUsername();
            RetrieveRoomName();
            RetrieveMaxPlayers();

            if (MultiplayerManager.m_Instance == null) return;
            if (MultiplayerManager.m_Instance.State == ConnectionState.INITIALIZED || MultiplayerManager.m_Instance.State == ConnectionState.DISCONNECTED)
            {
                MultiplayerManager.m_Instance.Connect();
            }

            if (updateDisplay) UpdateDisplay();
        }

        protected override void OnDisablePanel()
        {
            base.OnDisablePanel();

            if (MultiplayerManager.m_Instance == null) return;
            if (MultiplayerManager.m_Instance.State != ConnectionState.IN_ROOM)
            {
                MultiplayerManager.m_Instance.Disconnect();
            }
        }

        private static string GenerateUniqueRoomName()
        {
            const int maxAttempts = 10;
            string roomName;
            int attempts = 0;

            do
            {
                roomName = GenerateRandomRoomName();
                attempts++;
            } while (MultiplayerManager.m_Instance != null &&
             MultiplayerManager.m_Instance.DoesRoomNameExist(roomName) &&
             attempts < maxAttempts);

            if (attempts >= maxAttempts)
            {
                return "default room";
            }

            return roomName;
        }

        private static string GenerateRandomRoomName()
        {
            System.Random random = new System.Random();
            return random.Next(100000, 999999).ToString();
        }

        private void UpdateDisplay()
        {
            if (m_RoomNumber) m_RoomNumber.text = m_RoomNumberString.GetLocalizedString() + data.roomName;
            if (m_Nickname) m_Nickname.text = m_NicknameString.GetLocalizedString() + NickName;
            if (m_RoomMaxPlayer) m_RoomMaxPlayer.text = m_RoomMaxPlayerString.GetLocalizedString() + MaxPlayers;
            Alerts();
            updateDisplay = false;
        }

        private async void Connect()
        {
            if (MultiplayerManager.m_Instance != null)
            {
                await MultiplayerManager.m_Instance.Connect();
            }
        }

        private async void JoinRoom()
        {

            if (MultiplayerManager.m_Instance != null)
            {
                await MultiplayerManager.m_Instance.JoinRoom(data);
            }
        }

        private async void LeaveRoom()
        {
            if (MultiplayerManager.m_Instance != null)
            {
                await MultiplayerManager.m_Instance.LeaveRoom(false);
            }
        }

        private async void Disconnect()
        {
            if (MultiplayerManager.m_Instance != null)
            {
                await MultiplayerManager.m_Instance.Disconnect();
            }
        }

        private void OnStateUpdated(ConnectionState newState)
        {
            if (!m_State) return;
            m_State.text = m_StatString.GetLocalizedString() + StateToString(newState);
            if (newState == ConnectionState.IN_ROOM)
            {
                m_RoomOwnership.gameObject.SetActive(true);
                m_RoomAvailable.gameObject.SetActive(false);
            }
            else
            {
                m_RoomOwnership.gameObject.SetActive(false);
                m_RoomAvailable.gameObject.SetActive(true);
            }
            DisplayRoomSettingsButton(newState);
            UpdateDisplay();
        }

        private string StateToString(ConnectionState newState)
        {
            switch (newState)
            {
                case ConnectionState.INITIALIZING:
                    return "Initializing";
                case ConnectionState.INITIALIZED:
                    return "Initialized";
                case ConnectionState.DISCONNECTED:
                    return "Disconnected";
                case ConnectionState.DISCONNECTING:
                    return "Disconnecting";
                case ConnectionState.CONNECTING:
                    return "Connecting";
                case ConnectionState.AUTHENTICATING:
                    return "Authenticating";
                case ConnectionState.IN_LOBBY:
                    return "In Lobby";
                case ConnectionState.IN_ROOM:
                    return "In Room";
                case ConnectionState.ERROR:
                    return "Error";
                default:
                    return "Unknown";
            }
        }

        private void DisplayRoomSettingsButton(ConnectionState newState)
        {
            if (!m_RoomSettingsButton) return;

            switch (newState)
            {
                case ConnectionState.IN_ROOM:
                    m_RoomSettingsButton.SetActive(MultiplayerManager.m_Instance.IsUserRoomOwner());
                    break;
                case ConnectionState.INITIALIZING:
                case ConnectionState.INITIALIZED:
                case ConnectionState.DISCONNECTED:
                case ConnectionState.DISCONNECTING:
                case ConnectionState.CONNECTING:
                case ConnectionState.AUTHENTICATING:
                case ConnectionState.IN_LOBBY:
                case ConnectionState.ERROR:
                default:
                    m_RoomSettingsButton.SetActive(false);
                    break;
            }
        }

        private void OnRoomOwnershipUpdated(bool isRoomOwner)
        {
            if (m_RoomOwnership)
            {
                var localizedOwnershipString = isRoomOwner ? m_RoomOwnerString : m_NotRoomOwnerString;
                localizedOwnershipString.GetLocalizedStringAsync().Completed += handle =>
                    { m_RoomOwnership.text = handle.Result; };
            }
            if (m_RoomAvailable)
            {
                var localizedAvailableString = isRoomOwner ? m_RoomAvailableString : m_NotRoomAvailableString;
                localizedAvailableString.GetLocalizedStringAsync().Completed += handle =>
                    { m_RoomAvailable.text = handle.Result; };
            }

            // Update settings button visibility
            bool showRoomSettingsButton = MultiplayerManager.m_Instance.State == ConnectionState.IN_ROOM &&
                MultiplayerManager.m_Instance.IsUserRoomOwner();
            m_RoomSettingsButton.SetActive(showRoomSettingsButton);
        }

        private Tuple<bool, string> CheckAdvancedModeActive()
        {
            if (PanelManager.m_Instance != null)
            {
                bool isAdvancedModeActive = PanelManager.m_Instance.AdvancedModeActive();
                return Tuple.Create(isAdvancedModeActive, m_AlertsErrorBeginnerModeActive.GetLocalizedString());
            }
            return Tuple.Create(false, "");
        }

        private Tuple<bool, string> CheckMultiplayerManagerErrors()
        {

            if (MultiplayerManager.m_Instance != null)
            {
                if (MultiplayerManager.m_Instance.State == ConnectionState.ERROR)
                    return Tuple.Create(true, MultiplayerManager.m_Instance.LastError);
            }

            return Tuple.Create(false, "");

        }

        private Tuple<bool, string> CheckIfRoomExist()
        {

            if (MultiplayerManager.m_Instance != null && MultiplayerManager.m_Instance.State == ConnectionState.IN_LOBBY)
            {
                if (MultiplayerManager.m_Instance.DoesRoomNameExist(data.roomName))
                    return Tuple.Create(true, m_AlertsRoomAlreadyExistent.GetLocalizedString());
            }

            return Tuple.Create(false, "");

        }

        private Tuple<bool, string> CheckIfPassThroughEnvironment()
        {

            if (MultiplayerManager.m_Instance != null && MultiplayerManager.m_Instance.State == ConnectionState.IN_LOBBY)
            {
                TiltBrush.Environment targetEnvironment = SceneSettings.m_Instance.GetDesiredPreset();
                if (targetEnvironment.isPassthrough)
                    return Tuple.Create(true, m_AlertsPassThroughAcive.GetLocalizedString());
            }

            return Tuple.Create(false, "");
        }

        private void Alerts()
        {
            if (m_AlertsErrors)
            {
                bool shouldShowAlert = false;
                string alertMessage = "";

                foreach (Func<Tuple<bool, string>> check in alertChecks)
                {
                    var (isTriggered, message) = check.Invoke();
                    if (isTriggered)
                    {
                        shouldShowAlert = true;
                        alertMessage += message + "\n";
                        break;
                    }
                }
                m_AlertsErrors.gameObject.GetComponent<TextMeshPro>().text = alertMessage;
                m_AlertsErrors.gameObject.SetActive(shouldShowAlert);
            }
        }

        public void OnMultiplayerPanelButtonPressed(MultiplayerPanelButton button)
        {
            switch (button.m_Command)
            {

                case SketchControlsScript.GlobalCommands.Null:
                    //UpdateMode(Mode.Disconnected);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerConnect:
                    Connect();
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerPanelOptions:
                    //switch ((Mode)button.m_CommandParam)
                    //{
                    //    case Mode.Lobby:
                    //        UpdateMode(Mode.Lobby);
                    //        break;
                    //    default:
                    //        break;
                    //}
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerJoinRoom:
                    JoinRoom();
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerLeaveRoom:
                    LeaveRoom();
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerDisconnect:
                    Disconnect();
                    break;
            }
        }
    }
} // namespace TiltBrush
