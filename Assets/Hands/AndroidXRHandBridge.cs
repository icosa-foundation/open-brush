using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SpatialTracking;
using UnityEngine.XR.Hands;

namespace TiltBrush
{
    /// <summary>
    /// Android XR Hand Interaction bridge for Open Brush.
    ///
    /// Hybrid Android XR bridge.
    ///
    /// INPUT:
    ///     Hand Interaction (XR_EXT_hand_interaction)
    ///       pinchValue -> drawing
    ///       graspValue -> Grip / swimming
    ///
    /// SPATIAL POSE:
    ///     XRHandSubsystem joints reproduce the previously-working Open Brush mapping:
    ///       Left Palm                    -> Wand / panels
    ///       Right IndexDistal -> IndexTip -> Brush pointer / teleport aim
    ///       Right IndexTip               -> direct UI touch
    ///
    /// Physical controllers remain selectable independently per side.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class AndroidXRHandBridge : MonoBehaviour
    {
        public static AndroidXRHandBridge Instance { get; private set; }

        // --------------------------------------------------------------------
        // Inspector
        // --------------------------------------------------------------------

        [Header("Tracking Space")]
        [Tooltip("The same XR Origin / tracking origin used by the Open Brush camera.")]
        public Transform trackingOrigin;


        [Header("Hand Interaction - Permission Free")]

        [Tooltip("pinchValue required to START drawing.")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float pinchPressThreshold = 0.75f;

        [Tooltip("pinchValue below this releases drawing. Lower than press threshold for hysteresis.")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float pinchReleaseThreshold = 0.55f;

        [Tooltip("graspValue required on BOTH hands to START swimming/grab mode.")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float graspPressThreshold = 0.75f;

        [Tooltip("graspValue below this releases the hand Grip state.")]
        [Range(0.0f, 1.0f)]
        [SerializeField]
        private float graspReleaseThreshold = 0.55f;


        [Header("Hybrid Controller / Hand Routing")]

        [Tooltip(
            "When a real tracked controller and a tracked hand are both available on the same side, " +
            "prefer the physical controller. Disable only for hand-first experiments.")]
        [SerializeField]
        private bool preferPhysicalControllers = true;

        [Tooltip(
            "How long a new source must remain available before switching between hand and controller. " +
            "Prevents momentary tracking drops from causing source flicker.")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float sourceSwitchDelay = 0.18f;

        [Tooltip(
            "How often to rescan the Input System for Hand Interaction and physical controller devices.")]
        [SerializeField]
        [Range(0.1f, 5.0f)]
        private float deviceSearchInterval = 0.75f;


        [Header("Menu Touch - Right Index")]

        [Tooltip(
            "Radius in metres around the right index fingertip used to detect direct " +
            "contact with Open Brush panel/UI colliders.")]
        [SerializeField]
        private float menuTouchRadius = 0.012f;

        [Tooltip(
            "When the fingertip contacts UI, place Open Brush's pointer ray origin " +
            "this far behind the fingertip along the poke direction. This keeps the " +
            "ray origin outside the button collider so Open Brush can register hover/click.")]
        [SerializeField]
        private float menuTouchPointerBackoff = 0.020f;

        [Tooltip("Physics layers considered by right-index direct menu touch.")]
        [SerializeField]
        private LayerMask menuTouchLayerMask = ~0;

        [Tooltip(
            "Whether direct menu touch should include trigger colliders. " +
            "Collide is recommended for Open Brush UI.")]
        [SerializeField]
        private QueryTriggerInteraction menuTouchQueryTriggerInteraction =
            QueryTriggerInteraction.Collide;


        [Header("Brush / Right Hand")]

        [Tooltip(
            "Rotation correction applied after aligning the Open Brush pointer " +
            "with the right index finger. Start with (0, 0, 0).")]
        public Vector3 brushRotationOffset;

        [Tooltip(
            "Additional offset in pointer local space after placing the Open Brush " +
            "PointerAttachPoint at the right index fingertip.")]
        public Vector3 brushPositionOffset;


        [Header("Wand / Left Hand")]

        [Tooltip(
            "Optional local rotation correction after the Android XR left-hand grip pose. " +
            "Keep this at (0,0,0) first. The Wand root and all attached panels will " +
            "follow the hand rotation, including wrist roll.")]
        public Vector3 wandRotationOffset;

        [Tooltip("Local-space offset of the virtual Wand relative to the hand grip/palm pose.")]
        public Vector3 wandPositionOffset;

        // Legacy serialized fields from v2-v7. Retained only for scene/prefab
        // compatibility; the restored v8 spatial path does not use either one.
        [HideInInspector]
        [SerializeField]
        private bool useGripPoseForWand = true;

        [HideInInspector]
        public bool useAimPoseForWand = true;


        [Header("Restored Working Spatial Mapping")]

        [Tooltip(
            "Use XRHandSubsystem Palm / Index joints for Wand, teleport pointer and " +
            "fingertip UI location. This reproduces the previously working bridge pose.")]
        [SerializeField]
        private bool useXRHandsForSpatialPose = true;

        [Tooltip(
            "Ignore old serialized Wand/Brush pose offsets while using the restored mapping. " +
            "Leave ON initially so experiments from v5-v7 cannot keep the UI flipped.")]
        [SerializeField]
        private bool useExactRestoredPose = true;

        [Tooltip(
            "Use Open Brush's known OculusTouch controller hierarchy only as an INTERNAL " +
            "geometry/anchor scaffold. Input still comes from the selected physical controller " +
            "or hand independently per side.")]
        [SerializeField]
        private bool useLegacyOpenBrushAnchorGeometry = true;


        [Header("Open Brush")]

        [Tooltip("Show normal Open Brush brush/color/tool panels on the left-hand Wand.")]
        public bool showPanels = true;

        [Tooltip("Hide the fake Oculus/Quest controller models used internally by Open Brush.")]
        public bool hideControllerModels = true;


        [Header("Debug")]

        public bool debugLogging = true;

        [Tooltip("Seconds between state diagnostic messages.")]
        [SerializeField]
        private float debugStateInterval = 1.0f;


        // --------------------------------------------------------------------
        // Runtime state
        // --------------------------------------------------------------------

        private HandInteractionBinding m_LeftBinding;
        private HandInteractionBinding m_RightBinding;

        // XR Hands is used only for the spatial mapping that was known to work:
        // Palm orientation/position and IndexDistal -> IndexTip direction.
        private XRHandSubsystem m_XRHandSubsystem;
        private readonly List<XRHandSubsystem> m_XRHandSubsystems = new();
        private float m_NextXRHandsSearchTime;

        private ControllerBinding m_LeftControllerBinding;
        private ControllerBinding m_RightControllerBinding;

        private InputSourceKind m_LeftSource = InputSourceKind.None;
        private InputSourceKind m_RightSource = InputSourceKind.None;
        private InputSourceKind m_LeftCandidateSource = InputSourceKind.None;
        private InputSourceKind m_RightCandidateSource = InputSourceKind.None;
        private float m_LeftCandidateSince;
        private float m_RightCandidateSince;

        private bool m_OpenBrushHandModeInitialized;
        private bool m_ControllersPrepared;
        private bool m_PanelsRequested;

        private float m_NextDebugStateTime;
        private float m_NextDeviceSearchTime;

        private readonly HandState m_Left = new HandState();
        private readonly HandState m_Right = new HandState();

        private const int kMenuTouchHitBufferSize = 32;
        private readonly Collider[] m_MenuTouchHits =
            new Collider[kMenuTouchHitBufferSize];

        private bool m_MenuTouchHeld;
        private bool m_MenuTouchDown;
        private bool m_MenuTouchUp;
        private Collider m_MenuTouchCollider;

        // Direct fingertip UI target. We invoke the touched BaseButton directly
        // instead of routing poke through VrInput.Trigger, because Open Brush
        // shares Trigger between UI and painting.
        private BaseButton m_MenuTouchComponent;
        private BaseButton m_ActiveMenuTouchComponent;

        // Open Brush Trigger is reserved for painting while hand input is active:
        //   pinch -> drawing
        //   poke  -> direct UIComponent press/release (never a paint trigger)
        private bool m_PrimaryTriggerHeld;
        private bool m_PrimaryTriggerDown;
        private bool m_PrimaryTriggerUp;

        // Per-hand OpenXR grasp -> Open Brush Grip. Open Brush then gets the
        // same semantics as physical controllers: one grip can move the world,
        // two grips can scale/rotate it.
        private bool m_LeftGripHeld;
        private bool m_LeftGripDown;
        private bool m_LeftGripUp;
        private bool m_RightGripHeld;
        private bool m_RightGripDown;
        private bool m_RightGripUp;

        // Convenience diagnostics: true when both currently selected hand
        // sources are gripping at once.
        private bool m_SwimModeHeld;


        private enum InputSourceKind
        {
            None,
            Controller,
            Hand
        }


        private sealed class ControllerBinding
        {
            public InputDevice device;
            public ButtonControl isTracked;
            public IntegerControl trackingState;

            public bool IsValid =>
                device != null &&
                device.added &&
                device.enabled;
        }


        private sealed class HandInteractionBinding
        {
            public InputDevice device;

            public ButtonControl isTracked;
            public IntegerControl trackingState;

            public Vector3Control devicePosition;
            public QuaternionControl deviceRotation;

            public Vector3Control pokePosition;
            public QuaternionControl pokeRotation;

            public Vector3Control pinchPosition;
            public QuaternionControl pinchRotation;

            public Vector3Control pointerPosition;
            public QuaternionControl pointerRotation;

            public AxisControl pinchValue;
            public ButtonControl pinchReady;

            public AxisControl graspValue;
            public ButtonControl graspReady;

            public bool IsValid =>
                device != null &&
                device.added &&
                device.enabled;
        }


        private class HandState
        {
            public bool tracked;

            // Raw right-hand draw state (pinch only).
            public bool trigger;
            public bool triggerDown;
            public bool triggerUp;

            public float pinchValue;
            public float graspValue;

            // Kept for compatibility with existing bridge diagnostics/API.
            public float indexMiddleDistance = float.PositiveInfinity;

            // Grip/device pose. This is not raw palm-joint data.
            public Pose palmPose;

            // Runtime-defined OpenXR aim pose. OpenXR aim uses -Z as forward.
            public Pose aimPose;
            public bool aimPoseValid;

            // Permission-free poke pose is the best index interaction point.
            public Pose indexTipPose;

            // Runtime / XR Hands index direction in Unity world space.
            public bool indexDirectionValid;
            public Vector3 indexDirectionWorld = Vector3.forward;

            // True when Palm/Index spatial data was replaced this frame by
            // XRHandSubsystem joint poses from the restored working mapping.
            public bool xrHandsSpatialValid;
        }


        // --------------------------------------------------------------------
        // Public bridge API used by UnityXRControllerInfo
        // --------------------------------------------------------------------

        /// <summary>
        /// True while at least one side is currently routed to a hand.
        /// Use UseHand(isBrush) for the per-side decision.
        /// </summary>
        public static bool Active =>
            Instance != null &&
            (Instance.m_LeftSource == InputSourceKind.Hand ||
             Instance.m_RightSource == InputSourceKind.Hand);


        /// <summary>
        /// Returns true only when this side is currently routed to Hand Interaction.
        /// isBrush == false -> left Wand, true -> right Brush.
        /// </summary>
        public static bool UseHand(bool isBrush)
        {
            if (Instance == null)
                return false;

            return isBrush
                ? Instance.m_RightSource == InputSourceKind.Hand
                : Instance.m_LeftSource == InputSourceKind.Hand;
        }


        public static bool PhysicalControllerActive(bool isBrush)
        {
            if (Instance == null)
                return false;

            return isBrush
                ? Instance.m_RightSource == InputSourceKind.Controller
                : Instance.m_LeftSource == InputSourceKind.Controller;
        }


        public static bool LeftTracked =>
            UseHand(false) &&
            Instance.m_Left.tracked;


        public static bool RightTracked =>
            UseHand(true) &&
            Instance.m_Right.tracked;


        /// <summary>
        /// Tracking validity for the HAND path only. UnityXRControllerInfo should
        /// call this only when UseHand(isBrush) is true; otherwise it should use
        /// the normal physical controller validity path.
        /// </summary>
        public static bool IsTracked(bool isBrush)
        {
            if (!UseHand(isBrush))
                return false;

            return isBrush
                ? Instance.m_Right.tracked
                : Instance.m_Left.tracked;
        }


        /// <summary>
        /// Open Brush trigger for the right HAND source only.
        /// Physical controllers keep their normal TriggerAxis path.
        /// </summary>
        public static bool Trigger(bool isBrush)
        {
            return UseHand(isBrush) &&
                   isBrush &&
                   Instance.m_PrimaryTriggerHeld;
        }


        public static float TriggerValue(bool isBrush)
        {
            return Trigger(isBrush) ? 1.0f : 0.0f;
        }


        public static bool TriggerDown(bool isBrush)
        {
            return UseHand(isBrush) &&
                   isBrush &&
                   Instance.m_PrimaryTriggerDown;
        }


        public static bool TriggerUp(bool isBrush)
        {
            return UseHand(isBrush) &&
                   isBrush &&
                   Instance.m_PrimaryTriggerUp;
        }


        /// <summary>
        /// Hand grasp maps to Grip independently per side. This mirrors physical
        /// controller behaviour and allows mixed controller+hand swimming.
        /// </summary>
        public static bool Grip(bool isBrush)
        {
            if (!UseHand(isBrush))
                return false;

            return isBrush
                ? Instance.m_RightGripHeld
                : Instance.m_LeftGripHeld;
        }


        public static bool GripDown(bool isBrush)
        {
            if (!UseHand(isBrush))
                return false;

            return isBrush
                ? Instance.m_RightGripDown
                : Instance.m_LeftGripDown;
        }


        public static bool GripUp(bool isBrush)
        {
            if (!UseHand(isBrush))
                return false;

            return isBrush
                ? Instance.m_RightGripUp
                : Instance.m_LeftGripUp;
        }


        public static float GripValue(bool isBrush)
        {
            if (!UseHand(isBrush))
                return 0.0f;

            HandState state =
                isBrush
                    ? Instance.m_Right
                    : Instance.m_Left;

            return state.tracked
                ? state.graspValue
                : 0.0f;
        }


        public static float FingerDistance(bool isBrush)
        {
            return float.PositiveInfinity;
        }


        public static bool RightDrawHeld =>
            UseHand(true) && Instance.m_Right.trigger;

        public static bool RightDrawDown =>
            UseHand(true) && Instance.m_Right.triggerDown;

        public static bool RightDrawUp =>
            UseHand(true) && Instance.m_Right.triggerUp;


        public static bool MenuTouchHeld =>
            UseHand(true) && Instance.m_MenuTouchHeld;

        public static bool MenuTouchDown =>
            UseHand(true) && Instance.m_MenuTouchDown;

        public static bool MenuTouchUp =>
            UseHand(true) && Instance.m_MenuTouchUp;

        public static Collider MenuTouchCollider =>
            UseHand(true) ? Instance.m_MenuTouchCollider : null;

        public static Vector3 RightIndexTipPosition =>
            RightTracked
                ? Instance.m_Right.indexTipPose.position
                : Vector3.zero;

        public static bool RightIndexDirectionValid =>
            RightTracked &&
            Instance.m_Right.indexDirectionValid;

        public static Vector3 RightIndexDirection =>
            RightIndexDirectionValid
                ? Instance.m_Right.indexDirectionWorld
                : Vector3.forward;


        /// <summary>
        /// Permission-free whole-hand visual anchor.
        ///
        /// Position comes from the OpenXR grip/palm centroid. Rotation is rebuilt
        /// into a hand-friendly semantic basis instead of copying the raw grip
        /// quaternion directly:
        ///
        ///   local +Z = finger / aim direction
        ///   local +Y = outward palm normal
        ///
        /// OpenXR grip +X points OUT of the left palm but INTO the right palm,
        /// hence the handedness-dependent sign below.
        ///
        /// This is still only a coarse whole-hand pose. Per-finger articulation
        /// requires XR hand joints and therefore the Hand Tracking subsystem.
        /// </summary>
        public static bool TryGetHandVisualPose(bool isBrush, out Pose pose)
        {
            pose = default;

            if (!UseHand(isBrush) || Instance == null)
                return false;

            HandState state = isBrush ? Instance.m_Right : Instance.m_Left;
            if (!state.tracked)
                return false;

            Vector3 forward =
                state.indexDirectionValid
                    ? state.indexDirectionWorld
                    : state.aimPose.rotation * Vector3.back;

            if (forward.sqrMagnitude < 0.000001f)
                forward = state.palmPose.rotation * Vector3.back;

            forward.Normalize();

            // OpenXR grip +X: away from left palm, into right palm.
            Vector3 palmOut =
                state.palmPose.rotation *
                (isBrush ? Vector3.left : Vector3.right);

            Vector3 up =
                Vector3.ProjectOnPlane(
                    palmOut,
                    forward);

            if (up.sqrMagnitude < 0.000001f)
            {
                up =
                    Vector3.ProjectOnPlane(
                        state.palmPose.rotation * Vector3.up,
                        forward);
            }

            if (up.sqrMagnitude < 0.000001f)
                up = Vector3.up;

            up.Normalize();

            pose =
                new Pose(
                    state.palmPose.position,
                    Quaternion.LookRotation(
                        forward,
                        up));

            return true;
        }

        public static float HandPinchValue(bool isBrush)
        {
            if (!UseHand(isBrush) || Instance == null)
                return 0.0f;

            return isBrush
                ? Instance.m_Right.pinchValue
                : Instance.m_Left.pinchValue;
        }

        public static float HandGraspValue(bool isBrush)
        {
            if (!UseHand(isBrush) || Instance == null)
                return 0.0f;

            return isBrush
                ? Instance.m_Right.graspValue
                : Instance.m_Left.graspValue;
        }

        private bool HasAnyHandInteractionDevice =>
            (m_LeftBinding != null && m_LeftBinding.IsValid) ||
            (m_RightBinding != null && m_RightBinding.IsValid);

        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Awake()
        {
            Instance = this;

            // Do this in Awake (not only Start) so other Open Brush Awake paths
            // already see AndroidXRHandBridge.Active when OpenXR has created the
            // Hand Interaction devices.
            FindInputDevices(forceLog: true);

            Debug.Log(
                "ANDROID_XR_HAND: AWAKE - hybrid controller / Hand Interaction bridge active");
        }


        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
            InputSystem.onDeviceChange += OnInputDeviceChange;
            FindInputDevices(forceLog: false);
        }


        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
            InputSystem.onDeviceChange -= OnInputDeviceChange;
        }


        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;
            InputSystem.onDeviceChange -= OnInputDeviceChange;

            if (Instance == this)
            {
                Instance = null;
            }
        }


        private void Start()
        {
            Debug.Log("ANDROID_XR_HAND: START - hybrid controller / Hand Interaction mode");

            ForceOpenBrushSixDofModeEarly();

            FindInputDevices(forceLog: true);
        }


        private void Update()
        {
            if (Time.unscaledTime >= m_NextDeviceSearchTime)
            {
                FindInputDevices(forceLog: false);

                m_NextDeviceSearchTime =
                    Time.unscaledTime +
                    Mathf.Max(
                        0.1f,
                        deviceSearchInterval);
            }

            EnsureOpenBrushHandMode();

            if (!m_OpenBrushHandModeInitialized)
            {
                DebugStateIfNeeded();
                return;
            }

            PrepareOpenBrushControllers();

            if (!m_ControllersPrepared)
            {
                DebugStateIfNeeded();
                return;
            }

            bool wasDrawingBeforeHandUpdate =
                m_Right.trigger;

            // Always sample Hand Interaction in the background. This lets a hand
            // become the candidate source as soon as a controller is put down.
            UpdateInteractionHand(
                m_LeftBinding,
                m_Left,
                allowDraw: false);

            UpdateInteractionHand(
                m_RightBinding,
                m_Right,
                allowDraw: true);

            // Restore the exact spatial source that previously gave correct
            // Wand orientation, teleport aim and fingertip/button alignment.
            // Pinch/grasp values above remain sourced from Hand Interaction.
            UpdateXRHandsSpatialPoses();

            UpdateSourceSelection();

            ApplySourceRouting();

            UpdateGripStates();

            // Right-hand UI contact is meaningful only while the right side is
            // actually routed to the hand.
            if (UseHand(true))
            {
                UpdateMenuTouchState();

                if (m_MenuTouchHeld ||
                    m_MenuTouchUp)
                {
                    // Direct poke always wins over painting. Suppress pinch draw
                    // on both the contact and release frames so touching/lifting
                    // from a Wand button cannot leave a stray brush mark.
                    SuppressDrawForMenuContact(
                        wasDrawingBeforeHandUpdate);
                }
            }
            else
            {
                ResetMenuTouchState();
            }

            UpdatePrimaryTriggerState();

            DriveOpenBrushControllers();

            RequestPanels();

            DebugStateIfNeeded();
        }


        private void LateUpdate()
        {
            if (!m_OpenBrushHandModeInitialized ||
                !m_ControllersPrepared)
            {
                return;
            }


            /*
             * Keep Open Brush in six-DoF mode.
             * Other Open Brush startup/runtime paths may assign this value.
             */
            if (SketchControlsScript.m_Instance != null)
            {
                SketchControlsScript.m_Instance.ActiveControlsType =
                    SketchControlsScript.ControlsType.SixDofControllers;
            }


            /*
             * Fixed panels are normally positioned from:
             *
             *     InputManager.Wand.Geometry.MainAxisAttachPoint
             *
             * Since the Wand now follows the left palm, this makes the panels
             * follow the left hand.
             */
            if (showPanels &&
                UseHand(false) &&
                m_Left.tracked &&
                PanelManager.m_Instance != null &&
                PanelManager.m_Instance.GetAllPanels() != null)
            {
                PanelManager.m_Instance.LockPanelsToController();
            }
        }


        /// <summary>
        /// onBeforeRender happens later than normal Update/LateUpdate rendering
        /// preparation, so it is a reliable place to force the fake controller
        /// renderers off if Open Brush turns them back on during the frame.
        /// </summary>
        private void OnBeforeRender()
        {
            if (!hideControllerModels ||
                !m_ControllersPrepared)
            {
                return;
            }

            HideHandControllerVisuals();
        }


        // --------------------------------------------------------------------
        // Early Open Brush mode setup
        // --------------------------------------------------------------------

        private void ForceOpenBrushSixDofModeEarly()
        {
            if (SketchControlsScript.m_Instance == null)
            {
                Debug.LogWarning(
                    "ANDROID_XR_HAND: SketchControlsScript.m_Instance was null in Start(). " +
                    "Will keep trying from Update().");
                return;
            }

            SketchControlsScript.m_Instance.ActiveControlsType =
                SketchControlsScript.ControlsType.SixDofControllers;

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: Open Brush controls forced to SixDofControllers " +
                    "before SketchControlsScript.Start()");
            }
        }


