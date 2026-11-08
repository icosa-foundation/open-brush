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
        private enum Sequence { DrawStroke, ReshapeGrab }
        [SerializeField] private Sequence m_Sequence;
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
            EditorGUILayout.HelpBox("Sequences use controller input to select tools and interact with strokes. Action positions use the virtual body's reference pose in metres. Automatic framing fits the declared subjects once and holds the camera still for the shot. Framing controls are under Settings > Framing.", MessageType.Info);
            bool ready = Application.isPlaying && InputManager.m_Instance != null;
            string status = ready ? InputManager.m_Instance.ProceduralAutomationStatus : "Enter Play mode";
            EditorGUILayout.LabelField("Status", status);
            bool running = ready && status != "Stopped";
            using (new EditorGUI.DisabledScope(running))
            {
                Sequence sequence = (Sequence)EditorGUILayout.EnumPopup("Sequence", m_Sequence);
                if (sequence != m_Sequence) { m_Sequence = sequence; m_Target = null; }
                if (m_Sequence == Sequence.ReshapeGrab)
                {
                    EditorGUILayout.HelpBox("Start the video with a stroke already drawn. Preparation happens before recording; the video shows selecting Reshape, selecting Grab, and bending the stroke. Use Undo to restore the stroke before another take.", MessageType.Info);
                    using (new EditorGUI.DisabledScope(!ready))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Prepare starter stroke")) { PrepareReshape(false); }
                        if (GUILayout.Button("Use latest stroke")) { PrepareReshape(true); }
                    }
                }
                m_Target = (BaseButton)EditorGUILayout.ObjectField("Panel button", m_Target, typeof(BaseButton), true);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!ready))
                    {
                        if (GUILayout.Button("Find action button"))
                        {
                            if (m_Sequence == Sequence.ReshapeGrab) { PrepareReshape(false); }
                            else { m_Target = ProceduralAutomation.FindBrushButton(); }
                        }
                    }
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
                            StartSequence(record: false);
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
                            StartSequence(record: true);
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
            EditorGUILayout.HelpBox("Selections and tool actions use normal application input. Drawing adds a stroke; Reshape changes the prepared stroke. Use Undo before another take to restore the starting canvas. Settings changes apply to the next run. The virtual head starts at 1.65 metres and can be positioned with automation.head.", MessageType.None);
        }

        private void PrepareReshape(bool useLatestStroke)
        {
            try
            {
                m_Error = null;
                m_Target = ApiMethods.PrepareReshape(m_Settings, useLatestStroke: useLatestStroke).ToolButton;
            }
            catch (Exception exception) { m_Error = exception.Message; }
        }

        private void StartSequence(bool record)
        {
            Func<ProceduralAutomation.Settings, UnityEngine.Pose, ProceduralAction> factory = null;
            if (m_Sequence == Sequence.ReshapeGrab)
            {
                ReshapeStrokeSetup setup = ApiMethods.PrepareReshape(m_Settings);
                m_Target = setup.ToolButton;
                factory = setup.CreateAction;
            }
            else if (m_Target == null) { m_Target = ProceduralAutomation.FindBrushButton(); }
            if (record) { ApiMethods.RecordProceduralAutomation(m_Target, m_Settings, m_RecordingName, m_FrameRate, factory); }
            else { InputManager.m_Instance.StartProceduralAutomation(m_Target, m_Settings, factory); }
        }
    }
}
