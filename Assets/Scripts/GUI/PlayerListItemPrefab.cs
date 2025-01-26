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
    public class PlayerListItemPrefab : MonoBehaviour
    {
        public TextMeshPro PlayerIdObject;
        public TextMeshPro NickNameObject;
        public MultiplayerRoomOptionsPanelButton MuteButton;
        public MultiplayerRoomOptionsPanelButton TransferOwnershipButton;
        public MultiplayerRoomOptionsPanelButton ToggleViewOnly;
        public MultiplayerRoomOptionsPanelButton KickPlayerOut;
        [HideInInspector] public RemotePlayer remotePlayer;

        public void SetRemotePlayer(RemotePlayer Player)
        {
            remotePlayer = Player;
            SetPlayerId();
            SetNickname();
        }

        public void SetNickname() { NickNameObject.text = remotePlayer.Nickname; }
        public void SetPlayerId()
        {
            if (PlayerIdObject) PlayerIdObject.text = remotePlayer.PlayerId.ToString();
            if (TransferOwnershipButton) TransferOwnershipButton.playerId = remotePlayer.PlayerId;
            if (MuteButton) MuteButton.playerId = remotePlayer.PlayerId;
            if (ToggleViewOnly) ToggleViewOnly.playerId = remotePlayer.PlayerId;
            if (KickPlayerOut) KickPlayerOut.playerId = remotePlayer.PlayerId;
        }

        public void SetAudioToggleState(bool isActive)
        {
            if (MuteButton) MuteButton.SetToggleState(isActive);
        }

        public void SetViewOnlyToggleState(bool isActive)
        {
            if (ToggleViewOnly) ToggleViewOnly.SetToggleState(isActive);
        }

    }
}