        // --------------------------------------------------------------------
        // Permission-free OpenXR Hand Interaction device discovery
        // --------------------------------------------------------------------

        private void OnInputDeviceChange(
            InputDevice device,
            InputDeviceChange change)
        {
            // Controllers and Hand Interaction devices can appear/disappear as
            // the user puts controllers down or picks them back up.
            FindInputDevices(forceLog: true);
        }


        private void FindInputDevices(
            bool forceLog)
        {
            HandInteractionBinding foundLeftHand = null;
            HandInteractionBinding foundRightHand = null;
            ControllerBinding foundLeftController = null;
            ControllerBinding foundRightController = null;

            foreach (InputDevice device in InputSystem.devices)
            {
                bool isLeft = HasUsage(device, "LeftHand");
                bool isRight = HasUsage(device, "RightHand");

                string identity =
                    ((device.name ?? string.Empty) + " " +
                     (device.displayName ?? string.Empty)).ToLowerInvariant();

                if (!isLeft && !isRight)
                {
                    isLeft = identity.Contains("left");
                    isRight = identity.Contains("right");
                }

                if (IsHandInteractionDevice(device))
                {
                    if (isLeft && foundLeftHand == null)
                        foundLeftHand = BindHandInteractionDevice(device);

                    if (isRight && foundRightHand == null)
                        foundRightHand = BindHandInteractionDevice(device);

                    continue;
                }

                if (!IsPhysicalControllerDevice(device))
                    continue;

                ControllerBinding controller =
                    BindControllerDevice(device);

                if (isLeft && foundLeftController == null)
                    foundLeftController = controller;

                if (isRight && foundRightController == null)
                    foundRightController = controller;
            }

            bool changed =
                !SameDevice(m_LeftBinding?.device, foundLeftHand?.device) ||
                !SameDevice(m_RightBinding?.device, foundRightHand?.device) ||
                !SameDevice(m_LeftControllerBinding?.device, foundLeftController?.device) ||
                !SameDevice(m_RightControllerBinding?.device, foundRightController?.device);

            m_LeftBinding = foundLeftHand;
            m_RightBinding = foundRightHand;
            m_LeftControllerBinding = foundLeftController;
            m_RightControllerBinding = foundRightController;

            if (debugLogging && (forceLog || changed))
            {
                Debug.Log(
                    "ANDROID_XR_HAND: hybrid device discovery " +
                    $"leftHand={DescribeBinding(m_LeftBinding)} " +
                    $"rightHand={DescribeBinding(m_RightBinding)} " +
                    $"leftController={DescribeControllerBinding(m_LeftControllerBinding)} " +
                    $"rightController={DescribeControllerBinding(m_RightControllerBinding)}");
            }
        }


