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

using System;
using OpenBrush.Multiplayer;
using TMPro;
using UnityEngine;

namespace TiltBrush
{
    public class MultiplayerPanel : BasePanel
    {
        [SerializeField] private TextMeshPro m_RoomNumberTextLobby;
        [SerializeField] private TextMeshPro m_RoomNumberTextRoomSettings;

        public void SetRoomName(string roomName)
        {
            data.roomName = roomName;
            UpdateRoomNumberDisplay();
        }

        private RoomCreateData data = new RoomCreateData
        {
            roomName = GenerateRandomRoomName(),
            @private = false,
            maxPlayers = 4,
            voiceDisabled = false
        };

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
            Joined,
            //Create,
            //RoomSettings,
            //GeneralSettings
        }

        [SerializeField] private GameObject m_LobbyElements;
        [SerializeField] private GameObject m_JoinedElements;
        //[SerializeField] private GameObject m_RoomSettingsElements;
        //[SerializeField] private GameObject m_GeneralSettingsElements;

        private Mode m_CurrentMode;

        public override void InitPanel()
        {
            base.InitPanel();

            InitMultiplayer();
            UpdateRoomNumberDisplay();
        }

        public async void InitMultiplayer()
        {
            bool success = await MultiplayerManager.m_Instance.Init();
        }

        private async void JoinRoom()
        {
            if (MultiplayerManager.m_Instance != null)
            {

                bool success = await MultiplayerManager.m_Instance.Connect(data);

                if (success)
                {
                    Debug.Log("Connected to room successfully.");

                    // Additional UI updates or feedback
                    UpdateMode(Mode.Joined);
                    UpdateRoomNumberDisplay(); // Update room number display after joining
                }
                else
                {
                    Debug.LogError("Failed to connect to room.");
                    // Provide user feedback with some UI element
                }

            }
        }

        private void UpdateMode(Mode newMode)
        {
            m_CurrentMode = newMode;
            m_LobbyElements.SetActive(m_CurrentMode == Mode.Lobby);
            m_JoinedElements.SetActive(m_CurrentMode == Mode.Joined);
            //m_RoomSettingsElements.SetActive(m_CurrentMode == Mode.RoomSettings);
            //m_GeneralSettingsElements.SetActive(m_CurrentMode == Mode.GeneralSettings);

            // Update room number display if switching to a mode that shows it
            if (m_CurrentMode == Mode.Lobby || m_CurrentMode == Mode.Joined)
            {
                UpdateRoomNumberDisplay();
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
                    switch((Mode)button.m_CommandParam)
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
                    Debug.Log("Joining room");
                    break;
            }
        }
    }
} // namespace TiltBrush
