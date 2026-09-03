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
        [SerializeField] private LocalizedString m_AlertsPassthroughWarning;
        [SerializeField] private GameObject m_RoomSettingsButton;
        [SerializeField] private GameObject m_ManualColocationButton;
        [SerializeField] private TextMeshPro m_ManualColocationStatus;

        private PlayerPrefsDataStore m_multiplayer;
        private bool updateDisplay = false;
        private bool m_ManualColocationEventsSubscribed;
        private GameObject m_ManualColocationButtonTemplate;

        private const string kManualColocationButtonAlign =
            "MP_MANUAL_COLOCATION_ALIGN";
        private const string kManualColocationButtonSet =
            "MP_MANUAL_COLOCATION_SET";
        private const string kManualColocationButtonReset =
            "MP_MANUAL_COLOCATION_RESET";
        private const string kManualColocationButtonRealign =
            "MP_MANUAL_COLOCATION_REALIGN";
        private const string kManualColocationButtonUpdated =
            "MP_MANUAL_COLOCATION_UPDATED";
        private const string kManualColocationStatusNotSet =
            "MP_MANUAL_COLOCATION_STATUS_NOT_SET";
        private const string kManualColocationStatusRecording =
            "MP_MANUAL_COLOCATION_STATUS_RECORDING";
        private const string kManualColocationStatusReady =
            "MP_MANUAL_COLOCATION_STATUS_READY";
        private const string kManualColocationStatusAligned =
            "MP_MANUAL_COLOCATION_STATUS_ALIGNED";
        private const string kManualColocationStatusStale =
            "MP_MANUAL_COLOCATION_STATUS_STALE";
        private const string kManualColocationStatusError =
            "MP_MANUAL_COLOCATION_STATUS_ERROR";

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

        private Tuple<int, int> MaxPlayersRange = new Tuple<int, int>(2, 12);
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

        protected override void Awake()
        {
            base.Awake();
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
                CheckPassthroughMultiplayerWarning,
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

        private void OnDestroy()
        {
            if (MultiplayerManager.m_Instance != null)
            {
                MultiplayerManager.m_Instance.StateUpdated -= OnStateUpdated;
                MultiplayerManager.m_Instance.RoomOwnershipUpdated -= OnRoomOwnershipUpdated;
            }
            UnsubscribeManualColocationEvents();
            LocalizationSettings.SelectedLocaleChanged -= OnLanguageChanged;
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

            EnsureManualColocationUi();
            m_multiplayer = new PlayerPrefsDataStore("Multiplayer");
            RetrieveUsername();
            RetrieveRoomName();
            RetrieveMaxPlayers();

            if (MultiplayerManager.m_Instance == null) return;
            SubscribeManualColocationEvents();
            if (MultiplayerManager.m_Instance.State == ConnectionState.INITIALIZED || MultiplayerManager.m_Instance.State == ConnectionState.DISCONNECTED)
            {
                MultiplayerManager.m_Instance.Connect();
            }

            if (updateDisplay) UpdateDisplay();
            RefreshManualColocationDisplay();
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
            RefreshManualColocationDisplay();
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
            RefreshManualColocationDisplay();
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

        private Tuple<bool, string> CheckPassthroughMultiplayerWarning()
        {
            if (MultiplayerManager.m_Instance != null && MultiplayerManager.m_Instance.State == ConnectionState.IN_ROOM)
            {
                TiltBrush.Environment targetEnvironment = SceneSettings.m_Instance.GetDesiredPreset();
                if (targetEnvironment != null && targetEnvironment.isPassthrough)
                    return Tuple.Create(true, m_AlertsPassthroughWarning.GetLocalizedString());
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
                case SketchControlsScript.GlobalCommands.MultiplayerManualColocation:
                    ManualColocationManager colocation =
                        ManualColocationManager.m_Instance;
                    if (colocation == null)
                    {
                        break;
                    }
                    if (colocation.HasReference &&
                        MultiplayerManager.m_Instance != null &&
                        MultiplayerManager.m_Instance.IsUserRoomOwner())
                    {
                        _ = colocation.ClearReference();
                    }
                    else
                    {
                        colocation.BeginAlignmentWorkflow();
                    }
                    break;
            }
        }

        private void SubscribeManualColocationEvents()
        {
            if (m_ManualColocationEventsSubscribed ||
                ManualColocationManager.m_Instance == null)
            {
                return;
            }
            ManualColocationManager.m_Instance.ReferenceChanged +=
                OnManualColocationReferenceChanged;
            ManualColocationManager.m_Instance.LocalStateChanged +=
                OnManualColocationStateChanged;
            m_ManualColocationEventsSubscribed = true;
        }

        private void UnsubscribeManualColocationEvents()
        {
            if (!m_ManualColocationEventsSubscribed ||
                ManualColocationManager.m_Instance == null)
            {
                return;
            }
            ManualColocationManager.m_Instance.ReferenceChanged -=
                OnManualColocationReferenceChanged;
            ManualColocationManager.m_Instance.LocalStateChanged -=
                OnManualColocationStateChanged;
            m_ManualColocationEventsSubscribed = false;
        }

        private void OnManualColocationReferenceChanged(
            ManualColocationReference reference)
        {
            RefreshManualColocationDisplay();
        }

        private void OnManualColocationStateChanged(
            ManualColocationState state)
        {
            RefreshManualColocationDisplay();
        }

        private void RefreshManualColocationDisplay()
        {
            SubscribeManualColocationEvents();

            ManualColocationManager colocation =
                ManualColocationManager.m_Instance;
            bool inRoom = MultiplayerManager.m_Instance != null &&
                MultiplayerManager.m_Instance.State == ConnectionState.IN_ROOM;
            bool isOwner = inRoom &&
                MultiplayerManager.m_Instance.IsUserRoomOwner();
            bool showButton = inRoom && colocation != null &&
                (isOwner || colocation.HasReference);

            if (m_ManualColocationButtonTemplate != null)
            {
                m_ManualColocationButtonTemplate.SetActive(!inRoom);
            }

            if (m_ManualColocationButton != null)
            {
                m_ManualColocationButton.SetActive(showButton);
                MultiplayerPanelButton button =
                    m_ManualColocationButton.GetComponent<MultiplayerPanelButton>();
                if (button != null && showButton)
                {
                    string description;
                    if (isOwner)
                    {
                        description = Localize(colocation.HasReference
                            ? kManualColocationButtonReset
                            : kManualColocationButtonSet);
                    }
                    else
                    {
                        description =
                            colocation.State == ManualColocationState.Aligned
                                ? Localize(kManualColocationButtonRealign)
                                : colocation.State ==
                                  ManualColocationState.AlignmentStale
                                    ? Localize(kManualColocationButtonUpdated)
                                    : Localize(kManualColocationButtonAlign);
                    }
                    button.SetDescriptionText(description);
                }
            }

            if (m_ManualColocationStatus != null)
            {
                m_ManualColocationStatus.gameObject.SetActive(inRoom);
                m_ManualColocationStatus.text =
                    colocation == null ? string.Empty :
                    ManualColocationStatusText(colocation.State);
            }
        }

        private static string ManualColocationStatusText(
            ManualColocationState state)
        {
            switch (state)
            {
                case ManualColocationState.OwnerCanSetReference:
                    return Localize(kManualColocationStatusNotSet);
                case ManualColocationState.CapturingStart:
                    return Localize(kManualColocationStatusRecording);
                case ManualColocationState.ReferenceAvailable:
                    return Localize(kManualColocationStatusReady);
                case ManualColocationState.Aligned:
                    return Localize(kManualColocationStatusAligned);
                case ManualColocationState.AlignmentStale:
                    return Localize(kManualColocationStatusStale);
                case ManualColocationState.Error:
                    return Localize(kManualColocationStatusError);
                default:
                    return string.Empty;
            }
        }

        private void EnsureManualColocationUi()
        {
            MultiplayerPanelButton[] buttons =
                GetComponentsInChildren<MultiplayerPanelButton>(true);
            MultiplayerPanelButton template = null;
            foreach (MultiplayerPanelButton button in buttons)
            {
                if (button.m_Command ==
                    SketchControlsScript.GlobalCommands.MultiplayerJoinRoom)
                {
                    template = button;
                    m_ManualColocationButtonTemplate = button.gameObject;
                    break;
                }
            }

            if (m_ManualColocationButton == null)
            {
                if (template != null)
                {
                    m_ManualColocationButton = Instantiate(
                        template.gameObject,
                        template.transform.parent);
                    m_ManualColocationButton.name = "ManualColocationButton";
                    Transform buttonTransform =
                        m_ManualColocationButton.transform;
                    buttonTransform.localPosition =
                        template.transform.localPosition +
                        new Vector3(0f, 0f, -0.01f);
                    buttonTransform.localRotation =
                        template.transform.localRotation;
                    buttonTransform.localScale =
                        template.transform.localScale;

                    MultiplayerPanelButton manualButton =
                        m_ManualColocationButton
                            .GetComponent<MultiplayerPanelButton>();
                    manualButton.m_Command =
                        SketchControlsScript.GlobalCommands
                            .MultiplayerManualColocation;
                    manualButton.m_CommandParam = -1;
                    manualButton.m_CommandParam2 = -1;
                    manualButton.SetDescriptionText(
                        Localize(kManualColocationButtonAlign));
                }
            }

            if (m_ManualColocationStatus == null && m_RoomOwnership != null)
            {
                GameObject statusObject = Instantiate(
                    m_RoomOwnership.gameObject,
                    m_RoomOwnership.transform.parent);
                statusObject.name = "Manual Colocation Status";
                RectTransform statusTransform =
                    statusObject.transform as RectTransform;
                RectTransform ownershipTransform =
                    m_RoomOwnership.transform as RectTransform;
                if (statusTransform != null && ownershipTransform != null)
                {
                    statusTransform.anchoredPosition =
                        ownershipTransform.anchoredPosition +
                        new Vector2(0f, -0.09f);
                    statusTransform.sizeDelta =
                        ownershipTransform.sizeDelta;
                }
                m_ManualColocationStatus =
                    statusObject.GetComponent<TextMeshPro>();
                m_ManualColocationStatus.fontSize =
                    m_RoomOwnership.fontSize * 0.8f;
                m_ManualColocationStatus.text = string.Empty;
            }
        }

        private static string Localize(string key)
        {
            return LocalizationSettings.StringDatabase.GetLocalizedString(
                "Strings", key);
        }
    }
} // namespace TiltBrush
