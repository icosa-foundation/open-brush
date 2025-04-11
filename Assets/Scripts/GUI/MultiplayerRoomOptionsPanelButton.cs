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
using UnityEngine;

namespace TiltBrush
{
    public class MultiplayerRoomOptionsPanelButton : ToggleButton
    {
        [HideInInspector] public int playerId;

        private RemotePlayer _playerData =>
            MultiplayerManager.m_Instance.GetPlayerById(playerId);

        protected override void Start()
        {
            if (m_ToggleButton)
            {
                switch (m_Command)
                {
                    // "Single User" commands

                    case SketchControlsScript.GlobalCommands.MultiplayerMutePlayerForMe:
                        IsToggledOn = _playerData.m_IsMutedForMe;
                        break;
                    case SketchControlsScript.GlobalCommands.MultiplayerViewOnlyMode:
                        IsToggledOn = _playerData.m_IsViewOnly;
                        break;
                    case SketchControlsScript.GlobalCommands.MultiplayerPlayerMuteForAll:
                        IsToggledOn = _playerData.m_IsMutedForAll;
                        break;

                    // "All User" commands

                    case SketchControlsScript.GlobalCommands.MultiplayerSetAllViewOnly:
                        IsToggledOn = MultiplayerManager.m_Instance.IsViewOnly;
                        break;
                    case SketchControlsScript.GlobalCommands.MultiplayerMuteAllForMe:
                        IsToggledOn = MultiplayerManager.m_Instance.m_IsAllMutedForMe;
                        break;
                    case SketchControlsScript.GlobalCommands.MultiplayerMuteAllForAll:
                        IsToggledOn = MultiplayerManager.m_Instance.m_IsAllMutedForAll;
                        break;
                }
            }
            base.Start();
        }
    }
} // namespace TiltBrush