        private static bool SameDevice(
            InputDevice a,
            InputDevice b)
        {
            return a == b;
        }


        private static bool IsPhysicalControllerDevice(
            InputDevice device)
        {
            if (device == null ||
                IsHandInteractionDevice(device))
            {
                return false;
            }

            string layout = device.layout ?? string.Empty;
            string name = device.name ?? string.Empty;
            string display = device.displayName ?? string.Empty;

            bool looksLikeController =
                layout.IndexOf(
                    "XRController",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                layout.IndexOf(
                    "TrackedDevice",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf(
                    "Controller",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                display.IndexOf(
                    "Controller",
                    StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksLikeController)
                return false;

            // Exclude common non-hand-controller tracked devices.
            string identity =
                (layout + " " + name + " " + display).ToLowerInvariant();

            return !identity.Contains("hmd") &&
                   !identity.Contains("head") &&
                   !identity.Contains("eye") &&
                   !identity.Contains("hand interaction");
        }


        private static ControllerBinding BindControllerDevice(
            InputDevice device)
        {
            if (device == null)
                return null;

            return new ControllerBinding
            {
                device = device,
                isTracked =
                    FindControl<ButtonControl>(
                        device,
                        "isTracked"),
                trackingState =
                    FindControl<IntegerControl>(
                        device,
                        "trackingState")
            };
        }


        private static bool IsControllerTracked(
            ControllerBinding binding)
        {
            if (binding == null || !binding.IsValid)
                return false;

            if (binding.isTracked != null &&
                binding.isTracked.isPressed)
            {
                return true;
            }

            if (binding.trackingState != null &&
                binding.trackingState.ReadValue() != 0)
            {
                return true;
            }

            return false;
        }


        private static string DescribeControllerBinding(
            ControllerBinding binding)
        {
            if (binding == null || binding.device == null)
                return "none";

            return
                $"'{binding.device.name}' layout='{binding.device.layout}' " +
                $"id={binding.device.deviceId} " +
                $"tracked={IsControllerTracked(binding)}";
        }


        private static bool IsHandInteractionDevice(
            InputDevice device)
        {
            if (device == null)
                return false;

            string layout = device.layout ?? string.Empty;
            string name = device.name ?? string.Empty;
            string display = device.displayName ?? string.Empty;

            return layout.IndexOf(
                       "HandInteraction",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf(
                       "Hand Interaction",
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                   display.IndexOf(
                       "Hand Interaction",
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }


        private static bool HasUsage(
            InputDevice device,
            string usageName)
        {
            if (device == null)
                return false;

            foreach (var usage in device.usages)
            {
                if (string.Equals(
                        usage.ToString(),
                        usageName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        private static HandInteractionBinding BindHandInteractionDevice(
            InputDevice device)
        {
            if (device == null)
                return null;

            return new HandInteractionBinding
            {
                device = device,

                isTracked =
                    FindControl<ButtonControl>(
                        device,
                        "isTracked"),

                trackingState =
                    FindControl<IntegerControl>(
                        device,
                        "trackingState"),

                devicePosition =
                    FindControl<Vector3Control>(
                        device,
                        "devicePosition",
                        "devicePose/position"),

                deviceRotation =
                    FindControl<QuaternionControl>(
                        device,
                        "deviceRotation",
                        "devicePose/rotation"),

                pokePosition =
                    FindControl<Vector3Control>(
                        device,
                        "pokePosition",
                        "pokePose/position"),

                pokeRotation =
                    FindControl<QuaternionControl>(
                        device,
                        "pokeRotation",
                        "pokePose/rotation"),

                pinchPosition =
                    FindControl<Vector3Control>(
                        device,
                        "pinchPosition",
                        "pinchPose/position"),

                pinchRotation =
                    FindControl<QuaternionControl>(
                        device,
                        "pinchRotation",
                        "pinchPose/rotation"),

                pointerPosition =
                    FindControl<Vector3Control>(
                        device,
                        "pointerPosition",
                        "pointerPose/position",
                        "pointer/position"),

                pointerRotation =
                    FindControl<QuaternionControl>(
                        device,
                        "pointerRotation",
                        "pointerPose/rotation",
                        "pointer/rotation"),

                pinchValue =
                    FindControl<AxisControl>(
                        device,
                        "pinchValue"),

                pinchReady =
                    FindControl<ButtonControl>(
                        device,
                        "pinchReady"),

                graspValue =
                    FindControl<AxisControl>(
                        device,
                        "graspValue"),

                graspReady =
                    FindControl<ButtonControl>(
                        device,
                        "graspReady")
            };
        }


        private static T FindControl<T>(
            InputDevice device,
            params string[] paths)
            where T : InputControl
        {
            if (device == null)
                return null;

            foreach (string path in paths)
            {
                T control =
                    device.TryGetChildControl<T>(path);

                if (control != null)
                    return control;
            }

            return null;
        }


        private static string DescribeBinding(
            HandInteractionBinding binding)
        {
            if (binding == null || binding.device == null)
                return "none";

            return
                $"'{binding.device.name}' layout='{binding.device.layout}' " +
                $"id={binding.device.deviceId} " +
                $"trackedCtl={(binding.isTracked != null)} " +
                $"pinchCtl={(binding.pinchValue != null)} " +
                $"graspCtl={(binding.graspValue != null)} " +
                $"pokeCtl={(binding.pokePosition != null)} " +
                $"aimCtl={(binding.pointerRotation != null)}";
        }


        // --------------------------------------------------------------------
        // Open Brush controller initialization
        // --------------------------------------------------------------------

        private void EnsureOpenBrushHandMode()
        {
            if (m_OpenBrushHandModeInitialized)
                return;

            if (App.VrSdk == null ||
                InputManager.m_Instance == null)
            {
                return;
            }

            if (useLegacyOpenBrushAnchorGeometry)
            {
                /*
                 * RESTORED FROM THE KNOWN-WORKING BRIDGE.
                 *
                 * OculusTouch here is NOT the input source. It is only Open Brush's
                 * internal geometry scaffold, providing the known-good:
                 *
                 *     Wand.Geometry.MainAxisAttachPoint
                 *     Brush.PointerAttachPoint
                 *
                 * The hybrid routing later still chooses physical controller vs hand
                 * independently per side.
                 */
                App.VrSdk.SetControllerStyle(
                    ControllerStyle.OculusTouch);

                // SetControllerStyle recreates VrControls.
                InputManager.m_Instance.CreateControllerInfos();
            }

            if (InputManager.Wand == null ||
                InputManager.Brush == null)
            {
                return;
            }

            InputManager.m_Instance.AllowVrControllers = true;

            if (PointerManager.m_Instance != null)
            {
                // This call was present in the working bridge and recalculates
                // free-paint/teleport pointer presentation for the recreated geometry.
                PointerManager.m_Instance.RefreshFreePaintPointerAngle();
                PointerManager.m_Instance.RequestPointerRendering(true);
            }

            m_OpenBrushHandModeInitialized = true;

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: restored anchor geometry initialized. " +
                    $"legacyGeometry={useLegacyOpenBrushAnchorGeometry} " +
                    $"WandStyle={InputManager.Wand.Geometry?.Style} " +
                    $"BrushStyle={InputManager.Brush.Geometry?.Style}");
            }
        }


        // --------------------------------------------------------------------
        // Capture / prepare Open Brush controller objects
        // --------------------------------------------------------------------

        private void PrepareOpenBrushControllers()
        {
            if (m_ControllersPrepared)
                return;

            if (InputManager.m_Instance == null ||
                InputManager.Brush == null ||
                InputManager.Wand == null)
            {
                return;
            }

            m_ControllersPrepared = true;

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: hybrid Open Brush controller routing prepared");
            }
        }


        private static void SetTrackedPoseDriversEnabled(
            BaseControllerBehavior behavior,
            bool enabled)
        {
            if (behavior == null)
                return;

            TrackedPoseDriver[] poseDrivers =
                behavior.GetComponentsInChildren<TrackedPoseDriver>(
                    true);

            foreach (TrackedPoseDriver driver in poseDrivers)
            {
                driver.enabled = enabled;
            }
        }


        private void ApplySourceRouting()
        {
            if (!m_ControllersPrepared)
                return;

            bool leftUsesHand =
                m_LeftSource == InputSourceKind.Hand;

            bool rightUsesHand =
                m_RightSource == InputSourceKind.Hand;

            if (InputManager.Wand != null)
            {
                SetTrackedPoseDriversEnabled(
                    InputManager.Wand.Behavior,
                    !leftUsesHand);

                if (hideControllerModels && leftUsesHand)
                {
                    InputManager.Wand.ShowController(false);
                }
            }

            if (InputManager.Brush != null)
            {
                SetTrackedPoseDriversEnabled(
                    InputManager.Brush.Behavior,
                    !rightUsesHand);

                if (hideControllerModels && rightUsesHand)
                {
                    InputManager.Brush.ShowController(false);
                }
            }
        }


        private void RestoreControllerVisualForSide(
            bool isBrush)
        {
            if (!hideControllerModels)
                return;

            if (isBrush)
            {
                InputManager.Brush?.ShowController(true);
            }
            else
            {
                InputManager.Wand?.ShowController(true);
            }
        }


        private void HideHandControllerVisuals()
        {
            if (!hideControllerModels)
                return;

            if (UseHand(false))
                InputManager.Wand?.ShowController(false);

            if (UseHand(true))
                InputManager.Brush?.ShowController(false);
        }


        // --------------------------------------------------------------------
        // Hand Interaction processing (no raw joints)
        // --------------------------------------------------------------------

        private void UpdateInteractionHand(
            HandInteractionBinding binding,
            HandState state,
            bool allowDraw)
        {
            bool wasTrigger = state.trigger;

            if (binding == null || !binding.IsValid)
            {
                ResetMissingHand(binding, state);

                if (allowDraw)
                {
                    state.triggerDown = false;
                    state.triggerUp = wasTrigger;
                }

                return;
            }

            int trackingFlags =
                binding.trackingState != null
                    ? binding.trackingState.ReadValue()
                    : 0;

            bool explicitTracked =
                binding.isTracked != null &&
                binding.isTracked.isPressed;

            bool interactionReady =
                (binding.pinchReady != null &&
                 binding.pinchReady.isPressed) ||
                (binding.graspReady != null &&
                 binding.graspReady.isPressed);

            // On Android XR the trackingState/isTracked controls are expected.
            // interactionReady is a useful fallback for provider revisions.
            state.tracked =
                explicitTracked ||
                trackingFlags != 0 ||
                interactionReady;

            state.pinchValue =
                binding.pinchValue != null
                    ? Mathf.Clamp01(binding.pinchValue.ReadValue())
                    : 0.0f;

            state.graspValue =
                binding.graspValue != null
                    ? Mathf.Clamp01(binding.graspValue.ReadValue())
                    : 0.0f;


            Pose gripLocal = ReadLocalPose(
                binding.devicePosition,
                binding.deviceRotation);

            state.palmPose =
                ToWorldPose(gripLocal);


            // Poke is the permission-free index interaction point. If a runtime
            // does not supply it, fall back to pinch, then grip.
            Pose indexLocal;

            if (binding.pokePosition != null)
            {
                indexLocal = ReadLocalPose(
                    binding.pokePosition,
                    binding.pokeRotation);
            }
            else if (binding.pinchPosition != null)
            {
                indexLocal = ReadLocalPose(
                    binding.pinchPosition,
                    binding.pinchRotation);
            }
            else
            {
                indexLocal = gripLocal;
            }

            state.indexTipPose =
                ToWorldPose(indexLocal);


            // Aim/pointer pose gives a stable runtime-defined pointing ray
            // without reconstructing finger joints. OpenXR defines AIM FORWARD
            // as local -Z (not Unity's +Z / Vector3.forward).
            Pose aimLocal;
            if (binding.pointerPosition != null ||
                binding.pointerRotation != null)
            {
                aimLocal = ReadLocalPose(
                    binding.pointerPosition,
                    binding.pointerRotation);
                state.aimPoseValid =
                    state.tracked &&
                    binding.pointerRotation != null;
            }
            else
            {
                aimLocal = indexLocal;
                state.aimPoseValid = false;
            }

            state.aimPose = ToWorldPose(aimLocal);

            Quaternion aimRotationLocal = aimLocal.rotation;

            state.indexDirectionWorld =
                ToWorldDirection(
                    aimRotationLocal *
                    Vector3.back);

            state.indexDirectionValid =
                state.tracked &&
                state.indexDirectionWorld.sqrMagnitude >
                0.000001f;


            bool nextTrigger = false;

            if (allowDraw &&
                state.tracked &&
                binding.pinchValue != null)
            {
                float threshold =
                    wasTrigger
                        ? pinchReleaseThreshold
                        : pinchPressThreshold;

                nextTrigger =
                    state.pinchValue >= threshold;
            }

            state.trigger = nextTrigger;
            state.triggerDown =
                !wasTrigger && nextTrigger;
            state.triggerUp =
                wasTrigger && !nextTrigger;
        }


        private static Pose ReadLocalPose(
            Vector3Control positionControl,
            QuaternionControl rotationControl)
        {
            Vector3 position =
                positionControl != null
                    ? positionControl.ReadValue()
                    : Vector3.zero;

            Quaternion rotation =
                rotationControl != null
                    ? SafeQuaternion(
                        rotationControl.ReadValue())
                    : Quaternion.identity;

            return new Pose(
                position,
                rotation);
        }


        private static Quaternion SafeQuaternion(
            Quaternion value)
        {
            float sqrMagnitude =
                value.x * value.x +
                value.y * value.y +
                value.z * value.z +
                value.w * value.w;

            if (sqrMagnitude < 0.0001f)
                return Quaternion.identity;

            float invMagnitude =
                1.0f / Mathf.Sqrt(sqrMagnitude);

            return new Quaternion(
                value.x * invMagnitude,
                value.y * invMagnitude,
                value.z * invMagnitude,
                value.w * invMagnitude);
        }


        private static void ResetMissingHand(
            HandInteractionBinding binding,
            HandState state)
        {
            state.tracked = false;
            state.pinchValue = 0.0f;
            state.graspValue = 0.0f;
            state.indexDirectionValid = false;
            state.indexDirectionWorld = Vector3.forward;
        }


        // --------------------------------------------------------------------
        // XR Hands spatial pose restoration
        // --------------------------------------------------------------------

        private void UpdateXRHandsSpatialPoses()
        {
            m_Left.xrHandsSpatialValid = false;
            m_Right.xrHandsSpatialValid = false;

            if (!useXRHandsForSpatialPose)
                return;

            if (m_XRHandSubsystem == null ||
                !m_XRHandSubsystem.running)
            {
                if (Time.unscaledTime >= m_NextXRHandsSearchTime)
                {
                    FindRunningXRHandSubsystem();
                    m_NextXRHandsSearchTime =
                        Time.unscaledTime + 0.5f;
                }
            }

            if (m_XRHandSubsystem == null ||
                !m_XRHandSubsystem.running)
            {
                return;
            }

            // Only replace spatial pose when the high-level Hand Interaction
            // device also considers that side tracked. This preserves the
            // hybrid source-selection semantics.
            if (m_Left.tracked)
            {
                ApplyXRHandSpatialPose(
                    m_XRHandSubsystem.leftHand,
                    m_Left,
                    isRightHand: false);
            }

            if (m_Right.tracked)
            {
                ApplyXRHandSpatialPose(
                    m_XRHandSubsystem.rightHand,
                    m_Right,
                    isRightHand: true);
            }
        }


        private void FindRunningXRHandSubsystem()
        {
            m_XRHandSubsystem = null;
            m_XRHandSubsystems.Clear();

            SubsystemManager.GetSubsystems(
                m_XRHandSubsystems);

            foreach (XRHandSubsystem subsystem in
                     m_XRHandSubsystems)
            {
                if (subsystem != null &&
                    subsystem.running)
                {
                    m_XRHandSubsystem = subsystem;

                    if (debugLogging)
                    {
                        Debug.Log(
                            "ANDROID_XR_HAND: XRHands spatial source found " +
                            $"L={subsystem.leftHand.isTracked} " +
                            $"R={subsystem.rightHand.isTracked}");
                    }

                    return;
                }
            }
        }


        private void ApplyXRHandSpatialPose(
            XRHand hand,
            HandState state,
            bool isRightHand)
        {
            if (!hand.isTracked)
                return;

            XRHandJoint palmJoint =
                hand.GetJoint(
                    XRHandJointID.Palm);

            XRHandJoint indexTipJoint =
                hand.GetJoint(
                    XRHandJointID.IndexTip);

            XRHandJoint indexDistalJoint =
                hand.GetJoint(
                    XRHandJointID.IndexDistal);

            bool havePalm =
                palmJoint.TryGetPose(
                    out Pose palmLocal);

            bool haveTip =
                indexTipJoint.TryGetPose(
                    out Pose indexTipLocal);

            bool haveDistal =
                indexDistalJoint.TryGetPose(
                    out Pose indexDistalLocal);

            if (havePalm)
            {
                // EXACT source used by the older working Wand implementation.
                state.palmPose =
                    ToWorldPose(
                        palmLocal);
            }

            if (haveTip)
            {
                // EXACT fingertip source used by the older working UI touch.
                state.indexTipPose =
                    ToWorldPose(
                        indexTipLocal);
            }

            if (haveTip &&
                haveDistal)
            {
                Vector3 directionLocal =
                    indexTipLocal.position -
                    indexDistalLocal.position;

                if (directionLocal.sqrMagnitude >
                    0.00000001f)
                {
                    // EXACT IndexDistal -> IndexTip pointer direction used by
                    // the older working teleporter / colour pointer.
                    state.indexDirectionWorld =
                        ToWorldDirection(
                            directionLocal.normalized);

                    state.indexDirectionValid = true;
                }
            }

            state.xrHandsSpatialValid =
                havePalm &&
                (!isRightHand ||
                 (haveTip && haveDistal));
        }


        private void UpdateSourceSelection()
        {
            bool leftControllerTracked =
                IsControllerTracked(
                    m_LeftControllerBinding);

            bool rightControllerTracked =
                IsControllerTracked(
                    m_RightControllerBinding);

            InputSourceKind desiredLeft =
                ChooseDesiredSource(
                    leftControllerTracked,
                    m_Left.tracked);

            InputSourceKind desiredRight =
                ChooseDesiredSource(
                    rightControllerTracked,
                    m_Right.tracked);

            UpdateSideSource(
                isBrush: false,
                desiredLeft,
                ref m_LeftSource,
                ref m_LeftCandidateSource,
                ref m_LeftCandidateSince);

            UpdateSideSource(
                isBrush: true,
                desiredRight,
                ref m_RightSource,
                ref m_RightCandidateSource,
                ref m_RightCandidateSince);
        }


        private InputSourceKind ChooseDesiredSource(
            bool controllerTracked,
            bool handTracked)
        {
            if (preferPhysicalControllers)
            {
                if (controllerTracked)
                    return InputSourceKind.Controller;

                if (handTracked)
                    return InputSourceKind.Hand;
            }
            else
            {
                if (handTracked)
                    return InputSourceKind.Hand;

                if (controllerTracked)
                    return InputSourceKind.Controller;
            }

            return InputSourceKind.None;
        }


        private void UpdateSideSource(
            bool isBrush,
            InputSourceKind desired,
            ref InputSourceKind current,
            ref InputSourceKind candidate,
            ref float candidateSince)
        {
            if (desired == current)
            {
                candidate = desired;
                candidateSince = Time.unscaledTime;
                return;
            }

            if (candidate != desired)
            {
                candidate = desired;
                candidateSince = Time.unscaledTime;
                return;
            }

            bool switchImmediately =
                current == InputSourceKind.None;

            if (!switchImmediately &&
                Time.unscaledTime - candidateSince <
                Mathf.Max(
                    0.0f,
                    sourceSwitchDelay))
            {
                return;
            }

            InputSourceKind previous =
                current;

            current =
                desired;

            candidate =
                desired;

            candidateSince =
                Time.unscaledTime;

            if (previous == InputSourceKind.Hand &&
                current != InputSourceKind.Hand)
            {
                RestoreControllerVisualForSide(
                    isBrush);
            }

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROIDXR_SOURCE_SWITCH " +
                    $"side={(isBrush ? "RIGHT/BRUSH" : "LEFT/WAND")} " +
                    $"from={previous} " +
                    $"to={current} " +
                    $"controllerTracked=" +
                    IsControllerTracked(
                        isBrush
                            ? m_RightControllerBinding
                            : m_LeftControllerBinding) +
                    $" handTracked=" +
                    (isBrush
                        ? m_Right.tracked
                        : m_Left.tracked));
            }
        }


        private void UpdateGripStates()
        {
            UpdateOneHandGrip(
                m_Left,
                UseHand(false),
                ref m_LeftGripHeld,
                ref m_LeftGripDown,
                ref m_LeftGripUp);

            UpdateOneHandGrip(
                m_Right,
                UseHand(true),
                ref m_RightGripHeld,
                ref m_RightGripDown,
                ref m_RightGripUp);

            m_SwimModeHeld =
                m_LeftGripHeld &&
                m_RightGripHeld;
        }


        private void UpdateOneHandGrip(
            HandState hand,
            bool useHand,
            ref bool held,
            ref bool down,
            ref bool up)
        {
            bool previous =
                held;

            float threshold =
                previous
                    ? graspReleaseThreshold
                    : graspPressThreshold;

            bool next =
                useHand &&
                hand.tracked &&
                hand.graspValue >= threshold;

            held = next;
            down = !previous && next;
            up = previous && !next;
        }


        private void UpdatePrimaryTriggerState()
        {
            bool wasHeld =
                m_PrimaryTriggerHeld;

            /*
             * IMPORTANT:
             *
             * VrInput.Trigger remains DRAW ONLY in hand mode.
             *
             * Direct fingertip UI input is dispatched by UpdateMenuTouchState()
             * straight to the touched BaseButton, so a poke can never fall
             * through into a brush stroke.
             */
            bool drawPressHeld =
                m_Right.trigger &&
                !m_MenuTouchHeld &&
                !m_MenuTouchUp;

            bool nextHeld =
                UseHand(true) &&
                m_Right.tracked &&
                drawPressHeld;

            m_PrimaryTriggerHeld =
                nextHeld;

            m_PrimaryTriggerDown =
                !wasHeld && nextHeld;

            m_PrimaryTriggerUp =
                wasHeld && !nextHeld;

            if (debugLogging &&
                (m_PrimaryTriggerDown ||
                 m_PrimaryTriggerUp ||
                 m_MenuTouchDown ||
                 m_MenuTouchUp))
            {
                Debug.Log(
                    "ANDROIDXR_PRIMARY_TRIGGER " +
                    $"held={m_PrimaryTriggerHeld} " +
                    $"down={m_PrimaryTriggerDown} " +
                    $"up={m_PrimaryTriggerUp} " +
                    $"draw={drawPressHeld} " +
                    $"menuContact={m_MenuTouchHeld} " +
                    $"directUi={(m_ActiveMenuTouchComponent != null)}");
            }
        }


        // --------------------------------------------------------------------
        // Separate direct-touch menu input
        // --------------------------------------------------------------------

        private void UpdateMenuTouchState()
        {
            bool previousTouch =
                m_MenuTouchHeld;

            BaseButton previousComponent =
                m_ActiveMenuTouchComponent;

            m_MenuTouchHeld = false;
            m_MenuTouchDown = false;
            m_MenuTouchUp = false;
            m_MenuTouchCollider = null;
            m_MenuTouchComponent = null;


            if (!m_Right.tracked ||
                !UseHand(true))
            {
                if (previousComponent != null)
                {
                    ReleaseDirectUiComponent(
                        previousComponent);
                }

                m_ActiveMenuTouchComponent = null;
                m_MenuTouchUp = previousTouch;
                return;
            }


            int hitCount =
                Physics.OverlapSphereNonAlloc(
                    m_Right.indexTipPose.position,
                    Mathf.Max(
                        0.001f,
                        menuTouchRadius),
                    m_MenuTouchHits,
                    menuTouchLayerMask,
                    menuTouchQueryTriggerInteraction);


            float closestDistanceSquared =
                float.PositiveInfinity;

            for (int i = 0;
                 i < hitCount;
                 ++i)
            {
                Collider hit =
                    m_MenuTouchHits[i];

                if (hit == null ||
                    !hit.enabled ||
                    !hit.gameObject.activeInHierarchy)
                {
                    continue;
                }


                /*
                 * Only a real Open Brush BaseButton is pressable.
                 *
                 * The previous implementation also accepted a BasePanel body.
                 * That made physical contact visible in diagnostics but did not
                 * tell us which actual button should receive the click.
                 */
                BaseButton component =
                    hit.GetComponentInParent<BaseButton>();

                if (component == null ||
                    !component.isActiveAndEnabled)
                {
                    continue;
                }


                Collider componentCollider =
                    component.GetCollider();

                if (componentCollider == null ||
                    !componentCollider.enabled ||
                    !componentCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }


                Vector3 closestPoint =
                    componentCollider.ClosestPoint(
                        m_Right.indexTipPose.position);

                float distanceSquared =
                    (
                        closestPoint -
                        m_Right.indexTipPose.position
                    )
                    .sqrMagnitude;


                if (distanceSquared <
                    closestDistanceSquared)
                {
                    closestDistanceSquared =
                        distanceSquared;

                    m_MenuTouchCollider =
                        componentCollider;

                    m_MenuTouchComponent =
                        component;
                }
            }


            m_MenuTouchHeld =
                m_MenuTouchComponent != null;

            bool targetChanged =
                previousComponent !=
                m_MenuTouchComponent;


            if (targetChanged &&
                previousComponent != null)
            {
                ReleaseDirectUiComponent(
                    previousComponent);
            }


            if (m_MenuTouchHeld)
            {
                if (targetChanged)
                {
                    PressDirectUiComponent(
                        m_MenuTouchComponent);

                    m_ActiveMenuTouchComponent =
                        m_MenuTouchComponent;
                }
                else
                {
                    HoldDirectUiComponent(
                        m_MenuTouchComponent);
                }
            }
            else
            {
                m_ActiveMenuTouchComponent =
                    null;
            }


            m_MenuTouchDown =
                (!previousTouch &&
                 m_MenuTouchHeld) ||
                (targetChanged &&
                 m_MenuTouchHeld);

            m_MenuTouchUp =
                previousTouch &&
                !m_MenuTouchHeld;


            if (debugLogging &&
                (m_MenuTouchDown ||
                 m_MenuTouchUp ||
                 targetChanged))
            {
                Debug.Log(
                    "ANDROIDXR_MENU_TOUCH " +
                    $"held={m_MenuTouchHeld} " +
                    $"down={m_MenuTouchDown} " +
                    $"up={m_MenuTouchUp} " +
                    $"collider=" +
                    $"{(m_MenuTouchCollider != null ? m_MenuTouchCollider.name : "none")} " +
                    $"component=" +
                    $"{(m_MenuTouchComponent != null ? m_MenuTouchComponent.GetType().Name : "none")} " +
                    $"object=" +
                    $"{(m_MenuTouchComponent != null ? m_MenuTouchComponent.gameObject.name : "none")}");
            }
        }


        private void PressDirectUiComponent(
            BaseButton component)
        {
            if (component == null)
                return;

            /*
             * BaseButton.ButtonPressed does not depend on RaycastHit data for
             * normal buttons. Calling the public UIComponent API keeps this path
             * completely separate from Brush Trigger / drawing.
             */
            component.GainFocus();
            component.ButtonPressed(default);

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROIDXR_DIRECT_UI PRESS " +
                    $"component={component.GetType().Name} " +
                    $"object={component.gameObject.name}");
            }
        }


        private void HoldDirectUiComponent(
            BaseButton component)
        {
            if (component == null)
                return;

            component.ButtonHeld(default);
        }


        private void ReleaseDirectUiComponent(
            BaseButton component)
        {
            if (component == null)
                return;

            component.ButtonReleased();
            component.LostFocus();

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROIDXR_DIRECT_UI RELEASE " +
                    $"component={component.GetType().Name} " +
                    $"object={component.gameObject.name}");
            }
        }


        private void ResetMenuTouchState()
        {
            bool wasHeld =
                m_MenuTouchHeld;

            if (m_ActiveMenuTouchComponent != null)
            {
                ReleaseDirectUiComponent(
                    m_ActiveMenuTouchComponent);
            }

            m_MenuTouchHeld = false;
            m_MenuTouchDown = false;
            m_MenuTouchUp = wasHeld;
            m_MenuTouchCollider = null;
            m_MenuTouchComponent = null;
            m_ActiveMenuTouchComponent = null;
        }


        private static bool IsPanelUiCollider(
            Collider collider)
        {
            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy)
            {
                return false;
            }


            // BasePanel also catches most child/custom panel colliders.
            if (collider.GetComponentInParent<BasePanel>() != null)
            {
                return true;
            }

            if (collider.GetComponentInParent<PopUpWindow>() != null)
            {
                return true;
            }

            if (collider.GetComponentInParent<UIComponent>() != null)
            {
                return true;
            }

            return false;
        }


