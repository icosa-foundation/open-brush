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
    /// Drawing gesture (right hand):
    ///     Index and middle fingers extended and held together,
    ///     with thumb, ring and little fingers closed/tucked.
    ///
    /// Menu interaction is a separate right-index fingertip touch/collision
    /// state. It is intentionally not merged into the Open Brush draw trigger.
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

        [Tooltip("Maximum RingTip -> Palm distance for ring finger to count as closed.")]
        [SerializeField]
        private float ringClosedDistance = 0.075f;

        [Tooltip("Maximum LittleTip -> Palm distance for little finger to count as closed.")]
        [SerializeField]
        private float littleClosedDistance = 0.070f;

        [Tooltip("Maximum ThumbTip -> Palm distance for thumb to count as closed/tucked.")]
        [SerializeField]
        private float thumbClosedDistance = 0.070f;

        [Tooltip(
            "Maximum bend angle, in degrees, allowed in the index and middle fingers " +
            "for them to count as extended.")]
        [SerializeField]
        private float extendedFingerMaxBendAngle = 35.0f;

        [Tooltip(
            "Maximum angle between the final index and middle finger directions " +
            "while the draw gesture is active.")]
        [SerializeField]
        private float drawMaxFingerDirectionAngle = 30.0f;


        [Header("Menu Touch - Right Index")]

        [Tooltip(
            "Radius in metres around the right index fingertip used to detect direct " +
            "contact with Open Brush panel/UI colliders.")]
        [SerializeField]
        private float menuTouchRadius = 0.012f;

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

        private const int kMenuTouchHitBufferSize = 32;
        private readonly Collider[] m_MenuTouchHits =
            new Collider[kMenuTouchHitBufferSize];

        private bool m_MenuTouchHeld;
        private bool m_MenuTouchDown;
        private bool m_MenuTouchUp;
        private Collider m_MenuTouchCollider;


        private class HandState
        {
            public bool tracked;

            // "trigger" is deliberately the DRAW trigger only.
            public bool trigger;
            public bool triggerDown;
            public bool triggerUp;

            public float indexMiddleDistance = float.PositiveInfinity;
            public float ringPalmDistance = float.PositiveInfinity;
            public float littlePalmDistance = float.PositiveInfinity;
            public float thumbPalmDistance = float.PositiveInfinity;

            public float indexBendAngle = float.PositiveInfinity;
            public float middleBendAngle = float.PositiveInfinity;
            public float indexMiddleDirectionAngle = float.PositiveInfinity;

            public bool ringPoseValid;
            public bool littlePoseValid;
            public bool thumbPoseValid;

            public bool indexDirectionValid;
            public bool middleDirectionValid;
            public Vector3 indexDirectionWorld = Vector3.forward;
            public Vector3 middleDirectionWorld = Vector3.forward;

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


        /// <summary>
        /// Direct right-index contact with an Open Brush panel/UI collider.
        /// This is separate from the drawing trigger.
        /// </summary>
        public static bool MenuTouchHeld =>
            Active && Instance.m_MenuTouchHeld;

        public static bool MenuTouchDown =>
            Active && Instance.m_MenuTouchDown;

        public static bool MenuTouchUp =>
            Active && Instance.m_MenuTouchUp;

        public static Collider MenuTouchCollider =>
            Active ? Instance.m_MenuTouchCollider : null;

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

            bool wasDrawingBeforeHandUpdate =
                m_Right.trigger;

            UpdateHand(
                m_Subsystem.rightHand,
                m_Right,
                allowDrawGesture: true);

            // Direct panel contact is a separate action from drawing.
            UpdateMenuTouchState();

            // Never allow a draw stroke to continue/start while the right
            // index fingertip is physically pressing panel UI.
            if (m_MenuTouchHeld)
            {
                SuppressDrawForMenuContact(
                    wasDrawingBeforeHandUpdate);
            }


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
                ResetHandState(
                    state,
                    previousTrigger);

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
                ResetHandState(
                    state,
                    previousTrigger);

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
            // Index / middle final bone directions
            //
            // The index direction is also used for the Open Brush pointer.
            // ------------------------------------------------------------

            state.indexDirectionValid =
                TryGetFingerTipDirection(
                    hand,
                    XRHandJointID.IndexDistal,
                    XRHandJointID.IndexTip,
                    out Vector3 indexDirectionLocal);

            state.middleDirectionValid =
                TryGetFingerTipDirection(
                    hand,
                    XRHandJointID.MiddleDistal,
                    XRHandJointID.MiddleTip,
                    out Vector3 middleDirectionLocal);


            if (state.indexDirectionValid)
            {
                state.indexDirectionWorld =
                    ToWorldDirection(
                        indexDirectionLocal);
            }
            else
            {
                // Fallback keeps the pointer usable if a single distal joint
                // temporarily drops out.
                state.indexDirectionWorld =
                    state.indexTipPose.rotation *
                    Vector3.forward;
            }


            if (state.middleDirectionValid)
            {
                state.middleDirectionWorld =
                    ToWorldDirection(
                        middleDirectionLocal);
            }
            else
            {
                state.middleDirectionWorld =
                    state.middleTipPose.rotation *
                    Vector3.forward;
            }


            state.indexMiddleDirectionAngle =
                state.indexDirectionValid &&
                state.middleDirectionValid
                    ? Vector3.Angle(
                        indexDirectionLocal,
                        middleDirectionLocal)
                    : float.PositiveInfinity;


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
            // Closed/tucked finger checks
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
            // Index + middle must be extended.
            //
            // We use the maximum bend across the finger's final three
            // segments. This avoids a closed fist accidentally satisfying
            // the "tips are close together" test.
            // ------------------------------------------------------------

            bool indexBendValid =
                TryGetFingerMaxBendAngle(
                    hand,
                    XRHandJointID.IndexProximal,
                    XRHandJointID.IndexIntermediate,
                    XRHandJointID.IndexDistal,
                    XRHandJointID.IndexTip,
                    out state.indexBendAngle);

            bool middleBendValid =
                TryGetFingerMaxBendAngle(
                    hand,
                    XRHandJointID.MiddleProximal,
                    XRHandJointID.MiddleIntermediate,
                    XRHandJointID.MiddleDistal,
                    XRHandJointID.MiddleTip,
                    out state.middleBendAngle);

            bool indexExtended =
                indexBendValid &&
                state.indexBendAngle <=
                extendedFingerMaxBendAngle;

            bool middleExtended =
                middleBendValid &&
                state.middleBendAngle <=
                extendedFingerMaxBendAngle;

            bool indexAndMiddleParallel =
                state.indexDirectionValid &&
                state.middleDirectionValid &&
                state.indexMiddleDirectionAngle <=
                drawMaxFingerDirectionAngle;


            // ------------------------------------------------------------
            // Remaining fingers must be closed/tucked.
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

            bool requiredPose =
                indexExtended &&
                middleExtended &&
                indexAndMiddleParallel &&
                ringClosed &&
                littleClosed &&
                thumbClosed;


            // ------------------------------------------------------------
            // Drawing trigger with hysteresis
            //
            // Gesture:
            //   - index + middle fingertips together
            //   - index + middle extended
            //   - index + middle pointing in approximately same direction
            //   - thumb + ring + little closed/tucked
            // ------------------------------------------------------------

            float indexMiddleThreshold =
                previousTrigger
                    ? drawReleaseDistance
                    : drawPressDistance;

            state.trigger =
                state.indexMiddleDistance <
                indexMiddleThreshold
                &&
                requiredPose;


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
                    $"indexBend={state.indexBendAngle:F1} " +
                    $"middleBend={state.middleBendAngle:F1} " +
                    $"dirAngle={state.indexMiddleDirectionAngle:F1} " +
                    $"ringPalm={state.ringPalmDistance:F3} " +
                    $"littlePalm={state.littlePalmDistance:F3} " +
                    $"thumbPalm={state.thumbPalmDistance:F3} " +
                    $"indexExtended={indexExtended} " +
                    $"middleExtended={middleExtended} " +
                    $"parallel={indexAndMiddleParallel} " +
                    $"ringClosed={ringClosed} " +
                    $"littleClosed={littleClosed} " +
                    $"thumbClosed={thumbClosed}");
            }
        }


        private static void ResetHandState(
            HandState state,
            bool previousTrigger)
        {
            state.tracked = false;

            state.trigger = false;
            state.triggerDown = false;
            state.triggerUp = previousTrigger;

            state.indexMiddleDistance =
                float.PositiveInfinity;

            state.ringPalmDistance =
                float.PositiveInfinity;

            state.littlePalmDistance =
                float.PositiveInfinity;

            state.thumbPalmDistance =
                float.PositiveInfinity;

            state.indexBendAngle =
                float.PositiveInfinity;

            state.middleBendAngle =
                float.PositiveInfinity;

            state.indexMiddleDirectionAngle =
                float.PositiveInfinity;

            state.ringPoseValid = false;
            state.littlePoseValid = false;
            state.thumbPoseValid = false;

            state.indexDirectionValid = false;
            state.middleDirectionValid = false;

            state.indexDirectionWorld =
                Vector3.forward;

            state.middleDirectionWorld =
                Vector3.forward;
        }


        private static bool TryGetFingerTipDirection(
            XRHand hand,
            XRHandJointID distalJointId,
            XRHandJointID tipJointId,
            out Vector3 direction)
        {
            direction = Vector3.forward;

            XRHandJoint distalJoint =
                hand.GetJoint(
                    distalJointId);

            XRHandJoint tipJoint =
                hand.GetJoint(
                    tipJointId);

            if (!distalJoint.TryGetPose(out Pose distalPose) ||
                !tipJoint.TryGetPose(out Pose tipPose))
            {
                return false;
            }

            Vector3 delta =
                tipPose.position -
                distalPose.position;

            if (delta.sqrMagnitude <
                0.00000001f)
            {
                return false;
            }

            direction =
                delta.normalized;

            return true;
        }


        private static bool TryGetFingerMaxBendAngle(
            XRHand hand,
            XRHandJointID proximalJointId,
            XRHandJointID intermediateJointId,
            XRHandJointID distalJointId,
            XRHandJointID tipJointId,
            out float maxBendAngle)
        {
            maxBendAngle =
                float.PositiveInfinity;

            XRHandJoint proximalJoint =
                hand.GetJoint(
                    proximalJointId);

            XRHandJoint intermediateJoint =
                hand.GetJoint(
                    intermediateJointId);

            XRHandJoint distalJoint =
                hand.GetJoint(
                    distalJointId);

            XRHandJoint tipJoint =
                hand.GetJoint(
                    tipJointId);


            if (!proximalJoint.TryGetPose(out Pose proximalPose) ||
                !intermediateJoint.TryGetPose(out Pose intermediatePose) ||
                !distalJoint.TryGetPose(out Pose distalPose) ||
                !tipJoint.TryGetPose(out Pose tipPose))
            {
                return false;
            }


            Vector3 proximalDirection =
                intermediatePose.position -
                proximalPose.position;

            Vector3 middleDirection =
                distalPose.position -
                intermediatePose.position;

            Vector3 distalDirection =
                tipPose.position -
                distalPose.position;


            if (proximalDirection.sqrMagnitude <
                    0.00000001f ||
                middleDirection.sqrMagnitude <
                    0.00000001f ||
                distalDirection.sqrMagnitude <
                    0.00000001f)
            {
                return false;
            }


            float firstBend =
                Vector3.Angle(
                    proximalDirection,
                    middleDirection);

            float secondBend =
                Vector3.Angle(
                    middleDirection,
                    distalDirection);

            maxBendAngle =
                Mathf.Max(
                    firstBend,
                    secondBend);

            return true;
        }


        // --------------------------------------------------------------------
        // Separate direct-touch menu input
        // --------------------------------------------------------------------

        private void UpdateMenuTouchState()
        {
            bool previousTouch =
                m_MenuTouchHeld;

            m_MenuTouchHeld = false;
            m_MenuTouchDown = false;
            m_MenuTouchUp = false;
            m_MenuTouchCollider = null;


            if (!m_Right.tracked)
            {
                m_MenuTouchUp =
                    previousTouch;

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

                if (!IsPanelUiCollider(
                        hit))
                {
                    continue;
                }


                Vector3 closestPoint =
                    hit.ClosestPoint(
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
                        hit;
                }
            }


            m_MenuTouchHeld =
                m_MenuTouchCollider != null;

            m_MenuTouchDown =
                !previousTouch &&
                m_MenuTouchHeld;

            m_MenuTouchUp =
                previousTouch &&
                !m_MenuTouchHeld;


            if (debugLogging &&
                (m_MenuTouchDown ||
                 m_MenuTouchUp))
            {
                Debug.Log(
                    "ANDROIDXR_MENU_TOUCH " +
                    $"held={m_MenuTouchHeld} " +
                    $"down={m_MenuTouchDown} " +
                    $"up={m_MenuTouchUp} " +
                    $"collider=" +
                    $"{(m_MenuTouchCollider != null ? m_MenuTouchCollider.name : "none")}");
            }
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

            Transform pointer =
                behavior.PointerAttachPoint;


            /*
             * The Open Brush pointer now starts exactly at the right index tip.
             */
            Vector3 desiredPointerPosition =
                hand.indexTipPose.position;


            /*
             * Aim along the physical index finger:
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
                      Vector3.forward;


            if (pointerForward.sqrMagnitude <
                0.00000001f)
            {
                pointerForward =
                    hand.palmPose.rotation *
                    Vector3.forward;
            }

            pointerForward.Normalize();


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
                    pointerUp)
                *
                Quaternion.Euler(
                    brushRotationOffset);


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


            root.position =
                desiredPointerPosition
                -
                pointerOffsetWorld
                +
                desiredPointerRotation
                *
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
                $"menuTouch={m_MenuTouchHeld} " +
                $"IM={m_Right.indexMiddleDistance:F3} " +
                $"indexBend={m_Right.indexBendAngle:F1} " +
                $"middleBend={m_Right.middleBendAngle:F1} " +
                $"dirAngle={m_Right.indexMiddleDirectionAngle:F1} " +
                $"ring={m_Right.ringPalmDistance:F3} " +
                $"little={m_Right.littlePalmDistance:F3} " +
                $"thumb={m_Right.thumbPalmDistance:F3} " +
                $"wandValid={wandValid} " +
                $"brushValid={brushValid} " +
                $"panelsRequested={m_PanelsRequested}");
        }
    }
}
