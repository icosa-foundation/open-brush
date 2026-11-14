// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using UnityEngine;

namespace TiltBrush
{
    public static partial class ApiMethods
    {
        private static string m_AutomationRunId;
        [Serializable]
        private class AutomationSnapshot
        {
            public string Mode;
            public string Phase;
            public bool Recording;
            public int Frames;
            public string Output;
            public string Error;
            public string RunId;
        }

        internal static string GetAutomationStatusJson()
        {
            var input = InputManager.m_Instance;
            var capture = App.Instance.GetComponent<ProceduralCapture>();
            return JsonUtility.ToJson(new AutomationSnapshot
            {
                Mode = App.Config.m_SdkMode.ToString(),
                Phase = input.ProceduralAutomationStatus,
                Recording = capture != null && capture.IsRecording,
                Frames = capture == null ? 0 : capture.FrameCount,
                Output = capture == null ? "" : capture.OutputDirectory,
                Error = capture?.Error ?? input.ProceduralAutomationError ?? "",
                RunId = m_AutomationRunId
            });
        }

        [ApiEndpoint("automation.script", "Runs an externally authored version 1 automation plan. Arguments: run id, URL-safe base64 UTF-8 JSON.")]
        public static void AutomationRunScript(string runId, string encoded)
        {
            StartAutomationScript(runId, () =>
            {
                string json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/')));
                return AutomationScript.Parse(json);
            });
        }

        public static void StartAutomationScript(AutomationScript script) =>
            StartAutomationScript(Guid.NewGuid().ToString("N"), () => script);

        private static void StartAutomationScript(string runId, Func<AutomationScript> loadPlan)
        {
            if (InputManager.m_Instance.ProceduralAutomationStatus != "Stopped" ||
                App.Instance.GetComponent<ProceduralCapture>()?.IsRecording == true)
                throw new InvalidOperationException("Stop the current automation and recording before submitting a script.");
            m_AutomationRunId = runId;
            var capture = App.Instance.GetComponent<ProceduralCapture>() ?? App.Instance.gameObject.AddComponent<ProceduralCapture>();
            capture.Error = null;
            try
            {
                InputManager.m_Instance.StartAutomationScript(loadPlan());
            }
            catch (Exception error)
            {
                capture.Error = error.Message;
                throw;
            }
        }

        [ApiEndpoint("automation.stop", "Stops the procedural rehearsal and its recording.")]
        public static void AutomationStop()
        {
            InputManager.m_Instance.StopProceduralAutomation();
            App.Instance.GetComponent<ProceduralCapture>()?.Stop();
        }

        [ApiEndpoint("automation.head", "Sets the virtual body/head reference in world metres and rotation in degrees. Shot framing adjusts the camera independently. Rehearsal must be stopped.", "0,1.65,0,0,0,0")]
        public static void AutomationHead(Vector3 position, Vector3 rotation)
        {
            if (App.VrSdk.VirtualSimulation == null)
            {
                throw new InvalidOperationException("Launch with --virtualInput to control the virtual head.");
            }
            if (InputManager.m_Instance.ProceduralAutomationStatus != "Stopped")
            {
                throw new InvalidOperationException("Stop the rehearsal before repositioning its head and body.");
            }
            App.VrSdk.VirtualSimulation.SetHeadPose(position * App.METERS_TO_UNITS, Quaternion.Euler(rotation));
        }

    }
}