        private void SuppressDrawForMenuContact(
            bool wasDrawingBeforeHandUpdate)
        {
            m_Right.trigger = false;
            m_Right.triggerDown = false;
            m_Right.triggerUp =
                wasDrawingBeforeHandUpdate;
        }


        // --------------------------------------------------------------------
        // Tracking-space -> world-space
        // --------------------------------------------------------------------

        private Pose ToWorldPose(Pose localPose)
        {
            if (trackingOrigin == null)
            {
                return localPose;
            }

            return new Pose(
                trackingOrigin.TransformPoint(
                    localPose.position),

                trackingOrigin.rotation *
                localPose.rotation);
        }


        private Vector3 ToWorldDirection(
            Vector3 localDirection)
        {
            if (trackingOrigin == null)
            {
                return localDirection.normalized;
            }

            return trackingOrigin
                .TransformDirection(
                    localDirection)
                .normalized;
        }


        // --------------------------------------------------------------------
        // Drive Open Brush virtual controllers
        // --------------------------------------------------------------------

        private void DriveOpenBrushControllers()
        {
            if (!m_ControllersPrepared)
                return;


            // RIGHT HAND = BRUSH
            if (UseHand(true) &&
                m_Right.tracked &&
                InputManager.Brush != null)
            {
                DriveBrush(
                    InputManager.Brush.Behavior,
                    m_Right);
            }


            // LEFT HAND = WAND
            if (UseHand(false) &&
                m_Left.tracked &&
                InputManager.Wand != null)
            {
                DriveWand(
                    InputManager.Wand.Behavior,
                    m_Left);
            }
        }


