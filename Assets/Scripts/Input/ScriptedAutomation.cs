// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Executes externally authored primitives on the input clock, before native consumers.
    public sealed class ScriptedAutomation : IProceduralAutomation
    {
        private sealed class Instruction
        {
            public AutomationScript.Step Source;
            public int Hand;
            public BaseButton Button;
            public BasePanel Panel;
            public UnityEngine.Pose[] Path;
            public UnityEngine.Pose Start;
            public int Turns;
            public bool Deferred;
            public bool Floating;
        }

        private readonly List<Instruction> m_Instructions = new List<Instruction>();
        private readonly Dictionary<string, Stroke> m_Strokes;
        private readonly Dictionary<string, Vector3[]> m_OriginalPoints;
        private readonly Dictionary<string, Color32[]> m_OriginalColors;
        private readonly ControllerInfo[] m_Original;
        private readonly ProceduralControllerInfo[] m_Controllers;
        private readonly UnityEngine.Pose[] m_OriginalPoses;
        private readonly UnityEngine.Pose[] m_Poses;
        private readonly bool[] m_Visible;
        private readonly ProceduralArm[] m_Arms;
        private readonly List<Vector3>[] m_ControllerBounds;
        private readonly bool[] m_Triggers = new bool[2];
        private readonly Vector2[] m_Sticks = new Vector2[2];
        private readonly IDisposable m_Tracking;
        private readonly bool m_ShowControllers;
        private readonly Quaternion m_Yaw;
        private readonly UnityEngine.Pose m_Body;
        private readonly int m_InitialStrokeCount;
        private readonly AutomationScript m_Script;
        private int m_Index;
        private int m_Stage;
        private int m_Frames;
        private int m_ReleaseFrames;
        private float m_Elapsed;
        private float m_TotalElapsed;
        private bool m_Clicked;
        private bool m_Stopping;
        private bool m_Disposed;
        private ProceduralArm.Pose m_ReachStart;

        public string Error { get; private set; }
        public string Status => m_Stopping ? "Release" : $"{m_Index + 1}/{m_Instructions.Count}: {m_Instructions[m_Index].Source.Label ?? m_Instructions[m_Index].Source.Op}";

        public ScriptedAutomation(AutomationScript script)
        {
            if (!Application.isPlaying || App.Config.m_SdkMode != SdkMode.VirtualInput ||
                App.CurrentState != App.AppState.Standard || AndroidXRHandBridge.Active ||
                !InputManager.m_Instance.AllowVrControllers || InputManager.Controllers.Length != 2 ||
                InputManager.Controllers.Any(c => c.IsTrigger()) || PointerManager.MainPointerIsPainting() ||
                App.Instance.GetComponent<ProceduralCapture>()?.IsRecording == true)
                throw new InvalidOperationException("Scripts require an idle Virtual Input session with no active capture.");
            m_Script = script;
            AutomationScript.Positive(script.UpperArmMeters, "upperArmMeters", 0.15f, 0.5f);
            AutomationScript.Positive(script.ForearmMeters, "forearmMeters", 0.15f, 0.5f);
            m_Body = App.VrSdk.VirtualSimulation.BodyPose;
            m_Yaw = Quaternion.Euler(0, m_Body.rotation.eulerAngles.y, 0);
            m_Original = (ControllerInfo[])InputManager.Controllers.Clone();
            m_OriginalPoses = m_Original.Select(c => new UnityEngine.Pose(c.Transform.position, c.Transform.rotation)).ToArray();
            m_Visible = m_Original.Select(c => c.Transform.gameObject.activeSelf).ToArray();
            m_Poses = (UnityEngine.Pose[])m_OriginalPoses.Clone();
            m_Arms = new[] {MakeArm(-1), MakeArm(1)};
            m_ControllerBounds = m_Original.Select(ControllerBounds).ToArray();
            for (int i = 0; i < 2; ++i)
            {
                if (!m_Arms[i].CanReach(m_Poses[i].position))
                    m_Poses[i] = new UnityEngine.Pose(Point(new Vector3(i == 0 ? -0.24f : 0.24f, -0.48f, 0.23f)), m_OriginalPoses[i].rotation);
            }
            // Validate syntax and selectors before creating any fixture strokes.
            foreach (var step in script.Steps)
            {
                if (!new[] {"present", "focus", "click", "move", "input", "wait", "rest", "assert"}.Contains(step.Op))
                    throw new ArgumentException($"Unknown script operation: {step.Op}");
                AutomationScript.Positive(step.Seconds, "step seconds", 0.01f, 120);
                if (step.Op == "present" || step.Op == "focus" || step.Op == "click")
                {
                    if (step.Target == null) { throw new ArgumentException("present and click require a target."); }
                }
                if (step.Hand != "wand" && step.Hand != "brush") { throw new ArgumentException("hand must be wand or brush."); }
            }
            m_Strokes = script.PrepareScene(m_Body);
            m_OriginalPoints = m_Strokes.ToDictionary(p => p.Key, p => p.Value.m_ControlPoints.Select(c => c.m_Pos).ToArray());
            m_OriginalColors = m_Strokes.ToDictionary(p => p.Key,
                p => Enumerable.Range(0, p.Value.m_ControlPoints.Length).Select(p.Value.GetColor).ToArray());
            m_InitialStrokeCount = SketchMemoryScript.m_Instance.StrokeCount;
            var shot = Compile();
            var framing = script.Framing.Enabled ? (AutomationShot.FramingResult?)shot.Frame(App.VrSdk.GetVrCamera(), m_Body, script.Framing) : null;
            m_Controllers = m_Original.Select(c => new ProceduralControllerInfo(c.Behavior)).ToArray();
            m_ShowControllers = App.Instance.ShowControllers;
            m_Tracking = App.VrSdk.VrControls.SuspendPoseTracking();
            try
            {
                for (int i = 0; i < 2; ++i)
                {
                    InputManager.Controllers[i] = m_Controllers[i];
                    m_Controllers[i].Transform.gameObject.SetActive(true);
                    m_Controllers[i].ShowController(true);
                    m_Controllers[i].m_Position = m_Poses[i].position;
                }
                App.Instance.ShowControllers = true;
                ApplyPoses();
                if (framing.HasValue) { App.VrSdk.VirtualSimulation.SetFramingPose(framing.Value.CameraPose); }
                if (script.Capture != null)
                {
                    var capture = App.Instance.GetComponent<ProceduralCapture>() ?? App.Instance.gameObject.AddComponent<ProceduralCapture>();
                    capture.Begin(script.Capture.Name, script.Capture.Fps);
                }
                BeginStep();
            }
            catch { Dispose(); throw; }
        }

        private Vector3 Point(Vector3 metres) => m_Body.position + m_Yaw * (metres * App.METERS_TO_UNITS);
        private ProceduralArm MakeArm(float side) => new ProceduralArm(Point(new Vector3(side * 0.18f, -0.20f, -0.04f)),
            m_Yaw * new Vector3(side * 0.6f, -1, -0.15f), m_Script.UpperArmMeters * App.METERS_TO_UNITS,
            m_Script.ForearmMeters * App.METERS_TO_UNITS);

        private Vector3 Anchor(string id, float t)
        {
            if (string.IsNullOrEmpty(id)) { return m_Body.position; }
            if (!m_Strokes.TryGetValue(id, out Stroke stroke)) { throw new ArgumentException($"Unknown stroke anchor: {id}"); }
            if (float.IsNaN(t) || t < 0 || t > 1) { throw new ArgumentException("anchorT must be between 0 and 1."); }
            var points = m_OriginalPoints[id];
            float index = t * (points.Length - 1);
            int low = Mathf.FloorToInt(index);
            return stroke.Canvas.Pose * Vector3.Lerp(points[low], points[Mathf.Min(low + 1, points.Length - 1)], index - low);
        }

        private UnityEngine.Pose PanelPose(BasePanel panel, UnityEngine.Pose wand, float ringAngle)
        {
            Transform axis = m_Original[0].Geometry.MainAxisAttachPoint;
            Quaternion delta = wand.rotation * Quaternion.Inverse(m_OriginalPoses[0].rotation);
            Quaternion spin = Quaternion.AngleAxis(PanelManager.m_Instance.IsAdminPanel(panel.Type) ? 0 : ringAngle, axis.forward);
            Vector3 local = Quaternion.Inverse(m_OriginalPoses[0].rotation) *
                (axis.position + spin * (panel.transform.position - axis.position) - m_OriginalPoses[0].position);
            return new UnityEngine.Pose(wand.position + wand.rotation * local, delta * spin * panel.transform.rotation);
        }

        private UnityEngine.Pose PlanPresentation(BasePanel panel, float ring, out int turns)
        {
            Transform axis = m_Original[0].Geometry.MainAxisAttachPoint;
            Vector3 position = Point(AutomationScript.Vector(m_Script.PanelPosition, "panelPosition"));
            Quaternion facing = Quaternion.LookRotation(position - m_Body.position, Vector3.up);
            Quaternion axisInHand = Quaternion.Inverse(m_OriginalPoses[0].rotation) * axis.rotation;
            Quaternion neutral = facing * Quaternion.Inverse(Quaternion.Euler(90, 0, 0)) * Quaternion.Inverse(axisInHand);
            var predicted = PanelPose(panel, new UnityEngine.Pose(Vector3.zero, neutral), ring);
            Vector3 turnAxis = neutral * Quaternion.Inverse(m_OriginalPoses[0].rotation) * axis.forward;
            float turn = Vector3.SignedAngle(predicted.rotation * Vector3.forward, facing * Vector3.forward, turnAxis);
            turns = Mathf.RoundToInt(turn / PanelManager.WandPanelSnapAngle);
            Quaternion rotation = Quaternion.AngleAxis(turn - turns * PanelManager.WandPanelSnapAngle, turnAxis) * neutral;
            predicted = PanelPose(panel, new UnityEngine.Pose(Vector3.zero, rotation), ring + turns * PanelManager.WandPanelSnapAngle);
            return new UnityEngine.Pose(position - predicted.position, rotation);
        }

        private UnityEngine.Pose RootForContact(int hand, UnityEngine.Pose contact, AutomationScript.Step step)
        {
            if (step.Attachment == "root") { return contact; }
            Transform root = m_Original[hand].Transform;
            Transform attachment = step.Attachment == "tip" ? m_Original[hand].Behavior.PointerAttachPoint :
                step.Attachment == "tool" ? m_Original[hand].Geometry.ToolAttachPoint : null;
            if (attachment == null) { throw new ArgumentException("attachment must be root, tip or tool, available on the selected hand."); }
            float forward = 0;
            if (step.Attachment == "tool" && !string.IsNullOrEmpty(step.Tool))
            {
                var tool = UnityEngine.Object.FindObjectsByType<BaseStrokeIntersectionTool>(FindObjectsInactive.Include)
                    .SingleOrDefault(t => t.m_Type.ToString() == step.Tool);
                if (tool != null) { forward = tool.PointerForwardOffset; }
            }
            Quaternion localRotation = Quaternion.Inverse(root.rotation) * attachment.rotation;
            Vector3 localPosition = Quaternion.Inverse(root.rotation) * (attachment.position + attachment.forward * forward - root.position);
            Quaternion rotation = contact.rotation * Quaternion.Inverse(localRotation);
            return new UnityEngine.Pose(contact.position - rotation * localPosition, rotation);
        }

        private UnityEngine.Pose Aim(BaseButton button, UnityEngine.Pose panelPose, bool predict)
        {
            BasePanel panel = button.GetComponentInParent<BasePanel>(true);
            Collider collider = button.GetCollider() ?? button.GetComponent<Collider>();
            if (collider == null) { throw new ArgumentException("The selected button has no collider."); }
            Vector3 target = predict && collider is BoxCollider box ? panelPose.position +
                panelPose.rotation * panel.FullScalePoint(box.transform, box.center, true) : collider.bounds.center;
            Quaternion rotation = !predict && button.GetComponentInParent<PopUpWindow>() is PopUpWindow popup ? popup.transform.rotation : panelPose.rotation;
            return AimAt(target, rotation);
        }

        private UnityEngine.Pose AimAt(Vector3 target, Quaternion rotation)
        {
            Vector3 offset = AutomationScript.Vector(m_Script.PointingOffsetMeters, "pointingOffsetMeters");
            if (offset.z > -0.03f)
                throw new ArgumentException("pointingOffsetMeters must place the tip at least 3 cm in front of the panel.");
            Vector3 tip = target + rotation * (offset * App.METERS_TO_UNITS);
            return RootForContact(1, new UnityEngine.Pose(tip,
                Quaternion.LookRotation(target - tip, m_Yaw * Vector3.up)), new AutomationScript.Step());
        }

        private AutomationShot Compile()
        {
            var shot = new AutomationShot(m_Script.Name);
            var predicted = (UnityEngine.Pose[])m_Poses.Clone();
            float ring = 0;
            var floating = new Dictionary<BasePanel, UnityEngine.Pose>();
            GameObject popupPrefab = null;
            BasePanel popupOwner = null;
            UnityEngine.Pose popupPose = default;
            foreach (var subject in m_Script.Subjects)
            {
                Vector3 origin = Anchor(subject.Anchor, subject.AnchorT);
                Vector3 min = AutomationScript.Vector(subject.Min, "subject min"), max = AutomationScript.Vector(subject.Max, "subject max");
                if (min.x > max.x || min.y > max.y || min.z > max.z) { throw new ArgumentException("Subject min must not exceed max."); }
                var bounds = new Bounds((min + max) / 2 * App.METERS_TO_UNITS, (max - min) * App.METERS_TO_UNITS);
                var framed = shot.AddSubject(subject.Name ?? "Declared subject");
                for (int i = 0; i < 8; ++i) { framed.AddPoint(origin + m_Yaw * AutomationShot.Corner(bounds, i)); }
            }
            foreach (var stroke in m_Strokes.Values)
            {
                var bounds = new Bounds(stroke.Canvas.Pose * stroke.m_ControlPoints[0].m_Pos, Vector3.zero);
                foreach (var point in stroke.m_ControlPoints) { bounds.Encapsulate(stroke.Canvas.Pose * point.m_Pos); }
                bounds.Expand(2 * stroke.m_BrushSize);
                shot.AddSubject("Prepared stroke").AddBounds(bounds);
            }
            foreach (var step in m_Script.Steps)
            {
                var item = new Instruction {Source = step, Hand = step.Hand == "wand" ? 0 : 1};
                item.Start = predicted[item.Hand];
                var subject = shot.AddSubject(step.Label ?? step.Op);
                if (step.Op == "present" || step.Op == "focus" || step.Op == "click")
                {
                    item.Button = step.Target.Resolve(false);
                    if (item.Button == null && popupPrefab != null && step.Op == "click")
                    {
                        item.Button = popupPrefab.GetComponentsInChildren<BaseButton>(true).SingleOrDefault(step.Target.Matches);
                        item.Panel = popupOwner;
                        item.Deferred = item.Button != null;
                    }
                    else { item.Panel = item.Button?.GetComponentInParent<BasePanel>(true); }
                    if (item.Button == null || item.Panel == null)
                        throw new ArgumentException($"Step {m_Instructions.Count + 1} ({step.Op}): target '{step.Target.Tool ?? step.Target.Brush ?? step.Target.Subtool ?? step.Target.Command ?? step.Target.OpensPanel ?? step.Target.Path}' does not exist in the predicted UI state.");
                    if (step.Op == "present" && item.Panel.m_Fixed)
                    {
                        item.Hand = 0;
                        item.Start = predicted[0];
                        var end = PlanPresentation(item.Panel, ring, out item.Turns);
                        item.Path = new[] {end};
                        var localBounds = item.Panel.FullScaleRendererPoints().ToArray();
                        for (int i = 0; i <= 32; ++i)
                        {
                            float t = i / 32f;
                            var pose = PanelPose(item.Panel, end, ring + t * item.Turns * PanelManager.WandPanelSnapAngle);
                            subject.AddPose(pose, localBounds);
                        }
                        ring += item.Turns * PanelManager.WandPanelSnapAngle;
                        predicted[0] = end;
                    }
                    else if (step.Op == "present")
                    {
                        if (!floating.TryGetValue(item.Panel, out var pose))
                            throw new ArgumentException("Open the floating panel through its controls before presenting it.");
                        item.Hand = 0;
                        item.Floating = true;
                        subject.AddPose(pose, item.Panel.FullScaleRendererPoints());
                    }
                    else
                    {
                        item.Hand = 1;
                        item.Start = predicted[1];
                        UnityEngine.Pose pose;
                        UnityEngine.Pose buttonPose;
                        if (item.Deferred)
                        {
                            pose = popupPose;
                            Transform root = popupPrefab.transform;
                            BoxCollider collider = item.Button.GetComponent<BoxCollider>();
                            if (collider == null) { throw new ArgumentException("Popup preflight requires a box collider on the button."); }
                            Vector3 centre = Quaternion.Inverse(root.rotation) * (collider.transform.TransformPoint(collider.center) - root.position);
                            item.Path = new[] {AimAt(pose.position + pose.rotation * centre, pose.rotation)};
                            buttonPose = new UnityEngine.Pose(pose.position + pose.rotation *
                                (Quaternion.Inverse(root.rotation) * (item.Button.transform.position - root.position)),
                                pose.rotation * Quaternion.Inverse(root.rotation) * item.Button.transform.rotation);
                        }
                        else
                        {
                            if (item.Panel.m_Fixed) { pose = PanelPose(item.Panel, predicted[0], ring); }
                            else if (!floating.TryGetValue(item.Panel, out pose))
                                throw new ArgumentException("Open the floating panel before clicking its controls.");
                            if (step.Op == "focus")
                            {
                                var collider = item.Panel.GetCollider() as BoxCollider;
                                Vector3 local = collider != null ? item.Panel.FullScalePoint(collider.transform, collider.center) : Vector3.zero;
                                item.Path = new[] {AimAt(pose.position + pose.rotation * local, pose.rotation)};
                            }
                            else { item.Path = new[] {Aim(item.Button, pose, true)}; }
                            buttonPose = new UnityEngine.Pose(pose.position + pose.rotation * item.Panel.FullScalePoint(item.Button.transform, Vector3.zero),
                                pose.rotation * Quaternion.Inverse(item.Panel.transform.rotation) * item.Button.transform.rotation);
                        }
                        var tray = item.Button.GetComponentInParent<BaseTray>(true);
                        if (tray != null)
                        {
                            foreach (var renderer in tray.GetComponentsInChildren<Renderer>(true))
                                if (!(renderer is LineRenderer))
                                    for (int i = 0; i < 8; ++i)
                                        subject.AddPoint(pose.position + pose.rotation * item.Panel.FullScalePoint(renderer.transform, AutomationShot.Corner(renderer.localBounds, i), true));
                        }
                        if (step.Op == "click" && item.Button is OptionButton option && option.AutomationPopupPrefab != null)
                        {
                            popupPrefab = option.AutomationPopupPrefab;
                            popupOwner = item.Panel;
                            popupPose = option.PredictPopupPose(pose);
                            BoxCollider box = popupPrefab.GetComponent<BoxCollider>();
                            if (box != null)
                                for (int i = 0; i < 8; ++i)
                                    subject.AddPoint(popupPose.position + popupPose.rotation * Quaternion.Inverse(popupPrefab.transform.rotation) *
                                        (box.transform.TransformPoint(AutomationShot.Corner(new Bounds(box.center, box.size), i)) - popupPrefab.transform.position));
                        }
                        if (step.Op == "click" && item.Button is PanelButton open)
                        {
                            var panel = PanelManager.m_Instance.GetAllPanels().First(p => p.m_Panel.Type == open.m_Type && p.AvailableInCurrentMode).m_Panel;
                            var spawn = new TrTransform {translation = buttonPose.position, rotation = buttonPose.rotation, scale = 0};
                            var target = spawn; target.scale = 1;
                            var result = panel.WidgetSibling.CalculateIntroAnimTarget(spawn, target, false);
                            var opened = new UnityEngine.Pose(result.translation, result.rotation);
                            floating[panel] = opened;
                            var bounds = new Bounds(opened.position, Vector3.zero);
                            foreach (var point in panel.FullScaleRendererPoints()) { bounds.Encapsulate(opened.position + opened.rotation * point); }
                            bounds.Expand(0.04f * App.METERS_TO_UNITS);
                            subject.AddBounds(bounds);
                        }
                        predicted[1] = item.Path[0];
                        if (item.Deferred) { item.Button = null; }
                    }
                }
                else if (step.Op == "move")
                {
                    if (step.Points == null || step.Points.Length == 0 || (step.Curve != "polyline" && step.Curve != "bezier") ||
                        (step.Curve == "bezier" && step.Points.Length != 4))
                        throw new ArgumentException("move needs points and curve polyline, or exactly four points for bezier.");
                    Vector3 anchor = Anchor(step.Anchor, step.AnchorT);
                    Quaternion startRotation = m_Yaw * Quaternion.Euler(AutomationScript.Vector(step.Rotation, "rotation"));
                    Quaternion endRotation = step.EndRotation == null ? startRotation : m_Yaw * Quaternion.Euler(AutomationScript.Vector(step.EndRotation, "endRotation"));
                    var points = step.Points.Select(p => anchor + m_Yaw * (AutomationScript.Vector(p, "move point") * App.METERS_TO_UNITS)).ToArray();
                    item.Path = new UnityEngine.Pose[step.Points.Length == 1 ? 1 : 129];
                    for (int i = 0; i < item.Path.Length; ++i)
                    {
                        float t = item.Path.Length == 1 ? 1 : (float)i / (item.Path.Length - 1);
                        float segment = t * (points.Length - 1);
                        int low = Mathf.Min(Mathf.FloorToInt(segment), points.Length - 1);
                        Vector3 point = step.Curve == "bezier" ? ProceduralArm.Bezier(points[0], points[1], points[2], points[3], t) :
                            Vector3.Lerp(points[low], points[Mathf.Min(low + 1, points.Length - 1)], segment - low);
                        item.Path[i] = RootForContact(item.Hand, new UnityEngine.Pose(point, Quaternion.Slerp(startRotation, endRotation, t)), step);
                        if (step.PaddingMeters < 0 || float.IsNaN(step.PaddingMeters) || float.IsInfinity(step.PaddingMeters))
                            throw new ArgumentException("paddingMeters must be finite and nonnegative.");
                        subject.AddBounds(new Bounds(point, Vector3.one * (2 * step.PaddingMeters * App.METERS_TO_UNITS)));
                    }
                    predicted[item.Hand] = item.Path.Last();
                    if (item.Path.Length > 1 && (Vector3.Distance(item.Start.position, item.Path[0].position) > 0.005f * App.METERS_TO_UNITS ||
                        Quaternion.Angle(item.Start.rotation, item.Path[0].rotation) > 2))
                        throw new ArgumentException("Approach the first path pose with a single-point move before following a multi-point path.");
                }
                else if (step.Op == "rest")
                {
                    item.Path = new[] {new UnityEngine.Pose(Point(new Vector3(item.Hand == 0 ? -0.24f : 0.24f, -0.48f, 0.23f)),
                        item.Hand == 0 ? m_OriginalPoses[0].rotation : m_Yaw)};
                    predicted[item.Hand] = item.Path[0];
                }
                else if (step.Op == "input" && step.Thumbstick != null)
                {
                    if (step.Thumbstick.Length != 2 || step.Thumbstick.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 1))
                        throw new ArgumentException("thumbstick needs two numbers between -1 and 1.");
                }
                if (item.Path != null)
                {
                    foreach (var pose in item.Path) { m_Arms[item.Hand].Solve(pose.position, pose.rotation); }
                    if (step.Op == "present" || step.Op == "focus" || step.Op == "click" || (step.Op != "rest" && item.Path.Length == 1))
                        subject.AddPose(item.Path.Last(), m_ControllerBounds[item.Hand]);
                    else if (step.Op != "rest")
                    {
                        for (int i = 0; i <= 64; ++i)
                            subject.AddPose(Evaluate(item, i / 64f), m_ControllerBounds[item.Hand]);
                    }
                }
                m_Instructions.Add(item);
            }
            return shot;
        }

        private UnityEngine.Pose Evaluate(Instruction item, float t)
        {
            if (item.Path.Length == 1)
                return m_Arms[item.Hand].Reach(m_Arms[item.Hand].Solve(item.Start.position, item.Start.rotation),
                    m_Arms[item.Hand].Solve(item.Path[0].position, item.Path[0].rotation), t);
            float index = ProceduralArm.MinimumJerk(t) * (item.Path.Length - 1);
            int low = Mathf.FloorToInt(index), high = Mathf.Min(low + 1, item.Path.Length - 1);
            var pose = new UnityEngine.Pose(Vector3.Lerp(item.Path[low].position, item.Path[high].position, index - low),
                Quaternion.Slerp(item.Path[low].rotation, item.Path[high].rotation, index - low));
            return m_Arms[item.Hand].Evaluate(m_Arms[item.Hand].Solve(pose.position, pose.rotation));
        }

        private static List<Vector3> ControllerBounds(ControllerInfo controller)
        {
            var geometry = controller.Geometry;
            var renderers = new List<Renderer> {geometry.MainMesh, geometry.TriggerMesh, geometry.LeftGripMesh, geometry.RightGripMesh};
            if (geometry.OtherMeshes != null) { renderers.AddRange(geometry.OtherMeshes); }
            return AutomationShot.CaptureLocalBounds(controller.Transform, renderers.Where(r => r != null).Distinct(), true);
        }

        private void BeginStep()
        {
            m_Elapsed = 0; m_TotalElapsed = 0; m_Frames = 0; m_Stage = 0; m_Clicked = false;
            var item = m_Instructions[m_Index];
            item.Start = m_Poses[item.Hand];
            m_ReachStart = m_Arms[item.Hand].Solve(item.Start.position, item.Start.rotation);
            if (item.Source.Op == "click" && item.Button != null) { item.Button.Pressed += ButtonPressed; }
            Debug.Log($"[AutomationScript] {Status}");
        }

        private void ButtonPressed(BaseButton button) { if (button == m_Instructions[m_Index].Button) { m_Clicked = true; } }
        private void Advance()
        {
            var previous = m_Instructions[m_Index];
            if (previous.Source.Op == "click" && previous.Button != null) { previous.Button.Pressed -= ButtonPressed; }
            if (++m_Index == m_Instructions.Count) { Stop(); return; }
            BeginStep();
        }
        public void Stop() { m_Stopping = true; }

        public bool Tick(float deltaTime)
        {
            if (m_Disposed) { return false; }
            try
            {
                if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true ||
                    App.CurrentState != App.AppState.Standard || !InputManager.m_Instance.AllowVrControllers) { Stop(); }
                if (!m_Stopping)
                {
                    m_Elapsed += deltaTime; m_TotalElapsed += deltaTime; ++m_Frames;
                    var item = m_Instructions[m_Index];
                    var step = item.Source;
                    float t = Mathf.Clamp01((m_Frames == 1 ? 0 : m_Elapsed) / step.Seconds);
                    switch (step.Op)
                    {
                        case "present":
                            if (item.Floating)
                            {
                                if (PanelManager.m_Instance.IsPanelOpen(item.Panel.Type) && !item.Panel.WidgetSibling.IsAnimating()) { Advance(); }
                                else if (m_TotalElapsed > 8) { throw new InvalidOperationException("The floating panel did not finish opening."); }
                                break;
                            }
                            if (m_Triggers.Any(v => v)) { throw new InvalidOperationException("Release triggers before presenting a panel."); }
                            if (m_Stage == 0)
                            {
                                m_Poses[0] = Evaluate(item, t);
                                if (t >= 1) { m_Stage = 1; m_Elapsed = 0; }
                            }
                            else
                            {
                                m_Sticks[0] = Vector2.zero;
                                if (m_TotalElapsed > step.Seconds + 8) { throw new InvalidOperationException("Panel rotation did not settle."); }
                                if (!PanelManager.m_Instance.WandPanelRotationIsSettled) { break; }
                                if (m_Stage == 2)
                                {
                                    if (m_Elapsed < 0.1f) { break; }
                                    item.Turns -= Math.Sign(item.Turns);
                                    m_Stage = 1;
                                }
                                if (item.Turns == 0) { Advance(); }
                                else
                                {
                                    m_Sticks[0] = new Vector2(-Math.Sign(item.Turns), 0);
                                    m_Elapsed = 0; m_Stage = 2;
                                }
                            }
                            break;
                        case "click":
                            Click(item, deltaTime);
                            break;
                        case "focus":
                            var focusCollider = item.Panel.GetCollider();
                            var focus = AimAt(focusCollider.bounds.center, item.Panel.transform.rotation);
                            m_Poses[1] = m_Arms[1].Reach(m_ReachStart, m_Arms[1].Solve(focus.position, focus.rotation), t);
                            if (m_Elapsed >= step.Seconds + 0.45f && SketchControlsScript.m_Instance.IsUserLookingAtPanel(item.Panel)) { Advance(); }
                            else if (m_Elapsed > step.Seconds + 8) { throw new InvalidOperationException("The panel did not acquire focus."); }
                            break;
                        case "move":
                        case "rest":
                            m_Poses[item.Hand] = Evaluate(item, t);
                            if (t >= 1) { Advance(); }
                            break;
                        case "wait":
                            if (m_Elapsed >= step.Seconds) { Advance(); }
                            break;
                        case "input":
                            if (step.Trigger.HasValue) { m_Triggers[item.Hand] = step.Trigger.Value; }
                            if (step.Thumbstick != null) { m_Sticks[item.Hand] = new Vector2(step.Thumbstick[0], step.Thumbstick[1]); }
                            Advance();
                            break;
                        case "assert":
                            CheckResult(step); Advance();
                            break;
                    }
                }
            }
            catch (Exception error)
            {
                Error = $"Step {m_Index + 1}: {error.Message}";
                Debug.LogError($"[AutomationScript] {Error}");
                Stop();
            }
            if (m_Stopping)
            {
                for (int i = 0; i < 2; ++i) { m_Triggers[i] = false; m_Sticks[i] = Vector2.zero; }
                if (++m_ReleaseFrames > 2) { return false; }
            }
            for (int i = 0; i < 2; ++i)
            {
                m_Controllers[i].SetTrigger(m_Triggers[i]);
                m_Controllers[i].SetThumbstick(m_Sticks[i]);
            }
            ApplyPoses();
            return true;
        }

        private void Click(Instruction item, float deltaTime)
        {
            if (item.Hand != 1) { throw new InvalidOperationException("UI clicks use the brush hand."); }
            if (m_TotalElapsed > item.Source.Seconds + 8) { throw new InvalidOperationException("Button did not receive hover/click."); }
            if (m_Stage == 2 && m_Clicked)
            {
                m_Triggers[1] = true;
                if (m_Elapsed >= 0.16f) { m_Triggers[1] = false; m_Stage = 3; m_Elapsed = 0; }
                return;
            }
            if (m_Stage == 3)
            {
                m_Triggers[1] = false;
                if (m_Elapsed >= 0.45f)
                {
                    if (!m_Clicked) { throw new InvalidOperationException("The native UI did not accept the click."); }
                    CheckResult(item.Source);
                    Advance();
                }
                return;
            }
            if (item.Button == null && item.Deferred)
            {
                item.Button = item.Source.Target.Resolve(false);
                if (item.Button == null) { m_Elapsed = 0; return; }
                item.Button.Pressed += ButtonPressed;
            }
            if (item.Button == null) { throw new InvalidOperationException("The target control was destroyed before it was clicked."); }
            var collider = item.Button.GetCollider();
            if (!item.Button.isActiveAndEnabled || !collider.enabled || collider.bounds.size.sqrMagnitude < 0.000001f) { m_Elapsed = 0; return; }
            var aim = Aim(item.Button, new UnityEngine.Pose(item.Panel.transform.position, item.Panel.transform.rotation), false);
            if (m_Stage == 0)
            {
                if (m_Triggers[1]) { throw new InvalidOperationException("Release the brush trigger before clicking."); }
                m_Poses[1] = m_Arms[1].Reach(m_ReachStart, m_Arms[1].Solve(aim.position, aim.rotation), m_Elapsed / item.Source.Seconds);
                if (m_Elapsed >= item.Source.Seconds) { m_Stage = 1; m_Elapsed = 0; }
                return;
            }
            m_Poses[1] = aim;
            if (m_Stage == 1)
            {
                if (!item.Button.IsHover() || !SketchControlsScript.m_Instance.IsUserLookingAtPanel(item.Panel)) { m_Elapsed = 0; }
                if (m_Elapsed >= 0.45f) { m_Stage = 2; m_Elapsed = 0; }
            }
            else if (m_Stage == 2)
            {
                m_Triggers[1] = true;
                if (m_Elapsed >= 0.16f) { m_Triggers[1] = false; m_Stage = 3; m_Elapsed = 0; }
            }
            else if (m_Elapsed >= 0.45f)
            {
                if (!m_Clicked) { throw new InvalidOperationException("The native UI did not accept the click."); }
                CheckResult(item.Source);
                Advance();
            }
        }

        private void CheckResult(AutomationScript.Step step)
        {
            if (!string.IsNullOrEmpty(step.Panel) && step.PanelOpen.HasValue &&
                PanelManager.m_Instance.IsPanelOpen(AutomationScript.PanelType(step.Panel)) != step.PanelOpen.Value)
                throw new InvalidOperationException($"Unexpected visibility for panel {step.Panel}.");
            if (!string.IsNullOrEmpty(step.Tool) && SketchSurfacePanel.m_Instance.GetCurrentToolType().ToString() != step.Tool)
                throw new InvalidOperationException($"Expected active tool {step.Tool}.");
            if (!string.IsNullOrEmpty(step.Subtool) && SculptSubToolManager.m_Instance?.ActiveSubTool.ToString() != step.Subtool)
                throw new InvalidOperationException($"Expected active subtool {step.Subtool}.");
            if (step.StrokeCountDelta.HasValue && SketchMemoryScript.m_Instance.StrokeCount - m_InitialStrokeCount != step.StrokeCountDelta.Value)
                throw new InvalidOperationException("Unexpected stroke count after the script action.");
            if (string.IsNullOrEmpty(step.Stroke)) { return; }
            if (!m_Strokes.TryGetValue(step.Stroke, out Stroke stroke)) { throw new ArgumentException($"Unknown result stroke: {step.Stroke}"); }
            var original = m_OriginalPoints[step.Stroke];
            var result = stroke.m_ControlPoints;
            if (original.Length != result.Length) { throw new InvalidOperationException("Stroke control-point count changed."); }
            float movement = Enumerable.Range(0, result.Length).Max(i => Vector3.Distance(original[i], result[i].m_Pos)) * stroke.Canvas.Pose.scale * App.UNITS_TO_METERS;
            if (step.UnchangedGeometry && movement > 0.00001f)
                throw new InvalidOperationException("The tool unexpectedly moved the stroke's geometry.");
            if (movement < step.MinimumMovementMeters) { throw new InvalidOperationException("The stroke did not move far enough."); }
            if (step.FixedEndpoints && (Vector3.Distance(original[0], result[0].m_Pos) > 0.0001f || Vector3.Distance(original.Last(), result.Last().m_Pos) > 0.0001f))
                throw new InvalidOperationException("The stroke endpoints moved.");
            AutomationScript.Positive(step.ColorTolerance, "colorTolerance", 0, 1);
            if (step.MinimumChangedColorPoints < 0 || step.MaximumChangedColorPoints < 0)
                throw new ArgumentException("Changed colour point limits must be nonnegative.");
            int changed = Enumerable.Range(0, result.Length).Count(i => ColorDistance(m_OriginalColors[step.Stroke][i], stroke.GetColor(i)) > step.ColorTolerance);
            if (changed < step.MinimumChangedColorPoints || (step.MaximumChangedColorPoints.HasValue && changed > step.MaximumChangedColorPoints.Value))
                throw new InvalidOperationException($"Unexpected colour coverage: {changed} stroke points changed.");
            if (step.ExpectedColorRgb != null)
            {
                AutomationScript.Positive(step.ColorPointT, "colorPointT", 0, 1);
                int point = Mathf.RoundToInt(step.ColorPointT * (result.Length - 1));
                if (ColorDistance(stroke.GetColor(point), AutomationScript.Rgb(step.ExpectedColorRgb)) > step.ColorTolerance)
                    throw new InvalidOperationException("The inspected stroke point did not reach the expected colour.");
            }
            Debug.Log($"[AutomationScript] Stroke {step.Stroke} moved {movement:F3}m.");
            if (step.MinimumChangedColorPoints > 0 || step.ExpectedColorRgb != null)
                Debug.Log($"[AutomationScript] Stroke {step.Stroke}: {changed}/{result.Length} points changed colour; geometry movement {movement:F6}m.");
        }

        private static float ColorDistance(Color a, Color b) => Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b));

        public void ApplyPoses()
        {
            if (m_Disposed) { return; }
            for (int i = 0; i < 2; ++i) { m_Controllers[i].Transform.SetPositionAndRotation(m_Poses[i].position, m_Poses[i].rotation); }
        }

        public void Dispose()
        {
            if (m_Disposed) { return; }
            m_Disposed = true;
            if (m_Index < m_Instructions.Count && m_Instructions[m_Index].Button != null)
                m_Instructions[m_Index].Button.Pressed -= ButtonPressed;
            try
            {
                App.Instance.ShowControllers = m_ShowControllers;
                for (int i = 0; i < 2; ++i)
                {
                    if (!ReferenceEquals(InputManager.Controllers[i], m_Controllers[i])) { continue; }
                    InputManager.Controllers[i] = m_Original[i];
                    m_Original[i].Transform.SetPositionAndRotation(m_OriginalPoses[i].position, m_OriginalPoses[i].rotation);
                    m_Original[i].Transform.gameObject.SetActive(m_Visible[i]);
                    m_Original[i].m_Position = m_OriginalPoses[i].position;
                    m_Original[i].m_Velocity = Vector3.zero;
                    m_Original[i].m_Acceleration = Vector3.zero;
                    m_Original[i].ShowController(m_Visible[i] && m_ShowControllers);
                }
            }
            finally { m_Tracking.Dispose(); }
        }
    }
}
