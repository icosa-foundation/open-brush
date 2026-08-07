// Copyright 2026 The Open Brush Authors
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

using System.Collections.Generic;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;
using UnityEngine.XR.OpenXR.Input;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.XR.Management;
#endif

#if USE_INPUT_SYSTEM_POSE_CONTROL
using PoseControl = UnityEngine.InputSystem.XR.PoseControl;
#else
using PoseControl = UnityEngine.XR.OpenXR.Input.PoseControl;
#endif

namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    /// <summary>
    /// Adds the XR_LOGITECH_mx_ink_stylus_interaction profile to Unity OpenXR.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(
        UiName = "Logitech MX Ink Stylus Interaction Profile",
        BuildTargetGroups = new[]
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.WSA,
            BuildTargetGroup.Android
        },
        Company = "Logitech",
        Desc = "Maps the Logitech MX Ink OpenXR interaction profile to Unity Input System controls.",
        DocumentationLink = "https://logitech.github.io/mxink/OpenXR.html",
        OpenxrExtensionStrings = extensionString,
        Version = "1.0.0",
        Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Interaction,
        FeatureId = featureId)]
#endif
    public class LogitechMxInkControllerProfile : OpenXRInteractionFeature
    {
        public const string featureId = "com.openbrush.openxr.feature.input.logitechmxink";
        public const string extensionString = "XR_LOGITECH_mx_ink_stylus_interaction";
        public const string profile = "/interaction_profiles/logitech/mx_ink_stylus_logitech";

        private const string kDeviceLocalizedName = "Logitech MX Ink Stylus";

        private const string kTip = "/input/tip_logitech/force";
        private const string kClusterMiddle = "/input/cluster_middle_logitech/force";
        private const string kClusterFront = "/input/cluster_front_logitech/click";
        private const string kClusterFrontDoubleTap =
            "/input/cluster_front_logitech/double_tap_logitech";
        private const string kClusterBack = "/input/cluster_back_logitech/click";
        private const string kClusterBackDoubleTap =
            "/input/cluster_back_logitech/double_tap_logitech";
        private const string kSystem = "/input/system/click";
        private const string kDock = "/input/dock_logitech/docked_logitech";
        private const string kGripPose = "/input/grip/pose";
        private const string kAimPose = "/input/aim/pose";
        private const string kTipPose = "/input/tip_logitech/pose";
        private const string kHaptic = "/output/haptic";

        [Preserve]
        [InputControlLayout(
            displayName = kDeviceLocalizedName,
            commonUsages = new[] { "LeftHand", "RightHand" })]
        public class LogitechMxInkController : XRControllerWithRumble
        {
            [Preserve, InputControl(aliases = new[] { "Nib", "Ink" }, usage = "Tip")]
            public AxisControl tip { get; private set; }

            [Preserve, InputControl(aliases = new[] { "Trigger", "Middle" }, usage = "Trigger")]
            public AxisControl clusterMiddleButton { get; private set; }

            [Preserve, InputControl(aliases = new[] { "Grab", "squeezeClicked" }, usage = "GripButton")]
            public ButtonControl clusterFrontButton { get; private set; }

            [Preserve, InputControl]
            public ButtonControl clusterFrontDoubleTap { get; private set; }

            [Preserve, InputControl(aliases = new[] { "A", "X" }, usage = "PrimaryButton")]
            public ButtonControl clusterBackButton { get; private set; }

            [Preserve, InputControl]
            public ButtonControl clusterBackDoubleTap { get; private set; }

            [Preserve, InputControl(aliases = new[] { "menuButton" }, usage = "MenuButton")]
            public ButtonControl systemButton { get; private set; }

            [Preserve, InputControl]
            public ButtonControl docked { get; private set; }

            [Preserve, InputControl(offset = 0, aliases = new[] { "device", "gripPose" }, usage = "Device")]
            public PoseControl devicePose { get; private set; }

            [Preserve, InputControl(offset = 0, alias = "aimPose", usage = "Pointer")]
            public PoseControl pointer { get; private set; }

            [Preserve, InputControl(offset = 0, usage = "TipPose")]
            public PoseControl tipPose { get; private set; }

            [Preserve, InputControl(offset = 28, usage = "IsTracked")]
            new public ButtonControl isTracked { get; private set; }

            [Preserve, InputControl(offset = 32, usage = "TrackingState")]
            new public IntegerControl trackingState { get; private set; }

            [Preserve, InputControl(offset = 36, noisy = true, alias = "gripPosition")]
            new public Vector3Control devicePosition { get; private set; }

            [Preserve, InputControl(offset = 48, noisy = true, alias = "gripOrientation")]
            new public QuaternionControl deviceRotation { get; private set; }

            [Preserve, InputControl(usage = "Haptic")]
            public HapticControl haptic { get; private set; }

            protected override void FinishSetup()
            {
                base.FinishSetup();
                tip = GetChildControl<AxisControl>("tip");
                clusterMiddleButton = GetChildControl<AxisControl>("clusterMiddleButton");
                clusterFrontButton = GetChildControl<ButtonControl>("clusterFrontButton");
                clusterFrontDoubleTap = GetChildControl<ButtonControl>("clusterFrontDoubleTap");
                clusterBackButton = GetChildControl<ButtonControl>("clusterBackButton");
                clusterBackDoubleTap = GetChildControl<ButtonControl>("clusterBackDoubleTap");
                systemButton = GetChildControl<ButtonControl>("systemButton");
                docked = GetChildControl<ButtonControl>("docked");
                devicePose = GetChildControl<PoseControl>("devicePose");
                pointer = GetChildControl<PoseControl>("pointer");
                tipPose = GetChildControl<PoseControl>("tipPose");
                isTracked = GetChildControl<ButtonControl>("isTracked");
                trackingState = GetChildControl<IntegerControl>("trackingState");
                devicePosition = GetChildControl<Vector3Control>("devicePosition");
                deviceRotation = GetChildControl<QuaternionControl>("deviceRotation");
                haptic = GetChildControl<HapticControl>("haptic");
            }
        }

        protected override void RegisterDeviceLayout()
        {
#if UNITY_EDITOR
            if (!OpenXRLoaderEnabledForSelectedBuildTarget(
                    EditorUserBuildSettings.selectedBuildTargetGroup))
            {
                return;
            }
#endif
            InputSystem.InputSystem.RegisterLayout(
                typeof(LogitechMxInkController),
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(kDeviceLocalizedName));
        }

        protected override void UnregisterDeviceLayout()
        {
#if UNITY_EDITOR
            if (!OpenXRLoaderEnabledForSelectedBuildTarget(
                    EditorUserBuildSettings.selectedBuildTargetGroup))
            {
                return;
            }
#endif
            InputSystem.InputSystem.RemoveLayout(nameof(LogitechMxInkController));
        }

#if UNITY_EDITOR
        private static bool OpenXRLoaderEnabledForSelectedBuildTarget(
            BuildTargetGroup targetGroup)
        {
            XRManagerSettings managerSettings =
                UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget
                    .XRGeneralSettingsForBuildTarget(targetGroup)
                    ?.AssignedSettings;
            if (managerSettings == null)
            {
                return false;
            }

            foreach (XRLoader activeLoader in managerSettings.activeLoaders)
            {
                if (activeLoader is OpenXRLoader)
                {
                    return true;
                }
            }

            return false;
        }
#endif

        protected override string GetDeviceLayoutName()
        {
            return nameof(LogitechMxInkController);
        }

        protected override void RegisterActionMapsWithRuntime()
        {
            var actionMap = new ActionMapConfig
            {
                name = "logitechmxinkcontroller",
                localizedName = kDeviceLocalizedName,
                desiredInteractionProfile = profile,
                manufacturer = "Logitech",
                serialNumber = "",
                deviceInfos = new List<DeviceConfig>
                {
                    CreateDevice(InputDeviceCharacteristics.Left, UserPaths.leftHand),
                    CreateDevice(InputDeviceCharacteristics.Right, UserPaths.rightHand)
                },
                actions = new List<ActionConfig>
                {
                    CreateAction("tip", "Tip Pressure", ActionType.Axis1D, "Tip", kTip),
                    CreateAction(
                        "clusterMiddleButton",
                        "Middle Button Pressure",
                        ActionType.Axis1D,
                        "Trigger",
                        kClusterMiddle),
                    CreateAction(
                        "clusterFrontButton",
                        "Front Button",
                        ActionType.Binary,
                        "GripButton",
                        kClusterFront),
                    CreateAction(
                        "clusterFrontDoubleTap",
                        "Front Button Double Tap",
                        ActionType.Binary,
                        null,
                        kClusterFrontDoubleTap),
                    CreateAction(
                        "clusterBackButton",
                        "Back Button",
                        ActionType.Binary,
                        "PrimaryButton",
                        kClusterBack),
                    CreateAction(
                        "clusterBackDoubleTap",
                        "Back Button Double Tap",
                        ActionType.Binary,
                        null,
                        kClusterBackDoubleTap),
                    CreateAction(
                        "systemButton",
                        "System Button",
                        ActionType.Binary,
                        "MenuButton",
                        kSystem),
                    CreateAction("docked", "Docked", ActionType.Binary, null, kDock),
                    CreateAction(
                        "devicePose",
                        "Grip Pose",
                        ActionType.Pose,
                        "Device",
                        kGripPose),
                    CreateAction(
                        "pointer",
                        "Aim Pose",
                        ActionType.Pose,
                        "Pointer",
                        kAimPose),
                    CreateAction(
                        "tipPose",
                        "Tip Pose",
                        ActionType.Pose,
                        "TipPose",
                        kTipPose),
                    CreateAction("haptic", "Haptic Output", ActionType.Vibrate, "Haptic", kHaptic)
                }
            };

            AddActionMap(actionMap);
        }

        private static DeviceConfig CreateDevice(
            InputDeviceCharacteristics handedness,
            string userPath)
        {
            return new DeviceConfig
            {
                characteristics = InputDeviceCharacteristics.HeldInHand |
                    InputDeviceCharacteristics.TrackedDevice |
                    InputDeviceCharacteristics.Controller |
                    handedness,
                userPath = userPath
            };
        }

        private static ActionConfig CreateAction(
            string name,
            string localizedName,
            ActionType type,
            string usage,
            string interactionPath)
        {
            return new ActionConfig
            {
                name = name,
                localizedName = localizedName,
                type = type,
                usages = string.IsNullOrEmpty(usage)
                    ? new List<string>()
                    : new List<string> { usage },
                bindings = new List<ActionBinding>
                {
                    new ActionBinding
                    {
                        interactionPath = interactionPath,
                        interactionProfileName = profile
                    }
                }
            };
        }
    }
}
