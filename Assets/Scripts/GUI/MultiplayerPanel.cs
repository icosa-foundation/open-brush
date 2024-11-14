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

namespace TiltBrush
{
    public class MultiplayerPanel : BasePanel
    {

        [SerializeField] private TextMeshPro m_State;
        [SerializeField] private TextMeshPro m_RoomNumber;
        [SerializeField] private TextMeshPro m_Nickname;
        [SerializeField] private TextMeshPro m_AlertsErrors;

        public string RoomName
        {
            get { return data.roomName; }
            set
            {
                data.roomName = value;
                UpdateDisplay();
            }
        }

        public string NickName
        {
            get
            {

                if (MultiplayerManager.m_Instance != null) return MultiplayerManager.m_Instance.UserInfo.Nickname;
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
            }
        }

        private RoomCreateData data;

        private List<Func<Tuple<bool, string>>> alertChecks;

        public void Awake()
        {
            data = new RoomCreateData
            {
                roomName = GenerateUniqueRoomName(),
                @private = false,
                maxPlayers = 4,
                voiceDisabled = false
            };

            alertChecks = new List<Func<Tuple<bool, string>>>
            {
                CheckAdvancedModeActive,
                CheckMultiplayerManagerErrors,
                CheckIfRoomExist,
            };

            if (MultiplayerManager.m_Instance != null) MultiplayerManager.m_Instance.StateUpdated += OnStateUpdated;

        }

        protected override void OnEnablePanel()
        {
            base.OnEnablePanel();

            if (MultiplayerManager.m_Instance == null) return;
            if (MultiplayerManager.m_Instance.State == ConnectionState.INITIALIZED || MultiplayerManager.m_Instance.State == ConnectionState.DISCONNECTED)
            {
                MultiplayerManager.m_Instance.Connect();
            }
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

        private void UpdateDisplay()
        {
            if (m_RoomNumber) m_RoomNumber.text = "RoomName: " + data.roomName;
            if (m_Nickname) m_Nickname.text = "Nickname: " + NickName;
            Alerts();
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
            m_State.text = "State: " + newState.ToString();
            UpdateDisplay();
        }

        private Tuple<bool, string> CheckAdvancedModeActive()
        {
            if (PanelManager.m_Instance != null)
            {
                bool isAdvancedModeActive = PanelManager.m_Instance.AdvancedModeActive();
                return Tuple.Create(isAdvancedModeActive, "Switch to beginner mode to Join Room");
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
                    return Tuple.Create(true, $"The room {data.roomName} already exist your joining an existing session.");
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
                        alertMessage += message + "/n";
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