        // --------------------------------------------------------------------
        // Right hand -> Brush
        // --------------------------------------------------------------------

        private void DriveBrush(
            BaseControllerBehavior behavior,
            HandState hand)
        {
            if (behavior == null ||
                behavior.PointerAttachPoint == null)
            {
                return;
            }


            Transform root =
                behavior.transform;

            Transform pointer =
                behavior.PointerAttachPoint;


            /*
             * Aim along the physical index finger / runtime poke pose.
             *
             *     IndexDistal -> IndexTip
             *
             * This avoids inheriting the borrowed Quest controller's palm /
             * controller axis, which was responsible for the apparent 90 degree
             * pointer rotation.
             */
            Vector3 pointerForward =
                hand.indexDirectionValid
                    ? hand.indexDirectionWorld
                    : hand.indexTipPose.rotation *
                      Vector3.back;


            if (pointerForward.sqrMagnitude <
                0.00000001f)
            {
                pointerForward =
                    hand.palmPose.rotation *
                    Vector3.back;
            }

            pointerForward.Normalize();


            /*
             * Normal painting: PointerAttachPoint sits at the index tip.
             *
             * Direct UI poke: move the pointer origin slightly BEHIND the
             * fingertip along the poke ray. Starting a ray from inside a button
             * collider is unreliable, and was the reason a fingertip touch could
             * become a paint trigger instead of a panel click.
             *
             * The ray therefore travels:
             *
             *     origin -----> fingertip -----> UI collider
             */
            Vector3 desiredPointerPosition =
                hand.indexTipPose.position;

            if (m_MenuTouchHeld)
            {
                desiredPointerPosition -=
                    pointerForward *
                    Mathf.Max(
                        0.001f,
                        menuTouchPointerBackoff);
            }


            /*
             * Build a stable "up" vector from the palm, projected perpendicular
             * to the finger direction. If the palm up axis happens to be nearly
             * parallel to the finger, fall back to palm right.
             */
            Vector3 palmUp =
                hand.palmPose.rotation *
                Vector3.up;

            Vector3 pointerUp =
                Vector3.ProjectOnPlane(
                    palmUp,
                    pointerForward);

            if (pointerUp.sqrMagnitude <
                0.000001f)
            {
                pointerUp =
                    Vector3.ProjectOnPlane(
                        hand.palmPose.rotation *
                        Vector3.right,
                        pointerForward);
            }

            if (pointerUp.sqrMagnitude <
                0.000001f)
            {
                pointerUp =
                    Vector3.up;
            }

            pointerUp.Normalize();


            Quaternion desiredPointerRotation =
                Quaternion.LookRotation(
                    pointerForward,
                    pointerUp);

            if (!useExactRestoredPose)
            {
                desiredPointerRotation *=
                    Quaternion.Euler(
                        brushRotationOffset);
            }


            /*
             * PointerAttachPoint belongs to the borrowed controller prefab and
             * may itself be locally rotated. Preserve that local relationship,
             * then solve the controller root rotation required to make the
             * ACTUAL pointer forward match the index finger.
             */
            Vector3 pointerLocalPosition =
                root.InverseTransformPoint(
                    pointer.position);

            Quaternion pointerLocalRotation =
                Quaternion.Inverse(
                    root.rotation)
                *
                pointer.rotation;


            Quaternion desiredRootRotation =
                desiredPointerRotation
                *
                Quaternion.Inverse(
                    pointerLocalRotation);


            root.rotation =
                desiredRootRotation;


            Vector3 pointerOffsetWorld =
                root.TransformVector(
                    pointerLocalPosition);


            Vector3 activeBrushPositionOffset =
                useExactRestoredPose ||
                m_MenuTouchHeld
                    ? Vector3.zero
                    : brushPositionOffset;

            root.position =
                desiredPointerPosition
                -
                pointerOffsetWorld
                +
                desiredPointerRotation
                *
                activeBrushPositionOffset;
        }


