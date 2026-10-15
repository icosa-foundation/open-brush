// Copyright 2026 The Open Brush Authors. Licensed under Apache-2.0.
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Management;

namespace TiltBrush
{
    // Loading runs before App, VrSdk and shared configuration exist. Keep this UI independent
    // of the application's input managers, hand bridge, panels and storage-backed settings.
    public sealed class StartupFolderDialog : MonoBehaviour
    {
        private const int Layer = 24;
        private bool m_Done;
        private bool m_Accepted;
        private Camera m_Camera;
        private Canvas m_Canvas;
        private InputSystemUIInputModule m_Input;
        private InputActionAsset m_Actions;
        private XRManagerSettings m_Xr;
        private bool m_OwnsXr;
        private Material m_RayMaterial;
        private float m_NextInputDiagnostic;
        private readonly HashSet<int> m_LoggedPressedDevices = new HashSet<int>();
        private readonly Dictionary<int, Cursor> m_Cursors = new Dictionary<int, Cursor>();

        private sealed class Cursor
        {
            public LineRenderer ray;
            public RectTransform dot;
        }

        public static IEnumerator Show(Camera camera, GvrOverlay overlay, string folderName,
            bool retry, Action<bool> completed)
        {
            var root = new GameObject("Startup folder explanation");
            root.SetActive(false);
            var dialog = root.AddComponent<StartupFolderDialog>();
            bool overlayWasActive = overlay.gameObject.activeSelf;
            overlay.gameObject.SetActive(false);
            int cameraMask = camera.cullingMask;
            camera.cullingMask |= 1 << Layer;
            bool accepted = false;
            try
            {
                dialog.m_Camera = camera;
                // Manual XR initialization requires Unity's first graphics frame to complete.
                yield return null;
                bool xrReady = dialog.StartXr();
                root.SetActive(true);
                // Give the loading camera's tracked pose driver time to receive the first pose.
                // Place the panel once, rather than attaching it to the user's moving head.
                if (xrReady)
                {
                    double deadline = Time.realtimeSinceStartupAsDouble + 2;
                    do { yield return null; }
                    while (!HasTrackedHead() && Time.realtimeSinceStartupAsDouble < deadline);
                }
                dialog.Build(overlay.Status.font, folderName, retry, xrReady);
                LogStartup($"explanation shown retry={retry} xr={xrReady}");
                while (!dialog.m_Done) yield return null;
                accepted = dialog.m_Accepted;
            }
            finally
            {
                root.SetActive(false);
                try { dialog.ReleaseResources(); }
                finally
                {
                    Destroy(root);
                    camera.cullingMask = cameraMask;
                    overlay.gameObject.SetActive(overlayWasActive);
                }
            }
            // Destroy the EventSystem and release OpenXR before entering Android's system UI.
            yield return null;
            LogStartup($"explanation accepted={accepted}; startup XR released");
            completed(accepted);
        }

        private static bool HasTrackedHead()
        {
            foreach (var device in InputSystem.devices)
                if (device is UnityEngine.InputSystem.XR.XRHMD head && head.isTracked.isPressed)
                    return true;
            return false;
        }

        private bool StartXr()
        {
            m_Xr = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (m_Xr == null) return false;
            // Never take ownership of an already initialized session.
            if (m_Xr.activeLoader != null) return true;
            try
            {
                m_OwnsXr = true;
                m_Xr.InitializeLoaderSync();
                if (m_Xr.activeLoader == null) return false;
                m_Xr.StartSubsystems();
                return true;
            }
            catch (Exception error)
            {
                Debug.LogError($"OBDS_STARTUP XR initialization failed: {error}");
                return false;
            }
        }

        private void Build(TMP_FontAsset font, string folderName, bool retry, bool xrReady)
        {
            var panel = new GameObject("Folder explanation", typeof(RectTransform), typeof(Canvas));
            panel.layer = Layer;
            panel.transform.SetParent(transform, false);
            m_Canvas = panel.GetComponent<Canvas>();
            m_Canvas.worldCamera = m_Camera;
            m_Canvas.renderMode = xrReady ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            var rect = (RectTransform)panel.transform;
            rect.sizeDelta = new Vector2(1000, 700);
            if (xrReady)
            {
                rect.localScale = Vector3.one * 0.001f;
                var forward = Vector3.ProjectOnPlane(m_Camera.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.1f) forward = Vector3.forward;
                rect.position = m_Camera.transform.position + forward * 1.4f;
                rect.rotation = Quaternion.LookRotation(forward, Vector3.up);
                panel.AddComponent<TrackedDeviceRaycaster>();
            }
            else
            {
                var scaler = panel.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1000, 700);
                panel.AddComponent<GraphicRaycaster>();
            }
            // The background also catches rays, so aiming feedback is visible between buttons.
            ImageAt("Background", rect, Vector2.zero, new Vector2(1000, 700), new Color(0.04f, 0.06f, 0.09f));
            TextAt(font, rect, "Connect your Open Brush folder", new Vector2(0, 265), new Vector2(920, 70), 46);
            string message = retry ? "Open Brush couldn't connect to its folder. Please try again.\n\n" : "";
            message += $"Open Brush stores your sketches, exports and reference files in\nDocuments/{folderName}.\n\nGrant access so you can save new files and open existing ones.\n\nOn the next screen, select this folder, choose\n<b>Use this folder</b>, then <b>Allow</b>.\n\nCanceling folder selection exits Open Brush.";
            TextAt(font, rect, message, new Vector2(0, 20), new Vector2(920, 410), 29);
            ButtonAt(font, rect, retry ? "Try again" : "Choose folder", new Vector2(210, -265), true);
            ButtonAt(font, rect, "Exit", new Vector2(-210, -265), false);
            TextAt(font, rect, "Aim at a button. Pinch or press the trigger, then release.",
                new Vector2(0, -195), new Vector2(920, 42), 25);
            BuildInput();
        }

