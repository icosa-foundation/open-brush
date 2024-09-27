using System.Collections.Generic;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.XR;
using UnityEngine.Scripting;
using UnityEngine.XR.OpenXR.Input;
using UnityEngine.XR.Management;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if USE_INPUT_SYSTEM_POSE_CONTROL
using PoseControl = UnityEngine.InputSystem.XR.PoseControl;
#else
using PoseControl = UnityEngine.XR.OpenXR.Input.PoseControl;
#endif

namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    /// <summary>
    /// This <see cref="OpenXRInteractionFeature"/> enables the use of Logitech MX Ink interaction profile in OpenXR.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName = "Logitech MX Ink Stylus Interaction Profile",
        BuildTargetGroups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.WSA, BuildTargetGroup.Android },
        Company = "Logitech",
        Desc = "Allows for mapping input to the Logitech MX Ink interaction profile.",
        DocumentationLink = "https://logitech.github.io/mxink/OpenXR.html",
        OpenxrExtensionStrings = extensionString,
        Version = "0.0.1",
        Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Interaction,
        FeatureId = featureId)]
#endif
    public class LogitechMxInkControllerProfile : OpenXRInteractionFeature
    {
        /// <summary>
        /// The feature id string. This is used to give the feature a well known id for reference.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.input.logitechmxink";

        /// <summary>
        /// An Input System device based off the <a href="https://logitech.github.io/mxink/OpenXR.html">Logitech MX Ink Stylus interaction profile</a>.
        /// </summary>
        [Preserve, InputControlLayout(displayName = "Logitech MX Ink", commonUsages = new[] { "LeftHand", "RightHand" })]
        public class LogitechMxInkController : XRControllerWithRumble
        {
            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.AxisControl) that represents the <see cref="LogitechMxInkControllerProfile.tip"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "Nib", "Ink" }, usage = "Tip")]
            public AxisControl tip { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="LogitechMxInkControllerProfile.clusterBackButton"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "A", "X", "buttonA", "buttonX" }, usage = "PrimaryButton")]
            public ButtonControl clusterBackButton { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.AxisControl) that represents the <see cref="LogitechMxInkControllerProfile.clusterMiddleButton"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "Primary", "Middle" }, usage = "Trigger")]
            public AxisControl clusterMiddleButton { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="LogitechMxInkControllerProfile.clusterFrontButton"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "Grab", "squeezeClicked" }, usage = "GripButton")]
            public ButtonControl clusterFrontButton { get; private set; }

            /// <summary>
            /// A <see cref="PoseControl"/> that represents information from the <see cref="LogitechMxInkControllerProfile.devicePose"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(offset = 0, aliases = new[] { "device", "aimPose" }, usage = "Device")]
            public PoseControl devicePose { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) required for backwards compatibility with the XRSDK layouts. This represents the overall tracking state of the device. This value is equivalent to mapping devicePose/isTracked.
            /// </summary>
            [Preserve, InputControl(offset = 2)]
            new public ButtonControl isTracked { get; private set; }

            /// <summary>
            /// A [IntegerControl](xref:UnityEngine.InputSystem.Controls.IntegerControl) required for backwards compatibility with the XRSDK layouts. This represents the bit flag set indicating what data is valid. This value is equivalent to mapping devicePose/trackingState.
            /// </summary>
            [Preserve, InputControl(offset = 4)]
            new public IntegerControl trackingState { get; private set; }

            /// <summary>
            /// A [Vector3Control](xref:UnityEngine.InputSystem.Controls.Vector3Control) required for backwards compatibility with the XRSDK layouts. This is the device position, or grip position. This value is equivalent to mapping devicePose/position.
            /// </summary>
            [Preserve, InputControl(offset = 8, alias = "aimPosition")]
            new public Vector3Control devicePosition { get; private set; }

            /// <summary>
            /// A [QuaternionControl](xref:UnityEngine.InputSystem.Controls.QuaternionControl) required for backwards compatibility with the XRSDK layouts. This is the device orientation, or grip orientation. This value is equivalent to mapping devicePose/rotation.
            /// </summary>
            [Preserve, InputControl(offset = 20, alias = "aimOrientation")]
            new public QuaternionControl deviceRotation { get; private set; }

            /// <summary>
            /// A <see cref="HapticControl"/> that represents the <see cref="LogitechMxInkControllerProfile.haptic"/> binding.
            /// </summary>
            [Preserve, InputControl(usage = "Haptic")]
            public HapticControl haptic { get; private set; }

            /// <inheritdoc  cref="OpenXRDevice"/>
            protected override void FinishSetup()
            {
                base.FinishSetup();
                tip = GetChildControl<AxisControl>("tip");
                clusterBackButton = GetChildControl<ButtonControl>("clusterBackButton");
                clusterMiddleButton = GetChildControl<AxisControl>("clusterMiddleButton");
                clusterFrontButton = GetChildControl<ButtonControl>("clusterFrontButton");
                devicePose = GetChildControl<PoseControl>("devicePose");

                isTracked = GetChildControl<ButtonControl>("isTracked");
                trackingState = GetChildControl<IntegerControl>("trackingState");
                devicePosition = GetChildControl<Vector3Control>("devicePosition");
                deviceRotation = GetChildControl<QuaternionControl>("deviceRotation");

                haptic = GetChildControl<HapticControl>("haptic");
            }
        }

        /// <summary>The OpenXR Extension string. OpenXR uses this to check if this extension is available or enabled.</summary>
        public const string extensionString = "XR_LOGITECH_mx_ink_stylus_interaction";

        /// <summary>
        /// OpenXR string that represents the <a href="https://www.khronos.org/registry/OpenXR/specs/1.0/html/xrspec.html#semantic-path-interaction-profiles">Interaction Profile.</a>
        /// </summary>
        public const string profile = "/interaction_profiles/logitech/mx_ink_stylus_logitech";

        /// <summary>
        /// Constant for a boolean interaction binding '.../input/tip_logitech/force' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string tip = "/input/tip_logitech/force";

        /// <summary>
        /// Constant for a boolean interaction binding '.../input/cluster_back_logitech/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string clusterBack = "/input/cluster_back_logitech/click";

        /// <summary>
        /// Constant for a boolean interaction binding '.../input/cluster_middle_logitech/force' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string clusterMiddle = "/input/cluster_middle_logitech/force";

        /// <summary>
        /// Constant for a boolean interaction binding '.../input/cluster_front_logitech/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string clusterFront = "/input/cluster_front_logitech/click";

        /// <summary>
        /// Constant for a pose interaction binding '.../input/grip/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string grip = "/input/grip/pose";

        /// <summary>
        /// Constant for a pose interaction binding '.../input/aim/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string aim = "/input/aim/pose";

        /// <summary>
        /// Constant for a haptic interaction binding '.../input/output/haptic' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string haptic = "/output/haptic";

        private const string kDeviceLocalizedName = "Logitech MX Ink Stylus";