        // --------------------------------------------------------------------
        // Left hand -> Wand
        // --------------------------------------------------------------------

        private void DriveWand(
            BaseControllerBehavior behavior,
            HandState hand)
        {
            if (behavior == null)
                return;

            /*
             * RESTORED WORKING MAPPING:
             *
             * The old bridge drove the Wand root directly from XRHand Palm.
             * Do not use Hand Interaction aim/grip orientation here when XR Hands
             * joint pose is available.
             *
             * useExactRestoredPose intentionally ignores any inspector rotation/
             * position experiments left over from v5-v7.
             */
            Quaternion desiredRotation =
                hand.palmPose.rotation;

            Vector3 desiredPosition =
                hand.palmPose.position;

            if (!useExactRestoredPose)
            {
                desiredRotation *=
                    Quaternion.Euler(
                        wandRotationOffset);

                desiredPosition +=
                    desiredRotation *
                    wandPositionOffset;
            }

            behavior.transform.rotation =
                desiredRotation;

            behavior.transform.position =
                desiredPosition;

            if (debugLogging &&
                Time.frameCount % 120 == 0)
            {
                Debug.Log(
                    "ANDROIDXR_WAND_POSE " +
                    $"source={(hand.xrHandsSpatialValid ? "XRHANDS_PALM" : "HAND_INTERACTION_FALLBACK")} " +
                    $"exact={useExactRestoredPose} " +
                    $"pos={desiredPosition:F3} " +
                    $"rotEuler={desiredRotation.eulerAngles:F1}");
            }
        }


