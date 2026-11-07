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
    private static volatile string m_QualityStatus = "Quality status is not available yet.";
    private float m_NextQualityStatusUpdate;
    public static string QualityStatus => m_QualityStatus;
    private static volatile string m_AutomationStatus = "{}";
    private float m_NextAutomationStatusUpdate;
    public static string AutomationStatus => m_AutomationStatus;
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
        // Publish a main-thread snapshot for background HTTP query handlers.
        if (Time.unscaledTime >= m_NextAutomationStatusUpdate)
        {
            m_AutomationStatus = ApiMethods.GetAutomationStatusJson();
            m_NextAutomationStatusUpdate = Time.unscaledTime + 0.1f;
        }
        // HTTP handlers run off the main thread. Publish a recent snapshot so they
        // can return quality information without calling Unity rendering APIs.
        if (Time.unscaledTime >= m_NextQualityStatusUpdate)
        {
            m_QualityStatus = ApiMethods.GetQualityLevel();
            m_NextQualityStatusUpdate = Time.unscaledTime + 1f;
        }

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
