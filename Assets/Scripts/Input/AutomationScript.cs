// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace TiltBrush
{
    // A versioned wire format. Scenario names and tool-specific motion do not live here.
    public sealed class AutomationScript
    {
        public int Version;
        public string Name = "script";
        public float Speed = 1;
        public CaptureOptions Capture;
        public OutputOptions Output = new OutputOptions();
        public float UpperArmMeters = 0.30f;
        public float ForearmMeters = 0.30f;
        public float[] PanelPosition = {-0.06f, -0.04f, 0.34f};
        public float[] PointingOffsetMeters = {0.12f, 0.02f, -0.12f};
        public AutomationShot.FramingSettings Framing = new AutomationShot.FramingSettings();
        public Preparation[] Prepare = Array.Empty<Preparation>();
        public Step[] Steps;
        public Subject[] Subjects = Array.Empty<Subject>();

        public sealed class OutputOptions
        {
            public const int DefaultWidth = 1440;
            public const int DefaultHeight = 1080;
            public int Width = DefaultWidth;
            public int Height = DefaultHeight;

            public void Validate()
            {
                Positive(Width, "output width", 16, 8192);
                Positive(Height, "output height", 16, 8192);
                if (Width % 2 != 0 || Height % 2 != 0)
                    throw new ArgumentException("Output width and height must be even pixel dimensions.");
            }
        }

        public sealed class CaptureOptions
        {
            public string Name;
            public int Fps = 30;
        }

        public sealed class Selector
        {
            public string Tool;
            public string Brush;
            public string Subtool;
            public string Path;
            public string Command;
            public string OpensPanel;

            public bool Matches(BaseButton button)
            {
                if (new[] {Tool, Brush, Subtool, Path, Command, OpensPanel}.Count(s => !string.IsNullOrEmpty(s)) != 1)
                    throw new ArgumentException("A button selector needs exactly one of tool, brush, subtool, path, command or opensPanel.");
                if (!string.IsNullOrEmpty(Tool)) { return button is ToolButton tool && tool.Tool.ToString() == Tool; }
                if (!string.IsNullOrEmpty(Brush)) { return button is BrushTypeButton brush && brush.m_Brush?.DurableName == Brush; }
                if (!string.IsNullOrEmpty(Subtool)) { return button is SculptSubToolButton sub && sub.m_SubTool.ToString() == Subtool; }
                if (!string.IsNullOrEmpty(Command)) { return button is OptionButton option && option.m_Command.ToString() == Command; }
                if (!string.IsNullOrEmpty(OpensPanel)) { return button is PanelButton panel && panel.m_Type == PanelType(OpensPanel); }
                return HierarchyPath(button.transform) == Path;
            }

            public BaseButton Resolve(bool required = true)
            {
                var matches = UnityEngine.Object.FindObjectsByType<BaseButton>(FindObjectsInactive.Include)
                    .Where(b => b.GetComponentInParent<BasePanel>(true) is BasePanel p && PanelManager.m_Instance.IsPanelAvailable(p))
                    .Where(b => b.GetComponentInParent<PopUpWindow>(true) is PopUpWindow popup ? popup.GetParentPanel()?.PanelPopUp == popup : true)
                    .Where(Matches).ToArray();
                if (matches.Length == 0 && !required) { return null; }
                if (matches.Length != 1)
                    throw new ArgumentException($"Button selector matched {matches.Length} controls; use an exact hierarchy path to disambiguate.");
                return matches[0];
            }

            internal static string HierarchyPath(Transform t) => t.parent == null ? t.name : $"{HierarchyPath(t.parent)}/{t.name}";
        }

        internal static BasePanel.PanelType PanelType(string name)
        {
            if (!Enum.TryParse(name, out BasePanel.PanelType type) || !Enum.IsDefined(typeof(BasePanel.PanelType), type))
                throw new ArgumentException($"Unknown panel type: {name}");
            if (App.Config.IsMobileHardware && Enum.TryParse($"{name}Mobile", out BasePanel.PanelType mobile)) { return mobile; }
            return type;
        }

        public sealed class Preparation
        {
            public string Op;
            public string Id;
            public bool UseLatest;
            public string Brush = "DuctTape";
            public string PaletteBrush;
            public float BrushSizeMeters = 0.02f;
            public float[][] Points;
            public string Tool;
            public float RadiusMeters;
            public string Subtool;
            public float[] ColorRgb;
            public string Category;
            public string ScriptName;
            public bool SelectPrevious;
        }

        public sealed class Step
        {
            public string Op;
            public string Label;
            public float Seconds = 1.2f;
            public string Hand = "brush";
            public string Attachment = "tip";
            public Step Parallel;
            public string Anchor;
            public float AnchorT = 0.5f;
            public float[][] Points;
            public string Curve = "polyline";
            public float[] Rotation;
            public string RotationConstraint = "full";
            public float[] EndRotation;
            public float PaddingMeters = 0.02f;
            public Selector Target;
            public bool? Trigger;
            public float[] Thumbstick;
            public string[] Buttons;
            public string Brush;
            public string Category;
            public string ScriptName;
            public string Symmetry;
            public string Slider;
            public float Value;
            public bool? StraightEdge;
            public bool? EndpointSnap;
            public float[][] LastStrokeEndpoints;
            public float EndpointToleranceMeters = 0.003f;
            public string Tool;
            public string Subtool;
            public string Stroke;
            public float MinimumMovementMeters;
            public bool FixedEndpoints;
            public int? StrokeCountDelta;
            public int? VisibleStrokeCountDelta;
            public int MinimumChangedColorPoints;
            public int? MaximumChangedColorPoints;
            public bool UnchangedGeometry;
            public float[] ExpectedColorRgb;
            public float ColorPointT = 0.5f;
            public float ColorTolerance = 0.05f;
            public string Panel;
            public bool? PanelOpen;
        }

        public sealed class Subject
        {
            public string Name;
            public string Anchor;
            public float AnchorT = 0.5f;
            public float[] Min;
            public float[] Max;
        }

        public static AutomationScript Parse(string json)
        {
            var script = JsonConvert.DeserializeObject<AutomationScript>(json, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                MissingMemberHandling = MissingMemberHandling.Error,
                TypeNameHandling = TypeNameHandling.None
            });
            if (script == null || script.Version != 1 || script.Steps == null || script.Steps.Length == 0)
                throw new ArgumentException("An automation script needs version 1 and a nonempty steps array.");
            if (script.Prepare == null || script.Subjects == null || script.Framing == null || script.Output == null || script.Steps.Any(s => s == null))
                throw new ArgumentException("Script arrays, framing and output settings must not be null.");
            Positive(script.Speed, "speed", 0.25f, 2);
            script.Output.Validate();
            return script;
        }

        internal static Vector3 Vector(float[] values, string field)
        {
            if (values == null || values.Length != 3 || values.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new ArgumentException($"{field} must contain three finite numbers.");
            return new Vector3(values[0], values[1], values[2]);
        }

        internal static void Positive(float value, string field, float minimum = 0.001f, float maximum = 60)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < minimum || value > maximum)
                throw new ArgumentException($"{field} must be between {minimum} and {maximum}.");
        }

        internal static Color Rgb(float[] values)
        {
            Vector3 rgb = Vector(values, "colorRgb");
            if (values.Any(v => v < 0 || v > 1)) { throw new ArgumentException("RGB channels must be between 0 and 1."); }
            return new Color(rgb.x, rgb.y, rgb.z, 1);
        }

        internal Dictionary<string, Stroke> PrepareScene(UnityEngine.Pose body)
        {
            var strokes = new Dictionary<string, Stroke>(StringComparer.Ordinal);
            Quaternion yaw = Quaternion.Euler(0, body.rotation.eulerAngles.y, 0);
            PanelManager.m_Instance.ResetPanelsForAutomation();
            SketchControlsScript.m_Instance.RequestPanelsVisibility(true);
            foreach (Preparation prep in Prepare)
            {
                if (prep == null) { throw new ArgumentException("Preparation must not be null."); }
                if (prep.Op == "stroke")
                {
                    if (string.IsNullOrWhiteSpace(prep.Id) || strokes.ContainsKey(prep.Id))
                        throw new ArgumentException("Each prepared stroke needs a unique nonempty id.");
                    Stroke stroke;
                    if (prep.UseLatest)
                    {
                        stroke = SketchMemoryScript.m_Instance.MostRecentStroke;
                        if (stroke == null || !stroke.IsGeometryEnabled)
                            throw new InvalidOperationException("No visible latest stroke is available.");
                    }
                    else
                    {
                        Positive(prep.BrushSizeMeters, "brushSizeMeters", 0.001f, 1);
                        if (prep.Points == null || prep.Points.Length < 2)
                            throw new ArgumentException("A prepared stroke needs at least two points.");
                        BrushDescriptor brush = BrushCatalog.m_Instance.AllBrushes.FirstOrDefault(b => b.DurableName == prep.Brush);
                        if (brush == null) { throw new ArgumentException($"Unknown brush: {prep.Brush}"); }
                        CanvasScript canvas = App.ActiveCanvas;
                        var path = prep.Points.Select(p => new TrTransform
                        {
                            translation = canvas.Pose.inverse * (body.position + yaw * (Vector(p, "stroke point") * App.METERS_TO_UNITS)),
                            rotation = Quaternion.Inverse(canvas.Pose.rotation) * yaw,
                            scale = 1
                        }).ToList();
                        stroke = DrawStrokes.DrawNestedTrList(new[] {path}, TrTransform.identity, brush: brush,
                            brushSize: prep.BrushSizeMeters * App.METERS_TO_UNITS, canvas: canvas,
                            colors: prep.ColorRgb == null ? null : new List<Color> {Rgb(prep.ColorRgb)}).Single();
                    }
                    strokes.Add(prep.Id, stroke);
                }
                else if (prep.Op == "drawing")
                {
                    PointerManager.m_Instance.StraightEdgeModeEnabled = false;
                    PointerManager.m_Instance.StraightEdgeGuide.EndpointSnappingEnabled = false;
                    PointerManager.m_Instance.StraightEdgeGuide.ClearEndpointHistory();
                    PointerManager.m_Instance.CurrentSymmetryMode = PointerManager.SymmetryMode.None;
                    SelectionManager.m_Instance.SetSnappingGridSize(0);
                    SelectionManager.m_Instance.SetSnappingAngle(0);
                    foreach (var button in UnityEngine.Object.FindObjectsByType<SnapSettingsPanelToggleButton>(FindObjectsInactive.Include))
                    {
                        if (button.m_ButtonType == SnapSettingsPanelToggleType.StraightEdgeEndpointSnap)
                            button.ToggleState = false;
                    }
                }
                else if (prep.Op == "script")
                {
                    LuaManager.Instance.Init(immediate: true);
                    if (!Enum.TryParse(prep.Category, out LuaApiCategory category) || !Enum.IsDefined(typeof(LuaApiCategory), category))
                        throw new ArgumentException($"Unknown script category: {prep.Category}");
                    var names = LuaManager.Instance.GetScriptNames(category);
                    int index = names.IndexOf(prep.ScriptName);
                    if (index < 0) { throw new ArgumentException($"Unknown {prep.Category}: {prep.ScriptName}"); }
                    if (prep.SelectPrevious) { index = (index + names.Count - 1) % names.Count; }
                    LuaManager.Instance.SetActiveScriptByName(category, names[index]);
                    var panel = (ScriptsPanel)PanelManager.m_Instance.GetPanelByType(PanelType("Scripts"));
                    panel.InitScriptUiNav();
                }
                else if (prep.Op == "brush")
                {
                    Positive(prep.BrushSizeMeters, "brushSizeMeters", 0.001f, 1);
                    BrushDescriptor brush = BrushCatalog.m_Instance.AllBrushes.FirstOrDefault(b => b.DurableName == prep.Brush);
                    if (brush == null) { throw new ArgumentException($"Unknown brush: {prep.Brush}"); }
                    BrushController.m_Instance.SetActiveBrush(brush);
                    PointerManager.m_Instance.MainPointer.BrushSizeAbsolute = prep.BrushSizeMeters * App.METERS_TO_UNITS;
                    if (!string.IsNullOrEmpty(prep.PaletteBrush))
                    {
                        BrushDescriptor paletteBrush = BrushCatalog.m_Instance.AllBrushes.FirstOrDefault(b => b.DurableName == prep.PaletteBrush);
                        var panel = PanelManager.m_Instance.GetAllPanels()
                            .First(p => p.AvailableInCurrentMode && p.m_Panel.Type == PanelType("Brush")).m_Panel;
                        var grid = panel.GetComponentInChildren<BrushGrid>(true);
                        if (paletteBrush == null || grid == null || !grid.ShowBrushPage(paletteBrush))
                            throw new ArgumentException($"Brush is not in the current palette: {prep.PaletteBrush}");
                    }
                }
                else if (prep.Op == "color")
                {
                    App.BrushColor.CurrentColor = Rgb(prep.ColorRgb);
                }
                else if (prep.Op == "tool")
                {
                    if (float.IsNaN(prep.RadiusMeters) || float.IsInfinity(prep.RadiusMeters) || prep.RadiusMeters < 0)
                        throw new ArgumentException("radiusMeters must be finite and nonnegative.");
                    if (!Enum.TryParse(prep.Tool, out BaseTool.ToolType type) || !Enum.IsDefined(typeof(BaseTool.ToolType), type))
                        throw new ArgumentException($"Unknown preparation tool: {prep.Tool}");
                    BaseTool tool = SketchSurfacePanel.m_Instance.GetComponentsInChildren<BaseTool>(true).FirstOrDefault(t => t.m_Type == type);
                    if (tool == null) { throw new ArgumentException($"Tool not available: {prep.Tool}"); }
                    if (prep.RadiusMeters > 0)
                    {
                        if (!(tool is StrokeModificationTool intersection))
                            throw new ArgumentException("radiusMeters requires a stroke modification tool.");
                        float size = prep.RadiusMeters * App.METERS_TO_UNITS;
                        Positive(size, "tool radius", intersection.m_SizeRange.x, intersection.m_SizeRange.y);
                        intersection.UpdateSize(Mathf.InverseLerp(intersection.m_SizeRange.x, intersection.m_SizeRange.y, size) - intersection.GetSize01());
                    }
                    if (!string.IsNullOrEmpty(prep.Subtool))
                    {
                        if (!(tool is ReshapeTool reshape)) { throw new ArgumentException("This tool has no sculpt subtools."); }
                        reshape.SetSubTool(tool.GetComponentsInChildren<BaseSculptSubTool>(true)
                            .Single(s => s.SubToolIdentifier.ToString() == prep.Subtool));
                    }
                }
                else { throw new ArgumentException($"Unknown preparation operation: {prep.Op}"); }
            }
            SketchSurfacePanel.m_Instance.EnableSpecificTool(BaseTool.ToolType.FreePaintTool);
            return strokes;
        }
    }
}
