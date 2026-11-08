// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class GrabStrokeAction : ProceduralAction
    {
        private readonly Stroke m_Stroke;
        private readonly ReshapeTool m_Tool;
        private readonly SculptSubToolButton m_GrabButton;
        private readonly PointerManager.ControlPoint[] m_Original;
        private readonly Vector3 m_Start;
        private readonly Vector3 m_Offset;
        private readonly Quaternion m_Orientation;
        private readonly ProceduralAutomation.Settings m_Settings;
        private readonly Bounds m_StrokeBounds;

        public GrabStrokeAction(Stroke stroke, ReshapeTool tool, SculptSubToolButton grabButton,
            ProceduralAutomation.Settings settings, UnityEngine.Pose body)
        {
            if (stroke?.Canvas == null || stroke.m_ControlPoints == null || stroke.m_ControlPoints.Length < 3)
            {
                throw new InvalidOperationException("Prepare a starter stroke or choose an existing stroke before recording Grab.");
            }
            m_Stroke = stroke;
            m_Tool = tool;
            m_GrabButton = grabButton;
            m_Settings = settings;
            m_Original = stroke.m_ControlPoints.ToArray();
            Quaternion yaw = Quaternion.Euler(0, body.rotation.eulerAngles.y, 0);
            m_Orientation = yaw * Quaternion.Euler(12, 0, 0);
            m_Offset = yaw * (settings.GrabOffsetMeters * App.METERS_TO_UNITS);
            m_Start = stroke.Canvas.Pose * m_Original[m_Original.Length / 2].m_Pos;
            var bounds = new Bounds(stroke.Canvas.Pose * m_Original[0].m_Pos, Vector3.zero);
            foreach (var point in m_Original) { bounds.Encapsulate(stroke.Canvas.Pose * point.m_Pos); }
            m_StrokeBounds = bounds;
            if (float.IsNaN(m_Offset.sqrMagnitude) || float.IsInfinity(m_Offset.sqrMagnitude) ||
                m_Offset.magnitude < 0.005f * App.METERS_TO_UNITS)
            {
                throw new InvalidOperationException("The Grab action needs a finite movement of at least 5 millimetres.");
            }
        }

        public override string Name => "Grab";
        public override BaseTool.ToolType RequiredTool => BaseTool.ToolType.ReshapeTool;
        public override float DurationSeconds => m_Settings.ReshapeSeconds;
        public override bool UsesToolAttachment => true;
        public override float ToolForwardOffset => m_Tool.PointerForwardOffset;
        public override BaseButton FollowUpButton => m_GrabButton;
        public override float ContactPauseSeconds => m_Settings.PauseSeconds;
        public override UnityEngine.Pose ContactPose(float t) => new UnityEngine.Pose(m_Start + m_Offset * t, m_Orientation);

        public override void ValidateSelection()
        {
            if (SculptSubToolManager.m_Instance.ActiveSubTool != SculptSubToolManager.SubTool.Grab)
            {
                throw new InvalidOperationException("The Grab subtool was not selected through the tray.");
            }
        }

        public override void DeclareSubjects(AutomationShot shot, UnityEngine.Pose presentedPanel)
        {
            Bounds completeStroke = m_StrokeBounds;
            completeStroke.Encapsulate(new Bounds(m_StrokeBounds.center + m_Offset, m_StrokeBounds.size));
            completeStroke.Expand(2 * m_Stroke.m_BrushSize);
            shot.AddSubject("Stroke before and after Grab").AddBounds(completeStroke);
            float radius = m_Tool.GetSize();
            var sphere = new Bounds(m_Start, Vector3.one * (2 * radius));
            sphere.Encapsulate(new Bounds(m_Start + m_Offset, Vector3.one * (2 * radius)));
            shot.AddSubject("Grab tool influence").AddBounds(sphere);
            var tray = m_GrabButton.GetComponentInParent<BaseTray>();
            var panel = m_GrabButton.GetComponentInParent<BasePanel>();
            var traySubject = shot.AddSubject("Grab subtool selection");
            Quaternion inverse = Quaternion.Inverse(panel.transform.rotation);
            foreach (Vector3 point in tray.ExpandedRendererPoints())
            {
                traySubject.AddPoint(presentedPanel.position + presentedPanel.rotation * (inverse * (point - panel.transform.position)));
            }
        }

        public override void ValidateResult()
        {
            if (m_Stroke.m_ControlPoints.Length != m_Original.Length)
            {
                throw new InvalidOperationException("Grab unexpectedly changed the stroke's control-point count.");
            }
            float moved = 0;
            for (int i = 0; i < m_Original.Length; ++i)
            {
                Vector3 delta = m_Stroke.Canvas.Pose * m_Stroke.m_ControlPoints[i].m_Pos -
                    m_Stroke.Canvas.Pose * m_Original[i].m_Pos;
                moved = Mathf.Max(moved, Vector3.Dot(delta, m_Offset.normalized));
            }
            if (moved < m_Offset.magnitude * 0.25f)
            {
                throw new InvalidOperationException("Grab did not deform the existing stroke. Check tool contact and radius.");
            }
            Debug.Log($"[ProceduralReshape] Grab moved the existing stroke by {moved * App.UNITS_TO_METERS:F3}m.");
        }
    }
}
