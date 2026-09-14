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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly ConcurrentQueue<TaskCompletionSource<string>>
        m_MultiplayerStatusRequests =
            new ConcurrentQueue<TaskCompletionSource<string>>();
    private int m_MainThreadId;
    private const int k_MultiplayerStatusTimeoutMs = 2000;

    public Transform SpectatorCamTarget;

    void Awake()
    {
        m_Instance = this;
        m_MainThreadId = Thread.CurrentThread.ManagedThreadId;
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
        if (!m_MultiplayerStatusRequests.IsEmpty)
        {
            MultiplayerStatusJson = BuildMultiplayerStatusJson();
            while (m_MultiplayerStatusRequests.TryDequeue(out var request))
            {
                request.TrySetResult(MultiplayerStatusJson);
            }
        }
        //     m_Status = StatusTypes.Ready;
        // }

    }

    public string RequestMultiplayerStatus()
    {
        if (Thread.CurrentThread.ManagedThreadId == m_MainThreadId)
        {
            MultiplayerStatusJson = BuildMultiplayerStatusJson();
            return MultiplayerStatusJson;
        }

        var request = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        m_MultiplayerStatusRequests.Enqueue(request);
        if (request.Task.Wait(k_MultiplayerStatusTimeoutMs))
        {
            return request.Task.Result;
        }

        Debug.LogWarning(
            "[MultiplayerApiSnapshot] Timed out waiting for the Unity main thread; returning the most recent snapshot.");
        return MultiplayerStatusJson;
    }

    void OnDestroy()
    {
        while (m_MultiplayerStatusRequests.TryDequeue(out var request))
        {
            request.TrySetResult(MultiplayerStatusJson);
        }
        if (m_Instance == this)
        {
            m_Instance = null;
        }
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
            useDefaultPhotonCloudPorts = App.UserConfig.Flags.UseDefaultPhotonCloudPorts,
            room = inRoom ? new
            {
                name = room.roomName,
                voiceEnabled = manager.IsRoomVoiceEnabled,
                silent = room.silentRoom,
                viewOnly = room.viewOnlyRoom,
                liveStrokeStreaming = room.liveStrokeStreaming
            } : null,
            players
        });
    }

}
