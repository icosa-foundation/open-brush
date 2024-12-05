// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using OpenXR.Extensions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using InputDevice = UnityEngine.XR.InputDevice;

namespace TiltBrush
{
    // If these names are used in analytics etc, they must be protected from obfuscation.
    // Do not change the names of any of them, unless they've never been released.
    [Serializable]
    public enum ControllerStyle
    {
        Unset,
        None,
        InitializingUnityXR,   // can change to "initialising" or "discovering"
        Vive,
        Knuckles,
        OculusTouch,
        Wmr,
        Gvr,
        LogitechPen,
        Cosmos,
        Neo3,
        Phoenix,
        Zapbox,
    }

    //
    // The VrSdk is an abstraction over the actual VR hardware and SDK. It is responsible for:
    //
    //   * Initializating the VR system, cameras, controllers and associated state.
    //   * Providing hardware- and SDK-specific controls via a non-specific interface.
    //   * Providing abstract access to events sent from the SDK.
    //   * Exposing an interface to query Hardware and SDK capabilities.
    //
    // TODO: In its current form, the VrSdk is monolithic, though it should ultimately be
    // broken out into hardware- and SDK-specific modules, which can be loaded and unloaded at startup
    // or build time.
    //
    public class VrSdk : MonoBehaviour
    {
        // VR  Data and Prefabs for specific VR systems
        [SerializeField] private GameObject m_VrSystem;
        [SerializeField] private GameObject m_UnityXRUninitializedControlsPrefab;
        [SerializeField] private GameObject m_UnityXRViveControlsPrefab;
        [SerializeField] private GameObject m_UnityXRRiftControlsPrefab;
        [SerializeField] private GameObject m_UnityXRQuestControlsPrefab;
        [SerializeField] private GameObject m_UnityXRWmrControlsPrefab;
        [SerializeField] private GameObject m_UnityXRKnucklesControlsPrefab;
        [SerializeField] private GameObject m_UnityXRCosmosControlsPrefab;
        [SerializeField] private GameObject m_UnityXRNeo3ControlsPrefab;
        [SerializeField] private GameObject m_UnityXRPhoenixControlsPrefab;
        [SerializeField] private GameObject m_UnityXRZapboxControlsPrefab;
        [SerializeField] private GameObject m_GvrPointerControlsPrefab;
        [SerializeField] private GameObject m_NonVrControlsPrefab;

        // This is the object "Camera (eye)"
        [SerializeField] private Camera m_VrCamera;

        // Runtime VR Spawned Controllers
        //  - This is the source of truth for controllers.
        //  - InputManager.m_ControllerInfos stores links to some of these components, but may be
        //    out of date for a frame when controllers change.
        private VrControllers m_VrControls;
        public VrControllers VrControls { get { return m_VrControls; } }
        private bool m_HasVrFocus = true;

        public PassthroughMode PassthroughMode { get; private set; } = PassthroughMode.None;

        private Bounds? m_RoomBoundsAabbCached;

        private Action[] m_OldOnPoseApplied;

        private bool m_NeedsToAttachConsoleScript;
        private TrTransform? m_TrackingBackupXf;

        // Degrees of Freedom.
        public enum DoF
        {
            None,
            Two, // Mouse & Keyboard
            Six, // Vive, Rift, etc
        }

        // -------------------------------------------------------------------------------------------- //
        // Public Events
        // -------------------------------------------------------------------------------------------- //

        // Called when new poses are ready.
        public Action OnNewControllerPosesApplied;

        // -------------------------------------------------------------------------------------------- //
        // Public Controller Properties
        // -------------------------------------------------------------------------------------------- //

        public bool IsInitializingUnityXR
        {
            get => VrControls.Brush.ControllerGeometry.Style == ControllerStyle.InitializingUnityXR;
        }

        // -------------------------------------------------------------------------------------------- //
        // Private Unity Component Events
        // -------------------------------------------------------------------------------------------- //

