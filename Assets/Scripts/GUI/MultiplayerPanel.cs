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
using UnityEngine;

namespace TiltBrush
{
    public class MultiplayerPanel : BasePanel
    {
        public enum Mode
        {
            Null,
            Lobby,
            Create,
            RoomSettings,
            GeneralSettings
        }

        [SerializeField] private GameObject m_LobbyElements;
        [SerializeField] private GameObject m_CreateRoomElements;
        [SerializeField] private GameObject m_RoomSettingsElements;
        [SerializeField] private GameObject m_GeneralSettingsElements;

        private Mode m_CurrentMode;

        public override void InitPanel()
        {
            base.InitPanel();

            InitMultiplayer();
        }

        public async void InitMultiplayer()
        {
            bool success = await MultiplayerManager.m_Instance.Init();
        }

        private void UpdateMode(Mode newMode)
        {
            m_CurrentMode = newMode;
            m_LobbyElements.SetActive(m_CurrentMode == Mode.Lobby);
            m_CreateRoomElements.SetActive(m_CurrentMode == Mode.Create);
            m_RoomSettingsElements.SetActive(m_CurrentMode == Mode.RoomSettings);
            m_GeneralSettingsElements.SetActive(m_CurrentMode == Mode.GeneralSettings);
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
            }
        }
    }
} // namespace TiltBrush
