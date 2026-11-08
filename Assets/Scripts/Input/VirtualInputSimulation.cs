// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    /// Virtual camera and controller geometry with no XR runtime or physical tracking.
    /// InputManager uses virtual ControllerInfos for the entire session, including idle.
    public sealed class VirtualInputSimulation : IDisposable
    {
        private readonly Camera m_Camera;
        private readonly VrControllers m_Controllers;
        private readonly IDisposable m_ControllerTracking;
        private readonly List<Behaviour> m_HeadDrivers = new List<Behaviour>();
        private readonly List<bool> m_HeadDriverStates = new List<bool>();
        public UnityEngine.Pose BodyPose { get; private set; }

        public VirtualInputSimulation(Camera camera, VrControllers controllers)
        {
            m_Camera = camera;
            m_Controllers = controllers;
            m_ControllerTracking = controllers.SuspendPoseTracking();
            foreach (var driver in camera.GetComponents<UnityEngine.SpatialTracking.TrackedPoseDriver>())
            {
                SuspendHeadDriver(driver);
            }
            foreach (var driver in camera.GetComponents<UnityEngine.InputSystem.XR.TrackedPoseDriver>())
            {
                SuspendHeadDriver(driver);
            }
            camera.stereoTargetEye = StereoTargetEyeMask.None;
            camera.fieldOfView = 75;
            SetHeadPose(new Vector3(0, 1.65f * App.METERS_TO_UNITS, 0), Quaternion.identity);
            Debug.Log("[ProceduralAutomation] Virtual Input initialized; XR loading and hardware input are disabled.");
        }

        private void SuspendHeadDriver(Behaviour driver)
        {
            m_HeadDrivers.Add(driver);
            m_HeadDriverStates.Add(driver.enabled);
            driver.enabled = false;
        }

        public void SetHeadPose(Vector3 position, Quaternion rotation)
        {
            BodyPose = new UnityEngine.Pose(position, rotation);
            m_Camera.transform.SetPositionAndRotation(position, rotation);
            Quaternion yaw = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            m_Controllers.Brush.transform.SetPositionAndRotation(
                position + yaw * (new Vector3(0.24f, -0.48f, 0.23f) * App.METERS_TO_UNITS), yaw);
            // Both grips rest forward; panel presentation raises the wand when needed.
            m_Controllers.Wand.transform.SetPositionAndRotation(
                position + yaw * (new Vector3(-0.24f, -0.48f, 0.23f) * App.METERS_TO_UNITS),
                yaw);
        }

        // A filming adjustment must not move the shoulders, controllers, or authored actions.
        public void SetFramingPose(UnityEngine.Pose pose) =>
            m_Camera.transform.SetPositionAndRotation(pose.position, pose.rotation);

        public void Dispose()
        {
            m_ControllerTracking.Dispose();
            for (int i = 0; i < m_HeadDrivers.Count; ++i)
            {
                if (m_HeadDrivers[i] != null) { m_HeadDrivers[i].enabled = m_HeadDriverStates[i]; }
            }
        }
    }
}
