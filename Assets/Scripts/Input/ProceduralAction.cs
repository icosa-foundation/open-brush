// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using UnityEngine;

namespace TiltBrush
{
    /// Tool-specific intent. Controller reaches, UI clicks, framing and recording remain
    /// shared; an action supplies its contact path, subjects and completion checks.
    public abstract class ProceduralAction
    {
        public abstract string Name { get; }
        public abstract BaseTool.ToolType RequiredTool { get; }
        public abstract float DurationSeconds { get; }
        public virtual bool UsesToolAttachment => false;
        public virtual float ToolForwardOffset => 0;
        public virtual BaseButton FollowUpButton => null;
        public virtual float ContactPauseSeconds => 0;
        public abstract UnityEngine.Pose ContactPose(float t);
        public abstract void DeclareSubjects(AutomationShot shot, UnityEngine.Pose presentedPanel);
        public virtual void ValidateSelection() { }
        public virtual void ValidateResult() { }
    }

    internal sealed class DrawStrokeAction : ProceduralAction
    {
        private readonly ProceduralAutomation.Settings m_Settings;
        private readonly UnityEngine.Pose m_Body;
        private readonly Quaternion m_Yaw;
        public DrawStrokeAction(ProceduralAutomation.Settings settings, UnityEngine.Pose body)
        {
            m_Settings = settings;
            m_Body = body;
            m_Yaw = Quaternion.Euler(0, body.rotation.eulerAngles.y, 0);
        }
        public override string Name => "Draw";
        public override BaseTool.ToolType RequiredTool => BaseTool.ToolType.FreePaintTool;
        public override float DurationSeconds => m_Settings.DrawSeconds;
        private Vector3 Point(Vector3 metres) => m_Body.position + m_Yaw * (metres * App.METERS_TO_UNITS);

        public override UnityEngine.Pose ContactPose(float t) => new UnityEngine.Pose(
            Point(ProceduralArm.Bezier(m_Settings.StrokeStart, m_Settings.StrokeControl1,
                m_Settings.StrokeControl2, m_Settings.StrokeEnd, t)),
            m_Yaw * Quaternion.Euler(12, -8 + 16 * t, -12 + 24 * t));

        public override void DeclareSubjects(AutomationShot shot, UnityEngine.Pose presentedPanel)
        {
            float footprint = Mathf.Max(PointerManager.m_Instance.MainPointer.BrushSizeAbsolute,
                0.02f * App.METERS_TO_UNITS);
            shot.AddSubject("Complete stroke").AddBounds(AutomationShot.CubicBounds(Point(m_Settings.StrokeStart),
                Point(m_Settings.StrokeControl1), Point(m_Settings.StrokeControl2), Point(m_Settings.StrokeEnd), footprint));
        }
    }
}
