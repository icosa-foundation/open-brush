using System.Collections.Generic;
using UnityEngine.Scripting;
using UnityEngine.XR.OpenXR.Input;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;

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
    /// This <see cref="OpenXRInteractionFeature"/> enables the use of PICO TouchControllers interaction profiles in OpenXR.
    /// </summary>
#if UNITY_EDITOR
    [UnityEditor.XR.OpenXR.Features.OpenXRFeature(UiName = "PICO4 Touch Controller Profile",
        BuildTargetGroups = new[] { BuildTargetGroup.Android },
        Company = "PICO",
        Desc = "Allows for mapping input to the PICO4 Touch Controller interaction profile.",
        OpenxrExtensionStrings = extensionString,
        Version = "1.0.0",
        Category = UnityEditor.XR.OpenXR.Features.FeatureCategory.Interaction,
        FeatureId = featureId
        )]
#endif
    public class PICO4ControllerProfile : OpenXRInteractionFeature
    {
        /// <summary>
        /// The feature id string. This is used to give the feature a well known id for reference.
        /// </summary>
        public const string featureId = "com.unity.openxr.feature.input.PICO4touch";

        /// <summary>
        /// An Input System device based on the hand interaction profile in the PICO Touch Controller</a>.
        /// </summary>
        [Preserve, InputControlLayout(displayName = "PICO4 Touch Controller (OpenXR)", commonUsages = new[] { "LeftHand", "RightHand" })]
        public class PICO4TouchController : XRControllerWithRumble
        {
            /// <summary>
            /// A [Vector2Control](xref:UnityEngine.InputSystem.Controls.Vector2Control) that represents the <see cref="PICO4TouchControllerProfile.thumbstick"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "Primary2DAxis", "Joystick" }, usage = "Primary2DAxis")]
            public Vector2Control thumbstick { get; private set; }

            /// <summary>
            /// A [AxisControl](xref:UnityEngine.InputSystem.Controls.AxisControl) that represents the <see cref="PICO4TouchControllerProfile.squeezeValue"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "GripAxis", "squeeze" }, usage = "Grip")]
            public AxisControl grip { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.squeezeClick"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "GripButton", "squeezeClicked" }, usage = "GripButton")]
            public ButtonControl gripPressed { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.menu"/> OpenXR bindings.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "Primary", "menuButton" }, usage = "Menu")]
            public ButtonControl menu { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.system"/> OpenXR bindings.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "systemButton" }, usage = "system")]
            public ButtonControl system { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.buttonA"/> <see cref="PICO4TouchControllerProfile.buttonX"/> OpenXR bindings, depending on handedness.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "A", "X", "buttonA", "buttonX" }, usage = "PrimaryButton")]
            public ButtonControl primaryButton { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.buttonATouch"/> <see cref="PICO4TouchControllerProfile.buttonYTouch"/> OpenXR bindings, depending on handedness.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "ATouched", "XTouched", "ATouch", "XTouch", "buttonATouched", "buttonXTouched" }, usage = "PrimaryTouch")]
            public ButtonControl primaryTouched { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.buttonB"/> <see cref="PICO4TouchControllerProfile.buttonY"/> OpenXR bindings, depending on handedness.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "B", "Y", "buttonB", "buttonY" }, usage = "SecondaryButton")]
            public ButtonControl secondaryButton { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.buttonBTouch"/> <see cref="PICO4TouchControllerProfile.buttonYTouch"/> OpenXR bindings, depending on handedness.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "BTouched", "YTouched", "BTouch", "YTouch", "buttonBTouched", "buttonYTouched" }, usage = "SecondaryTouch")]
            public ButtonControl secondaryTouched { get; private set; }

            /// <summary>
            /// A [AxisControl](xref:UnityEngine.InputSystem.Controls.AxisControl) that represents the <see cref="PICO4TouchControllerProfile.trigger"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(usage = "Trigger")]
            public AxisControl trigger { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.triggerClick"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "indexButton", "indexTouched", "triggerbutton" }, usage = "TriggerButton")]
            public ButtonControl triggerPressed { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.triggerTouch"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "indexTouch", "indexNearTouched" }, usage = "TriggerTouch")]
            public ButtonControl triggerTouched { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.thumbstickClick"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "JoystickOrPadPressed", "thumbstickClick", "joystickClicked" }, usage = "Primary2DAxisClick")]
            public ButtonControl thumbstickClicked { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) that represents the <see cref="PICO4TouchControllerProfile.thumbstickTouch"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(aliases = new[] { "JoystickOrPadTouched", "thumbstickTouch", "joystickTouched" }, usage = "Primary2DAxisTouch")]
            public ButtonControl thumbstickTouched { get; private set; }
            /// <summary>
            /// A <see cref="PoseControl"/> that represents the <see cref="PICO4TouchControllerProfile.grip"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(offset = 0, aliases = new[] { "device", "gripPose" }, usage = "Device")]
            public PoseControl devicePose { get; private set; }

            /// <summary>
            /// A <see cref="PoseControl"/> that represents the <see cref="PICO4TouchControllerProfile.aim"/> OpenXR binding.
            /// </summary>
            [Preserve, InputControl(offset = 0, alias = "aimPose", usage = "Pointer")]
            public PoseControl pointer { get; private set; }

            /// <summary>
            /// A [ButtonControl](xref:UnityEngine.InputSystem.Controls.ButtonControl) required for backwards compatibility with the XRSDK layouts. This represents the overall tracking state of the device. This value is equivalent to mapping devicePose/isTracked.
            /// </summary>
            [Preserve, InputControl(offset = 28, usage = "IsTracked")]
            new public ButtonControl isTracked { get; private set; }

            /// <summary>
            /// A [IntegerControl](xref:UnityEngine.InputSystem.Controls.IntegerControl) required for backwards compatibility with the XRSDK layouts. This represents the bit flag set to indicate what data is valid. This value is equivalent to mapping devicePose/trackingState.
            /// </summary>
            [Preserve, InputControl(offset = 32, usage = "TrackingState")]
            new public IntegerControl trackingState { get; private set; }

            /// <summary>
            /// A [Vector3Control](xref:UnityEngine.InputSystem.Controls.Vector3Control) required for backwards compatibility with the XRSDK layouts. This is the device position. For the PICO Touch device, this is both the grip and the pointer position. This value is equivalent to mapping devicePose/position.
            /// </summary>
            [Preserve, InputControl(offset = 36, noisy = true, alias = "gripPosition")]
            new public Vector3Control devicePosition { get; private set; }

            /// <summary>
            /// A [QuaternionControl](xref:UnityEngine.InputSystem.Controls.QuaternionControl) required for backwards compatibility with the XRSDK layouts. This is the device orientation. For the PICO Touch device, this is both the grip and the pointer rotation. This value is equivalent to mapping devicePose/rotation.
            /// </summary>
            [Preserve, InputControl(offset = 48, noisy = true, alias = "gripOrientation")]
            new public QuaternionControl deviceRotation { get; private set; }

            /// <summary>
            /// A [Vector3Control](xref:UnityEngine.InputSystem.Controls.Vector3Control) required for back compatibility with the XRSDK layouts. This is the pointer position. This value is equivalent to mapping pointerPose/position.
            /// </summary>
            [Preserve, InputControl(offset = 96)]
            public Vector3Control pointerPosition { get; private set; }

            /// <summary>
            /// A [QuaternionControl](xref:UnityEngine.InputSystem.Controls.QuaternionControl) required for backwards compatibility with the XRSDK layouts. This is the pointer rotation. This value is equivalent to mapping pointerPose/rotation.
            /// </summary>
            [Preserve, InputControl(offset = 108, alias = "pointerOrientation")]
            public QuaternionControl pointerRotation { get; private set; }

            /// <summary>
            /// A <see cref="HapticControl"/> that represents the <see cref="PICO4TouchControllerProfile.haptic"/> binding.
            /// </summary>
            [Preserve, InputControl(usage = "Haptic")]
            public HapticControl haptic { get; private set; }
               
            [Preserve, InputControl(usage = "BatteryLevel")]
            public AxisControl batteryLevel { get; private set; }

            /// <summary>
            /// Internal call used to assign controls to the the correct element.
            /// </summary>
            protected override void FinishSetup()
            {
                base.FinishSetup();
                thumbstick = GetChildControl<Vector2Control>("thumbstick");
                trigger = GetChildControl<AxisControl>("trigger");
                triggerPressed = GetChildControl<ButtonControl>("triggerPressed");
                triggerTouched = GetChildControl<ButtonControl>("triggerTouched");
                grip = GetChildControl<AxisControl>("grip");
                gripPressed = GetChildControl<ButtonControl>("gripPressed");
                menu = GetChildControl<ButtonControl>("menu");
                primaryButton = GetChildControl<ButtonControl>("primaryButton");
                primaryTouched = GetChildControl<ButtonControl>("primaryTouched");
                secondaryButton = GetChildControl<ButtonControl>("secondaryButton");
                secondaryTouched = GetChildControl<ButtonControl>("secondaryTouched");
                thumbstickClicked = GetChildControl<ButtonControl>("thumbstickClicked");
                thumbstickTouched = GetChildControl<ButtonControl>("thumbstickTouched");

                devicePose = GetChildControl<PoseControl>("devicePose");
                pointer = GetChildControl<PoseControl>("pointer");

                isTracked = GetChildControl<ButtonControl>("isTracked");
                trackingState = GetChildControl<IntegerControl>("trackingState");
                devicePosition = GetChildControl<Vector3Control>("devicePosition");
                deviceRotation = GetChildControl<QuaternionControl>("deviceRotation");
                pointerPosition = GetChildControl<Vector3Control>("pointerPosition");
                pointerRotation = GetChildControl<QuaternionControl>("pointerRotation");

                haptic = GetChildControl<HapticControl>("haptic");
                batteryLevel = GetChildControl<AxisControl>("BatteryLevel");
            }
        }

        public const string profile = "/interaction_profiles/bytedance/pico4_controller";

        // Available Bindings
        // Left Hand Only
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/x/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.leftHand"/> user path.
        /// </summary>
        public const string buttonX = "/input/x/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/x/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.leftHand"/> user path.
        /// </summary>
        public const string buttonXTouch = "/input/x/touch";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/y/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.leftHand"/> user path.
        /// </summary>
        public const string buttonY = "/input/y/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/y/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.leftHand"/> user path.
        /// </summary>
        public const string buttonYTouch = "/input/y/touch";

        // Right Hand Only
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/a/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.rightHand"/> user path.
        /// </summary>
        public const string buttonA = "/input/a/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/a/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.rightHand"/> user path.
        /// </summary>
        public const string buttonATouch = "/input/a/touch";
        /// <summary>
        /// Constant for a boolean interaction binding '..."/input/b/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.rightHand"/> user path.
        /// </summary>
        public const string buttonB = "/input/b/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/b/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs. This binding is only available for the <see cref="OpenXRInteractionFeature.UserPaths.rightHand"/> user path.
        /// </summary>
        public const string buttonBTouch = "/input/b/touch";

        // Both Hands
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/menu/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string menu = "/input/menu/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/system/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.(may not be available for application use)
        /// </summary>
        public const string system = "/input/system/click";
        /// <summary>
        /// Constant for a float interaction binding '.../input/trigger/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string triggerClick = "/input/trigger/click";
        /// <summary>
        /// Constant for a float interaction binding '.../input/trigger/value' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string trigger = "/input/trigger/value";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/trigger/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string triggerTouch = "/input/trigger/touch";
        /// <summary>
        /// Constant for a Vector2 interaction binding '.../input/thumbstick' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string thumbstick = "/input/thumbstick";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/thumbstick/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string thumbstickClick = "/input/thumbstick/click";
        /// <summary>
        /// Constant for a boolean interaction binding '.../input/thumbstick/touch' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string thumbstickTouch = "/input/thumbstick/touch";
        /// <summary>
        /// Constant for a float interaction binding '.../input/squeeze/click' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string squeezeClick = "/input/squeeze/click";
        /// <summary>
        /// Constant for a float interaction binding '.../input/squeeze/value' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string squeezeValue = "/input/squeeze/value";
        /// <summary>
        /// Constant for a pose interaction binding '.../input/grip/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string grip = "/input/grip/pose";
        /// <summary>
        /// Constant for a pose interaction binding '.../input/aim/pose' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string aim = "/input/aim/pose";
        /// <summary>
        /// Constant for a haptic interaction binding '.../output/haptic' OpenXR Input Binding. Used by input subsystem to bind actions to physical inputs.
        /// </summary>
        public const string haptic = "/output/haptic";
                
        public const string batteryLevel = "/input/battery/value";

        private const string kDeviceLocalizedName = "PICO4 Touch Controller OpenXR";

        /// <summary>
        /// The OpenXR Extension string. This extension defines the interaction profile for PICO Neo3 and PICO 4 Controllers.
        /// /// </summary>
        public const string extensionString = "XR_BD_controller_interaction";

        /// <inheritdoc/>
        protected override void RegisterDeviceLayout()
        {
            InputSystem.InputSystem.RegisterLayout(typeof(PICO4TouchController),
                        matches: new InputDeviceMatcher()
                        .WithInterface(XRUtilities.InterfaceMatchAnyVersion)
                        .WithProduct(kDeviceLocalizedName));
        }

        /// <inheritdoc/>
        protected override void UnregisterDeviceLayout()
        {
            InputSystem.InputSystem.RemoveLayout(nameof(PICO4TouchController));
        }

        /// <inheritdoc/>
        protected override void RegisterActionMapsWithRuntime()
        {
            ActionMapConfig actionMap = new ActionMapConfig()
            {
                name = "PICO4TouchController",
                localizedName = kDeviceLocalizedName,
                desiredInteractionProfile = profile,
                manufacturer = "PICO",
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
                    // Grip
                    new ActionConfig()
                    {
                        name = "grip",
                        localizedName = "Grip",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Grip"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = squeezeValue,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Grip Pressed
                    new ActionConfig()
                    {
                        name = "gripPressed",
                        localizedName = "Grip Pressed",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "GripButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = squeezeClick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    //A / X Press
                    new ActionConfig()
                    {
                        name = "primaryButton",
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
                                interactionPath = buttonX,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.leftHand }
                            },
                            new ActionBinding()
                            {
                                interactionPath = buttonA,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.rightHand }
                            },
                        }
                    },
                    //A / X Touch
                    new ActionConfig()
                    {
                        name = "primaryTouched",
                        localizedName = "Primary Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "PrimaryTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonXTouch,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.leftHand }
                            },
                            new ActionBinding()
                            {
                                interactionPath = buttonATouch,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.rightHand }
                            },
                        }
                    },
                    //B / Y Press
                    new ActionConfig()
                    {
                        name = "secondaryButton",
                        localizedName = "Secondary Button",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "SecondaryButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonY,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.leftHand }
                            },
                            new ActionBinding()
                            {
                                interactionPath = buttonB,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.rightHand }
                            },
                        }
                    },
                    //B / Y Touch
                    new ActionConfig()
                    {
                        name = "secondaryTouched",
                        localizedName = "Secondary Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "SecondaryTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = buttonYTouch,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.leftHand }
                            },
                            new ActionBinding()
                            {
                                interactionPath = buttonBTouch,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.rightHand }
                            },
                        }
                    },
                    // Menu
                    new ActionConfig()
                    {
                        name = "menu",
                        localizedName = "Menu",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "Menu"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = menu,
                                interactionProfileName = profile,
                                userPaths = new List<string>() { UserPaths.leftHand }
                            }
                        }
                    },
                    // System
                    new ActionConfig()
                    {
                        name = "system",
                        localizedName = "system",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "System"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = system,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Trigger
                    new ActionConfig()
                    {
                        name = "trigger",
                        localizedName = "Trigger",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "Trigger"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = trigger,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Trigger Pressed
                    new ActionConfig()
                    {
                        name = "triggerPressed",
                        localizedName = "Trigger Pressed",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "TriggerButton"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = triggerClick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    //Trigger Touch
                    new ActionConfig()
                    {
                        name = "triggerTouched",
                        localizedName = "Trigger Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "TriggerTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = triggerTouch,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Joystick
                    new ActionConfig()
                    {
                        name = "thumbstick",
                        localizedName = "Thumbstick",
                        type = ActionType.Axis2D,
                        usages = new List<string>()
                        {
                            "Primary2DAxis"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = thumbstick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    //Thumbstick Clicked
                    new ActionConfig()
                    {
                        name = "thumbstickClicked",
                        localizedName = "Thumbstick Clicked",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "Primary2DAxisClick"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = thumbstickClick,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    //Thumbstick Touched
                    new ActionConfig()
                    {
                        name = "thumbstickTouched",
                        localizedName = "Thumbstick Touched",
                        type = ActionType.Binary,
                        usages = new List<string>()
                        {
                            "Primary2DAxisTouch"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = thumbstickTouch,
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
                                interactionPath = grip,
                                interactionProfileName = profile,
                            }
                        }
                    },
                    // Pointer Pose
                    new ActionConfig()
                    {
                        name = "pointer",
                        localizedName = "Pointer Pose",
                        type = ActionType.Pose,
                        usages = new List<string>()
                        {
                            "Pointer"
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
                    new ActionConfig()
                    {
                        name = "batteryLevel",
                        localizedName = "BatteryLevel",
                        type = ActionType.Axis1D,
                        usages = new List<string>()
                        {
                            "BatteryLevel"
                        },
                        bindings = new List<ActionBinding>()
                        {
                            new ActionBinding()
                            {
                                interactionPath = batteryLevel,
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