        // --------------------------------------------------------------------
        // Open Brush panels
        // --------------------------------------------------------------------

        private void RequestPanels()
        {
            if (!showPanels ||
                !UseHand(false) ||
                !m_Left.tracked)
            {
                return;
            }


            SketchControlsScript sketchControls =
                SketchControlsScript.m_Instance;

            PanelManager panels =
                PanelManager.m_Instance;


            if (sketchControls == null ||
                panels == null)
            {
                return;
            }


            if (panels.GetAllPanels() == null)
                return;


            /*
             * Keep Open Brush in its normal 6DoF-controller interaction mode.
             */
            sketchControls.ActiveControlsType =
                SketchControlsScript.ControlsType.SixDofControllers;


            /*
             * High-level Open Brush visibility request.
             */
            sketchControls.RequestPanelsVisibility(true);


            /*
             * Also explicitly keep PanelManager's visible state enabled.
             */
            panels.SetVisible(true);


            if (!m_PanelsRequested)
            {
                m_PanelsRequested = true;

                Debug.Log(
                    "ANDROID_XR_HAND: PANELS REQUESTED " +
                    $"count={panels.GetAllPanels().Count} " +
                    $"leftTracked={m_Left.tracked}");
            }
        }


        // --------------------------------------------------------------------
        // Diagnostics
        // --------------------------------------------------------------------

