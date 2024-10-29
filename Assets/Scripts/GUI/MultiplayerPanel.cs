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
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class MultiplayerPanel : BasePanel
    {
        [SerializeField] private TextMeshPro m_RoomNumberTextLobby;
        [SerializeField] private TextMeshPro m_RoomNumberTextRoomSettings;
        [SerializeField] private TextMeshPro m_DoesRoomNumberExist;
        [SerializeField] private TextMeshPro m_AlertUserInBeginnerMode;


        public string RoomName
        {
            get { return data.roomName; }
            set
            {
                data.roomName = value;
                UpdateRoomNumberDisplay();
                UpdateRoomExistenceMessage();
            }
        }

        private RoomCreateData data;

        public void Awake()
        {
            data = new RoomCreateData
            {
                roomName = GenerateUniqueRoomName(),
                @private = false,
                maxPlayers = 4,
                voiceDisabled = false
            };

        }

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();

            if (m_CurrentMode == Mode.Lobby)
            {
                UserInBeginnerMode();
            }
        }

        private void UserInBeginnerMode()
        {
            if (m_AlertUserInBeginnerMode)
            {
                PanelManager panelManager = PanelManager.m_Instance;
                bool IsAdavancedModeActive = panelManager.AdvancedModeActive();
                Debug.Log(IsAdavancedModeActive);
                m_AlertUserInBeginnerMode.gameObject.SetActive(IsAdavancedModeActive);
            }
        }

        private static string GenerateUniqueRoomName()
        {
            string roomName;
            do
            {
                roomName = GenerateRandomRoomName();
            } while (MultiplayerManager.m_Instance != null && MultiplayerManager.m_Instance.DoesRoomNameExist(roomName));

            return roomName;
        }

        private static string GenerateRandomRoomName()
        {
            System.Random random = new System.Random();
            return random.Next(100000, 999999).ToString();
        }

        private void UpdateRoomNumberDisplay()
        {
            if (m_RoomNumberTextLobby)
            {
                m_RoomNumberTextLobby.text = data.roomName;
            }
            if (m_RoomNumberTextRoomSettings)
            {
                m_RoomNumberTextRoomSettings.text = data.roomName;
            }
        }

        public enum Mode
        {
            Null,
            Lobby,
            Joined
        }

        [SerializeField] private GameObject m_LobbyElements;
        [SerializeField] private GameObject m_JoinedElements;

        private Mode m_CurrentMode = Mode.Lobby;

        public override void InitPanel()
        {
            base.InitPanel();

            InitMultiplayer();
            UpdateRoomNumberDisplay();
            UserInBeginnerMode();
        }

        public async void InitMultiplayer()
        {
            await MultiplayerManager.m_Instance.Connect();
            MultiplayerManager.m_Instance.Disconnected += OnDisconnected;
        }

        private void OnDisconnected()
        {
            UpdateMode(Mode.Lobby);
        }

        private async void JoinRoom()
        {

            if (MultiplayerManager.m_Instance != null)
            {

                bool success = await MultiplayerManager.m_Instance.JoinRoom(data);

                if (success)
                {

                    // Additional UI updates or feedback
                    UpdateMode(Mode.Joined);
                    UpdateRoomNumberDisplay(); // Update room number display after joining
                }
                else
                {
                    // Provide user feedback with some UI element
                }

            }
        }

        private async void LeaveRoom()
        {
            if (MultiplayerManager.m_Instance != null)
            {

                bool success = await MultiplayerManager.m_Instance.LeaveRoom(false);

                if (success)
                {

                    // Additional UI updates or feedback
                    UpdateMode(Mode.Lobby);
                    UpdateRoomNumberDisplay(); // Update room number display after joining
                }
                else
                {
                    // Provide user feedback with some UI element
                }

            }
        }

        private void UpdateRoomExistenceMessage()
        {
            if (m_RoomNumberTextLobby) return;

            if (MultiplayerManager.m_Instance != null && m_DoesRoomNumberExist != null)
            {
                if (MultiplayerManager.m_Instance.DoesRoomNameExist(data.roomName))
                {
                    m_DoesRoomNumberExist.text = "This room exists. You will be joining an active session. You can change the room number by pressing edit.";
                }
                else
                {
                    m_DoesRoomNumberExist.text = "This room does not exist yet. By pressing join, the room will be created.";
                }
            }
        }

        private void UpdateMode(Mode newMode)
        {
            m_CurrentMode = newMode;
            m_LobbyElements.SetActive(m_CurrentMode == Mode.Lobby);
            m_JoinedElements.SetActive(m_CurrentMode == Mode.Joined);

            // Update room number display if switching to a mode that shows it
            if (m_CurrentMode == Mode.Lobby || m_CurrentMode == Mode.Joined)
            {
                UpdateRoomNumberDisplay();
            }

            if (m_CurrentMode == Mode.Lobby)
            {
                UserInBeginnerMode();
            }
        }

        private void RefreshObjects()
        {

        }


        // This function serves as a callback from ProfilePopUpButtons that want to
        // change the mode of the popup on click.
        public void OnMultiplayerPanelButtonPressed(MultiplayerPanelButton button)
        {
            switch (button.m_Command)
            {
                // Identifier for signaling we understand the info message.
                case SketchControlsScript.GlobalCommands.Null:
                    UpdateMode(Mode.Lobby);
                    RefreshObjects();
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerPanelOptions:
                    switch ((Mode)button.m_CommandParam)
                    {
                        case Mode.Lobby:
                            UpdateMode(Mode.Lobby);
                            break;
                        default:
                            break;
                    }
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerJoinRoom:
                    JoinRoom();
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerLeaveRoom:
                    LeaveRoom();
                    break;
            }
        }
    }
} // namespace TiltBrush
