using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.SpatialTracking;

namespace TiltBrush
{
    /// <summary>
    /// Android XR hand tracking bridge for Open Brush.
    ///
    /// Mapping:
    ///     Left hand  -> Open Brush Wand / panels
    ///     Right hand -> Open Brush Brush / drawing
    ///
    /// Drawing gesture:
    ///     Index fingertip close to middle fingertip.
    ///
    /// For initial debugging, Require Other Fingers Closed is OFF by default.
    /// Once drawing works reliably, enable it to additionally require thumb,
    /// ring and little fingertips to be close to the palm.
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


        [Header("Draw Gesture - Right Hand")]

        [Tooltip("Index-middle fingertip distance in metres required to START drawing.")]
        [SerializeField]
        private float drawPressDistance = 0.035f;

        [Tooltip("Index-middle fingertip distance in metres required to STOP drawing.")]
        [SerializeField]
        private float drawReleaseDistance = 0.045f;

        [Tooltip(
            "OFF is recommended for initial testing. " +
            "When ON, thumb, ring and little fingers must also be closed.")]
        [SerializeField]
        private bool requireOtherFingersClosed = false;

        [Tooltip("Maximum RingTip -> Palm distance for ring finger to count as closed.")]
        [SerializeField]
        private float ringClosedDistance = 0.075f;

        [Tooltip("Maximum LittleTip -> Palm distance for little finger to count as closed.")]
        [SerializeField]
        private float littleClosedDistance = 0.070f;

        [Tooltip("Maximum ThumbTip -> Palm distance for thumb to count as closed.")]
        [SerializeField]
        private float thumbClosedDistance = 0.070f;


        [Header("Brush / Right Hand")]

        [Tooltip("Rotation correction for the virtual Open Brush Brush.")]
        public Vector3 brushRotationOffset;

        [Tooltip(
            "Additional local-space offset after placing the Open Brush pointer " +
            "between the index and middle fingertips.")]
        public Vector3 brushPositionOffset;


        [Header("Wand / Left Hand")]

        [Tooltip("Rotation correction for the virtual Open Brush Wand.")]
        public Vector3 wandRotationOffset;

        [Tooltip("Local-space offset of the virtual Wand relative to the palm.")]
        public Vector3 wandPositionOffset;


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

        private XRHandSubsystem m_Subsystem;

        private bool m_OpenBrushHandModeInitialized;
        private bool m_ControllersPrepared;
        private bool m_PanelsRequested;

        private float m_NextDebugStateTime;

        private readonly HandState m_Left = new HandState();
        private readonly HandState m_Right = new HandState();


        private class HandState
        {
            public bool tracked;

            public bool trigger;
            public bool triggerDown;
            public bool triggerUp;

            public float indexMiddleDistance = float.PositiveInfinity;
            public float ringPalmDistance = float.PositiveInfinity;
            public float littlePalmDistance = float.PositiveInfinity;
            public float thumbPalmDistance = float.PositiveInfinity;

            public bool ringPoseValid;
            public bool littlePoseValid;
            public bool thumbPoseValid;

            public Pose palmPose;
            public Pose indexTipPose;
            public Pose middleTipPose;
        }


        // --------------------------------------------------------------------
        // Public bridge API used by UnityXRControllerInfo
        // --------------------------------------------------------------------

        public static bool Active =>
            Instance != null &&
            Instance.m_Subsystem != null &&
            Instance.m_Subsystem.running;


        public static bool LeftTracked =>
            Active &&
            Instance.m_Left.tracked;


        public static bool RightTracked =>
            Active &&
            Instance.m_Right.tracked;


        /// <summary>
        /// isBrush == false -> left Wand
        /// isBrush == true  -> right Brush
        /// </summary>
        public static bool IsTracked(bool isBrush)
        {
            if (!Active)
                return false;

            return isBrush
                ? Instance.m_Right.tracked
                : Instance.m_Left.tracked;
        }


        /// <summary>
        /// Only the right-hand Brush receives a virtual trigger.
        /// </summary>
        public static bool Trigger(bool isBrush)
        {
            return Active &&
                   isBrush &&
                   Instance.m_Right.trigger;
        }


        public static bool TriggerDown(bool isBrush)
        {
            return Active &&
                   isBrush &&
                   Instance.m_Right.triggerDown;
        }