        void Awake()
        {
            bool forceMonoscopic =
                App.UserConfig.Flags.EnableMonoscopicMode ||
                Keyboard.current[Key.M].isPressed;

            bool disableXr = App.UserConfig.Flags.DisableXrMode ||
                Keyboard.current[Key.D].isPressed;

            // Allow forcing of monoscopic mode even if launching in XR
            if (forceMonoscopic && !(App.Config.m_SdkMode == SdkMode.Ods))
            {
                App.Config.m_SdkMode = SdkMode.Monoscopic;
            }
            else if (!disableXr)
            {
                // We no longer initialize XR SDKs automatically
                // so we need to do it manually

                // Null checks are for Linux view mode
                // TODO: Need to investigate exactly why Linux hits an NRE here
                // When other platforms don't
                XRGeneralSettings.Instance?.Manager?.InitializeLoaderSync();

                if (XRGeneralSettings.Instance?.Manager?.activeLoader != null)
                {
                    XRGeneralSettings.Instance?.Manager?.StartSubsystems();
                }
            }

            if (App.Config.m_SdkMode == SdkMode.UnityXR)
            {
                InputDevices.deviceConnected += OnUnityXRDeviceConnected;
                InputDevices.deviceDisconnected += OnUnityXRDeviceDisconnected;

                // TODO:Mikesky - We need to set a controller style, is it best here or is it best later when controllers register themselves?
                // Does this entire system need a rethink for the 'modularity' of the XR subsystem?
                InputDevice tryGetUnityXRController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
                if (!tryGetUnityXRController.isValid)
                {
                    // Try the right hand instead
                    tryGetUnityXRController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                }

                if (!tryGetUnityXRController.isValid)
                {
                    // Leave for when UnityXR is ready.
                    SetControllerStyle(ControllerStyle.InitializingUnityXR);
                }
                else
                {
                    SetUnityXRControllerStyle(tryGetUnityXRController);
                }

                SetPassthroughStrategy();
            }
            else if (App.Config.m_SdkMode == SdkMode.Monoscopic)
            {
                // ---------------------------------------------------------------------------------------- //
                // Monoscopic
                // ---------------------------------------------------------------------------------------- //
                m_VrCamera.gameObject.AddComponent<MonoCameraControlScript>();
                var xrOrigin = m_VrCamera.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>();
                xrOrigin.CameraFloorOffsetObject.transform.localPosition = new Vector3(0.0f, 1.5f, 0.0f);
                SetControllerStyle(ControllerStyle.None);
            }
            else
            {
                // ---------------------------------------------------------------------------------------- //
                // Non-VR
                // ---------------------------------------------------------------------------------------- //
                SetControllerStyle(ControllerStyle.None);
            }

            m_VrCamera.gameObject.SetActive(true);
            m_VrSystem.SetActive(m_VrCamera.gameObject.activeSelf);

            // Skip the rest of the VR setup if we're not using XR
            if (App.UserConfig.Flags.DisableXrMode || App.UserConfig.Flags.EnableMonoscopicMode) return;

            UnityEngine.XR.OpenXR.OpenXRSettings.SetAllowRecentering(false);

            // Let it fail on non-oculus platforms
            //Get Oculus ID
            var oculusAppId = App.Config.OculusSecrets.ClientId;
            bool packagePresent = true;
#if UNITY_ANDROID
            oculusAppId = App.Config.OculusMobileSecrets.ClientId;
            // Initialize() will crash android if the required system packages are not present.
            // This is the earliest in the chain.
            packagePresent = AndroidUtils.IsPackageInstalled("com.oculus.platformsdkruntime");
#endif
            if (packagePresent)
            {
                Oculus.Platform.Core.Initialize(oculusAppId);

                Oculus.Platform.UserAgeCategory.Get()?.OnComplete((msg) =>
                {
                    if (!msg.IsError)
                    {
                        var unused = msg.Data.AgeCategory;
                    }
                });
            }
        }

