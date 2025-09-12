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
using System.Collections.Generic;
using UnityEngine;

namespace OpenBrush.Multiplayer
{

    [System.Serializable]
    public class RemotePlayer
    {
        public int PlayerId;
        public bool m_IsMutedForMe;
        public bool m_IsMutedForAll;
        public bool m_IsViewOnly;

        private string _nickname;
        public string Nickname
        {

            get
            {
                if (_nickname?.Length == 0) _nickname = RetrieveNickNameFromRig();
                return _nickname;
            }
            set { _nickname = value; }

        }

        // send/receive rig data interface
        public ITransientData<PlayerRigData> TransientData;

        // The underlying GameObjects in the scene that represents this player 
        public GameObject PlayerGameObject;
        public GameObject VoiceGameObject;

        private string RetrieveNickNameFromRig()
        {

            if (PlayerGameObject == null) return "";
            string nickname = null;
#if MP_PHOTON
            PhotonPlayerRig Rig = PlayerGameObject.GetComponent<PhotonPlayerRig>();
            if (Rig == null) return "";
            nickname = Rig.PersistentNickname;
#endif
            return nickname;

        }
    }


    [System.Serializable]
    public class RemotePlayers
    {
        public Action<int> remotePlayerAdded;
        public Action<int> remotePlayerRemoved;
        public Action remotePlayersListCleared;

        private List<RemotePlayer> list = new List<RemotePlayer>();

        public List<RemotePlayer> List
        {
            get { return list; }
        }

        public void AddPlayer(RemotePlayer player)
        {
            if (player != null)
            {
                list.Add(player);
                remotePlayerAdded?.Invoke(player.PlayerId);
            }
        }

        public void RemovePlayer(RemotePlayer player)
        {
            if (player != null && list.Remove(player))
                remotePlayerRemoved?.Invoke(player.PlayerId);
        }

        public void RemovePlayerById(int playerId)
        {
            RemotePlayer playerToRemove = GetPlayerById(playerId);

            if (playerToRemove != null && list.Remove(playerToRemove))
                remotePlayerRemoved?.Invoke(playerId);

        }

        public RemotePlayer GetPlayerById(int playerId)
        {
            RemotePlayer playerToRemove = list.Find(player => player.PlayerId == playerId);

            return playerToRemove;
        }

        public void ClearList()
        {
            list.Clear();
            remotePlayersListCleared?.Invoke();
        }
    }

}
