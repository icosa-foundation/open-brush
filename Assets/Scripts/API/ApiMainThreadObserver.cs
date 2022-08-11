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
        //     m_Status = StatusTypes.Ready;
        // }

    }

}