        void Start()
        {
            if (App.Config.m_SdkMode == SdkMode.UnityXR)
            {
                Application.onBeforeRender += OnNewPoses;
            }

            var displaySubsystem = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();

            if (displaySubsystem != null)
            {
                displaySubsystem.displayFocusChanged += OnInputFocus;
            }

            if (m_NeedsToAttachConsoleScript && m_VrControls != null)
            {
                ControllerConsoleScript.m_Instance.AttachToController(m_VrControls.Brush);
                m_NeedsToAttachConsoleScript = false;
            }
        }

        void Update()
        {
            if (App.Config.m_SdkMode == SdkMode.UnityXR)
            {
                OnNewPoses();
            }
        }

        void OnDestroy()
        {
            if (App.Config.m_SdkMode == SdkMode.UnityXR)
            {
                Application.onBeforeRender -= OnNewPoses;
                InputDevices.deviceConnected -= OnUnityXRDeviceConnected;
                InputDevices.deviceDisconnected -= OnUnityXRDeviceDisconnected;
                if (XRGeneralSettings.Instance?.Manager?.activeLoader != null)
                {
                    XRGeneralSettings.Instance?.Manager?.StopSubsystems();
                    XRGeneralSettings.Instance?.Manager?.DeinitializeLoader();
                }
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // Private VR SDK-Related Events
        // -------------------------------------------------------------------------------------------- //

        private void OnInputFocus(bool focused)
        {
            App.Log($"VrSdk.OnInputFocus -> {focused}");
            InputManager.m_Instance.AllowVrControllers = focused;
            m_HasVrFocus = focused;
        }

        private void OnNewPoses()
        {
            OnNewControllerPosesApplied?.Invoke();
        }

        // -------------------------------------------------------------------------------------------- //
        // Camera Methods
        // -------------------------------------------------------------------------------------------- //

        /// Returns a camera actually used for rendering. The associated transform
        /// may not be the transform of the head -- camera may have an eye offset.
        /// TODO: revisit callers and see if anything should be using GetHeadTransform() instead.
        ///
        /// XXX: Why do we have this instead of Camera.main? Also, due to our current setup,
        /// Camera.main is currently broken in monoscope mode (and maybe oculus?) due ot the fact that the
        /// camera is not tagged as "MainCamera".
        public Camera GetVrCamera()
        {
            return m_VrCamera;
        }

        // -------------------------------------------------------------------------------------------- //
        // Feature Methods
        // -------------------------------------------------------------------------------------------- //

        private void SetPassthroughStrategy()
        {
            if (FBPassthrough.FeatureEnabled)
            {
                PassthroughMode = PassthroughMode.FBPassthrough;
                return;
            }

#if ZAPBOX_SUPPORTED
            PassthroughMode = PassthroughMode.Zapbox;
            return;
#endif // ZAPBOX_SUPPORTED

            PassthroughMode = PassthroughMode.None;
        }

        // -------------------------------------------------------------------------------------------- //
        // Profiling / VR Utility Methods
        // -------------------------------------------------------------------------------------------- //

        // Returns the time of the most recent number of dropped frames, null on failure.
        public int? GetDroppedFrames()
        {
            var displaySubsystem = XRGeneralSettings.Instance?.Manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
            if (displaySubsystem != null && displaySubsystem.TryGetDroppedFrameCount(out var droppedFrames))
            {
                return droppedFrames;
            }

            return null;
        }

        // -------------------------------------------------------------------------------------------- //
        // Room Bounds / Chaperone Methods
        // -------------------------------------------------------------------------------------------- //

        // Returns true if GetRoomBounds() will return a non-zero volume.
        public bool HasRoomBounds()
        {
            return GetRoomBoundsAabb().extents != Vector3.zero;
        }

        // Returns the extents of the room bounds, which is the half-vector of the axis aligned bounds.
        // This value is returned in Tilt Brush room coordinates.
        // Extents are non-negative.
        public Vector3 GetRoomExtents()
        {
            return GetRoomBoundsAabb().extents;
        }

        /// Returns room bounds as an AABB in Tilt Brush room coordinates.
        public Bounds GetRoomBoundsAabb()
        {
            if (m_RoomBoundsAabbCached == null)
            {
                RefreshRoomBoundsCache();
            }
            return m_RoomBoundsAabbCached.Value;
        }

        // re-calculate m_RoomBoundsPointsCached and m_RoomBoundsAabbCached
        private void RefreshRoomBoundsCache()
        {
            Vector3[] points_RS = null;

#if OCULUS_SUPPORTED
                // N points, clockwise winding (but axis is undocumented), undocumented convexity
                // In practice, it's clockwise looking along Y-
                points_RS = OVRManager.boundary
                    ?.GetGeometry(OVRBoundary.BoundaryType.OuterBoundary)
                    ?.Select(v => UnityFromOculus(v))
                    .ToArray();
#else // OCULUS_SUPPORTED
            // if (App.Config.m_SdkMode == SdkMode.SteamVR)
            // {
            //     // TODO:Mikesky - Setting OpenVR Chaperone bounds. Does XR have the equivalent generic?
            //     // var chaperone = OpenVR.Chaperone;
            //     // if (chaperone != null)
            //     // {
            //     //     HmdQuad_t rect = new HmdQuad_t();
            //     //     // 4 points, undocumented winding, undocumented convexity
            //     //     // Undocumented if it's an AABB
            //     //     // In practice, seems to always be an axis-aligned clockwise box.
            //     //     chaperone.GetPlayAreaRect(ref rect);
            //     //     var steamPoints = new[]
            //     //     {
            //     //         rect.vCorners0, rect.vCorners1, rect.vCorners2, rect.vCorners3
            //     //     };
            //     //     points_RS = steamPoints.Select(v => UnityFromSteamVr(v)).ToArray();
            //     // }
            // }
#endif // OCULUS_SUPPORTED

            if (points_RS == null)
            {
                points_RS = new Vector3[0];
            }

            // We could use points_RS to expose a raw-points-based API, and currently
            // we can offer the guarantee that the points are clockwise (looking along Y-),
            // and convex. So far, nobody needs it.
            // Debug.Assert(IsClockwiseConvex(points_RS));
            // m_RoomBoundsPointsCached = points_RS.

            m_RoomBoundsAabbCached = FromPoints(points_RS);
        }

        /// If points is empty, returns the default (empty) Bounds
        static private Bounds FromPoints(IEnumerable<Vector3> points)
        {
            using (var e = points.GetEnumerator())
            {
                if (!e.MoveNext())
                {
                    return new Bounds();
                }
                Bounds bounds = new Bounds(e.Current, Vector3.zero);
                while (e.MoveNext())
                {
                    bounds.Encapsulate(e.Current);
                }
                return bounds;
            }
        }

        // Used for debugging.
        static private bool IsClockwiseConvex(Vector3[] points)
        {
            for (int i = 0; i < points.Length; ++i)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Length];
                Vector3 c = points[(i + 2) % points.Length];
                Vector3 ab = b - a;
                Vector3 bc = c - b;
                if (Vector3.Dot(Vector3.Cross(ab, bc), Vector3.up) < 0)
                {
                    return false;
                }
            }
            return true;
        }

