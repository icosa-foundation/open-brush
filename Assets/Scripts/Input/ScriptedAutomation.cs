// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    /// Executes externally authored primitives on the input clock, before native consumers.
    public sealed class ScriptedAutomation : IDisposable
    {
        private sealed class Instruction
        {
            public AutomationScript.Step Source;
            public int Hand;
            public BaseButton Button;
            public BaseSlider Slider;
            public float SliderStartValue;
            public float SliderAimOffset;
            public BasePanel Panel;
            public UnityEngine.Pose[] Path;
            public UnityEngine.Pose Start;
            public int Turns;
            public bool Deferred;
            public bool Floating;
            public VrInput[] Buttons;
            public float DurationSeconds;
            public Vector3 PanelDestination;
            public Quaternion PanelDestinationRotation;
            public float RingAngle;
            public Instruction Parallel;
            public bool AimPlanned;
            public Vector3 PointingOffset;
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
        private readonly VrInput[][] m_Buttons = {Array.Empty<VrInput>(), Array.Empty<VrInput>()};
        private readonly IDisposable m_Tracking;
        private readonly bool m_ShowControllers;
        private readonly Quaternion m_Yaw;
        private readonly UnityEngine.Pose m_Workspace;
        private readonly Ray? m_OriginalGaze;
        private readonly float? m_OriginalViewAspect;
        private readonly int m_InitialStrokeCount;
        private readonly int m_InitialVisibleStrokeCount;
        private readonly AutomationScript m_Script;
        private Vector3 m_PanelLayoutOffset;
        private UnityEngine.Pose? m_RestView;
        private Vector3 m_ArmHead;
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
            if (script.Output == null) { throw new ArgumentException("Output settings must not be null."); }
            script.Output.Validate();
            AutomationScript.Positive(script.Speed, "speed", 0.25f, 2);
            AutomationScript.Positive(script.UpperArmMeters, "upperArmMeters", 0.15f, 0.5f);
            AutomationScript.Positive(script.ForearmMeters, "forearmMeters", 0.15f, 0.5f);
            m_Workspace = App.VrSdk.VirtualSimulation.BodyPose;
            m_OriginalGaze = App.VrSdk.VirtualSimulation.InteractionGaze;
            m_OriginalViewAspect = App.VrSdk.VirtualSimulation.ViewAspect;
            m_Yaw = Quaternion.Euler(0, m_Workspace.rotation.eulerAngles.y, 0);
            m_Original = (ControllerInfo[])InputManager.Controllers.Clone();
            m_OriginalPoses = m_Original.Select(c => new UnityEngine.Pose(c.Transform.position, c.Transform.rotation)).ToArray();
            m_Visible = m_Original.Select(c => c.Transform.gameObject.activeSelf).ToArray();
            m_Poses = (UnityEngine.Pose[])m_OriginalPoses.Clone();
            m_Arms = new ProceduralArm[2];
            SetArmHead(m_Workspace.position);
            m_ControllerBounds = m_Original.Select(ControllerBounds).ToArray();
            // Validate syntax and selectors before creating any fixture strokes.
            foreach (var step in script.Steps.SelectMany(ExpandMoves))
            {
                if (!new[] {"present", "focus", "click", "selectScript", "slider", "placePanel", "move", "input", "wait", "rest", "assert"}.Contains(step.Op))
                    throw new ArgumentException($"Unknown script operation: {step.Op}");
                AutomationScript.Positive(step.Seconds, "step seconds", 0.01f, 120);
                if (step.Op == "slider")
                {
                    AutomationScript.Positive(step.Value, "slider value", 0, 1);
                    if (string.IsNullOrEmpty(step.Slider) || step.Hand != "brush")
                        throw new ArgumentException("slider needs a slider hierarchy path and the brush hand.");
                }
                if (step.EndRotation != null && step.Rotation == null)
                    throw new ArgumentException("endRotation requires an explicit rotation.");
                if (step.Op == "placePanel" && step.EndRotation != null)
                    throw new ArgumentException("placePanel uses rotation for the panel destination, not endRotation.");
                if (step.RotationConstraint != "full" && step.RotationConstraint != "forward")
                    throw new ArgumentException("rotationConstraint must be full or forward.");
                if (step.RotationConstraint == "forward" && step.Rotation == null)
                    throw new ArgumentException("A forward rotation constraint needs rotation.");
                if (step.Op == "present" || step.Op == "focus" || step.Op == "click")
                {
                    if (step.Target == null) { throw new ArgumentException("present and click require a target."); }
                }
                if (step.Hand != "wand" && step.Hand != "brush") { throw new ArgumentException("hand must be wand or brush."); }
            }
            m_Strokes = script.PrepareScene(m_Workspace);
            m_OriginalPoints = m_Strokes.ToDictionary(p => p.Key, p => p.Value.m_ControlPoints.Select(c => c.m_Pos).ToArray());
            m_OriginalColors = m_Strokes.ToDictionary(p => p.Key,
                p => Enumerable.Range(0, p.Value.m_ControlPoints.Length).Select(p.Value.GetColor).ToArray());
            m_InitialStrokeCount = SketchMemoryScript.m_Instance.StrokeCount;
            m_InitialVisibleStrokeCount = SketchMemoryScript.m_Instance.GetMemoryList.Count(s => s.IsGeometryEnabled);
            var framing = PlanFraming();
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
                App.VrSdk.VirtualSimulation.SetViewAspect((float)script.Output.Width / script.Output.Height);
                ApplyPoses();
                if (framing.HasValue) { App.VrSdk.VirtualSimulation.SetFramingPose(framing.Value); }
                if (script.Capture != null)
                {
                    var capture = App.Instance.GetComponent<ProceduralCapture>() ?? App.Instance.gameObject.AddComponent<ProceduralCapture>();
                    capture.Begin(script.Capture.Name, script.Capture.Fps, script.Output.Width, script.Output.Height);
                }
                BeginStep();
            }
            catch { Dispose(); throw; }
        }

        private Vector3 Point(Vector3 metres) => m_Workspace.position + m_Yaw * (metres * App.METERS_TO_UNITS);
        private static IEnumerable<AutomationScript.Step> ExpandMoves(AutomationScript.Step step)
        {
            yield return step;
            if (step.Parallel == null) { yield break; }
            if (step.Op != "move" || step.Parallel.Op != "move" || step.Parallel.Parallel != null ||
                step.Hand == step.Parallel.Hand || Mathf.Abs(step.Seconds - step.Parallel.Seconds) > 0.0001f)
                throw new ArgumentException("parallel requires two move steps for different hands with equal seconds.");
            yield return step.Parallel;
        }
        private void SetArmHead(Vector3 position)
        {
            m_ArmHead = position;
            for (int hand = 0; hand < 2; ++hand)
            {
                m_Arms[hand] = MakeArm(hand == 0 ? -1 : 1);
                Vector3 resting = m_ArmHead + m_Yaw *
                    (new Vector3(hand == 0 ? -0.24f : 0.24f, -0.48f, 0.23f) * App.METERS_TO_UNITS);
                m_Poses[hand] = m_Arms[hand].RelaxedGripPose(resting, Vector3.zero);
            }
        }

        private ProceduralArm MakeArm(float side)
        {
            var controller = m_Original[side < 0 ? 0 : 1];
            Transform grip = controller.Geometry.GripAttachPoint;
            if (grip == null) { throw new InvalidOperationException("The controller model needs a calibrated grip attachment."); }
            Quaternion inverse = Quaternion.Inverse(controller.Transform.rotation);
            var local = new UnityEngine.Pose(inverse * (grip.position - controller.Transform.position), inverse * grip.rotation);
            return new ProceduralArm(m_ArmHead + m_Yaw *
                (new Vector3(side * 0.18f, -0.20f, -0.04f) * App.METERS_TO_UNITS),
                m_Yaw * new Vector3(side * 0.6f, -1, -0.15f), m_Script.UpperArmMeters * App.METERS_TO_UNITS,
                m_Script.ForearmMeters * App.METERS_TO_UNITS, local, m_Yaw * Vector3.up);
        }

        private Vector3 Anchor(string id, float t)
        {
            if (string.IsNullOrEmpty(id)) { return m_Workspace.position; }
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

        private UnityEngine.Pose PlanPresentation(BasePanel panel, float ring, UnityEngine.Pose start, out int turns)
        {
            Vector3 preferred = AutomationScript.Vector(m_Script.PanelPosition, "panelPosition") + m_PanelLayoutOffset;
            Quaternion inverse = Quaternion.Inverse(m_Original[0].Transform.rotation);
            Quaternion pointerLocal = inverse * m_Original[0].Behavior.PointerAttachPoint.rotation;
            var from = m_Arms[0].Solve(start.position, start.rotation);
            UnityEngine.Pose best = default;
            int bestTurns = 0;
            float bestScore = float.PositiveInfinity;
            // Choose the grip first. Native thumbstick snaps bring the pane around it;
            // a panel can remain oblique rather than bending the wrist to face the camera.
            for (int lift = 0; lift <= 6; ++lift)
            for (int pitch = -10; pitch <= 10; pitch += 5)
            for (int yaw = -30; yaw <= 30; yaw += 15)
            for (int snap = -1; snap <= 1; ++snap)
            {
                Vector3 position = m_ArmHead + m_Yaw *
                    ((preferred + Vector3.up * (lift * 0.02f)) * App.METERS_TO_UNITS);
                Quaternion rotation = m_Yaw * Quaternion.Euler(pitch, yaw, 0) * Quaternion.Inverse(pointerLocal);
                var presented = PanelPose(panel, new UnityEngine.Pose(Vector3.zero, rotation),
                    ring + snap * PanelManager.WandPanelSnapAngle);
                float angle = Vector3.Angle(presented.rotation * Vector3.forward, position - m_ArmHead);
                if (angle > 45) { continue; }
                float score = angle + lift * 0.2f + Mathf.Abs(yaw) * 0.02f;
                if (score >= bestScore) { continue; }
                var pose = new UnityEngine.Pose(position - presented.position, rotation);
                try
                {
                    var to = m_Arms[0].Solve(pose.position, pose.rotation);
                    score += 20 * m_Arms[0].Comfort(to);
                    if (score >= bestScore) { continue; }
                    for (int i = 0; i <= 32; ++i) { m_Arms[0].Reach(from, to, i / 32f); }
                }
                catch (InvalidOperationException) { continue; }
                best = pose;
                bestTurns = snap;
                bestScore = score;
            }
            if (float.IsPositiveInfinity(bestScore))
                throw new ArgumentException($"Cannot present {panel.Type} with a comfortable grip near panelPosition. Move panelPosition closer or higher.");
            turns = bestTurns;
            return best;
        }

        private UnityEngine.Pose ContactLocal(int hand, AutomationScript.Step step)
        {
            if (step.Attachment == "grip")
            {
                // Match the anchor used by SketchControlsScript's native world grab.
                Vector3 anchor = m_Original[1].Geometry.GripAttachPoint.localPosition;
                anchor.x = 0;
                return new UnityEngine.Pose(anchor, Quaternion.Inverse(m_Original[hand].Transform.rotation) *
                    m_Original[hand].Geometry.GripAttachPoint.rotation);
            }
            if (step.Attachment == "root") { return new UnityEngine.Pose(Vector3.zero, Quaternion.identity); }
            Transform root = m_Original[hand].Transform;
            Transform attachment = step.Attachment == "tip" ? m_Original[hand].Behavior.PointerAttachPoint :
                step.Attachment == "tool" ? m_Original[hand].Geometry.ToolAttachPoint : null;
            if (attachment == null) { throw new ArgumentException("attachment must be root, grip, tip or tool, available on the selected hand."); }
            float forward = 0;
            if (step.Attachment == "tool" && !string.IsNullOrEmpty(step.Tool))
            {
                var tool = UnityEngine.Object.FindObjectsByType<BaseStrokeIntersectionTool>(FindObjectsInactive.Include)
                    .SingleOrDefault(t => t.m_Type.ToString() == step.Tool);
                if (tool != null) { forward = tool.PointerForwardOffset; }
            }
            Quaternion localRotation = Quaternion.Inverse(root.rotation) * attachment.rotation;
            Vector3 localPosition = Quaternion.Inverse(root.rotation) * (attachment.position + attachment.forward * forward - root.position);
            return new UnityEngine.Pose(localPosition, localRotation);
        }

        private UnityEngine.Pose RootForContact(int hand, UnityEngine.Pose contact, AutomationScript.Step step,
            bool freeRoll = false) => m_Arms[hand].ContactPose(contact, ContactLocal(hand, step), freeRoll);

        private UnityEngine.Pose RootForMotion(int hand, UnityEngine.Pose contact, AutomationScript.Step step) =>
            step.Rotation == null ? m_Arms[hand].RelaxedGripPose(contact.position, ContactLocal(hand, step).position) :
                RootForContact(hand, contact, step, step.RotationConstraint == "forward");

        private IEnumerable<AutomationScript.Step> ExpandScriptSelections()
        {
            var selected = new Dictionary<LuaApiCategory, int>();
            foreach (var step in m_Script.Steps)
            {
                if (step.Op != "selectScript") { yield return step; continue; }
                LuaManager.Instance.Init(immediate: true);
                if (!Enum.TryParse(step.Category, out LuaApiCategory category) || !Enum.IsDefined(typeof(LuaApiCategory), category))
                    throw new ArgumentException($"Unknown script category: {step.Category}");
                var names = LuaManager.Instance.GetScriptNames(category);
                int target = names.IndexOf(step.ScriptName);
                if (target < 0) { throw new ArgumentException($"Unknown {step.Category}: {step.ScriptName}"); }
                int current = selected.TryGetValue(category, out int previous) ? previous :
                    names.IndexOf(LuaManager.Instance.GetActiveScriptName(category));
                int forward = (target - current + names.Count) % names.Count;
                int backward = (current - target + names.Count) % names.Count;
                bool next = forward <= backward;
                int clicks = Mathf.Min(forward, backward);
                var panel = PanelManager.m_Instance.GetPanelByType(AutomationScript.PanelType("Scripts"));
                string nav = step.Category.Replace("Script", "ScriptsNav");
                string button = next ? "NextScriptButton" : "PrevScriptButton";
                string path = $"{AutomationScript.Selector.HierarchyPath(panel.transform)}/Mesh/{nav}/{button}";
                for (int i = 0; i < clicks; ++i)
                    yield return new AutomationScript.Step {Op = "click", Label = $"Choose {step.ScriptName}",
                        Target = new AutomationScript.Selector {Path = path}, Seconds = i == 0 ? step.Seconds : 0.08f, IsRepeatedScriptClick = i > 0};
                yield return new AutomationScript.Step {Op = "assert", Category = step.Category, ScriptName = step.ScriptName};
                selected[category] = target;
            }
        }

        private UnityEngine.Pose AimSlider(Instruction item, UnityEngine.Pose panel, float value, bool predict)
        {
            Vector3 local = item.Slider.GetKnobLocalPosition(value);
            Transform parent = item.Slider.m_Nob.transform.parent;
            Vector3 target = predict ? panel.position + panel.rotation * item.Panel.FullScalePoint(parent, local) :
                parent.TransformPoint(local);
            return AimAt(target, panel.rotation, item);
        }

        private UnityEngine.Pose[] PlanPanelGrip(UnityEngine.Pose source, UnityEngine.Pose destination, Vector3 center)
        {
            Vector3 local = ContactLocal(1, new AutomationScript.Step()).position;
            Vector3 start = source.position + source.rotation * center;
            Vector3 finish = destination.position + destination.rotation * center;
            Quaternion delta = destination.rotation * Quaternion.Inverse(source.rotation);
            Quaternion first = m_Arms[1].RelaxedGripPose(start, local).rotation;
            Quaternion last = Quaternion.Inverse(delta) * m_Arms[1].RelaxedGripPose(finish, local).rotation;
            float bestCost = float.PositiveInfinity;
            UnityEngine.Pose[] best = null;
            // The grip must be comfortable at both ends while preserving the panel's
            // rigid attachment to the hand. Pointer-tip contact is required for native grabs.
            for (int i = 0; i <= 8; ++i)
            {
                Quaternion rotation = Quaternion.Slerp(first, last, i / 8f);
                var grab = new UnityEngine.Pose(start - rotation * local, rotation);
                Quaternion releasedRotation = delta * rotation;
                var released = new UnityEngine.Pose(finish - releasedRotation * local, releasedRotation);
                try
                {
                    var from = m_Arms[1].Solve(grab.position, grab.rotation);
                    var to = m_Arms[1].Solve(released.position, released.rotation);
                    float cost = m_Arms[1].Comfort(from) + m_Arms[1].Comfort(to);
                    if (cost >= bestCost) { continue; }
                    for (int sample = 0; sample <= 32; ++sample) { m_Arms[1].Reach(from, to, sample / 32f); }
                    bestCost = cost;
                    best = new[] {grab, released};
                }
                catch (InvalidOperationException) { }
            }
            return best ?? throw new InvalidOperationException("Cannot grip and place the panel with comfortable wrists.");
        }

        private UnityEngine.Pose Aim(BaseButton button, UnityEngine.Pose panelPose, bool predict, Instruction instruction)
        {
            BasePanel panel = button.GetComponentInParent<BasePanel>(true);
            Collider collider = button.GetCollider() ?? button.GetComponent<Collider>();
            if (collider == null) { throw new ArgumentException("The selected button has no collider."); }
            Vector3 target = predict && collider is BoxCollider box ? panelPose.position +
                panelPose.rotation * panel.FullScalePoint(box.transform, box.center, true) : collider.bounds.center;
            Quaternion rotation = !predict && button.GetComponentInParent<PopUpWindow>() is PopUpWindow popup ? popup.transform.rotation : panelPose.rotation;
            return AimAt(target, rotation, instruction);
        }

        private UnityEngine.Pose AimAt(Vector3 target, Quaternion rotation, Instruction instruction)
        {
            Vector3 offset = AutomationScript.Vector(m_Script.PointingOffsetMeters, "pointingOffsetMeters");
            if (offset.z > -0.03f)
                throw new ArgumentException("pointingOffsetMeters must place the tip at least 3 cm in front of the panel.");
            UnityEngine.Pose PointFrom(Vector3 candidateOffset)
            {
                Vector3 tip = target + rotation * (candidateOffset * App.METERS_TO_UNITS);
                return RootForContact(1, new UnityEngine.Pose(tip,
                    Quaternion.LookRotation(target - tip, m_Yaw * Vector3.up)), new AutomationScript.Step(), true);
            }
            if (instruction.AimPlanned) { return PointFrom(instruction.PointingOffset); }
            float bestCost = float.PositiveInfinity;
            UnityEngine.Pose best = default;
            Exception failure = null;
            foreach (float lateral in new[] {1f, 0.75f, 0.5f})
            {
                var candidateOffset = new Vector3(offset.x * lateral, offset.y, offset.z);
                try
                {
                    var pose = PointFrom(candidateOffset);
                    float cost = m_Arms[1].Comfort(m_Arms[1].Solve(pose.position, pose.rotation)) +
                        (CoversTarget(pose, target) ? 100 : 0) + 0.02f * (1 - lateral);
                    if (cost >= bestCost) { continue; }
                    best = pose; bestCost = cost; instruction.PointingOffset = candidateOffset;
                }
                catch (InvalidOperationException error) { failure = error; }
            }
            if (float.IsPositiveInfinity(bestCost))
                throw new InvalidOperationException("Cannot reach a comfortable pointing pose for the control.", failure);
            instruction.AimPlanned = true;
            return best;
        }

        private bool CoversTarget(UnityEngine.Pose pose, Vector3 target)
        {
            Vector3 ray = target - m_ArmHead;
            Quaternion inverse = Quaternion.Inverse(Quaternion.LookRotation(ray, m_Yaw * Vector3.up));
            Vector2 min = Vector2.one * float.PositiveInfinity, max = Vector2.one * float.NegativeInfinity;
            bool inFront = false;
            foreach (var corner in m_ControllerBounds[1])
            {
                Vector3 point = inverse * (pose.position + pose.rotation * corner - m_ArmHead);
                if (point.z <= 0) { return true; }
                inFront |= point.z < ray.magnitude;
                var projected = new Vector2(point.x, point.y) / point.z;
                min = Vector2.Min(min, projected); max = Vector2.Max(max, projected);
            }
            return inFront && min.x <= 0 && max.x >= 0 && min.y <= 0 && max.y >= 0;
        }

        private UnityEngine.Pose? PlanFraming()
        {
            var camera = App.VrSdk.GetVrCamera();
            float aspect = (float)m_Script.Output.Width / m_Script.Output.Height;
            if (!m_Script.Framing.Enabled)
            {
                m_RestView = new UnityEngine.Pose(camera.transform.position, camera.transform.rotation);
                SetArmHead(m_RestView.Value.position);
                Compile(); return null;
            }
            Exception failure = null;
            int failureStage = -1;
            var planningTime = System.Diagnostics.Stopwatch.StartNew();
            int candidates = 0;
            AutomationScript.Positive(m_Script.Framing.MaximumPullbackMeters, "maximumPullbackMeters", 0, 0.5f);
            var fixedEyeSettings = new AutomationShot.FramingSettings
            {
                Margin = m_Script.Framing.Margin,
                MaximumPullbackMeters = 0,
                MaximumAngleDegrees = m_Script.Framing.MaximumAngleDegrees,
                PanelGap = m_Script.Framing.PanelGap
            };
            // Prefer the non-drawing side, then try the other side if the artwork occupies it.
            // Every candidate uses the native panel spawn and the same grip/reach checks.
            foreach (float depth in new[] {0f, -0.06f, 0.06f})
            foreach (float side in new[] {0f, -0.06f, 0.06f, -0.12f, 0.12f, -0.18f, 0.18f, -0.24f, 0.24f, -0.30f, 0.30f, 0.36f, 0.42f})
            foreach (float lift in new[] {0f, 0.06f, 0.12f})
            foreach (int cameraStep in Enumerable.Range(0, m_Script.Framing.MaximumPullbackMeters == 0 ? 1 : 7))
            {
                m_PanelLayoutOffset = new Vector3(side, lift, depth);
                ++candidates;
                float pullback = m_Script.Framing.MaximumPullbackMeters * cameraStep / 6;
                var eye = new UnityEngine.Pose(m_Workspace.position -
                    m_Yaw * Vector3.forward * (pullback * App.METERS_TO_UNITS), m_Workspace.rotation);
                int stage = 0;
                try
                {
                    m_RestView = null;
                    SetArmHead(eye.position);
                    stage = 1;
                    var shot = Compile();
                    stage = 2;
                    var view = shot.Frame(camera, eye, fixedEyeSettings, aspect);
                    bool stable = false;
                    for (int iteration = 0; iteration < 4; ++iteration)
                    {
                        m_RestView = view;
                        stage = 3;
                        shot = Compile();
                        stage = 4;
                        var next = shot.Frame(camera, eye, fixedEyeSettings, aspect);
                        if (Quaternion.Angle(next.rotation, view.rotation) < 0.05f)
                        {
                            view = next;
                            stable = true;
                            break;
                        }
                        view = next;
                    }
                    if (!stable) { throw new InvalidOperationException("Cannot find a stable camera-relative arm layout."); }
                    stage = 5;
                    foreach (var item in m_Instructions.Where(i => i.Source.Op == "rest" && i.Hand == 0))
                        if (!WandBelowFrame(item.Path[0], item.RingAngle, view))
                            throw new InvalidOperationException("The resting wand would remain visible in the final frame.");
                    Debug.Log($"[AutomationPosePlan] {m_Script.Name}: {candidates} candidates, {planningTime.Elapsed.TotalSeconds:F2}s");
                    return view;
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
                {
                    if (stage >= failureStage) { failure = exception; failureStage = stage; }
                }
            }
            throw new InvalidOperationException($"Cannot plan '{m_Script.Name}' with comfortable poses and clear framing. {failure?.Message}", failure);
        }

        private bool WandBelowFrame(UnityEngine.Pose wand, float ring, UnityEngine.Pose view)
        {
            Quaternion inverse = Quaternion.Inverse(view.rotation);
            float tangent = Mathf.Tan(App.VrSdk.GetVrCamera().fieldOfView * Mathf.Deg2Rad * 0.5f);
            bool Below(Vector3 point)
            {
                Vector3 local = inverse * (point - view.position);
                return local.y + local.z * tangent < -0.005f * App.METERS_TO_UNITS;
            }
            foreach (var point in m_ControllerBounds[0])
                if (!Below(wand.position + wand.rotation * point)) { return false; }
            foreach (var info in PanelManager.m_Instance.GetAllPanels())
            {
                var panel = info.m_Panel;
                if (!info.AvailableInCurrentMode || !panel.m_Fixed || !panel.gameObject.activeInHierarchy) { continue; }
                var pose = PanelPose(panel, wand, ring);
                foreach (var point in panel.FullScaleRendererPoints())
                    if (!Below(pose.position + pose.rotation * point)) { return false; }
            }
            return true;
        }

        private UnityEngine.Pose PlanWandRest(UnityEngine.Pose start, float ring, UnityEngine.Pose view)
        {
            var from = m_Arms[0].Solve(start.position, start.rotation);
            foreach (float lower in new[] {-0.60f, -0.65f, -0.70f})
            foreach (float depth in new[] {0.15f, 0.08f, 0.02f})
            {
                var pose = m_Arms[0].RelaxedGripPose(m_ArmHead +
                    m_Yaw * (new Vector3(-0.24f, lower, depth) * App.METERS_TO_UNITS), Vector3.zero);
                if (!WandBelowFrame(pose, ring, view)) { continue; }
                try
                {
                    var to = m_Arms[0].Solve(pose.position, pose.rotation);
                    for (int i = 0; i <= 32; ++i) { m_Arms[0].Reach(from, to, i / 32f); }
                    return pose;
                }
                catch (InvalidOperationException) { }
            }
            throw new InvalidOperationException("Cannot withdraw the wand below the recording view with a comfortable reach. Adjust the shot or panel positions.");
        }

        private AutomationShot Compile()
        {
            m_Instructions.Clear();
            var shot = new AutomationShot(m_Script.Name);
            var predicted = (UnityEngine.Pose[])m_Poses.Clone();
            float ring = 0;
            var floating = new Dictionary<BasePanel, UnityEngine.Pose>();
            var sliderValues = new Dictionary<BaseSlider, float>();
            GameObject popupPrefab = null;
            BasePanel popupOwner = null;
            UnityEngine.Pose popupPose = default;
            foreach (var subject in m_Script.Subjects)
            {
                Vector3 origin = Anchor(subject.Anchor, subject.AnchorT);
                Vector3 min = AutomationScript.Vector(subject.Min, "subject min"), max = AutomationScript.Vector(subject.Max, "subject max");
                if (min.x > max.x || min.y > max.y || min.z > max.z) { throw new ArgumentException("Subject min must not exceed max."); }
                var bounds = new Bounds((min + max) / 2 * App.METERS_TO_UNITS, (max - min) * App.METERS_TO_UNITS);
                var framed = shot.AddSubject(subject.Name ?? "Declared subject", AutomationShot.SubjectRole.Artwork);
                for (int i = 0; i < 8; ++i) { framed.AddPoint(origin + m_Yaw * AutomationShot.Corner(bounds, i)); }
            }
            foreach (var stroke in m_Strokes.Values)
            {
                var bounds = new Bounds(stroke.Canvas.Pose * stroke.m_ControlPoints[0].m_Pos, Vector3.zero);
                foreach (var point in stroke.m_ControlPoints) { bounds.Encapsulate(stroke.Canvas.Pose * point.m_Pos); }
                bounds.Expand(2 * stroke.m_BrushSize);
                shot.AddSubject("Prepared stroke", AutomationShot.SubjectRole.Artwork).AddBounds(bounds);
            }
            foreach (var step in ExpandScriptSelections().SelectMany(ExpandMoves))
            {
                var item = new Instruction {Source = step, Hand = step.Hand == "wand" ? 0 : 1,
                    DurationSeconds = step.Seconds / m_Script.Speed};
                if (step.Buttons != null)
                {
                    item.Buttons = step.Buttons.Select(name =>
                    {
                        if (!Enum.TryParse(name, out VrInput input) || !Enum.IsDefined(typeof(VrInput), input) || input == VrInput.Trigger || input == VrInput.Any)
                            throw new ArgumentException($"Unknown button '{name}'; use trigger for the trigger input.");
                        return input;
                    }).ToArray();
                }
                item.Start = predicted[item.Hand];
                var subject = shot.AddSubject(step.Label ?? step.Op);
                if (step.Op == "present" || step.Op == "focus" || step.Op == "click")
                {
                    item.Button = step.Op == "click" && popupPrefab != null
                        ? popupPrefab.GetComponentsInChildren<BaseButton>(true).SingleOrDefault(step.Target.Matches) : null;
                    if (step.Op == "click" && popupPrefab != null && item.Button == null)
                        throw new ArgumentException($"Step {m_Instructions.Count + 1}: the target is not in the open popup.");
                    if (item.Button != null)
                    {
                        item.Panel = popupOwner;
                        item.Deferred = true;
                    }
                    else
                    {
                        item.Button = step.Target.Resolve(false);
                        item.Panel = item.Button?.GetComponentInParent<BasePanel>(true);
                    }
                    if (item.Button == null || item.Panel == null)
                        throw new ArgumentException($"Step {m_Instructions.Count + 1} ({step.Op}): target '{step.Target.Tool ?? step.Target.Brush ?? step.Target.Subtool ?? step.Target.Command ?? step.Target.OpensPanel ?? step.Target.Path}' does not exist in the predicted UI state.");
                    if (step.Op == "present" && item.Panel.m_Fixed)
                    {
                        item.Hand = 0;
                        item.Start = predicted[0];
                        var end = PlanPresentation(item.Panel, ring, predicted[0], out item.Turns);
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
                        shot.AddSubject($"Floating {item.Panel.Type}", AutomationShot.SubjectRole.Panel)
                            .AddPose(pose, item.Panel.FullScaleRendererPoints());
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
                            item.Path = new[] {AimAt(pose.position + pose.rotation * centre, pose.rotation, item)};
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
                                item.Path = new[] {AimAt(pose.position + pose.rotation * local, pose.rotation, item)};
                            }
                            else { item.Path = new[] {Aim(item.Button, pose, true, item)}; }
                            buttonPose = new UnityEngine.Pose(pose.position + pose.rotation * item.Panel.FullScalePoint(item.Button.transform, Vector3.zero),
                                pose.rotation * Quaternion.Inverse(item.Panel.transform.rotation) * item.Button.transform.rotation);
                        }
                        var controls = shot.AddSubject($"Controls on {item.Panel.Type}",
                            item.Panel.m_Fixed ? AutomationShot.SubjectRole.Framing : AutomationShot.SubjectRole.Panel);
                        if (!item.Deferred) { controls.AddPose(pose, item.Panel.FullScaleRendererPoints()); }
                        var tray = item.Button.GetComponentInParent<BaseTray>(true);
                        if (tray != null)
                        {
                            foreach (var renderer in tray.GetComponentsInChildren<Renderer>(true))
                                if (!(renderer is LineRenderer))
                                    for (int i = 0; i < 8; ++i)
                                    {
                                        var point = pose.position + pose.rotation * item.Panel.FullScalePoint(renderer.transform, AutomationShot.Corner(renderer.localBounds, i), true);
                                        subject.AddPoint(point);
                                        controls.AddPoint(point);
                                    }
                        }
                        if (step.Op == "click" && item.Button is OptionButton option && option.AutomationPopupPrefab != null)
                        {
                            popupPrefab = option.AutomationPopupPrefab;
                            popupOwner = item.Panel;
                            popupPose = option.PredictPopupPose(pose);
                            BoxCollider box = popupPrefab.GetComponent<BoxCollider>();
                            if (box != null)
                                for (int i = 0; i < 8; ++i)
                                {
                                    var point = popupPose.position + popupPose.rotation * Quaternion.Inverse(popupPrefab.transform.rotation) *
                                        (box.transform.TransformPoint(AutomationShot.Corner(new Bounds(box.center, box.size), i)) - popupPrefab.transform.position);
                                    subject.AddPoint(point);
                                    controls.AddPoint(point);
                                }
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
                            bool willPlace = m_Script.Steps.Any(s => s.Op == "placePanel" && AutomationScript.PanelType(s.Panel) == panel.Type);
                            shot.AddSubject($"Opened {panel.Type}", willPlace ? AutomationShot.SubjectRole.Framing : AutomationShot.SubjectRole.Panel).AddBounds(bounds);
                            popupPrefab = null;
                            popupOwner = null;
                        }
                        predicted[1] = item.Path[0];
                        if (item.Deferred) { item.Button = null; }
                    }
                }
                else if (step.Op == "slider")
                {
                    item.Slider = UnityEngine.Object.FindObjectsByType<BaseSlider>(FindObjectsInactive.Include)
                        .SingleOrDefault(s => AutomationScript.Selector.HierarchyPath(s.transform) == step.Slider);
                    item.Panel = item.Slider?.GetComponentInParent<BasePanel>(true);
                    if (item.Panel == null || item.Slider.m_Nob == null)
                        throw new ArgumentException($"Slider with knob geometry not found: {step.Slider}");
                    UnityEngine.Pose panelPose;
                    if (item.Panel.m_Fixed) { panelPose = PanelPose(item.Panel, predicted[0], ring); }
                    else if (!floating.TryGetValue(item.Panel, out panelPose))
                        throw new ArgumentException("Open the floating slider panel before dragging its controls.");
                    item.Hand = 1;
                    item.Start = predicted[1];
                    item.SliderStartValue = sliderValues.TryGetValue(item.Slider, out float previousValue) ?
                        previousValue : item.Slider.GetCurrentValue();
                    sliderValues[item.Slider] = step.Value;
                    item.Path = new UnityEngine.Pose[33];
                    for (int i = 0; i < item.Path.Length; ++i)
                    {
                        item.Path[i] = AimSlider(item, panelPose,
                            Mathf.Lerp(item.SliderStartValue, step.Value, i / 32f), true);
                        subject.AddPose(item.Path[i], m_ControllerBounds[1]);
                    }
                    var controls = shot.AddSubject("Slider control", AutomationShot.SubjectRole.Panel);
                    if (item.Slider.GetCollider() is BoxCollider box)
                        for (int i = 0; i < 8; ++i)
                            controls.AddPoint(panelPose.position + panelPose.rotation * item.Panel.FullScalePoint(box.transform,
                                AutomationShot.Corner(new Bounds(box.center, box.size), i)));
                    predicted[1] = item.Path.Last();
                }
                else if (step.Op == "placePanel")
                {
                    var type = AutomationScript.PanelType(step.Panel);
                    item.Panel = floating.Keys.SingleOrDefault(p => p.Type == type);
                    if (item.Panel == null || step.Points == null || step.Points.Length != 1)
                        throw new ArgumentException("placePanel needs an opened floating panel and one destination point.");
                    item.Hand = 1;
                    item.Start = predicted[1];
                    var source = floating[item.Panel];
                    Vector3 target = m_ArmHead + m_Yaw *
                        ((AutomationScript.Vector(step.Points[0], "panel destination") + m_PanelLayoutOffset) * App.METERS_TO_UNITS);
                    Quaternion panelRotation = step.Rotation == null ?
                        Quaternion.LookRotation(target - m_ArmHead, m_Yaw * Vector3.up) :
                        m_Yaw * Quaternion.Euler(AutomationScript.Vector(step.Rotation, "panel destination rotation"));
                    var destination = new UnityEngine.Pose(target, panelRotation);
                    item.PanelDestination = destination.position;
                    item.PanelDestinationRotation = destination.rotation;
                    var box = item.Panel.GetCollider() as BoxCollider;
                    Vector3 center = box == null ? Vector3.zero : item.Panel.FullScalePoint(box.transform, box.center);
                    item.Path = PlanPanelGrip(source, destination, center);
                    var contact = item.Path[0];
                    Vector3 tipLocal = ContactLocal(1, new AutomationScript.Step()).position;
                    var from = m_Arms[1].Solve(item.Start.position, item.Start.rotation);
                    var grab = m_Arms[1].Solve(item.Path[0].position, item.Path[0].rotation);
                    var end = m_Arms[1].Solve(item.Path[1].position, item.Path[1].rotation);
                    for (int i = 0; i <= 32; ++i)
                    {
                        float t = i / 32f;
                        subject.AddPose(m_Arms[1].Reach(from, grab, t), m_ControllerBounds[1]);
                        var pose = m_Arms[1].Reach(grab, end, t);
                        subject.AddPose(pose, m_ControllerBounds[1]);
                        Quaternion rotation = pose.rotation * Quaternion.Inverse(contact.rotation) * source.rotation;
                        subject.AddPose(new UnityEngine.Pose(pose.position + pose.rotation * tipLocal - rotation * center, rotation), item.Panel.FullScaleRendererPoints());
                    }
                    subject.AddPose(source, item.Panel.FullScaleRendererPoints());
                    shot.AddSubject($"Parked {type}", AutomationShot.SubjectRole.Panel).AddPose(destination, item.Panel.FullScaleRendererPoints());
                    floating[item.Panel] = destination;
                    predicted[1] = item.Path[1];
                }
                else if (step.Op == "move")
                {
                    var artwork = shot.AddSubject(step.Label ?? "Tool path", AutomationShot.SubjectRole.Artwork);
                    if (step.Points == null || step.Points.Length == 0 || (step.Curve != "polyline" && step.Curve != "bezier") ||
                        (step.Curve == "bezier" && step.Points.Length != 4))
                        throw new ArgumentException("move needs points and curve polyline, or exactly four points for bezier.");
                    Vector3 anchor = Anchor(step.Anchor, step.AnchorT);
                    Quaternion startRotation = step.Rotation == null ? Quaternion.identity :
                        m_Yaw * Quaternion.Euler(AutomationScript.Vector(step.Rotation, "rotation"));
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
                        item.Path[i] = RootForMotion(item.Hand, new UnityEngine.Pose(point, Quaternion.Slerp(startRotation, endRotation, t)), step);
                        if (step.PaddingMeters < 0 || float.IsNaN(step.PaddingMeters) || float.IsInfinity(step.PaddingMeters))
                            throw new ArgumentException("paddingMeters must be finite and nonnegative.");
                        var bounds = new Bounds(point, Vector3.one * (2 * step.PaddingMeters * App.METERS_TO_UNITS));
                        subject.AddBounds(bounds);
                        artwork.AddBounds(bounds);
                    }
                    predicted[item.Hand] = item.Path.Last();
                    if (item.Path.Length > 1 && (Vector3.Distance(item.Start.position, item.Path[0].position) > 0.005f * App.METERS_TO_UNITS ||
                        Quaternion.Angle(item.Start.rotation, item.Path[0].rotation) > 2))
                        throw new ArgumentException("Approach the first path pose with a single-point move before following a multi-point path.");
                }
                else if (step.Op == "rest")
                {
                    Vector3 restingPoint = item.Hand == 0 ? new Vector3(-0.24f, -0.60f, 0.15f) : new Vector3(0.24f, -0.48f, 0.23f);
                    item.RingAngle = ring;
                    item.Path = new[] {item.Hand == 0 && m_RestView.HasValue ? PlanWandRest(predicted[0], ring, m_RestView.Value) :
                        m_Arms[item.Hand].RelaxedGripPose(m_ArmHead + m_Yaw * (restingPoint * App.METERS_TO_UNITS), Vector3.zero)};
                    predicted[item.Hand] = item.Path[0];
                }
                else if (step.Op == "input" && step.Thumbstick != null)
                {
                    if (step.Thumbstick.Length != 2 || step.Thumbstick.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Mathf.Abs(v) > 1))
                        throw new ArgumentException("thumbstick needs two numbers between -1 and 1.");
                }
                if (item.Path != null)
                {
                    try
                    {
                        foreach (var pose in item.Path) { m_Arms[item.Hand].Solve(pose.position, pose.rotation); }
                        // Endpoint checks alone miss wrist/forearm violations during a joint-space reach.
                        for (int i = 0; i <= 32; ++i) { Evaluate(item, i / 32f); }
                    }
                    catch (InvalidOperationException error)
                    {
                        throw new ArgumentException($"Step {m_Instructions.Count + 1} ({step.Label ?? step.Op}, {(item.Hand == 0 ? "wand" : "brush")} hand): {error.Message}", error);
                    }
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
            foreach (var item in m_Instructions.Where(i => i.Source.Parallel != null).ToArray())
            {
                item.Parallel = m_Instructions.Single(i => ReferenceEquals(i.Source, item.Source.Parallel));
                m_Instructions.Remove(item.Parallel);
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
            if (item.Slider != null) { item.SliderStartValue = item.Slider.GetCurrentValue(); }
            if (item.Parallel != null) { item.Parallel.Start = m_Poses[item.Parallel.Hand]; }
            Vector3 gazeTarget = item.Panel != null ? item.Panel.transform.position :
                item.Path != null ? item.Path.Last().position : m_Workspace.position + m_Workspace.rotation * Vector3.forward;
            Vector3 gazeDirection = gazeTarget - m_ArmHead;
            App.VrSdk.VirtualSimulation.InteractionGaze = gazeDirection.sqrMagnitude > 0.0001f ?
                (Ray?)new Ray(m_ArmHead, gazeDirection.normalized) : m_OriginalGaze;
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
                    float t = Mathf.Clamp01((m_Frames == 1 ? 0 : m_Elapsed) / item.DurationSeconds);
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
                                if (m_TotalElapsed > item.DurationSeconds + 8) { throw new InvalidOperationException("Panel rotation did not settle."); }
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
                            Click(item);
                            break;
                        case "placePanel":
                            PlacePanel(item);
                            break;
                        case "slider":
                            DragSlider(item);
                            break;
                        case "focus":
                            var focusCollider = item.Panel.GetCollider();
                            var focus = AimAt(focusCollider.bounds.center, item.Panel.transform.rotation, item);
                            m_Poses[1] = m_Arms[1].Reach(m_ReachStart, m_Arms[1].Solve(focus.position, focus.rotation), t);
                            if (m_Elapsed >= item.DurationSeconds + 0.45f && SketchControlsScript.m_Instance.IsUserLookingAtPanel(item.Panel)) { Advance(); }
                            else if (m_Elapsed > item.DurationSeconds + 8) { throw new InvalidOperationException("The panel did not acquire focus."); }
                            break;
                        case "move":
                        case "rest":
                            m_Poses[item.Hand] = Evaluate(item, t);
                            if (item.Parallel != null) { m_Poses[item.Parallel.Hand] = Evaluate(item.Parallel, t); }
                            if (t >= 1) { Advance(); }
                            break;
                        case "wait":
                            if (m_Elapsed >= item.DurationSeconds) { Advance(); }
                            break;
                        case "input":
                            if (step.Trigger.HasValue) { m_Triggers[item.Hand] = step.Trigger.Value; }
                            if (step.Thumbstick != null) { m_Sticks[item.Hand] = new Vector2(step.Thumbstick[0], step.Thumbstick[1]); }
                            if (item.Buttons != null) { m_Buttons[item.Hand] = item.Buttons; }
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
                for (int i = 0; i < 2; ++i) { m_Triggers[i] = false; m_Sticks[i] = Vector2.zero; m_Buttons[i] = Array.Empty<VrInput>(); }
                if (++m_ReleaseFrames > 2) { return false; }
            }
            for (int i = 0; i < 2; ++i)
            {
                m_Controllers[i].SetTrigger(m_Triggers[i]);
                m_Controllers[i].SetThumbstick(m_Sticks[i]);
                m_Controllers[i].SetButtons(m_Buttons[i]);
            }
            ApplyPoses();
            return true;
        }

        private void DragSlider(Instruction item)
        {
            if (m_TotalElapsed > item.DurationSeconds * 2 + 8)
                throw new InvalidOperationException("Slider did not accept the native drag.");
            var panel = new UnityEngine.Pose(item.Panel.transform.position, item.Panel.transform.rotation);
            float hoverValue = Mathf.Clamp(item.SliderStartValue, 0.02f, 0.98f);
            float value = m_Stage == 3 ? Mathf.Lerp(item.SliderStartValue, item.Source.Value,
                ProceduralArm.MinimumJerk(m_Elapsed / item.DurationSeconds)) :
                m_Stage > 3 ? item.Source.Value : hoverValue;
            var aim = AimSlider(item, panel, value + item.SliderAimOffset, false);
            m_Poses[1] = m_Stage == 0 ? m_Arms[1].Reach(m_ReachStart,
                m_Arms[1].Solve(aim.position, aim.rotation), m_Elapsed / item.DurationSeconds) : aim;
            if (m_Stage == 0 && m_Elapsed >= item.DurationSeconds) { m_Stage = 1; m_Elapsed = 0; }
            else if (m_Stage == 1)
            {
                if (!item.Slider.isActiveAndEnabled || !item.Slider.HasPointerFocus ||
                    !SketchControlsScript.m_Instance.IsUserLookingAtPanel(item.Panel)) { m_Elapsed = 0; }
                else
                {
                    // Panels can project the controller ray onto a different depth from the
                    // knob. Align to the reticle the native UI actually consumes before pressing.
                    float error = hoverValue - item.Slider.GetValueAtWorldPosition(
                        SketchControlsScript.m_Instance.GetUIReticlePos());
                    item.SliderAimOffset = Mathf.Clamp(item.SliderAimOffset + error * 0.4f, -0.5f, 0.5f);
                    if (Mathf.Abs(error) > 0.005f) { m_Elapsed = 0; }
                }
                if (m_Elapsed >= 0.35f) { m_Triggers[1] = true; m_Stage = 2; m_Elapsed = 0; }
            }
            else if (m_Stage == 2 && m_Elapsed >= 0.16f)
            { item.SliderStartValue = item.Slider.GetCurrentValue(); m_Stage = 3; m_Elapsed = 0; }
            else if (m_Stage == 3 && m_Elapsed >= item.DurationSeconds)
            { m_Triggers[1] = false; m_Stage = 4; m_Elapsed = 0; }
            else if (m_Stage == 4 && m_Elapsed >= 0.35f)
            {
                if (Mathf.Abs(item.Slider.GetCurrentValue() - item.Source.Value) > 0.025f)
                    throw new InvalidOperationException($"Native slider reached {item.Slider.GetCurrentValue():F3}; expected {item.Source.Value:F3}.");
                Advance();
            }
        }

        private void PlacePanel(Instruction item)
        {
            if (m_TotalElapsed > item.DurationSeconds * 2 + 8) { throw new InvalidOperationException("Panel placement timed out."); }
            var grab = m_Arms[1].Solve(item.Path[0].position, item.Path[0].rotation);
            var end = m_Arms[1].Solve(item.Path[1].position, item.Path[1].rotation);
            if (m_Stage == 0)
            {
                m_Poses[1] = m_Arms[1].Reach(m_ReachStart, grab, m_Elapsed / item.DurationSeconds);
                if (m_Elapsed >= item.DurationSeconds) { m_Stage = 1; m_Elapsed = 0; }
            }
            else if (m_Stage == 1)
            {
                m_Poses[1] = item.Path[0];
                if (!item.Panel.WidgetSibling.IsAnimating() && m_Elapsed >= 0.35f)
                { m_Buttons[1] = new[] {VrInput.Grip}; m_Stage = 2; m_Elapsed = 0; }
            }
            else if (m_Stage == 2)
            {
                if (m_Elapsed >= 0.25f && SketchControlsScript.m_Instance.IsUserInteractingWithAnyWidget())
                { m_Stage = 3; m_Elapsed = 0; }
            }
            else if (m_Stage == 3)
            {
                m_Poses[1] = m_Arms[1].Reach(grab, end, m_Elapsed / item.DurationSeconds);
                if (m_Elapsed >= item.DurationSeconds) { m_Stage = 4; m_Elapsed = 0; }
            }
            else
            {
                if (m_Elapsed >= 0.1f) { m_Buttons[1] = Array.Empty<VrInput>(); }
                if (m_Elapsed >= 0.35f)
                {
                    if (Vector3.Distance(item.Panel.transform.position, item.PanelDestination) > 0.025f * App.METERS_TO_UNITS ||
                        Quaternion.Angle(item.Panel.transform.rotation, item.PanelDestinationRotation) > 5)
                        throw new InvalidOperationException("The native panel grab did not reach its planned destination.");
                    Advance();
                }
            }
        }

        private void Click(Instruction item)
        {
            // This state was published by the preceding Tick, so a press cannot
            // disappear when one frame is longer than the requested hold time.
            bool pressWasPublished = m_Triggers[1];
            float hoverSeconds = item.Source.IsRepeatedScriptClick ? 0.05f : 0.45f;
            float pressSeconds = item.Source.IsRepeatedScriptClick ? 0.08f : 0.16f;
            float releaseSeconds = item.Source.IsRepeatedScriptClick ? 0.12f : 0.45f;
            if (item.Hand != 1) { throw new InvalidOperationException("UI clicks use the brush hand."); }
            if (m_TotalElapsed > item.DurationSeconds + 8) { throw new InvalidOperationException("Button did not receive hover/click."); }
            if (m_Stage == 2 && m_Clicked)
            {
                m_Triggers[1] = true;
                if (pressWasPublished && m_Elapsed >= pressSeconds) { m_Triggers[1] = false; m_Stage = 3; m_Elapsed = 0; }
                return;
            }
            if (m_Stage == 3)
            {
                m_Triggers[1] = false;
                if (m_Elapsed >= releaseSeconds)
                {
                    if (!m_Clicked) { throw new InvalidOperationException("The native UI did not accept the click."); }
                    CheckResult(item.Source);
                    Advance();
                }
                return;
            }
            if (item.Button == null && item.Deferred)
            {
                item.Button = item.Panel.PanelPopUp?.GetComponentsInChildren<BaseButton>(true)
                    .SingleOrDefault(item.Source.Target.Matches);
                if (item.Button == null) { m_Elapsed = 0; return; }
                item.Button.Pressed += ButtonPressed;
            }
            if (item.Button == null) { throw new InvalidOperationException("The target control was destroyed before it was clicked."); }
            var collider = item.Button.GetCollider() ?? item.Button.GetComponent<Collider>();
            bool ready = item.Button.isActiveAndEnabled && collider != null && collider.enabled &&
                collider.bounds.size.sqrMagnitude >= 0.000001f;
            // Approach the expanded target even while hover-driven controls are collapsed.
            // Waiting for their collider before moving would require a separate panel-focus detour.
            var aim = Aim(item.Button, new UnityEngine.Pose(item.Panel.transform.position, item.Panel.transform.rotation), m_Stage == 0 || !ready, item);
            if (m_Stage == 0)
            {
                if (m_Triggers[1]) { throw new InvalidOperationException("Release the brush trigger before clicking."); }
                m_Poses[1] = m_Arms[1].Reach(m_ReachStart, m_Arms[1].Solve(aim.position, aim.rotation), m_Elapsed / item.DurationSeconds);
                if (m_Elapsed >= item.DurationSeconds) { m_Stage = 1; m_Elapsed = 0; }
                return;
            }
            m_Poses[1] = aim;
            if (!ready) { m_Elapsed = 0; return; }
            if (m_Stage == 1)
            {
                // Releasing a button clears its Hover state even when the ray stays on it.
                if ((!item.Button.IsHover() && !item.Button.HasPointerFocus) ||
                    !SketchControlsScript.m_Instance.IsUserLookingAtPanel(item.Panel)) { m_Elapsed = 0; }
                if (m_Elapsed >= hoverSeconds) { m_Stage = 2; m_Elapsed = 0; }
            }
            else if (m_Stage == 2)
            {
                m_Triggers[1] = true;
                if (pressWasPublished && m_Elapsed >= pressSeconds) { m_Triggers[1] = false; m_Stage = 3; m_Elapsed = 0; }
            }
            else if (m_Elapsed >= releaseSeconds)
            {
                if (!m_Clicked) { throw new InvalidOperationException("The native UI did not accept the click."); }
                CheckResult(item.Source);
                Advance();
            }
        }

        private void CheckResult(AutomationScript.Step step)
        {
            if (!string.IsNullOrEmpty(step.Brush) && BrushController.m_Instance.ActiveBrush.DurableName != step.Brush)
                throw new InvalidOperationException($"Expected active brush {step.Brush}.");
            if (!string.IsNullOrEmpty(step.ScriptName) &&
                (!Enum.TryParse(step.Category, out LuaApiCategory category) || LuaManager.Instance.GetActiveScriptName(category) != step.ScriptName))
                throw new InvalidOperationException($"Expected active {step.Category}: {step.ScriptName}.");
            if (!string.IsNullOrEmpty(step.Symmetry) && PointerManager.m_Instance.CurrentSymmetryMode.ToString() != step.Symmetry)
                throw new InvalidOperationException($"Expected symmetry mode {step.Symmetry}.");
            if (step.StraightEdge.HasValue && PointerManager.m_Instance.StraightEdgeModeEnabled != step.StraightEdge.Value)
                throw new InvalidOperationException("Unexpected straight-edge state.");
            if (step.EndpointSnap.HasValue && PointerManager.m_Instance.StraightEdgeGuide.EndpointSnappingEnabled != step.EndpointSnap.Value)
                throw new InvalidOperationException("Unexpected endpoint snapping state.");
            if (step.LastStrokeEndpoints != null)
            {
                if (step.LastStrokeEndpoints.Length != 2) { throw new ArgumentException("lastStrokeEndpoints requires two points."); }
                AutomationScript.Positive(step.EndpointToleranceMeters, "endpointToleranceMeters", 0.00001f, 1);
                Stroke latest = SketchMemoryScript.m_Instance.MostRecentStroke;
                if (latest == null) { throw new InvalidOperationException("No latest stroke for endpoint assertion."); }
                for (int i = 0; i < 2; ++i)
                {
                    Vector3 expected = Point(AutomationScript.Vector(step.LastStrokeEndpoints[i], "lastStrokeEndpoints"));
                    Vector3 actual = latest.Canvas.Pose * latest.m_ControlPoints[i == 0 ? 0 : latest.m_ControlPoints.Length - 1].m_Pos;
                    if (Vector3.Distance(expected, actual) * App.UNITS_TO_METERS > step.EndpointToleranceMeters)
                        throw new InvalidOperationException($"Latest stroke endpoint {i} is outside the expected tolerance.");
                }
            }
            if (!string.IsNullOrEmpty(step.Panel) && step.PanelOpen.HasValue &&
                PanelManager.m_Instance.IsPanelOpen(AutomationScript.PanelType(step.Panel)) != step.PanelOpen.Value)
                throw new InvalidOperationException($"Unexpected visibility for panel {step.Panel}.");
            if (!string.IsNullOrEmpty(step.Tool) && SketchSurfacePanel.m_Instance.GetCurrentToolType().ToString() != step.Tool)
                throw new InvalidOperationException($"Expected active tool {step.Tool}.");
            if (!string.IsNullOrEmpty(step.Subtool) && SculptSubToolManager.m_Instance?.ActiveSubTool.ToString() != step.Subtool)
                throw new InvalidOperationException($"Expected active subtool {step.Subtool}.");
            if (step.StrokeCountDelta.HasValue && SketchMemoryScript.m_Instance.StrokeCount - m_InitialStrokeCount != step.StrokeCountDelta.Value)
                throw new InvalidOperationException($"Stroke count delta is {SketchMemoryScript.m_Instance.StrokeCount - m_InitialStrokeCount}; expected {step.StrokeCountDelta.Value}.");
            if (step.VisibleStrokeCountDelta.HasValue)
            {
                int visible = SketchMemoryScript.m_Instance.GetMemoryList.Count(s => s.IsGeometryEnabled) - m_InitialVisibleStrokeCount;
                if (visible != step.VisibleStrokeCountDelta.Value)
                    throw new InvalidOperationException($"Visible stroke count delta is {visible}; expected {step.VisibleStrokeCountDelta.Value}.");
            }
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
            App.VrSdk.VirtualSimulation.UpdateView();
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
            finally
            {
                var simulation = App.Instance != null ? App.VrSdk?.VirtualSimulation : null;
                if (simulation != null)
                {
                    simulation.InteractionGaze = m_OriginalGaze;
                    simulation.SetViewAspect(m_OriginalViewAspect);
                }
                m_Tracking.Dispose();
            }
        }
    }
}