        private void DebugStateIfNeeded()
        {
            if (!debugLogging)
                return;

            if (Time.unscaledTime < m_NextDebugStateTime)
                return;

            m_NextDebugStateTime =
                Time.unscaledTime +
                Mathf.Max(
                    0.1f,
                    debugStateInterval);


            string wandValid =
                InputManager.Wand != null
                    ? InputManager.Wand.IsTrackedObjectValid.ToString()
                    : "null";

            string brushValid =
                InputManager.Brush != null
                    ? InputManager.Brush.IsTrackedObjectValid.ToString()
                    : "null";


            Debug.Log(
                "ANDROID_XR_HAND STATE " +
                $"mode=Hybrid " +
                $"sourceL={m_LeftSource} " +
                $"sourceR={m_RightSource} " +
                $"controllerL={IsControllerTracked(m_LeftControllerBinding)} " +
                $"controllerR={IsControllerTracked(m_RightControllerBinding)} " +
                $"leftDevice={DescribeBinding(m_LeftBinding)} " +
                $"rightDevice={DescribeBinding(m_RightBinding)} " +
                $"initialized={m_OpenBrushHandModeInitialized} " +
                $"prepared={m_ControllersPrepared} " +
                $"L={m_Left.tracked} " +
                $"R={m_Right.tracked} " +
                $"pinch={m_Right.pinchValue:F3} " +
                $"draw={m_Right.trigger} " +
                $"primaryTrigger={m_PrimaryTriggerHeld} " +
                $"graspL={m_Left.graspValue:F3} " +
                $"graspR={m_Right.graspValue:F3} " +
                $"swim={m_SwimModeHeld} " +
                $"menuTouch={m_MenuTouchHeld} " +
                $"xrSpatialL={m_Left.xrHandsSpatialValid} " +
                $"xrSpatialR={m_Right.xrHandsSpatialValid} " +
                $"directUi={(m_ActiveMenuTouchComponent != null ? m_ActiveMenuTouchComponent.GetType().Name : "none")} " +
                $"uiTarget={(SketchControlsScript.m_Instance != null && SketchControlsScript.m_Instance.IsUserInteractingWithUI())} " +
                $"wandValid={wandValid} " +
                $"brushValid={brushValid} " +
                $"panelsRequested={m_PanelsRequested}");
        }

    }
}