        // TODO:Mikesky - This function is only used in SteamVR's version of RefreshRoomBoundsCache
        // /// Converts from SteamVR axis conventions and units to Unity
        // static private Vector3 UnityFromSteamVr(HmdVector3_t v)
        // {
        //     return new Vector3(v.v0, v.v1, v.v2) * App.METERS_TO_UNITS;
        // }

        /// Converts from Oculus axis conventions and units to Unity
        static private Vector3 UnityFromOculus(Vector3 v)
        {
            return v * App.METERS_TO_UNITS;
        }

        // -------------------------------------------------------------------------------------------- //
        // Controller Methods
        // -------------------------------------------------------------------------------------------- //
        // A scaling factor for when adjusting the brush size.
        // The thumbstick 0..1 value moves too fast.
        public float SwipeScaleAdjustment(InputManager.ControllerName name)
        {
            return AnalogIsStick(name) ? 0.025f : 1.0f;
        }

        public bool AnalogIsStick(InputManager.ControllerName name)
        {
            var style = VrControls.GetBehavior(name).ControllerGeometry.Style;
            return style == ControllerStyle.Wmr ||
                style == ControllerStyle.OculusTouch ||
                style == ControllerStyle.Knuckles ||
                style == ControllerStyle.Cosmos ||
                style == ControllerStyle.Neo3 ||
                style == ControllerStyle.Phoenix ||
                style == ControllerStyle.Zapbox;
        }

