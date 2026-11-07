// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Opt-in rehearsal: a real wand-panel click followed by a real free-paint stroke.
    /// InputManager advances this before controller input consumers and reapplies the poses
    /// before dispatching tracking callbacks. No visible skeleton or direct button invocation.
    public sealed class ProceduralAutomation : IDisposable
    {
        [Serializable]
        public class Settings
        {
            public float UpperArmLength = 0.30f;
            public float ForearmLength = 0.30f;
            public float ReachSeconds = 1.2f;
            public float DrawSeconds = 2.4f;
            public float PauseSeconds = 0.45f;
            [Tooltip("Panel centre in metres relative to the initial view. The wand pose is solved from the panel's actual attachment.")]
            public Vector3 PanelPosition = new Vector3(-0.12f, -0.10f, 0.34f);
            public Vector3 StrokeStart = new Vector3(-0.08f, -0.24f, 0.40f);
            public Vector3 StrokeControl1 = new Vector3(0.02f, -0.04f, 0.44f);
            public Vector3 StrokeControl2 = new Vector3(0.24f, -0.43f, 0.46f);
            public Vector3 StrokeEnd = new Vector3(0.33f, -0.20f, 0.42f);

            public static Settings FromUserConfig()
            {
                var config = App.UserConfig.ProceduralAutomation;
                var settings = new Settings();
                settings.UpperArmLength = config.UpperArmLength ?? settings.UpperArmLength;
                settings.ForearmLength = config.ForearmLength ?? settings.ForearmLength;
                settings.ReachSeconds = config.ReachSeconds ?? settings.ReachSeconds;
                settings.DrawSeconds = config.DrawSeconds ?? settings.DrawSeconds;
                settings.PauseSeconds = config.PauseSeconds ?? settings.PauseSeconds;
                settings.PanelPosition = config.PanelPosition ?? settings.PanelPosition;
                settings.StrokeStart = config.StrokeStart ?? settings.StrokeStart;
                settings.StrokeControl1 = config.StrokeControl1 ?? settings.StrokeControl1;
                settings.StrokeControl2 = config.StrokeControl2 ?? settings.StrokeControl2;
                settings.StrokeEnd = config.StrokeEnd ?? settings.StrokeEnd;
                return settings;
            }
        }

        private enum Phase { RaiseWand, Aim, Hover, Press, Release, ApproachStroke, Draw, Inspect, Rest, Finish }
        private const string kLogPrefix = "[ProceduralAutomation]";
        private readonly Settings m_Settings;
        private readonly BaseButton m_Button;
        private readonly BasePanel m_Panel;
        private readonly ControllerInfo[] m_Original;
        private readonly ProceduralControllerInfo[] m_Simulated;
        private readonly UnityEngine.Pose[] m_OriginalPoses;
        private readonly bool[] m_OriginalVisibility;
        private readonly IDisposable m_Tracking;
        private readonly bool m_ShowControllers;
        private readonly Vector3 m_Origin;
        private readonly Quaternion m_BodyRotation;
        private readonly Vector3 m_TipOffset;
        private readonly Quaternion m_TipRotation;
        private readonly ProceduralArm m_BrushArm;
        private readonly ProceduralArm m_WandArm;
        private readonly UnityEngine.Pose m_BrushRest;
        private readonly UnityEngine.Pose m_WandRest;
        private readonly UnityEngine.Pose m_WandRaised;
        private UnityEngine.Pose m_BrushPose;
        private UnityEngine.Pose m_WandPose;
        private ProceduralArm.Pose m_ReachStart;
        private Phase m_Phase;
        private float m_Elapsed;
        private float m_HoverStableSeconds;
        private bool m_ClickAccepted;
        private int m_ReleaseFrames;
        private int m_PhaseFrames;
        private bool m_Disposed;

        public string Status => m_Phase.ToString();
        public string Error { get; private set; }

        public static BaseButton FindBrushButton(string durableName = "")
        {
            return UnityEngine.Object.FindObjectsByType<BrushTypeButton>()
                .Where(button => button.isActiveAndEnabled && button.IsAvailable() && button.m_Brush != null &&
                    (string.IsNullOrEmpty(durableName) || button.m_Brush.DurableName == durableName) &&
                    button.GetCollider() != null && button.GetComponentInParent<BasePanel>() is BasePanel panel &&
                    panel.m_Fixed && panel.IsAvailable())
                .OrderBy(button => button.m_Brush.DurableName, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        public ProceduralAutomation(BaseButton button, Settings settings)
        {
            if (!Application.isPlaying || App.CurrentState != App.AppState.Standard ||
                SketchControlsScript.m_Instance.ActiveControlsType != SketchControlsScript.ControlsType.SixDofControllers)
            {
                throw new InvalidOperationException("Start the demo in Play mode with six-DoF controller controls, after loading finishes.");
            }
            if (AndroidXRHandBridge.Active || !InputManager.m_Instance.AllowVrControllers)
            {
                throw new InvalidOperationException("The demo requires controller input with application focus; hand tracking must be inactive.");
            }
            if (App.VrSdk.IsInitializingUnityXR || InputManager.Controllers == null ||
                InputManager.Controllers.Length != 2 || InputManager.Brush.IsTrigger() || InputManager.Wand.IsTrigger())
            {
                throw new InvalidOperationException("Wait for controller initialization and release both triggers before starting.");
            }
            m_Button = button;
            m_Panel = button == null ? null : button.GetComponentInParent<BasePanel>();
            if (button == null || !button.isActiveAndEnabled || !button.IsAvailable() ||
                button.GetCollider() == null || m_Panel == null || !m_Panel.m_Fixed ||
                !m_Panel.IsAvailable() || !PanelManager.m_Instance.GazePanelsAreVisible())
            {
                throw new InvalidOperationException("Choose an available button on a visible wand-attached panel.");
            }
            if (SketchSurfacePanel.m_Instance.GetCurrentToolType() != BaseTool.ToolType.FreePaintTool)
            {
                throw new InvalidOperationException("Select the paint tool before starting the demo.");
            }
            // Freeze settings for the run; Inspector edits apply to the next rehearsal.
            m_Settings = JsonUtility.FromJson<Settings>(JsonUtility.ToJson(settings));
            ValidatePositive(m_Settings.UpperArmLength, "Upper arm length", 0.15f, 0.5f);
            ValidatePositive(m_Settings.ForearmLength, "Forearm length", 0.15f, 0.5f);
            ValidatePositive(m_Settings.ReachSeconds, "Reach duration", 0.2f, 20);
            ValidatePositive(m_Settings.DrawSeconds, "Draw duration", 0.3f, 60);
            ValidatePositive(m_Settings.PauseSeconds, "Pause duration", 0.1f, 10);
            if (m_Settings.PanelPosition.z < 0.15f)
            {
                throw new ArgumentException("Panel position must be at least 0.15 metres in front of the initial view.");
            }

            m_Origin = ViewpointScript.Head.position;
            Vector3 facing = Vector3.ProjectOnPlane(ViewpointScript.Head.forward, Vector3.up);
            m_BodyRotation = Quaternion.LookRotation(facing.sqrMagnitude < 0.001f ? Vector3.forward : facing, Vector3.up);
            m_BrushArm = MakeArm(1);
            m_WandArm = MakeArm(-1);
            m_BrushRest = new UnityEngine.Pose(Point(new Vector3(0.24f, -0.48f, 0.23f)), m_BodyRotation);
            m_WandRest = new UnityEngine.Pose(Point(new Vector3(-0.24f, -0.48f, 0.23f)), InputManager.Wand.Transform.rotation);
            // Target the panel, not an arbitrary wand position. Its attachment offset can put
            // it well below the head's focus cone even when both hands can reach it.
            Vector3 panelPosition = m_Origin + ViewpointScript.Head.rotation * (m_Settings.PanelPosition * App.METERS_TO_UNITS);
            Quaternion panelRotation = Quaternion.LookRotation(panelPosition - m_Origin, Vector3.up);
            Transform wand = InputManager.Wand.Transform;
            Quaternion wandRotation = panelRotation * Quaternion.Inverse(m_Panel.transform.rotation) * wand.rotation;
            Vector3 panelOffset = Quaternion.Inverse(wand.rotation) * (m_Panel.transform.position - wand.position);
            m_WandRaised = new UnityEngine.Pose(panelPosition - wandRotation * panelOffset, wandRotation);
            Transform root = InputManager.Brush.Transform;
            Transform tip = InputManager.Brush.Behavior.PointerAttachPoint;
            m_TipOffset = Quaternion.Inverse(root.rotation) * (tip.position - root.position);
            m_TipRotation = Quaternion.Inverse(root.rotation) * tip.rotation;
            // Fail before taking input ownership if any of the authored drawing positions is unreachable.
            m_BrushArm.Solve(m_BrushRest.position, m_BrushRest.rotation);
            m_WandArm.Solve(m_WandRest.position, m_WandRest.rotation);
            m_WandArm.Solve(m_WandRaised.position, m_WandRaised.rotation);
            for (int i = 0; i <= 64; ++i) { StrokePose(i / 64f); }

            m_Original = (ControllerInfo[])InputManager.Controllers.Clone();
            m_Simulated = new ProceduralControllerInfo[m_Original.Length];
            m_OriginalPoses = new UnityEngine.Pose[m_Original.Length];
            m_OriginalVisibility = new bool[m_Original.Length];
            for (int i = 0; i < m_Original.Length; ++i)
            {
                var transform = m_Original[i].Transform;
                m_OriginalPoses[i] = new UnityEngine.Pose(transform.position, transform.rotation);
                m_OriginalVisibility[i] = transform.gameObject.activeSelf;
                m_Simulated[i] = new ProceduralControllerInfo(m_Original[i].Behavior);
            }
            // Begin from the current tracked poses when reachable; otherwise use comfortable rests.
            m_BrushPose = m_BrushArm.CanReach(m_OriginalPoses[1].position) ? m_OriginalPoses[1] : m_BrushRest;
            m_WandPose = m_WandArm.CanReach(m_OriginalPoses[0].position) ? m_OriginalPoses[0] : m_WandRest;
            m_BrushArm.Solve(m_BrushPose.position, m_BrushPose.rotation);
            m_ReachStart = m_WandArm.Solve(m_WandPose.position, m_WandPose.rotation);
            m_ShowControllers = App.Instance.ShowControllers;
            m_Tracking = App.VrSdk.VrControls.SuspendPoseTracking();
            try
            {
                m_Button.Pressed += OnButtonPressed;
                for (int i = 0; i < m_Original.Length; ++i)
                {
                    InputManager.Controllers[i] = m_Simulated[i];
                    m_Simulated[i].Transform.gameObject.SetActive(true);
                    m_Simulated[i].ShowController(true);
                }
                App.Instance.ShowControllers = true;
                ApplyPoses();
                m_Simulated[0].m_Position = m_WandPose.position;
                m_Simulated[1].m_Position = m_BrushPose.position;
            }
            catch
            {
                Dispose();
                throw;
            }
            Debug.Log($"{kLogPrefix} Started: {button.name}. Escape or Stop releases control.");
        }

        private static void ValidatePositive(float value, string name, float minimum, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
            {
                throw new ArgumentException($"{name} must be between {minimum} and {maximum}.");
            }
        }

        private Vector3 Point(Vector3 metres) => m_Origin + m_BodyRotation * (metres * App.METERS_TO_UNITS);

        private ProceduralArm MakeArm(float side) => new ProceduralArm(
            Point(new Vector3(side * 0.18f, -0.20f, -0.04f)),
            m_BodyRotation * new Vector3(side * 0.6f, -1, -0.15f),
            m_Settings.UpperArmLength * App.METERS_TO_UNITS,
            m_Settings.ForearmLength * App.METERS_TO_UNITS);

        private UnityEngine.Pose RootForTip(Vector3 position, Quaternion rotation)
        {
            Quaternion rootRotation = rotation * Quaternion.Inverse(m_TipRotation);
            return new UnityEngine.Pose(position - rootRotation * m_TipOffset, rootRotation);
        }

        private UnityEngine.Pose ButtonPose()
        {
            Vector3 target = m_Button.GetCollider().bounds.center;
            Vector3 forward = m_Panel.transform.forward;
            var pose = RootForTip(target - forward * (0.18f * App.METERS_TO_UNITS),
                Quaternion.LookRotation(forward, m_BodyRotation * Vector3.up));
            m_BrushArm.Solve(pose.position, pose.rotation);
            return pose;
        }

        private UnityEngine.Pose StrokePose(float t)
        {
            Vector3 tip = Point(ProceduralArm.Bezier(m_Settings.StrokeStart, m_Settings.StrokeControl1,
                m_Settings.StrokeControl2, m_Settings.StrokeEnd, t));
            // Deliberate gradual wrist roll; no uncorrelated controller noise.
            Quaternion rotation = m_BodyRotation * Quaternion.Euler(12, -8 + 16 * t, -12 + 24 * t);
            var pose = RootForTip(tip, rotation);
            return m_BrushArm.Evaluate(m_BrushArm.Solve(pose.position, pose.rotation));
        }

        private void Next(Phase phase)
        {
            m_Phase = phase;
            m_Elapsed = 0;
            m_PhaseFrames = 0;
            if (phase == Phase.Hover) { m_HoverStableSeconds = 0; }
            m_ReachStart = m_BrushArm.Solve(m_BrushPose.position, m_BrushPose.rotation);
            Debug.Log($"{kLogPrefix} {phase}");
        }

        private void OnButtonPressed(BaseButton button)
        {
            if (button == m_Button && (m_Phase == Phase.Press || m_Phase == Phase.Release))
            {
                m_ClickAccepted = true;
            }
        }

        public void Stop()
        {
            if (m_Phase == Phase.Finish) { return; }
            m_Phase = Phase.Finish;
            m_ReleaseFrames = 0;
            Debug.Log($"{kLogPrefix} Releasing trigger and returning controller ownership.");
        }

        // Returns false only after consumers have had whole frames to observe trigger release.
        public bool Tick(float deltaTime)
        {
            if (m_Disposed) { return false; }
            bool trigger = false;
            try
            {
                if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                    App.CurrentState != App.AppState.Standard || AndroidXRHandBridge.Active ||
                    !InputManager.m_Instance.AllowVrControllers)
                {
                    Stop();
                }
                m_Elapsed += deltaTime;
                ++m_PhaseFrames;
                float progress = Mathf.Clamp01(m_Elapsed / m_Settings.ReachSeconds);
                if (m_Phase != Phase.Finish && (m_Button == null || !m_Button.isActiveAndEnabled ||
                    !m_Panel.m_Fixed || !m_Button.IsAvailable()))
                {
                    throw new InvalidOperationException("The target panel/button became unavailable.");
                }
                switch (m_Phase)
                {
                    case Phase.RaiseWand:
                        m_WandPose = m_WandArm.Reach(m_ReachStart,
                            m_WandArm.Solve(m_WandRaised.position, m_WandRaised.rotation), progress);
                        if (progress >= 1) { Next(Phase.Aim); }
                        break;
                    case Phase.Aim:
                        var aim = ButtonPose();
                        m_BrushPose = m_BrushArm.Reach(m_ReachStart, m_BrushArm.Solve(aim.position, aim.rotation), progress);
                        if (progress >= 1) { Next(Phase.Hover); }
                        break;
                    case Phase.Hover:
                        m_BrushPose = ButtonPose();
                        // Dwell starts after the real UI acquires the button, not on arrival
                        // at the nominal pose. Panel gaze animation can move its colliders.
                        if (m_Button.IsHover() && PanelManager.m_Instance.PanelsAreStable() &&
                            SketchControlsScript.m_Instance.IsUserLookingAtPanel(m_Panel))
                        {
                            m_HoverStableSeconds += deltaTime;
                        }
                        else { m_HoverStableSeconds = 0; }
                        if (m_HoverStableSeconds >= m_Settings.PauseSeconds)
                        {
                            Next(Phase.Press);
                        }
                        else if (m_Elapsed > 5) { throw new InvalidOperationException("Button did not receive hover. Check the panel pose and visibility."); }
                        break;
                    case Phase.Press:
                        m_BrushPose = ButtonPose();
                        if (m_PhaseFrames == 1 && !m_Button.IsHover())
                        {
                            Next(Phase.Hover);
                            break;
                        }
                        trigger = true;
                        if (m_Elapsed >= 0.16f && m_PhaseFrames > 1)
                        {
                            trigger = false;
                            Next(Phase.Release);
                        }
                        break;
                    case Phase.Release:
                        if (m_Elapsed >= m_Settings.PauseSeconds)
                        {
                            if (!m_ClickAccepted) { throw new InvalidOperationException("The panel did not accept the simulated click."); }
                            if (SketchSurfacePanel.m_Instance.GetCurrentToolType() != BaseTool.ToolType.FreePaintTool)
                            {
                                throw new InvalidOperationException("The button changed tools. Choose a brush button for this drawing rehearsal.");
                            }
                            Next(Phase.ApproachStroke);
                        }
                        break;
                    case Phase.ApproachStroke:
                        var start = StrokePose(0);
                        m_BrushPose = m_BrushArm.Reach(m_ReachStart, m_BrushArm.Solve(start.position, start.rotation), progress);
                        m_WandPose = m_WandArm.Reach(m_WandArm.Solve(m_WandRaised.position, m_WandRaised.rotation),
                            m_WandArm.Solve(m_WandRest.position, m_WandRest.rotation), progress);
                        if (progress >= 1) { Next(Phase.Draw); }
                        break;
                    case Phase.Draw:
                        // Start with the exact first point for one full input frame.
                        if (m_PhaseFrames == 1) { m_Elapsed = 0; }
                        float t = Mathf.Clamp01(m_Elapsed / m_Settings.DrawSeconds);
                        m_BrushPose = StrokePose(ProceduralArm.MinimumJerk(t));
                        trigger = true;
                        if (t >= 1) { Next(Phase.Inspect); }
                        break;
                    case Phase.Inspect:
                        if (m_Elapsed >= m_Settings.PauseSeconds) { Next(Phase.Rest); }
                        break;
                    case Phase.Rest:
                        m_BrushPose = m_BrushArm.Reach(m_ReachStart,
                            m_BrushArm.Solve(m_BrushRest.position, m_BrushRest.rotation), progress);
                        if (progress >= 1) { Stop(); }
                        break;
                    case Phase.Finish:
                        if (++m_ReleaseFrames > 2) { return false; }
                        break;
                }
            }
            catch (Exception exception)
            {
                Error = exception.Message;
                Debug.LogError($"{kLogPrefix} {exception.Message}");
                Stop();
                trigger = false;
            }
            m_Simulated[0].SetTrigger(false);
            m_Simulated[1].SetTrigger(trigger);
            ApplyPoses();
            return true;
        }

        public void ApplyPoses()
        {
            if (m_Disposed) { return; }
            m_Simulated[0].Transform.SetPositionAndRotation(m_WandPose.position, m_WandPose.rotation);
            m_Simulated[1].Transform.SetPositionAndRotation(m_BrushPose.position, m_BrushPose.rotation);
        }

        public void Dispose()
        {
            if (m_Disposed) { return; }
            m_Disposed = true;
            if (m_Button != null) { m_Button.Pressed -= OnButtonPressed; }
            try
            {
                if (App.Instance != null) { App.Instance.ShowControllers = m_ShowControllers; }
                for (int i = 0; i < m_Original.Length; ++i)
                {
                    if (InputManager.Controllers != null && ReferenceEquals(InputManager.Controllers[i], m_Simulated[i]))
                    {
                        InputManager.Controllers[i] = m_Original[i];
                        if (m_Original[i].Transform != null)
                        {
                            m_Original[i].Transform.SetPositionAndRotation(m_OriginalPoses[i].position, m_OriginalPoses[i].rotation);
                            m_Original[i].Transform.gameObject.SetActive(m_OriginalVisibility[i]);
                            m_Original[i].m_Position = m_OriginalPoses[i].position;
                            m_Original[i].m_Velocity = Vector3.zero;
                            m_Original[i].m_Acceleration = Vector3.zero;
                            m_Original[i].ShowController(m_Original[i].IsTrackedObjectValid && m_ShowControllers);
                        }
                    }
                }
            }
            finally { m_Tracking.Dispose(); }
            Debug.Log($"{kLogPrefix} Controller ownership restored.");
        }
    }
}
