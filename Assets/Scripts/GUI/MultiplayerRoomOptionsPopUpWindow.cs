// Copyright 2020 The Tilt Brush Authors
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

using UnityEngine;
using OpenBrush.Multiplayer;
using System;
using System.Collections.Generic;
using OpenBrush;
using UnityEngine.Localization;
using TMPro;

namespace TiltBrush
{
    public class MultiplayerRoomOptionsPopUpWindow : PopUpWindow
    {
        [SerializeField] public GameObject m_playerGuiPrefab;
        public Vector2 PlayerGuiPrefabSize;
        public Vector2 PlayerListOffset;
        public Vector2 PlayerListArea;

        private List<GameObject> m_instantiatedGuiPrefabs = new List<GameObject>();

        [SerializeField] private LocalizedString m_popupWindowTitleString;
        [SerializeField] public TextMeshPro m_playerLIstTitle;
        [SerializeField] private LocalizedString m_playerLIstTitleString;

        public RemotePlayers m_RemotePlayers
        {
            get
            {
                if (MultiplayerManager.m_Instance == null)
                    throw new InvalidOperationException("MultiplayerManager is not initialized.");
                return MultiplayerManager.m_Instance.m_RemotePlayers;
            }

        }

        public ITransientData<PlayerRigData> m_LocalPlayer
        {
            get
            {
                if (MultiplayerManager.m_Instance == null)
                    throw new InvalidOperationException("MultiplayerManager is not initialized.");
                return MultiplayerManager.m_Instance.m_LocalPlayer;
            }
        }

        #region overrides base class

        public override void Init(GameObject rParent, string sText)
        {
            base.Init(rParent, sText);

            m_RemotePlayers.remotePlayerAdded += RemotePlayerAdded;
            m_RemotePlayers.remotePlayerRemoved += RemotePlayerRemoved;
            m_RemotePlayers.remotePlayersListCleared += RemotePlayersListCleared;
        }

        public void OnDestroy()
        {
            if (m_RemotePlayers != null)
            {
                m_RemotePlayers.remotePlayerAdded -= RemotePlayerAdded;
                m_RemotePlayers.remotePlayerRemoved -= RemotePlayerRemoved;
                m_RemotePlayers.remotePlayersListCleared -= RemotePlayersListCleared;
            }
        }

        protected override void UpdateOpening()
        {
            base.UpdateOpening();
            UpdateTitles();
            GeneratePlayerList();
        }

        #endregion

        public void RemotePlayerAdded(int playerId)
        {
            GeneratePlayerList();
        }

        public void RemotePlayerRemoved(int playerId)
        {
            GeneratePlayerList();
        }

        public void RemotePlayersListCleared()
        {
            GeneratePlayerList();
        }

        public void UpdateTitles()
        {
            if (m_WindowText) m_WindowText.text = m_popupWindowTitleString.GetLocalizedString();
            if (m_playerLIstTitle) m_playerLIstTitle.text = m_playerLIstTitleString.GetLocalizedString();
        }

        public void GeneratePlayerList(List<RemotePlayer> playersList = null)
        {
            if (m_playerGuiPrefab == null)
            {
                Debug.LogWarning("Player GUI Prefab is not assigned!");
                return;
            }

            ClearGuiPrefabsList();

            List<RemotePlayer> playersToDisplay = playersList ?? m_RemotePlayers.List;

            Vector3 basePosition = new Vector3(
                -PlayerListArea.x / 2 + PlayerGuiPrefabSize.x / 2,
                PlayerListOffset.y + PlayerListArea.y / 2 - PlayerGuiPrefabSize.y / 2,
                0
            );
            float yOffset = PlayerGuiPrefabSize.y;

            foreach (var remotePlayer in playersToDisplay)
            {
                GameObject playerListItem = Instantiate(m_playerGuiPrefab, basePosition, transform.rotation, transform);
                playerListItem.name = $"Player_{remotePlayer.PlayerId}";
                playerListItem.transform.localPosition = basePosition;

                PlayerListItemPrefab playerGuiComponent = playerListItem.GetComponent<PlayerListItemPrefab>();
                if (playerGuiComponent != null) playerGuiComponent.SetRemotePlayer(remotePlayer);

                basePosition -= new Vector3(0, yOffset, 0);
                playerListItem.SetActive(true);
                AddTodGuiPrefabsList(playerListItem);
            }
        }

        private void AddTodGuiPrefabsList(GameObject item)
        {
            m_instantiatedGuiPrefabs.Add(item);
        }

        private void ClearGuiPrefabsList()
        {
            foreach (GameObject g in m_instantiatedGuiPrefabs) DestroyImmediate(g);
            m_instantiatedGuiPrefabs.Clear();
        }

