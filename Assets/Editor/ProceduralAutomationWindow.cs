// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    public sealed class ProceduralAutomationWindow : EditorWindow
    {
        [SerializeField] private string m_PlanPath;
        [SerializeField] private string m_PlanJson;
        [SerializeField] private string m_RecordingName = "tutorial";
        [SerializeField] private int m_FrameRate = 30;
        [SerializeField] private float m_PlaybackSpeed = 1;
        private string m_Error;

        [MenuItem("Open Brush/Procedural Automation")]
        public static void Open()
        {
            SessionState.SetBool(Config.VirtualInputSessionKey, true);
            GetWindow<ProceduralAutomationWindow>("Procedural Automation");
        }

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                bool virtualInput = SessionState.GetBool(Config.VirtualInputSessionKey, false);
                SessionState.SetBool(Config.VirtualInputSessionKey,
                    EditorGUILayout.Toggle("Virtual Input next Play", virtualInput));
            }
            EditorGUILayout.HelpBox("Enable Virtual Input and enter Play mode. Load a JSON plan exported by the Python runner with --write-plan. The Editor uses the same executor as automation.script.", MessageType.Info);
            bool ready = Application.isPlaying && InputManager.m_Instance != null;
            var capture = ready ? App.Instance.GetComponent<ProceduralCapture>() : null;
            string status = ready ? InputManager.m_Instance.ProceduralAutomationStatus : "Enter Play mode";
            bool recording = capture?.IsRecording == true;
            bool running = ready && (status != "Stopped" || recording);
            EditorGUILayout.LabelField("Status", status);
            if (recording) { EditorGUILayout.LabelField("Recorded frames", capture.FrameCount.ToString()); }

            using (new EditorGUI.DisabledScope(running))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Load JSON plan"))
                    {
                        string directory = string.IsNullOrEmpty(m_PlanPath) ? Path.GetDirectoryName(Application.dataPath) : Path.GetDirectoryName(m_PlanPath);
                        string path = EditorUtility.OpenFilePanel("Load automation plan", directory, "json");
                        if (!string.IsNullOrEmpty(path)) { TryAction(() => LoadPlan(path)); }
                    }
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(m_PlanPath)))
                    {
                        if (GUILayout.Button("Reload")) { TryAction(() => LoadPlan(m_PlanPath)); }
                    }
                }
                EditorGUILayout.LabelField("Plan", string.IsNullOrEmpty(m_PlanPath) ? "No plan loaded" : Path.GetFileName(m_PlanPath));
                m_RecordingName = EditorGUILayout.TextField("Recording name", m_RecordingName);
                m_FrameRate = EditorGUILayout.IntSlider("Recording FPS", m_FrameRate, 1, 120);
                m_PlaybackSpeed = EditorGUILayout.Slider("Playback speed", m_PlaybackSpeed, 0.25f, 2);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!ready || running || string.IsNullOrEmpty(m_PlanJson) || App.Config.m_SdkMode != SdkMode.VirtualInput))
                {
                    if (GUILayout.Button("Rehearse")) { TryAction(() => StartPlan(record: false)); }
                    if (GUILayout.Button("Record")) { TryAction(() => StartPlan(record: true)); }
                }
                using (new EditorGUI.DisabledScope(!running))
                {
                    if (GUILayout.Button("Stop (Escape)")) { ApiMethods.AutomationStop(); }
                }
            }
            string error = m_Error ?? capture?.Error ?? (ready ? InputManager.m_Instance.ProceduralAutomationError : null);
            if (!string.IsNullOrEmpty(error)) { EditorGUILayout.HelpBox(error, MessageType.Warning); }
            if (!string.IsNullOrEmpty(capture?.OutputDirectory))
                EditorGUILayout.LabelField("Output", capture.OutputDirectory);
            EditorGUILayout.HelpBox("Rehearse ignores the plan's capture settings. Record uses the name and FPS above and writes PNG frames into Open Brush's Videos folder. Keep the Game view visible. Geometry changes remain in the sketch; use Undo before repeating a take.", MessageType.None);
        }

        private void LoadPlan(string path)
        {
            string json = File.ReadAllText(path);
            AutomationScript.Parse(json);
            m_PlanPath = path;
            m_PlanJson = json;
        }

        private void StartPlan(bool record)
        {
            var plan = AutomationScript.Parse(m_PlanJson);
            plan.Speed = m_PlaybackSpeed;
            plan.Capture = record ? new AutomationScript.CaptureOptions {Name = m_RecordingName, Fps = m_FrameRate} : null;
            if (record) { EditorApplication.ExecuteMenuItem("Window/General/Game"); }
            ApiMethods.StartAutomationScript(plan);
        }

        private void TryAction(Action action)
        {
            try { m_Error = null; action(); }
            catch (Exception exception) { m_Error = exception.Message; }
        }
    }
}