        // Destroy and recreate the ControllerBehavior and ControllerGeometry objects.
        // This is mostly useful if you want different geometry.
        //
        // TODO: this will always give the wand left-hand geometry and the brush right-hand geometry,
        // so InputManager.WandOnRight should probably be reset to false after this? Or maybe
        // SetControllerStyle should be smart enough to figure that out.
        public void SetControllerStyle(ControllerStyle style)
        {
            // Clear console parent in case we're switching controllers.
            if (ControllerConsoleScript.m_Instance != null)
            {
                ControllerConsoleScript.m_Instance.transform.parent = null;
            }

            // Clean up existing controllers.
            // Note that we are explicitly not transferring state.  This is because, in practice,
            // we only change controller style when we're initializing SteamVR, and the temporary
            // controllers are largely disabled.  Any bugs that occur will be trivial and cosmetic.
            // If we add the ability to dynamically change controllers or my above comment about
            // trivial bugs is not true, state transfer should occur here.
            //
            // In practice, the only style transitions we should see are:
            // - None -> correct style                   During VrSdk.Awake()
            // - None -> InitializingUnityXr             During VrSdk.Awake()
            //   InitializingUnityXr -> correct style    Many frames after VrSdk.Awake()
            if (m_VrControls != null)
            {
                Destroy(m_VrControls.gameObject);
                m_VrControls = null;
            }

            m_NeedsToAttachConsoleScript = true;

            GameObject controlsPrefab;
            switch (style)
            {
                case ControllerStyle.None:
                    controlsPrefab = m_NonVrControlsPrefab;
                    m_NeedsToAttachConsoleScript = false;
                    break;
                case ControllerStyle.InitializingUnityXR:
                    controlsPrefab = m_UnityXRUninitializedControlsPrefab;
                    m_NeedsToAttachConsoleScript = false;
                    break;
                case ControllerStyle.Vive:
                    controlsPrefab = m_UnityXRViveControlsPrefab;
                    break;
                case ControllerStyle.Knuckles:
                    controlsPrefab = m_UnityXRKnucklesControlsPrefab;
                    break;
                case ControllerStyle.Cosmos:
                    controlsPrefab = m_UnityXRCosmosControlsPrefab;
                    break;
                case ControllerStyle.OculusTouch:
                    {
                        // TODO:Mikesky - there's new input profiles for the legacy hardware we can check against
                        // https://registry.khronos.org/OpenXR/specs/1.1/html/xrspec.html#_additional_openxr_1_1_changes
                        bool isQuestController = App.Config.IsMobileHardware;
                        controlsPrefab = isQuestController ? m_UnityXRQuestControlsPrefab : m_UnityXRRiftControlsPrefab;
                        break;
                    }
                case ControllerStyle.Wmr:
                    controlsPrefab = m_UnityXRWmrControlsPrefab;
                    break;
                case ControllerStyle.Neo3:
                    controlsPrefab = m_UnityXRNeo3ControlsPrefab;
                    break;
                case ControllerStyle.Phoenix:
                    controlsPrefab = m_UnityXRPhoenixControlsPrefab;
                    break;
                case ControllerStyle.Zapbox:
                    controlsPrefab = m_UnityXRZapboxControlsPrefab;
                    break;
                case ControllerStyle.Gvr:
                    controlsPrefab = m_GvrPointerControlsPrefab;
                    break;
                case ControllerStyle.Unset:
                default:
                    controlsPrefab = null;
                    m_NeedsToAttachConsoleScript = false;
                    break;
            }

            if (controlsPrefab != null)
            {
                Debug.Assert(m_VrControls == null);
                GameObject controlsObject = Instantiate(controlsPrefab);
                m_VrControls = controlsObject.GetComponent<VrControllers>();
                if (m_VrControls == null)
                {
                    throw new InvalidOperationException($"Bad prefab for {style} {controlsPrefab}");
                }
                m_VrControls.transform.parent = m_VrSystem.transform;
            }

            if (m_VrControls != null)
            {
                if (m_NeedsToAttachConsoleScript && ControllerConsoleScript.m_Instance)
                {
                    ControllerConsoleScript.m_Instance.AttachToController(m_VrControls.Brush);
                    m_NeedsToAttachConsoleScript = false;
                }

                // TODO: the only case where this is necessary is when using empty geometry
                // for ControllerStyle.InitializingSteamVR. Can we keep track of "initializing"
                // some other way?
                m_VrControls.Brush.ControllerGeometry.TempWritableStyle = style;
                m_VrControls.Wand.ControllerGeometry.TempWritableStyle = style;
            }
        }

