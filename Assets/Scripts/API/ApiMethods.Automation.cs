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

        [ApiEndpoint("automation.start", "Presses a visible wand-panel brush button and draws a procedural stroke. Optional argument: brush durable name.")]
        public static void AutomationStart(string brush = "")
        {
            InputManager.m_Instance.StartProceduralAutomation(ProceduralAutomation.FindBrushButton(brush), ProceduralAutomation.Settings.FromUserConfig());
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

        [ApiEndpoint("automation.record", "Records a procedural rehearsal to a PNG sequence in Videos. Arguments: filename, fps, optional brush durable name.", "tutorial,30")]
        public static string AutomationRecord(string filename = "", int fps = 30, string brush = "")
        {
            return RecordProceduralAutomation(ProceduralAutomation.FindBrushButton(brush), ProceduralAutomation.Settings.FromUserConfig(), filename, fps);
        }

        [ApiEndpoint("automation.reshape.prepare", "Creates a starter stroke before recording and prepares the Reshape controls. Optional brush durable name.", "DuctTape")]
        public static void AutomationReshapePrepare(string brush = "DuctTape") =>
            PrepareReshape(ProceduralAutomation.Settings.FromUserConfig(), brush);

        [ApiEndpoint("automation.reshape.use", "Uses the most recent existing stroke for the Grab demonstration.")]
        public static void AutomationReshapeUse() =>
            PrepareReshape(ProceduralAutomation.Settings.FromUserConfig(), "DuctTape", useLatestStroke: true);

        [ApiEndpoint("automation.reshape.start", "Selects Reshape and Grab through the UI, then bends the middle of an existing stroke.")]
        public static void AutomationReshapeStart()
        {
            var settings = ProceduralAutomation.Settings.FromUserConfig();
            ReshapeStrokeSetup setup = PrepareReshape(settings);
            InputManager.m_Instance.StartProceduralAutomation(setup.ToolButton, settings, setup.CreateAction);
        }

        [ApiEndpoint("automation.reshape.record", "Records selecting Reshape and Grab and bending an existing stroke. Creates a starter stroke before recording if needed.", "reshape,30,DuctTape")]
        public static string AutomationReshapeRecord(string filename = "", int fps = 30, string brush = "DuctTape")
        {
            var settings = ProceduralAutomation.Settings.FromUserConfig();
            ReshapeStrokeSetup setup = PrepareReshape(settings, brush);
            return RecordProceduralAutomation(setup.ToolButton, settings, filename, fps, setup.CreateAction);
        }

        public static ReshapeStrokeSetup PrepareReshape(ProceduralAutomation.Settings settings,
            string brush = "DuctTape", bool useLatestStroke = false)
        {
            var capture = App.Instance.GetComponent<ProceduralCapture>();
            if (capture == null) { capture = App.Instance.gameObject.AddComponent<ProceduralCapture>(); }
            try
            {
                var setup = App.Instance.GetComponent<ReshapeStrokeSetup>();
                if (setup == null) { setup = App.Instance.gameObject.AddComponent<ReshapeStrokeSetup>(); }
                setup.Prepare(settings, string.IsNullOrEmpty(brush) ? "DuctTape" : brush, useLatestStroke);
                capture.Error = null;
                return setup;
            }
            catch (Exception exception)
            {
                capture.Error = exception.Message;
                throw;
            }
        }

        public static string RecordProceduralAutomation(BaseButton button, ProceduralAutomation.Settings settings, string filename, int fps,
            Func<ProceduralAutomation.Settings, UnityEngine.Pose, ProceduralAction> actionFactory = null)
        {
            if (InputManager.m_Instance.ProceduralAutomationStatus != "Stopped")
            {
                throw new InvalidOperationException("Stop the current rehearsal before recording another.");
            }
            var capture = App.Instance.GetComponent<ProceduralCapture>();
            if (capture == null) { capture = App.Instance.gameObject.AddComponent<ProceduralCapture>(); }
            try
            {
                string path = capture.Begin(filename, fps);
                InputManager.m_Instance.StartProceduralAutomation(button, settings, actionFactory);
                return path;
            }
            catch (Exception exception)
            {
                capture.Error = exception.Message;
                capture.Stop();
                throw;
            }
        }
    }
}
