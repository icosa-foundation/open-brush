// Copyright 2021 The Open Brush Authors
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
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace TiltBrush
{
    public class FlyTool : BaseTool
    {

        public GameObject m_NonVRFlyingUi;

        [Header("Touchscreen controls")]
        [SerializeField] private TouchJoystick m_MoveJoystick;
        [SerializeField] private TouchscreenVirtualKey m_UpButton;
        [SerializeField] private TouchscreenVirtualKey m_DownButton;

        private GameObject _toolDirectionIndicator;
        private bool m_LockToController;

        private FlyPathRecorder m_PathRecorder;
        [SerializeField]
        [Range(0f, 2f)]
        private float m_MaxSpeed = 1f;
        [SerializeField]
        [Range(0f, 18f)]
        private float m_DampingUp = 2f;
        [SerializeField]
        [Range(0f, 18f)]
        private float m_DampingDown = 12f;
        [Range(0f, 1f)]
        private float m_StopThresholdSpeed = 0.01f;

        private bool m_Armed = false;
        private bool m_InvertLook = false;

        private Vector3 m_Velocity;

        private const float LookSpeed = 1f;
        private const float MoveSpeed = 0.05f;
        private const float SprintMultiplier = 5f;
        private const float MaxPitch = 85f;

        bool m_IsTouchScreen => !App.VrSdk.IsHmdInitialized() && App.Config.IsMobileHardware;

        public override void Init()
        {
            base.Init();
            _toolDirectionIndicator = transform.Find("DirectionIndicator").gameObject;

            // Find or create the FlyPathRecorder
            m_PathRecorder = FindObjectOfType<FlyPathRecorder>();
            if (m_PathRecorder == null)
            {
                GameObject recorderGo = new GameObject("FlyPathRecorder");
                recorderGo.transform.SetParent(App.Instance.transform);
                m_PathRecorder = recorderGo.AddComponent<FlyPathRecorder>();
            }
        }

        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);

            if (bEnable)
            {
                EatInput();

                // Enable onscreenUI if no headset present and we're on a touchscreen device
                // TODO logic for detecting mice/gamepads on mobile and disabling on-screen controls
                m_NonVRFlyingUi.SetActive(m_IsTouchScreen);
            }

            m_Armed = false;

            // Make sure our UI reticle isn't active.
            SketchControlsScript.m_Instance.ForceShowUIReticle(false);
        }

        float BoundsRadius
        {
            get
            {
                return SceneSettings.m_Instance.HardBoundsRadiusMeters_SS;
            }
        }

        public override bool InputBlocked()
        {
            return !m_Armed;
        }

        public override bool AvailableDuringLoading()
        {
            return true;
        }

        public override void HideTool(bool bHide)
        {
            base.HideTool(bHide);
            if (m_IsTouchScreen)
            {
                EnhancedTouchSupport.Disable();
            }
            _toolDirectionIndicator.SetActive(!bHide);
        }

        override public void UpdateTool()
        {
            base.UpdateTool();

            //don't start teleporting until the user has released the trigger they pulled to enable the tool!
            if (InputBlocked())
            {
                if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Fly))
                {
                    m_Armed = true;
                }
                else
                {
                    return;
                }
            }

            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();

            // Handle non-VR navigation
            if (!App.VrSdk.IsHmdInitialized())
            {
                if (!EnhancedTouchSupport.enabled) EnhancedTouchSupport.Enable();

                Gamepad gamepad = Gamepad.current;
                Vector2 mv = Vector2.zero;
                Vector3 touchTranslation = Vector3.zero;

                // Read the on-screen touch controls first. While one is held it takes
                // priority, so a drag on it doesn't also get treated as a look-drag
                // (this also keeps mouse-look from fighting the joystick when testing
                // the touch UI in the editor).
                bool uiControlTouched = false;
                if (m_IsTouchScreen)
                {
                    if (m_MoveJoystick != null && m_MoveJoystick.IsPressed)
                    {
                        Vector2 j = m_MoveJoystick.Value;
                        touchTranslation += new Vector3(j.x, 0f, j.y);
                        uiControlTouched = true;
                    }
                    if (m_UpButton != null && m_UpButton.m_IsPressed)
                    {
                        touchTranslation += Vector3.up;
                        uiControlTouched = true;
                    }
                    if (m_DownButton != null && m_DownButton.m_IsPressed)
                    {
                        touchTranslation += Vector3.down;
                        uiControlTouched = true;
                    }
                }

                if (!uiControlTouched && Mouse.current != null && Mouse.current.leftButton.isPressed)
                {
                    mv += InputManager.m_Instance.GetMouseMoveDelta();
                }
                if (gamepad != null)
                {
                    Vector2 look = gamepad.rightStick.ReadValue();
                    look = new Vector2(look.x * Mathf.Abs(look.x), look.y * Mathf.Abs(look.y));
                    mv += look * LookSpeed;
                    if (gamepad.rightStickButton.wasPressedThisFrame)
                    {
                        m_InvertLook = !m_InvertLook;
                    }
                }

                if (m_IsTouchScreen && !uiControlTouched
                    && EnhancedTouchSupport.enabled && Touch.activeTouches.Count > 0)
                {
                    var t = Touch.activeTouches[0];
                    Vector2 delta = t.delta;

                    // Normalize to screen size
                    delta.x /= Screen.width;
                    delta.y /= Screen.height;

                    // Sensitivity tuning
                    float touchLookSensitivity = 300f; // tweak as needed
                    mv = delta * touchLookSensitivity;
                }

                if (mv != Vector2.zero)
                {
                    Vector3 cameraRotation = App.VrSdk.GetVrCamera().transform.rotation.eulerAngles;
                    cameraRotation.y += mv.x;
                    if (cameraRotation.y <= -180)
                    {
                        cameraRotation.y += 360;
                    }
                    else if (cameraRotation.y > 180)
                    {
                        cameraRotation.y -= 360;
                    }

                    cameraRotation.x -= m_InvertLook ? -mv.y : mv.y;

                    // Clamp the pitch to prevent flipping
                    float x = cameraRotation.x;
                    if (x > 180f) x -= 360f;
                    x = Mathf.Clamp(x, -MaxPitch, MaxPitch);
                    // Only normalize if x is less than -MaxPitch (outside clamped range)
                    cameraRotation.x = x;

                    App.VrSdk.GetVrCamera().transform.localEulerAngles = cameraRotation;
                }

                Vector3 cameraTranslation = touchTranslation;

                bool isSprinting = InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.SprintMode) ||
                                   (gamepad != null && gamepad.leftStickButton.isPressed);
                float movementSpeed = MoveSpeed * (isSprinting ? SprintMultiplier : 1f);

                if (gamepad != null)
                {
                    Vector2 move = gamepad.leftStick.ReadValue();
                    cameraTranslation += new Vector3(move.x, 0f, move.y);
                    float upDown = gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
                    cameraTranslation += Vector3.up * upDown;
                }

                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveForward))
                {
                    cameraTranslation += Vector3.forward;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveBackwards))
                {
                    cameraTranslation += Vector3.back;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveUp))
                {
                    cameraTranslation += Vector3.up;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveDown))
                {
                    cameraTranslation += Vector3.down;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveLeft))
                {
                    cameraTranslation += Vector3.left;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveRight))
                {
                    cameraTranslation += Vector3.right;
                }
                if (InputManager.m_Instance.GetKeyboardShortcutDown(InputManager.KeyboardShortcut.InvertLook))
                {
                    m_InvertLook = !m_InvertLook;
                }

                if (cameraTranslation != Vector3.zero)
                {
                    TrTransform newScene = App.Scene.Pose;
                    var sceneTranslation = App.VrSdk.GetVrCamera().transform.rotation * (cameraTranslation * movementSpeed);
                    newScene.translation -= sceneTranslation;
                    newScene = SketchControlsScript.MakeValidScenePose(newScene, BoundsRadius);
                    App.Scene.Pose = newScene;
                }
            }

            if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Fly))
            {
                Vector3 position;
                Vector3 vMovement;

                if (App.Config.m_SdkMode == SdkMode.Monoscopic)
                {
                    position = App.Scene.Pose.translation;
                    vMovement = Camera.main.transform.forward;
                }
                else
                {
                    position = rAttachPoint.position;
                    vMovement = rAttachPoint.forward;
                }

                m_Velocity = Vector3.Lerp(m_Velocity, vMovement * m_MaxSpeed, Time.deltaTime * m_DampingUp);

                AudioManager.m_Instance.WorldGrabLoop(true);
                AudioManager.m_Instance.WorldGrabbed(position);
                AudioManager.m_Instance.ChangeLoopVolume("WorldGrab",
                    Mathf.Clamp((m_Velocity.magnitude / m_MaxSpeed) /
                        AudioManager.m_Instance.m_WorldGrabLoopAttenuation, 0f,
                        AudioManager.m_Instance.m_WorldGrabLoopMaxVolume));
            }
            else
            {
                m_Velocity = Vector3.Lerp(m_Velocity, Vector3.zero, Time.deltaTime * m_DampingDown);
                float mag = m_Velocity.magnitude;
                if (mag < m_StopThresholdSpeed)
                {
                    AudioManager.m_Instance.WorldGrabLoop(false);
                    m_Velocity = Vector3.zero;
                }
                else
                {
                    AudioManager.m_Instance.ChangeLoopVolume("WorldGrab",
                        Mathf.Clamp((mag / m_MaxSpeed) /
                            AudioManager.m_Instance.m_WorldGrabLoopAttenuation, 0f,
                            AudioManager.m_Instance.m_WorldGrabLoopMaxVolume));
                }
            }

            PointerManager.m_Instance.SetMainPointerPosition(rAttachPoint.position);
        }

        void ApplyVelocity(Vector3 velocity)
        {
            TrTransform newScene = App.Scene.Pose;
            newScene.translation -= velocity;
            // newScene might have gotten just a little bit invalid.
            // Enforce the invariant that fly always sends you
            // to a scene which is MakeValidPose(scene)
            newScene = SketchControlsScript.MakeValidScenePose(newScene, BoundsRadius);
            App.Scene.Pose = newScene;
        }

        protected void Update()
        {
            ApplyVelocity(m_Velocity);
            if (!m_LockToController)
            {
                // If we're not locking to a controller, update our transforms now, instead of in LateUpdate.
                UpdateTransformsFromControllers();
            }
        }

        override public void LateUpdateTool()
        {
            base.LateUpdateTool();
            UpdateTransformsFromControllers();
        }

        private void UpdateTransformsFromControllers()
        {
            Transform rAttachPoint = InputManager.m_Instance.GetBrushControllerAttachPoint();
            // Lock tool to camera controller.
            if (m_LockToController)
            {
                transform.position = rAttachPoint.position;
                transform.rotation = rAttachPoint.rotation;
            }
            else
            {
                transform.position = SketchSurfacePanel.m_Instance.transform.position;
                transform.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
            }
        }

        /// <summary>
        /// Start recording camera path while flying
        /// </summary>
        public bool StartPathRecording()
        {
            if (m_PathRecorder == null)
            {
                Debug.LogError("FlyTool: PathRecorder not initialized");
                return false;
            }

            return m_PathRecorder.StartRecording();
        }

        /// <summary>
        /// Stop recording and get the recorded frames
        /// </summary>
        public List<FlyPathRecorder.RecordedFrame> StopPathRecording()
        {
            if (m_PathRecorder == null)
            {
                Debug.LogError("FlyTool: PathRecorder not initialized");
                return null;
            }

            return m_PathRecorder.StopRecording();
        }

        /// <summary>
        /// Check if currently recording a camera path
        /// </summary>
        public bool IsRecordingPath()
        {
            return m_PathRecorder != null && m_PathRecorder.IsRecording;
        }

        /// <summary>
        /// Get recording statistics
        /// </summary>
        public string GetRecordingStats()
        {
            if (m_PathRecorder == null) return "PathRecorder not initialized";
            return m_PathRecorder.GetRecordingStats();
        }

        /// <summary>
        /// Clear recorded frames
        /// </summary>
        public void ClearRecording()
        {
            if (m_PathRecorder != null)
            {
                m_PathRecorder.ClearRecording();
            }
        }
    }
}