        // Stitches together these things:
        // - Behavior, which encapsulates Wand and Brush
        // - Geometry, which encapsulates physical controller appearance (Touch, Knuckles, ...)
        // - Info, which encapsulates VR APIs (OVR, SteamVR, GVR, ...)
        public ControllerInfo CreateControllerInfo(BaseControllerBehavior behavior, bool isLeftHand)
        {
            // if (App.Config.m_SdkMode == SdkMode.SteamVR)
            // {
            //     // TODO:Mikesky - set to return the default instead.
            //     return new NonVrControllerInfo(behavior);
            //     //return new SteamControllerInfo(behavior);
            // }
            // else
            if (App.Config.m_SdkMode == SdkMode.UnityXR)
            {
                return new UnityXRControllerInfo(behavior, isLeftHand);
            }
            /*else if (App.Config.m_SdkMode == SdkMode.Gvr)
            {
                return new GvrControllerInfo(behavior, isLeftHand);
            }*/
            else
            {
                return new NonVrControllerInfo(behavior);
            }
        }

        // Swap the hand that each ControllerInfo is associated with
        // TODO: if the tracking were associated with the Geometry rather than the Info+Behavior,
        // we wouldn't have to do any swapping. So rather than putting Behaviour_Pose on the Behavior,
        // we should dynamically add it when creating the Geometry. This might make the Behavior
        // prefabs VRAPI-agnostic, too.
        public bool TrySwapLeftRightTracking()
        {
            bool leftRightSwapped = true;

            // TODO:Mikesky - swapping controller hands in. The Oculus specific stuff might actually be better than SteamVR here? See main branch.
            if (App.Config.m_SdkMode == SdkMode.UnityXR)
            {
                UnityXRControllerInfo wandInfo = InputManager.Wand as UnityXRControllerInfo;
                UnityXRControllerInfo brushInfo = InputManager.Brush as UnityXRControllerInfo;
                wandInfo.SwapLeftRight();
                brushInfo.SwapLeftRight();

                var wandPose = InputManager.Wand.Behavior.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                var brushPose = InputManager.Brush.Behavior.GetComponent<UnityEngine.SpatialTracking.TrackedPoseDriver>();
                var tempSource = wandPose.poseSource;
                var tempType = wandPose.deviceType;
                wandPose.SetPoseSource(brushPose.deviceType, brushPose.poseSource);
                brushPose.SetPoseSource(tempType, tempSource);
            }

            return leftRightSwapped;
        }

        // Returns the Degrees of Freedom for the VR system controllers.
        public DoF GetControllerDof()
        {
            switch (App.Config.m_SdkMode)
            {
                case SdkMode.UnityXR:
                    // @bill - Won't this depend of the device?
                    return DoF.Six;

                case SdkMode.Monoscopic:
                    return DoF.Two;

                default:
                    return DoF.None;
            }
        }

