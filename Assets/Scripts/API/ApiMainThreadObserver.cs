// Copyright 2022 The Open Brush Authors
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
using Newtonsoft.Json;
using OpenBrush.Multiplayer;
using TiltBrush;
using UnityEngine;

public class ApiMainThreadObserver : MonoBehaviour
{

    public enum StatusTypes
    {
        Dormant,
        Requested,
        Ready,
    }

    private static ApiMainThreadObserver m_Instance;
    [NonSerialized] public StatusTypes m_Status;
    [NonSerialized] public Vector3 SpectatorCamPosition;
    [NonSerialized] public Vector3 SpectatorCamTargetPosition;
    [NonSerialized] public Quaternion SpectatorCamRotation;
    [NonSerialized] public volatile string MultiplayerStatusJson = @"{""state"":""UNAVAILABLE"",""inRoom"":false,""players"":[]}";
    private volatile bool m_MultiplayerStatusRequested;

    public Transform SpectatorCamTarget;

    void Awake()
    {
        m_Instance = this;
    }

    public static ApiMainThreadObserver Instance => m_Instance;

    void Update()
    {
        // if (m_Status == StatusTypes.Requested)
        // {
        var spectator = SketchControlsScript.m_Instance.GetDropCampWidget();
        var spectatorTr = spectator.transform;
        SpectatorCamPosition = spectatorTr.position;
        SpectatorCamRotation = spectatorTr.rotation;
        SpectatorCamTargetPosition = SpectatorCamTarget.position;
        if (m_MultiplayerStatusRequested)
        {
            m_MultiplayerStatusRequested = false;
            MultiplayerStatusJson = BuildMultiplayerStatusJson();
        }
        //     m_Status = StatusTypes.Ready;
        // }

    }

    public string RequestMultiplayerStatus()
    {
        m_MultiplayerStatusRequested = true;
        return MultiplayerStatusJson;
    }

    private static string BuildMultiplayerStatusJson()
    {
        var manager = MultiplayerManager.m_Instance;
        if (manager == null)
        {
            return "{\"state\":\"UNAVAILABLE\",\"inRoom\":false,\"players\":[]}";
        }

        bool inRoom = manager.State == ConnectionState.IN_ROOM;
        var players = new List<object>();
        if (inRoom && manager.m_RemotePlayers != null)
        {
            foreach (var player in manager.m_RemotePlayers.List)
            {
                players.Add(new
                {
                    playerId = player.PlayerId,
                    nickname = player.Nickname,
                    isOwner = manager.IsPlayerRoomOwner(player.PlayerId),
                    mutedForMe = player.m_IsMutedForMe,
                    mutedForAll = player.m_IsMutedForAll,
                    viewOnly = player.m_IsViewOnly,
                    hiddenForMe = player.m_IsHiddenForMe
                });
            }
        }

        var room = manager.CurrentRoomData;
        return JsonConvert.SerializeObject(new
        {
            state = manager.State.ToString(),
            inRoom,
            localPlayerId = manager.LocalPlayerId,
            localNickname = manager.UserInfo.Nickname,
            isOwner = inRoom && manager.IsUserRoomOwner(),
            localVoiceEnabled = manager.IsLocalVoiceEnabled,
            playerAvatarsHiddenForMe = manager.ArePlayerAvatarsHiddenForMe,
            voiceEnabled = manager.IsVoiceEnabled,
            room = inRoom ? new
            {
                name = room.roomName,
                voiceEnabled = manager.IsRoomVoiceEnabled,
                silent = room.silentRoom,
                viewOnly = room.viewOnlyRoom,
                liveStrokeStreaming = room.liveStrokeStreaming,
                liveStrokeProtocolVersion = room.liveStrokeProtocolVersion
            } : null,
            players
        });
    }

}
