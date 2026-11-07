// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using UnityEditor;
using UnityEngine;

namespace TiltBrush
{
    public sealed class ProceduralAutomationWindow : EditorWindow
    {
        [SerializeField] private BaseButton m_Target;
        [SerializeField] private ProceduralAutomation.Settings m_Settings = new ProceduralAutomation.Settings();
        private Vector2 m_Scroll;
        private string m_Error;
        [SerializeField] private string m_RecordingName = "tutorial";
        [SerializeField] private int m_FrameRate = 30;

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
            EditorGUILayout.HelpBox("Virtual Input mode needs no headset or controllers. Launch a player with --virtualInput. Recording writes PNG frames plus frame-rate metadata; keep the Game view visible during capture.", MessageType.Info);
            EditorGUILayout.HelpBox("In Play mode, select the paint tool and show the wand's brush panel. This rehearsal points at a button, clicks it, then draws a curved stroke. Only the controllers are animated; the arm model is invisible. Panel Position is relative to the initial view; stroke positions use the initial head position and horizontal facing. Distances are metres.", MessageType.Info);
            bool ready = Application.isPlaying && InputManager.m_Instance != null;
            string status = ready ? InputManager.m_Instance.ProceduralAutomationStatus : "Enter Play mode";
            EditorGUILayout.LabelField("Status", status);
            bool running = ready && status != "Stopped";
            using (new EditorGUI.DisabledScope(running))
            {
                m_Target = (BaseButton)EditorGUILayout.ObjectField("Panel button", m_Target, typeof(BaseButton), true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Find brush button")) { m_Target = ProceduralAutomation.FindBrushButton(); }
                    if (GUILayout.Button("Use selected button"))
                    {
                        m_Target = Selection.activeGameObject == null ? null : Selection.activeGameObject.GetComponentInParent<BaseButton>();
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!ready || running))
                {
                    if (GUILayout.Button("Start rehearsal"))
                    {
                        try
                        {
                            m_Error = null;
                            if (m_Target == null) { m_Target = ProceduralAutomation.FindBrushButton(); }
                            InputManager.m_Instance.StartProceduralAutomation(m_Target, m_Settings);
                        }
                        catch (Exception exception) { m_Error = exception.Message; }
                    }
                }
                using (new EditorGUI.DisabledScope(!ready || running || App.Config.m_SdkMode != SdkMode.VirtualInput))
                {
                    if (GUILayout.Button("Record rehearsal"))
                    {
                        try
                        {
                            m_Error = null;
                            if (m_Target == null) { m_Target = ProceduralAutomation.FindBrushButton(); }
                            ApiMethods.RecordProceduralAutomation(m_Target, m_Settings, m_RecordingName, m_FrameRate);
                            EditorApplication.ExecuteMenuItem("Window/General/Game");
                        }
                        catch (Exception exception) { m_Error = exception.Message; }
                    }
                }
                using (new EditorGUI.DisabledScope(!running))
                {
                    if (GUILayout.Button("Stop (Escape)")) { ApiMethods.AutomationStop(); }
                }
            }
            if (!string.IsNullOrEmpty(m_Error)) { EditorGUILayout.HelpBox(m_Error, MessageType.Warning); }
            using (new EditorGUI.DisabledScope(running))
            {
                m_RecordingName = EditorGUILayout.TextField("Recording name", m_RecordingName);
                m_FrameRate = EditorGUILayout.IntSlider("Recording FPS", m_FrameRate, 15, 60);
                if (GUILayout.Button("Load settings from UserConfig"))
                {
                    m_Settings = ready ? ProceduralAutomation.Settings.FromUserConfig() : new ProceduralAutomation.Settings();
                }
                m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
                var serialized = new SerializedObject(this);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty("m_Settings"), true);
                serialized.ApplyModifiedProperties();
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.HelpBox("The click and stroke use normal application input. Each run adds a stroke; use Undo to remove it. Settings changes apply to the next run. The virtual head starts at 1.65 metres and can be positioned with automation.head.", MessageType.None);
        }
    }
}