        private void OnUnityXRDeviceConnected(InputDevice device)
        {
            // Headset Connected
            const InputDeviceCharacteristics kHeadset =
                InputDeviceCharacteristics.HeadMounted | InputDeviceCharacteristics.TrackedDevice;

            // Left Hand Connected
            const InputDeviceCharacteristics kLeftHandController =
                InputDeviceCharacteristics.Left | InputDeviceCharacteristics.HeldInHand;

            // Right Hand Connected
            const InputDeviceCharacteristics kRightHandController =
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.HeldInHand;

            if (!device.isValid)
                return;

            if ((device.characteristics & kHeadset) == kHeadset)
            {
                Debug.Log($"Headset connected: {device.manufacturer}, {device.name}");
            }
            else if ((device.characteristics & kLeftHandController) == kLeftHandController)
            {
                Debug.Log($"Left Controller: {device.manufacturer}, {device.name}");
                if (IsInitializingUnityXR)
                {
                    UnityXRFinishControllerInit(device);
                }
            }
            else if ((device.characteristics & kRightHandController) == kRightHandController)
            {
                Debug.Log($"Right Controller: {device.manufacturer}, {device.name}");
                if (IsInitializingUnityXR)
                {
                    UnityXRFinishControllerInit(device);
                }
            }
            else
            {
                Debug.LogWarning("Unrecognised device connected: {device.manufacturer}, {device.name}");
            }
        }

        private void SetUnityXRControllerStyle(InputDevice device)
        {
            if (device.name.Contains("Oculus Touch"))
            {
                SetControllerStyle(ControllerStyle.OculusTouch);
            }
            else if (device.name.StartsWith("Index Controller OpenXR"))
            {
                SetControllerStyle(ControllerStyle.Knuckles);
            }
            else if (device.name.StartsWith("HTC Vive Controller OpenXR"))
            {
                SetControllerStyle(ControllerStyle.Vive);
            }
            else if (device.name.StartsWith("Windows MR Controller"))
            {
                SetControllerStyle(ControllerStyle.Wmr);
            }
            else if (device.name.StartsWith("HP Reverb G2 Controller"))
            {
                SetControllerStyle(ControllerStyle.Wmr);
            }
            else if (device.name.Contains("PICO Controller"))
            {
                // TODO:Mikesky - OpenXR controller profiles for each type of pico, it's now available
                // Controller name isn't specified in Pico's device layout
                // so we have to run some additional checks if available.
                // Default to Pico 4 as newest.
                SetControllerStyle(ControllerStyle.Phoenix);
            }
            else if (device.name.StartsWith("Zapbox"))
            {
                SetControllerStyle(ControllerStyle.Zapbox);
            }
            else
            {
                Debug.LogWarning("Unrecognised controller device name: " + device.name);
            }
        }

        private void UnityXRFinishControllerInit(InputDevice device)
        {
            SetUnityXRControllerStyle(device);
            InputManager.m_Instance.CreateControllerInfos();
            PointerManager.m_Instance.RefreshFreePaintPointerAngle();
            PointerManager.m_Instance.RequestPointerRendering(true);
        }