        private PlayerListItemPrefab GetGameobjectWithPlayerId(int playerID)
        {
            foreach (GameObject playerGui in m_instantiatedGuiPrefabs)
            {
                PlayerListItemPrefab playerComponent = playerGui.GetComponent<PlayerListItemPrefab>();
                if (playerComponent != null && playerComponent.remotePlayer.PlayerId == playerID)
                    return playerComponent;
            }

            return null;
        }


        public void OnMultiplayerRoomOptionsPopUpWindowButtonPressed(MultiplayerRoomOptionsPanelButton button)
        {
            switch (button.m_Command)
            {
                case SketchControlsScript.GlobalCommands.Null:
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerMutePlayerForMe:
                    Debug.Log($"ToggleAudioMuteForPlayer: {button.IsToggledOn}::{button.playerId}");
                    MultiplayerManager.m_Instance.MutePlayerForMe(button.IsToggledOn, button.playerId);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerPlayerMuteForAll:
                    Debug.Log($"MutePlayerForAll: {button.IsToggledOn}::{button.playerId}");
                    MultiplayerManager.m_Instance.MutePlayerForAll(button.IsToggledOn, button.playerId);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerTransferRoomOwnership:
                    Debug.Log($"RoomOwnershipTransferToUser: {button.IsToggledOn}::{button.playerId}");
                    MultiplayerManager.m_Instance.RoomOwnershipTransferToUser(button.playerId);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerViewOnlyMode:
                    Debug.Log($"SetUserViewOnlyMode: {button.IsToggledOn}::{button.playerId}");
                    MultiplayerManager.m_Instance.SetUserViewOnlyMode(button.IsToggledOn, button.playerId);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerKickPlayerOut:
                    Debug.Log($"KickPlayerOut: {button.IsToggledOn}::{button.playerId}");
                    MultiplayerManager.m_Instance.KickPlayerOut(button.playerId);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerMuteAllForMe:
                    foreach (var remotePlayer in m_RemotePlayers.List)
                    {
                        Debug.Log($"ToggleAllUserAudio: {button.IsToggledOn}::{remotePlayer}");
                        MultiplayerAudioSourcesManager.m_Instance.SetMuteForPlayer(remotePlayer.PlayerId, button.IsToggledOn);
                        PlayerListItemPrefab playerComponent = GetGameobjectWithPlayerId(remotePlayer.PlayerId);
                        if (playerComponent) playerComponent.SetAudioToggleState(button.IsToggledOn);
                    }
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerMuteAllForAll:
                    foreach (var remotePlayer in m_RemotePlayers.List)
                    {
                        Debug.Log($"ToggleAllUserAudioForAll: {button.IsToggledOn}::{remotePlayer}");
                        MultiplayerManager.m_Instance.MutePlayerForAll(button.IsToggledOn, remotePlayer.PlayerId);
                        PlayerListItemPrefab playerComponent = GetGameobjectWithPlayerId(remotePlayer.PlayerId);
                        if (playerComponent) playerComponent.SetAudioForAllToggleState(button.IsToggledOn);
                    }
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerSetAllViewOnly:
                    foreach (var remotePlayer in m_RemotePlayers.List)
                    {
                        Debug.Log($"ToggleAllUserViewEditMode: {button.IsToggledOn}::{remotePlayer}");
                        MultiplayerManager.m_Instance.SetUserViewOnlyMode(button.IsToggledOn, remotePlayer.PlayerId);
                        PlayerListItemPrefab playerComponent = GetGameobjectWithPlayerId(remotePlayer.PlayerId);
                        if (playerComponent) playerComponent.SetViewOnlyToggleState(button.IsToggledOn);
                    }
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerSetRoomViewOnly:
                    Debug.Log($"SetRoomViewOnly: {button.IsToggledOn}");
                    MultiplayerManager.m_Instance.SetRoomViewOnly(button.IsToggledOn);
                    break;
                case SketchControlsScript.GlobalCommands.MultiplayerSetRoomSilent:
                    Debug.Log($"SetRoomSilent: {button.IsToggledOn}");
                    MultiplayerManager.m_Instance.SetRoomSilent(button.IsToggledOn);
                    break;
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;

            Vector3 basePosition = transform.position + new Vector3(PlayerListOffset.x, PlayerListOffset.y, 0);

            Vector3 topLeft = basePosition + new Vector3(-PlayerListArea.x / 2, PlayerListArea.y / 2, 0);
            Vector3 topRight = basePosition + new Vector3(PlayerListArea.x / 2, PlayerListArea.y / 2, 0);
            Vector3 bottomLeft = basePosition + new Vector3(-PlayerListArea.x / 2, -PlayerListArea.y / 2, 0);
            Vector3 bottomRight = basePosition + new Vector3(PlayerListArea.x / 2, -PlayerListArea.y / 2, 0);

            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);

            Vector3 topEdgeCenter = (topLeft + topRight) / 2;
            UnityEditor.Handles.Label(topEdgeCenter, "Player List Area");

        }
#endif


    }
} // namespace TiltBrush
