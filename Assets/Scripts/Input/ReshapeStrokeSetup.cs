// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy at http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Stages the starting canvas before recording. Creation is a normal undoable stroke;
    /// the recorded interaction changes it only through Reshape's actual input path.
    public sealed class ReshapeStrokeSetup : MonoBehaviour
    {
        public Stroke PreparedStroke { get; private set; }
        public ToolButton ToolButton { get; private set; }
        public SculptSubToolButton GrabButton { get; private set; }
        private ReshapeTool m_Tool;

        public void Prepare(ProceduralAutomation.Settings settings, string brushName = "DuctTape", bool useLatestStroke = false)
        {
            if (App.VrSdk.VirtualSimulation == null || App.CurrentState != App.AppState.Standard ||
                InputManager.m_Instance.ProceduralAutomationStatus != "Stopped" ||
                App.Instance.GetComponent<ProceduralCapture>()?.IsRecording == true || PointerManager.MainPointerIsPainting())
            {
                throw new InvalidOperationException("Prepare the reshape scene in an idle Virtual Input session.");
            }
            PanelManager panels = PanelManager.m_Instance;
            panels.SetPanelAvailabilityMode(PanelManager.PanelAvailabilityMode.Advanced);
            SketchControlsScript.m_Instance.RequestPanelsVisibility(true);
            ToolButton = UnityEngine.Object.FindObjectsByType<ToolButton>(FindObjectsInactive.Include)
                .FirstOrDefault(button => button.Tool == BaseTool.ToolType.ReshapeTool &&
                    button.GetComponentInParent<BasePanel>() is BasePanel panel && panel.m_Fixed &&
                    panel.AdvancedModePanel && panel.IsAvailable());
            if (ToolButton == null) { throw new InvalidOperationException("No wand-attached Reshape tool button is available."); }
            BasePanel toolPanel = ToolButton.GetComponentInParent<BasePanel>();
            GrabButton = toolPanel.GetComponentsInChildren<SculptSubToolButton>(true)
                .FirstOrDefault(button => button.m_SubTool == SculptSubToolManager.SubTool.Grab);
            m_Tool = UnityEngine.Object.FindObjectsByType<ReshapeTool>(FindObjectsInactive.Include).FirstOrDefault();
            if (GrabButton == null || m_Tool == null) { throw new InvalidOperationException("Reshape's Grab controls are missing."); }
            float desiredRadius = settings.GrabRadiusMeters * App.METERS_TO_UNITS;
            if (float.IsNaN(desiredRadius) || desiredRadius < m_Tool.m_SizeRange.x || desiredRadius > m_Tool.m_SizeRange.y)
            {
                throw new ArgumentException($"Grab radius must be between {m_Tool.m_SizeRange.x * App.UNITS_TO_METERS} and {m_Tool.m_SizeRange.y * App.UNITS_TO_METERS} metres.");
            }
            m_Tool.UpdateSize(Mathf.InverseLerp(m_Tool.m_SizeRange.x, m_Tool.m_SizeRange.y, desiredRadius) - m_Tool.GetSize01());
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FreePaintTool);
            // Stage a consistent initial mode before recording. The inactive tool's
            // manager may not have awakened yet, so use the tool's explicit setter.
            BaseSculptSubTool push = m_Tool.GetComponentsInChildren<BaseSculptSubTool>(true)
                .First(item => item.SubToolIdentifier == SculptSubToolManager.SubTool.Push);
            m_Tool.SetSubTool(push);
            if (useLatestStroke)
            {
                PreparedStroke = SketchMemoryScript.m_Instance.MostRecentStroke;
                if (PreparedStroke == null) { throw new InvalidOperationException("There is no existing stroke to reshape."); }
            }
            else if (PreparedStroke == null || PreparedStroke.Canvas == null || !PreparedStroke.IsGeometryEnabled)
            {
                PreparedStroke = CreateStarter(settings, brushName);
            }
            Debug.Log("[ProceduralReshape] Ready: a stroke is already drawn; recording will show Reshape, Grab, and the bend.");
        }

        private static Stroke CreateStarter(ProceduralAutomation.Settings settings, string brushName)
        {
            BrushDescriptor brush = BrushCatalog.m_Instance.AllBrushes.FirstOrDefault(item => item.DurableName == brushName);
            if (brush == null) { throw new InvalidOperationException($"Starter brush '{brushName}' is not available."); }
            UnityEngine.Pose body = App.VrSdk.VirtualSimulation.BodyPose;
            Quaternion yaw = Quaternion.Euler(0, body.rotation.eulerAngles.y, 0);
            CanvasScript canvas = App.ActiveCanvas;
            var path = new List<TrTransform>();
            for (int i = 0; i <= 96; ++i)
            {
                Vector3 world = body.position + yaw * (ProceduralArm.Bezier(settings.StrokeStart,
                    settings.StrokeControl1, settings.StrokeControl2, settings.StrokeEnd, i / 96f) * App.METERS_TO_UNITS);
                path.Add(new TrTransform
                {
                    translation = canvas.Pose.inverse * world,
                    rotation = Quaternion.Inverse(canvas.Pose.rotation) * yaw,
                    scale = 1
                });
            }
            return DrawStrokes.DrawNestedTrList(new[] {path}, TrTransform.identity,
                brush: brush, brushSize: 0.02f * App.METERS_TO_UNITS, canvas: canvas).Single();
        }

        public ProceduralAction CreateAction(ProceduralAutomation.Settings settings, UnityEngine.Pose body) =>
            new GrabStrokeAction(PreparedStroke, m_Tool, GrabButton, settings, body);
    }
}