        private void OnUnityXRDeviceDisconnected(InputDevice device)
        {
            // Headset Disconnected
            const InputDeviceCharacteristics kHeadset =
                InputDeviceCharacteristics.HeadMounted | InputDeviceCharacteristics.TrackedDevice;

            if (device.isValid && (device.characteristics & kHeadset) == kHeadset)
            {
                Debug.Log($"Headset disconnected: {device.manufacturer}, {device.name}");
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // HMD Related Methods
        // -------------------------------------------------------------------------------------------- //

        // Returns false if SDK Mode uses an HMD, but it is not initialized.
        // Retruns true if SDK does not have an HMD or if it is correctly initialized.
        // Monoscopic mode returns true for some reason
        // but we make use of this to trigger the view-only mode so if that's ever fixed
        // we need to also fix the conditions for triggering view-only mode
        public bool IsHmdInitialized()
        {
            switch (App.Config.m_SdkMode)
            {
                case SdkMode.UnityXR:
                    return XRGeneralSettings.Instance?.Manager?.activeLoader != null;
                default:
                    return true;
            }
        }

        // Returns the native frame rate of the HMD (or screen) in frames per second.
        public int GetHmdTargetFrameRate()
        {
            switch (App.Config.m_SdkMode)
            {
                case SdkMode.UnityXR:
                    return 60; // 90?
                case SdkMode.Monoscopic:
                    return 60;
                case SdkMode.Ods:
                    // TODO: 30 would be correct, buf feels too slow.
                    return 60;
                default:
                    return 60;
            }
        }

        // Returns the Degrees of Freedom for the VR system headset.
        public DoF GetHmdDof()
        {
            switch (App.Config.m_SdkMode)
            {
                case SdkMode.UnityXR:
                    return DoF.Six;
                default:
                    return DoF.None;
            }
        }

        // If the SDK is blocking the user's view of the application, return true.
        public bool IsAppFocusBlocked()
        {
            return !m_HasVrFocus;
        }

        // -------------------------------------------------------------------------------------------- //
        // Tracking Methods
        // -------------------------------------------------------------------------------------------- //

        /// Clears the callbacks that get called when a new pose is received. The callbacks are saved
        /// So that they can be restored later with RestorePoseTracking.
        public void DisablePoseTracking()
        {
            m_TrackingBackupXf = TrTransform.FromTransform(GetVrCamera().transform);
            if (OnNewControllerPosesApplied == null)
            {
                m_OldOnPoseApplied = Array.Empty<Action>();
            }
            else
            {
                m_OldOnPoseApplied = OnNewControllerPosesApplied.GetInvocationList().Cast<Action>().ToArray();
            }
            OnNewControllerPosesApplied = null;
        }

        /// Restores the pose recieved callbacks that were saved off with DisablePoseTracking. Will merge
        /// any callbacks currently on OnControllerNewPoses.
        public void RestorePoseTracking()
        {
            if (m_OldOnPoseApplied != null)
            {
                if (OnNewControllerPosesApplied != null)
                {
                    var list = m_OldOnPoseApplied.Concat(OnNewControllerPosesApplied.GetInvocationList())
                        .Distinct().Cast<Action>();
                    OnNewControllerPosesApplied = null;
                    foreach (var handler in list)
                    {
                        OnNewControllerPosesApplied += handler;
                    }
                }
            }

            // Restore camera xf.
            if (m_TrackingBackupXf != null)
            {
                Transform camXf = GetVrCamera().transform;
                camXf.position = m_TrackingBackupXf.Value.translation;
                camXf.rotation = m_TrackingBackupXf.Value.rotation;
                camXf.localScale = Vector3.one;
                m_TrackingBackupXf = null;
            }
        }

        // -------------------------------------------------------------------------------------------- //
        // Performance Methods
        // -------------------------------------------------------------------------------------------- //
        public void SetFixedFoveation(int level)
        {
#if OCULUS_SUPPORTED
            Debug.Assert(level >= 0 && level <= 3);
            if (App.Config.IsMobileHardware && !SpoofMobileHardware.MobileHardware)
            {
                OVRManager.tiledMultiResLevel = (OVRManager.TiledMultiResLevel)level;
            }
#endif // OCULUS_SUPPORTED
        }

        /// Gets GPU utilization 0 .. 1 if supported, otherwise returns 0.
        public float GetGpuUtilization()
        {
#if OCULUS_SUPPORTED
            if (OVRManager.gpuUtilSupported)
            {
                return OVRManager.gpuUtilLevel;
            }
#endif // OCULUS_SUPPORTED
            return 0;
        }

        public void SetGpuClockLevel(int level)
        {
#if OCULUS_SUPPORTED
            if (App.Config.IsMobileHardware)
            {
                OVRManager.gpuLevel = level;
            }
#endif // OCULUS_SUPPORTED
        }
    }
}