        private void BuildInput()
        {
            m_Actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Startup UI");
            m_Actions.AddActionMap(map);
            var position = map.AddAction("Position", InputActionType.PassThrough, expectedControlLayout: "Vector3");
            // HandInteraction also derives from XRController. Use the OpenXR aim pose for
            // both, without adding a competing controller grip-pose binding to hand devices.
            position.AddBinding("<XRController>/pointerPosition");
            var rotation = map.AddAction("Rotation", InputActionType.PassThrough, expectedControlLayout: "Quaternion");
            rotation.AddBinding("<XRController>/pointerRotation");
            var click = map.AddAction("Click", InputActionType.PassThrough, expectedControlLayout: "Button");
            // Aim activation is a separate OpenXR signal. Match the pinch input used by
            // AndroidXRHandBridge, while leaving press/release handling to Unity UI.
            click.AddBinding("<HandInteraction>/pinchValue");
            click.AddBinding("<XRController>/triggerPressed");
            click.AddBinding("<KHRSimpleController>/select");
            click.AddBinding("<Mouse>/leftButton");
            click.AddBinding("<Touchscreen>/touch*/press");
            click.performed += LogClickInput;
            click.canceled += LogClickInput;
            var point = map.AddAction("Point", InputActionType.PassThrough, expectedControlLayout: "Vector2");
            point.AddBinding("<Mouse>/position");
            point.AddBinding("<Touchscreen>/touch*/position");
            var events = new GameObject("Startup EventSystem");
            events.SetActive(false);
            events.transform.SetParent(transform, false);
            events.AddComponent<EventSystem>();
            m_Input = events.AddComponent<InputSystemUIInputModule>();
            m_Input.actionsAsset = m_Actions;
            m_Input.xrTrackingOrigin = m_Camera.transform.parent;
            m_Input.pointerBehavior = UIPointerBehavior.AllPointersAsIs;
            m_Input.trackedDevicePosition = Reference(position);
            m_Input.trackedDeviceOrientation = Reference(rotation);
            m_Input.leftClick = Reference(click);
            m_Input.point = Reference(point);
            // InputSystemUIInputModule does all hover/press/release processing. No custom pinch.
            events.SetActive(true);
            m_Actions.Enable();
            LogStartup($"UI controls position={position.controls.Count} rotation={rotation.controls.Count} click={click.controls.Count}");
        }

        private InputActionReference Reference(InputAction action) => InputActionReference.Create(action);

        private Image ImageAt(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private void TextAt(TMP_FontAsset font, RectTransform parent, string text, Vector2 position,
            Vector2 size, float fontSize)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = Layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.enableAutoSizing = true;
            label.fontSizeMin = fontSize * 0.8f;
            label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
        }

        private void ButtonAt(TMP_FontAsset font, RectTransform parent, string title, Vector2 position, bool accept)
        {
            var image = ImageAt(title, parent, position, new Vector2(360, 100), Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.12f, 0.28f, 0.43f);
            colors.highlightedColor = new Color(0.17f, 0.55f, 0.75f);
            colors.pressedColor = new Color(0.1f, 0.8f, 0.8f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(() => { m_Accepted = accept; m_Done = true; });
            var events = image.gameObject.AddComponent<EventTrigger>();
            LogButtonEvent(events, EventTriggerType.PointerEnter, title);
            LogButtonEvent(events, EventTriggerType.PointerExit, title);
            LogButtonEvent(events, EventTriggerType.PointerDown, title);
            LogButtonEvent(events, EventTriggerType.PointerUp, title);
            LogButtonEvent(events, EventTriggerType.PointerClick, title);
            TextAt(font, (RectTransform)image.transform, title, Vector2.zero, new Vector2(350, 90), 36);
        }

        private static void LogButtonEvent(EventTrigger events, EventTriggerType type, string title)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(data =>
            {
                var pointer = (PointerEventData)data;
                LogStartup($"button={title} event={type} pointer={pointer.pointerId}");
            });
            events.triggers.Add(entry);
        }