        public static bool TriggerUp(bool isBrush)
        {
            return Active &&
                   isBrush &&
                   Instance.m_Right.triggerUp;
        }


        public static float FingerDistance(bool isBrush)
        {
            if (!Active)
                return float.PositiveInfinity;

            return isBrush
                ? Instance.m_Right.indexMiddleDistance
                : Instance.m_Left.indexMiddleDistance;
        }


        public static bool RightDrawHeld =>
            Active && Instance.m_Right.trigger;

        public static bool RightDrawDown =>
            Active && Instance.m_Right.triggerDown;

        public static bool RightDrawUp =>
            Active && Instance.m_Right.triggerUp;


        // --------------------------------------------------------------------
        // Unity lifecycle
        // --------------------------------------------------------------------

        private void Awake()
        {
            Instance = this;

            Debug.Log(
                "ANDROID_XR_HAND: AWAKE - AndroidXRHandBridge is active");
        }


        private void OnEnable()
        {
            Application.onBeforeRender += OnBeforeRender;
        }


        private void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }


        private void OnDestroy()
        {
            Application.onBeforeRender -= OnBeforeRender;

            if (Instance == this)
            {
                Instance = null;
            }
        }


        private void Start()
        {
            Debug.Log("ANDROID_XR_HAND: START");

            /*
             * IMPORTANT:
             *
             * AndroidXRHandBridge has DefaultExecutionOrder(-10000), so this Start()
             * executes before the normal SketchControlsScript.Start().
             *
             * SketchControlsScript.Start() initializes PanelManager using:
             *
             *     InitPanels(m_ControlsType == SixDofControllers)
             *
             * Therefore we MUST establish SixDofControllers here, before that Start()
             * runs. Setting this later in Update() is too late for the initial panel setup.
             */
            ForceOpenBrushSixDofModeEarly();

            FindHandSubsystem();
        }


        private void Update()
        {
            // ------------------------------------------------------------
            // 1. Find / recover XR Hands subsystem
            // ------------------------------------------------------------

            if (m_Subsystem == null || !m_Subsystem.running)
            {
                FindHandSubsystem();

                if (m_Subsystem == null || !m_Subsystem.running)
                {
                    DebugStateIfNeeded();
                    return;
                }
            }


            // ------------------------------------------------------------
            // 2. Replace Open Brush InitializingUnityXR controller structure
            // ------------------------------------------------------------

            EnsureOpenBrushHandMode();

            if (!m_OpenBrushHandModeInitialized)
            {
                DebugStateIfNeeded();
                return;
            }


            // ------------------------------------------------------------
            // 3. Disable physical controller pose drivers
            // ------------------------------------------------------------

            PrepareOpenBrushControllers();

            if (!m_ControllersPrepared)
            {
                DebugStateIfNeeded();
                return;
            }


            // ------------------------------------------------------------
            // 4. Read hand joints
            //
            // Left  = tracking only
            // Right = tracking + custom drawing trigger
            // ------------------------------------------------------------

            UpdateHand(
                m_Subsystem.leftHand,
                m_Left,
                allowDrawGesture: false);

            UpdateHand(
                m_Subsystem.rightHand,
                m_Right,
                allowDrawGesture: true);


            // ------------------------------------------------------------
            // 5. Drive Open Brush's virtual controllers from hand poses
            // ------------------------------------------------------------

            DriveOpenBrushControllers();


            // ------------------------------------------------------------
            // 6. Make sure Open Brush treats this as a 6DoF controller setup
            //    and requests its normal Wand panels.
            // ------------------------------------------------------------

            RequestPanels();


            // ------------------------------------------------------------
            // 7. Diagnostics
            // ------------------------------------------------------------

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

            HideOpenBrushControllerVisuals();
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
        // XR Hands subsystem
        // --------------------------------------------------------------------

        private void FindHandSubsystem()
        {
            var subsystems = new List<XRHandSubsystem>();

            SubsystemManager.GetSubsystems(subsystems);

            XRHandSubsystem firstSubsystem = null;

            foreach (XRHandSubsystem subsystem in subsystems)
            {
                if (subsystem == null)
                    continue;

                if (firstSubsystem == null)
                {
                    firstSubsystem = subsystem;
                }

                if (subsystem.running)
                {
                    m_Subsystem = subsystem;

                    if (debugLogging)
                    {
                        Debug.Log(
                            "ANDROID_XR_HAND: XRHandSubsystem found and running");
                    }

                    return;
                }
            }


            // Keep a reference even if it has not started running yet.
            m_Subsystem = firstSubsystem;

            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: XRHandSubsystem search complete. " +
                    $"found={(m_Subsystem != null)} " +
                    $"running={(m_Subsystem != null && m_Subsystem.running)}");
            }
        }


        // --------------------------------------------------------------------
        // Open Brush controller initialization
        // --------------------------------------------------------------------

        private void EnsureOpenBrushHandMode()
        {
            if (m_OpenBrushHandModeInitialized)
                return;

            if (m_Subsystem == null ||
                !m_Subsystem.running)
            {
                return;
            }

            if (App.VrSdk == null ||
                InputManager.m_Instance == null)
            {
                return;
            }


            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: switching Open Brush from " +
                    "InitializingUnityXR to virtual OculusTouch controller structure");
            }


            /*
             * We borrow OculusTouch only as Open Brush's INTERNAL controller
             * hierarchy. Android XR hands provide the actual pose and trigger.
             *
             * This gives the rest of Open Brush the objects it expects:
             *
             *     VrControllers
             *       Wand
             *         ControllerGeometry
             *         MainAxisAttachPoint
             *
             *       Brush
             *         ControllerGeometry
             *         PointerAttachPoint
             */
            App.VrSdk.SetControllerStyle(
                ControllerStyle.OculusTouch);


            /*
             * SetControllerStyle destroys/recreates VrControls.
             * Recreate InputManager ControllerInfo wrappers afterwards.
             */
            InputManager.m_Instance.CreateControllerInfos();


            if (InputManager.Wand == null ||
                InputManager.Brush == null)
            {
                Debug.LogWarning(
                    "ANDROID_XR_HAND: Open Brush controller infos were not ready " +
                    "after CreateControllerInfos(). Will retry.");

                return;
            }


            /*
             * Make sure controller processing itself stays enabled.
             */
            InputManager.m_Instance.AllowVrControllers = true;


            /*
             * Reinitialize Open Brush pointer presentation.
             */
            if (PointerManager.m_Instance != null)
            {
                PointerManager.m_Instance.RefreshFreePaintPointerAngle();
                PointerManager.m_Instance.RequestPointerRendering(true);
            }


            m_OpenBrushHandModeInitialized = true;


            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: Open Brush hand mode initialized. " +
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

            if (InputManager.m_Instance == null)
                return;

            if (InputManager.Controllers == null ||
                InputManager.Controllers.Length < 2)
            {
                return;
            }

            if (InputManager.Brush == null ||
                InputManager.Wand == null)
            {
                return;
            }


            /*
             * The borrowed Quest controller prefabs contain TrackedPoseDriver
             * components. Disable them so they cannot overwrite the transforms
             * that this bridge writes from XR Hands.
             */
            DisableTrackedPoseDrivers(
                InputManager.Brush.Behavior);

            DisableTrackedPoseDrivers(
                InputManager.Wand.Behavior);


            /*
             * Hide the controller visuals immediately.
             * OnBeforeRender() repeats this every frame.
             */
            if (hideControllerModels)
            {
                HideOpenBrushControllerVisuals();
            }


            m_ControllersPrepared = true;


            if (debugLogging)
            {
                Debug.Log(
                    "ANDROID_XR_HAND: virtual Open Brush controllers prepared");
            }
        }


        private static void DisableTrackedPoseDrivers(
            BaseControllerBehavior behavior)
        {
            if (behavior == null)
                return;

            TrackedPoseDriver[] poseDrivers =
                behavior.GetComponentsInChildren<TrackedPoseDriver>(
                    true);

            foreach (TrackedPoseDriver driver in poseDrivers)
            {
                driver.enabled = false;
            }
        }


        private void HideOpenBrushControllerVisuals()
        {
            if (InputManager.Brush != null)
            {
                InputManager.Brush.ShowController(false);
            }

            if (InputManager.Wand != null)
            {
                InputManager.Wand.ShowController(false);
            }
        }


        // --------------------------------------------------------------------
        // Hand processing
        // --------------------------------------------------------------------

        private void UpdateHand(
            XRHand hand,
            HandState state,
            bool allowDrawGesture)
        {
            bool previousTrigger = state.trigger;

            state.triggerDown = false;
            state.triggerUp = false;


            // ------------------------------------------------------------
            // Hand not tracked
            // ------------------------------------------------------------

            if (!hand.isTracked)
            {
                state.tracked = false;

                state.trigger = false;

                state.indexMiddleDistance =
                    float.PositiveInfinity;

                state.ringPalmDistance =
                    float.PositiveInfinity;

                state.littlePalmDistance =
                    float.PositiveInfinity;

                state.thumbPalmDistance =
                    float.PositiveInfinity;

                if (previousTrigger)
                {
                    state.triggerUp = true;
                }

                return;
            }


            // ------------------------------------------------------------
            // Mandatory joints
            // ------------------------------------------------------------

            XRHandJoint palmJoint =
                hand.GetJoint(
                    XRHandJointID.Palm);

            XRHandJoint indexTipJoint =
                hand.GetJoint(
                    XRHandJointID.IndexTip);

            XRHandJoint middleTipJoint =
                hand.GetJoint(
                    XRHandJointID.MiddleTip);


            if (!palmJoint.TryGetPose(out Pose palmLocal) ||
                !indexTipJoint.TryGetPose(out Pose indexLocal) ||
                !middleTipJoint.TryGetPose(out Pose middleLocal))
            {
                state.tracked = false;

                state.trigger = false;

                if (previousTrigger)
                {
                    state.triggerUp = true;
                }

                return;
            }


            state.tracked = true;


            // ------------------------------------------------------------
            // World-space poses used to move Open Brush
            // ------------------------------------------------------------

            state.palmPose =
                ToWorldPose(palmLocal);

            state.indexTipPose =
                ToWorldPose(indexLocal);

            state.middleTipPose =
                ToWorldPose(middleLocal);


            // ------------------------------------------------------------
            // Gesture distances stay in XR tracking/session space.
            //
            // XR Hands joint positions are in metres, so these thresholds
            // stay meaningful even if the Open Brush world has a scale.
            // ------------------------------------------------------------

            state.indexMiddleDistance =
                Vector3.Distance(
                    indexLocal.position,
                    middleLocal.position);


            // ------------------------------------------------------------
            // Optional finger-closed joints
            //
            // These are NOT mandatory when Require Other Fingers Closed is
            // disabled. This avoids losing all drawing simply because one
            // secondary fingertip pose is temporarily unavailable.
            // ------------------------------------------------------------

            state.ringPoseValid = false;
            state.littlePoseValid = false;
            state.thumbPoseValid = false;

            state.ringPalmDistance =
                float.PositiveInfinity;

            state.littlePalmDistance =
                float.PositiveInfinity;

            state.thumbPalmDistance =
                float.PositiveInfinity;


            XRHandJoint ringTipJoint =
                hand.GetJoint(
                    XRHandJointID.RingTip);

            if (ringTipJoint.TryGetPose(out Pose ringLocal))
            {
                state.ringPoseValid = true;

                state.ringPalmDistance =
                    Vector3.Distance(
                        ringLocal.position,
                        palmLocal.position);
            }


            XRHandJoint littleTipJoint =
                hand.GetJoint(
                    XRHandJointID.LittleTip);

            if (littleTipJoint.TryGetPose(out Pose littleLocal))
            {
                state.littlePoseValid = true;

                state.littlePalmDistance =
                    Vector3.Distance(
                        littleLocal.position,
                        palmLocal.position);
            }


            XRHandJoint thumbTipJoint =
                hand.GetJoint(
                    XRHandJointID.ThumbTip);

            if (thumbTipJoint.TryGetPose(out Pose thumbLocal))
            {
                state.thumbPoseValid = true;

                state.thumbPalmDistance =
                    Vector3.Distance(
                        thumbLocal.position,
                        palmLocal.position);
            }


            // ------------------------------------------------------------
            // Left hand never generates the drawing trigger.
            // ------------------------------------------------------------

            if (!allowDrawGesture)
            {
                state.trigger = false;

                if (previousTrigger)
                {
                    state.triggerUp = true;
                }

                return;
            }


            // ------------------------------------------------------------
            // Optional "other fingers closed" condition
            // ------------------------------------------------------------

            bool ringClosed =
                state.ringPoseValid &&
                state.ringPalmDistance <
                ringClosedDistance;

            bool littleClosed =
                state.littlePoseValid &&
                state.littlePalmDistance <
                littleClosedDistance;

            bool thumbClosed =
                state.thumbPoseValid &&
                state.thumbPalmDistance <
                thumbClosedDistance;


            bool otherFingersAccepted =
                !requireOtherFingersClosed ||
                (
                    ringClosed &&
                    littleClosed &&
                    thumbClosed
                );


            // ------------------------------------------------------------
            // Drawing trigger with hysteresis
            // ------------------------------------------------------------

            if (previousTrigger)
            {
                state.trigger =
                    state.indexMiddleDistance <
                    drawReleaseDistance
                    &&
                    otherFingersAccepted;
            }
            else
            {
                state.trigger =
                    state.indexMiddleDistance <
                    drawPressDistance
                    &&
                    otherFingersAccepted;
            }


            state.triggerDown =
                !previousTrigger &&
                state.trigger;

            state.triggerUp =
                previousTrigger &&
                !state.trigger;


            if (debugLogging &&
                (state.triggerDown ||
                 state.triggerUp))
            {
                Debug.Log(
                    "ANDROIDXR_DRAW " +
                    $"trigger={state.trigger} " +
                    $"down={state.triggerDown} " +
                    $"up={state.triggerUp} " +
                    $"indexMiddle={state.indexMiddleDistance:F3} " +
                    $"ringPalm={state.ringPalmDistance:F3} " +
                    $"littlePalm={state.littlePalmDistance:F3} " +
                    $"thumbPalm={state.thumbPalmDistance:F3} " +
                    $"requireClosed={requireOtherFingersClosed} " +
                    $"ringClosed={ringClosed} " +
                    $"littleClosed={littleClosed} " +
                    $"thumbClosed={thumbClosed}");
            }
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


        // --------------------------------------------------------------------
        // Drive Open Brush virtual controllers
        // --------------------------------------------------------------------

        private void DriveOpenBrushControllers()
        {
            if (!m_ControllersPrepared)
                return;


            // RIGHT HAND = BRUSH
            if (m_Right.tracked &&
                InputManager.Brush != null)
            {
                DriveBrush(
                    InputManager.Brush.Behavior,
                    m_Right);
            }


            // LEFT HAND = WAND
            if (m_Left.tracked &&
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


            /*
             * Put Open Brush's existing PointerAttachPoint directly between
             * the index and middle fingertips.
             */
            Vector3 desiredPointerPosition =
                (
                    hand.indexTipPose.position +
                    hand.middleTipPose.position
                )
                * 0.5f;


            /*
             * Start with palm orientation.
             * brushRotationOffset can correct the pointer direction in Inspector.
             */
            Quaternion desiredRotation =
                hand.palmPose.rotation *
                Quaternion.Euler(
                    brushRotationOffset);


            /*
             * Preserve the controller prefab's local offset between its root
             * and PointerAttachPoint.
             */
            Vector3 pointerLocal =
                root.InverseTransformPoint(
                    behavior.PointerAttachPoint.position);


            root.rotation =
                desiredRotation;


            Vector3 pointerOffsetWorld =
                root.TransformVector(
                    pointerLocal);


            root.position =
                desiredPointerPosition -
                pointerOffsetWorld +
                desiredRotation *
                brushPositionOffset;
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


            Quaternion desiredRotation =
                hand.palmPose.rotation *
                Quaternion.Euler(
                    wandRotationOffset);


            behavior.transform.rotation =
                desiredRotation;


            behavior.transform.position =
                hand.palmPose.position +
                desiredRotation *
                wandPositionOffset;
        }


        // --------------------------------------------------------------------
        // Open Brush panels
        // --------------------------------------------------------------------

        private void RequestPanels()
        {
            if (!showPanels ||
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
                $"subsystem={(m_Subsystem != null)} " +
                $"running={(m_Subsystem != null && m_Subsystem.running)} " +
                $"initialized={m_OpenBrushHandModeInitialized} " +
                $"prepared={m_ControllersPrepared} " +
                $"L={m_Left.tracked} " +
                $"R={m_Right.tracked} " +
                $"draw={m_Right.trigger} " +
                $"IM={m_Right.indexMiddleDistance:F3} " +
                $"ring={m_Right.ringPalmDistance:F3} " +
                $"little={m_Right.littlePalmDistance:F3} " +
                $"thumb={m_Right.thumbPalmDistance:F3} " +
                $"wandValid={wandValid} " +
                $"brushValid={brushValid} " +
                $"panelsRequested={m_PanelsRequested}");
        }
    }
}
