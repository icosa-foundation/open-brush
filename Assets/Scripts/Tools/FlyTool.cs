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

        private GameObject _toolDirectionIndicator;
        private bool m_LockToController;
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

        // Touch gesture variables
        private Vector2 m_LastSingleTouchPosition;
        private float m_LastPinchDistance;
        private Vector2 m_PinchCenter;
        private bool m_IsPinching;
        private float m_SwipeThreshold = 50f; // pixels
        private float m_PinchThreshold = 20f; // pixels
        private float m_TouchLookSensitivity = 300f;
        private float m_PinchMoveSpeed = 0.1f;
        private Vector2 m_TouchStartPosition;
        private bool m_IsSwipeGesture;
        
        // UI toggle variables
        private float m_LastTapTime;
        private const float m_DoubleTapTimeWindow = 0.3f;
        private bool m_VirtualUIVisible = false;

        bool m_IsTouchScreen => !App.VrSdk.IsHmdInitialized() && App.Config.IsMobileHardware;

        public override void Init()
        {
            base.Init();
            _toolDirectionIndicator = transform.Find("DirectionIndicator").gameObject;
        }

        override public void EnableTool(bool bEnable)
        {
            base.EnableTool(bEnable);

            if (bEnable)
            {
                EatInput();

                // Enable onscreen UI as fallback for touchscreen devices
                // Users can toggle virtual buttons with double-tap
                // Primary interaction is through gestures
                if (m_IsTouchScreen)
                {
                    m_VirtualUIVisible = false;
                    m_NonVRFlyingUi.SetActive(false); // Start with gestures only
                }
                else
                {
                    m_NonVRFlyingUi.SetActive(false); // No UI for non-touchscreen
                }
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
                if (Mouse.current != null && Mouse.current.leftButton.isPressed)
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

                var virtualButtons = new Dictionary<char, bool> { { 'W', false }, { 'A', false }, { 'S', false }, { 'D', false } };
                Vector3 cameraTranslation = Vector3.zero;

                if (m_IsTouchScreen)
                {
                    HandleTouchGestures(ref mv, ref cameraTranslation, ref virtualButtons);
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
                    if (x < 0f && x < -MaxPitch) x += 360f;
                    cameraRotation.x = x;

                    App.VrSdk.GetVrCamera().transform.localEulerAngles = cameraRotation;
                }

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

                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveForward) || virtualButtons['W'])
                {
                    cameraTranslation += Vector3.forward;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveBackwards) || virtualButtons['S'])
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
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveLeft) || virtualButtons['A'])
                {
                    cameraTranslation += Vector3.left;
                }
                if (InputManager.m_Instance.GetKeyboardShortcut(InputManager.KeyboardShortcut.CameraMoveRight) || virtualButtons['D'])
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

        void HandleTouchGestures(ref Vector2 mv, ref Vector3 cameraTranslation, ref Dictionary<char, bool> virtualButtons)
        {
            if (!EnhancedTouchSupport.enabled) return;

            var touches = Touch.activeTouches;
            int touchCount = touches.Count;

            // Check for virtual button presses first
            TouchscreenVirtualKey[] btns = m_NonVRFlyingUi.GetComponentsInChildren<TouchscreenVirtualKey>();
            bool virtualButtonPressed = false;
            foreach (var btn in btns)
            {
                if (btn.m_IsPressed)
                {
                    virtualButtons[btn.m_Key] = true;
                    virtualButtonPressed = true;
                }
            }

            // Only process gestures if no virtual buttons are pressed
            if (virtualButtonPressed) return;

            if (touchCount == 1)
            {
                HandleSingleTouchGesture(touches[0], ref mv, ref cameraTranslation);
            }
            else if (touchCount == 2)
            {
                HandlePinchGesture(touches[0], touches[1], ref cameraTranslation);
            }
            else if (touchCount == 0)
            {
                // Reset gesture state when no touches
                ResetGestureState();
            }
        }

        void HandleSingleTouchGesture(Touch touch, ref Vector2 mv, ref Vector3 cameraTranslation)
        {
            Vector2 touchPosition = touch.screenPosition;
            Vector2 touchDelta = touch.delta;

            switch (touch.phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    m_TouchStartPosition = touchPosition;
                    m_LastSingleTouchPosition = touchPosition;
                    m_IsSwipeGesture = false;
                    
                    // Check for double-tap to toggle virtual UI
                    float currentTime = Time.time;
                    if (currentTime - m_LastTapTime < m_DoubleTapTimeWindow)
                    {
                        ToggleVirtualUI();
                        m_LastTapTime = 0f; // Reset to prevent triple-tap
                    }
                    else
                    {
                        m_LastTapTime = currentTime;
                    }
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                    Vector2 swipeVector = touchPosition - m_TouchStartPosition;
                    float swipeDistance = swipeVector.magnitude;

                    // Determine if this is a swipe gesture or camera look
                    if (!m_IsSwipeGesture && swipeDistance > m_SwipeThreshold)
                    {
                        m_IsSwipeGesture = true;
                        HandleSwipeMovement(swipeVector, ref cameraTranslation);
                    }
                    else if (!m_IsSwipeGesture)
                    {
                        // Camera look control - only when not swiping
                        Vector2 normalizedDelta = new Vector2(
                            touchDelta.x / Screen.width,
                            touchDelta.y / Screen.height
                        );
                        mv = normalizedDelta * m_TouchLookSensitivity;
                    }
                    else
                    {
                        // Continue swipe movement
                        Vector2 moveDelta = touchPosition - m_LastSingleTouchPosition;
                        HandleSwipeMovement(moveDelta, ref cameraTranslation);
                    }

                    m_LastSingleTouchPosition = touchPosition;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    ResetGestureState();
                    break;
            }
        }

        void HandleSwipeMovement(Vector2 swipeVector, ref Vector3 cameraTranslation)
        {
            // Convert screen swipe to movement direction
            Vector2 normalizedSwipe = swipeVector.normalized;
            float swipeMagnitude = Mathf.Min(swipeVector.magnitude / Screen.height, 1f);

            // Map swipe directions to movement
            Vector3 movement = Vector3.zero;
            
            // Forward/backward (vertical swipe)
            if (Mathf.Abs(normalizedSwipe.y) > 0.3f)
            {
                movement += Vector3.forward * normalizedSwipe.y * swipeMagnitude;
            }
            
            // Left/right (horizontal swipe)
            if (Mathf.Abs(normalizedSwipe.x) > 0.3f)
            {
                movement += Vector3.right * normalizedSwipe.x * swipeMagnitude;
            }

            cameraTranslation += movement * 2f; // Boost swipe movement
        }

        void HandlePinchGesture(Touch touch1, Touch touch2, ref Vector3 cameraTranslation)
        {
            Vector2 touch1Pos = touch1.screenPosition;
            Vector2 touch2Pos = touch2.screenPosition;
            float currentDistance = Vector2.Distance(touch1Pos, touch2Pos);
            Vector2 currentCenter = (touch1Pos + touch2Pos) * 0.5f;

            if (!m_IsPinching)
            {
                // Start pinch gesture
                m_IsPinching = true;
                m_LastPinchDistance = currentDistance;
                m_PinchCenter = currentCenter;
            }
            else
            {
                // Continue pinch gesture
                float distanceDelta = currentDistance - m_LastPinchDistance;
                
                if (Mathf.Abs(distanceDelta) > m_PinchThreshold)
                {
                    // Pinch to zoom - translate forward/backward
                    float zoomDirection = Mathf.Sign(distanceDelta);
                    float zoomAmount = Mathf.Abs(distanceDelta) / Screen.height;
                    cameraTranslation += Vector3.forward * zoomDirection * zoomAmount * m_PinchMoveSpeed;
                    
                    m_LastPinchDistance = currentDistance;
                }

                // Two-finger pan for up/down movement
                Vector2 centerDelta = currentCenter - m_PinchCenter;
                if (centerDelta.magnitude > 10f) // minimum movement threshold
                {
                    Vector2 normalizedDelta = centerDelta / Screen.height;
                    cameraTranslation += Vector3.up * normalizedDelta.y * m_PinchMoveSpeed;
                    m_PinchCenter = currentCenter;
                }
            }
        }

        void ResetGestureState()
        {
            m_IsPinching = false;
            m_IsSwipeGesture = false;
            m_LastPinchDistance = 0f;
        }

        void ToggleVirtualUI()
        {
            if (m_IsTouchScreen && m_NonVRFlyingUi != null)
            {
                m_VirtualUIVisible = !m_VirtualUIVisible;
                m_NonVRFlyingUi.SetActive(m_VirtualUIVisible);
            }
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
    }
}