        private static void LogStartup(string message)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Keep these diagnostics available even when Unity's player logging is disabled.
            using (var log = new AndroidJavaClass("android.util.Log"))
                log.CallStatic<int>("i", "OBDS_STARTUP", message);
#else
            Debug.Log($"OBDS_STARTUP {message}");
#endif
        }

        private void LogClickInput(InputAction.CallbackContext context)
        {
            // Diagnostics only: Unity UI independently processes this same action.
            if (context.control == null) return;
            int deviceId = context.control.device.deviceId;
            bool pressed = context.ReadValueAsButton();
            bool changed = pressed ? m_LoggedPressedDevices.Add(deviceId) : m_LoggedPressedDevices.Remove(deviceId);
            if (changed) LogStartup($"clickInput device={deviceId} control={context.control.path} pressed={pressed}");
        }

        private void LogInputState()
        {
            if (Time.unscaledTime < m_NextInputDiagnostic) return;
            m_NextInputDiagnostic = Time.unscaledTime + 2;
            foreach (var device in InputSystem.devices)
            {
                if (device.TryGetChildControl<Vector3Control>("pointerPosition") == null) continue;
                var pinch = device.TryGetChildControl<AxisControl>("pinchValue");
                var aimActivate = device.TryGetChildControl<AxisControl>("pointerActivateValue");
                var trigger = device.TryGetChildControl<ButtonControl>("triggerPressed");
                var hit = m_Input.GetLastRaycastResult(device.deviceId);
                LogStartup($"device={device.deviceId} layout={device.layout} focused={Application.isFocused} pinch={pinch?.ReadValue():F2} aimActivate={aimActivate?.ReadValue():F2} trigger={trigger?.isPressed} hit={hit.gameObject?.name}");
            }
        }

        private void LateUpdate()
        {
            if (m_Input == null || m_Canvas.renderMode != RenderMode.WorldSpace) return;
            LogInputState();
            foreach (var cursor in m_Cursors.Values)
            {
                cursor.ray.enabled = false;
                cursor.dot.gameObject.SetActive(false);
            }
            if (!Application.isFocused) return;
            foreach (var device in InputSystem.devices)
            {
                var tracked = device.TryGetChildControl<ButtonControl>("pointer/isTracked") ??
                    device.TryGetChildControl<ButtonControl>("isTracked");
                var position = device.TryGetChildControl<Vector3Control>("pointerPosition");
                var rotation = device.TryGetChildControl<QuaternionControl>("pointerRotation");
                if (tracked == null || !tracked.isPressed || position == null || rotation == null) continue;
                if (!m_Cursors.TryGetValue(device.deviceId, out var cursor))
                {
                    if (m_RayMaterial == null) m_RayMaterial = new Material(Graphic.defaultGraphicMaterial);
                    var rayObject = new GameObject("Startup aim ray");
                    rayObject.layer = Layer;
                    rayObject.transform.SetParent(transform, false);
                    var ray = rayObject.AddComponent<LineRenderer>();
                    ray.sharedMaterial = m_RayMaterial;
                    ray.positionCount = 2;
                    ray.startWidth = ray.endWidth = 0.002f;
                    ray.startColor = ray.endColor = new Color(0.4f, 0.9f, 1);
                    var dot = ImageAt("Aim reticle", (RectTransform)m_Canvas.transform,
                        Vector2.zero, new Vector2(14, 14), Color.cyan);
                    dot.raycastTarget = false;
                    cursor = new Cursor { ray = ray, dot = (RectTransform)dot.transform };
                    m_Cursors.Add(device.deviceId, cursor);
                }
                var origin = position.ReadValue();
                var direction = rotation.ReadValue() * Vector3.forward;
                // The loading scene's tracking origin can differ from world coordinates.
                var trackingOrigin = m_Camera.transform.parent;
                if (trackingOrigin != null)
                {
                    origin = trackingOrigin.TransformPoint(origin);
                    direction = trackingOrigin.TransformDirection(direction);
                }
                var hit = m_Input.GetLastRaycastResult(device.deviceId);
                bool onPanel = hit.isValid && hit.module != null && hit.module.gameObject == m_Canvas.gameObject;
                var end = onPanel ? hit.worldPosition : origin + direction * 2;
                cursor.ray.SetPosition(0, origin);
                cursor.ray.SetPosition(1, end);
                cursor.ray.enabled = true;
                cursor.dot.gameObject.SetActive(onPanel);
                if (onPanel) cursor.dot.position = end - m_Canvas.transform.forward * 0.003f;
            }
        }

        private void ReleaseResources()
        {
            if (m_Actions != null)
            {
                m_Actions.Disable();
                if (m_Input != null)
                {
                    Destroy(m_Input.trackedDevicePosition);
                    Destroy(m_Input.trackedDeviceOrientation);
                    Destroy(m_Input.leftClick);
                    Destroy(m_Input.point);
                }
                Destroy(m_Actions);
                m_Actions = null;
            }
            if (m_RayMaterial != null) Destroy(m_RayMaterial);
            m_RayMaterial = null;
            if (m_OwnsXr)
            {
                m_OwnsXr = false;
                if (m_Xr.isInitializationComplete) m_Xr.DeinitializeLoader();
            }
        }

        private void OnDestroy() => ReleaseResources();
    }
}