#if UNITY_EDITOR
        internal static bool OpenXRLoaderEnabledForSelectedBuildTarget(BuildTargetGroup targetGroup)
        {
            XRManagerSettings xRManagerSettings = UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(targetGroup)?.AssignedSettings;
            if (!xRManagerSettings)
            {
                return false;
            }

            bool result = false;
            foreach (XRLoader activeLoader in xRManagerSettings.activeLoaders)
            {
                if (activeLoader as OpenXRLoader != null)
                {
                    result = true;
                    break;
                }
            }

            return result;
        }
#endif
        /// <summary>
        /// Registers the <see cref="LogitechMxInkController"/> layout with the Input System.
        /// </summary>
        protected override void RegisterDeviceLayout()
        {
#if UNITY_EDITOR
            if (!OpenXRLoaderEnabledForSelectedBuildTarget(EditorUserBuildSettings.selectedBuildTargetGroup))
                return;
#endif
            InputSystem.InputSystem.RegisterLayout(typeof(LogitechMxInkController),
                matches: new InputDeviceMatcher()
                    .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                    .WithProduct(kDeviceLocalizedName));
        }

        /// <summary>
        /// Removes the <see cref="LogitechMxInkController"/> layout from the Input System.
        /// </summary>
        protected override void UnregisterDeviceLayout()
        {
#if UNITY_EDITOR
            if (!OpenXRLoaderEnabledForSelectedBuildTarget(EditorUserBuildSettings.selectedBuildTargetGroup))
                return;
#endif
            InputSystem.InputSystem.RemoveLayout(typeof(LogitechMxInkController).Name);
        }

        /// <summary>
        /// Return device layout string that used for registering device for the Input System.
        /// </summary>
        /// <returns>Device layout string.</returns>
        protected override string GetDeviceLayoutName()
        {
            return nameof(LogitechMxInkController);
        }

        /// <inheritdoc/>
        protected override void RegisterActionMapsWithRuntime()
        {
            ActionMapConfig actionMap = new ActionMapConfig()
            {
                name = "logitechmxinkcontroller",
                localizedName = kDeviceLocalizedName,
                desiredInteractionProfile = profile,
                manufacturer = "Logitech",
                serialNumber = "",
                deviceInfos = new List<DeviceConfig>()
                {
                    new DeviceConfig()
                    {
                        characteristics = (InputDeviceCharacteristics)(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Left),
                        userPath = UserPaths.leftHand
                    },
                    new DeviceConfig()
                    {
                        characteristics = (InputDeviceCharacteristics)(InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.TrackedDevice | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.Right),
                        userPath = UserPaths.rightHand
                    }
                },
                actions = new List<ActionConfig>()
                {
                    // tip
                    new ActionConfig()
                    {
                        name = "tip",
                        localizedName = "Tip",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Tip"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = tip,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // front
                    new ActionConfig()
                    {
                        name = "clusterFrontButton",
                        localizedName = "Grab Button",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "GripButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = clusterFront,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // middle
                    new ActionConfig()
                    {
                        name = "clusterMiddleButton",
                        localizedName = "Middle Button",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Trigger"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = clusterMiddle,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // back
                    new ActionConfig()
                    {
                        name = "clusterBackButton",
                        localizedName = "Primary Button",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "PrimaryButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = clusterBack,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Device Pose
                    new ActionConfig()
                    {
                        name = "devicePose",
                        localizedName = "Device Pose",
                        type = ActionType.Pose,
                        usages = new List<string>()
                        {
                            "Device"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = aim,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Haptics
                    new ActionConfig()
                    {
                        name = "haptic",
                        localizedName = "Haptic Output",
                        type = ActionType.Vibrate,
                        usages = new List<string>() { "Haptic" },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = haptic,
                                interactionProfileName = profile,
                            }
                        }
                    }
                }
            };

            AddActionMap(actionMap);
        }
    }
}
