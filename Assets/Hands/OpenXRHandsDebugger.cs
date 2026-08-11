using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

/// <summary>
/// OpenXR / XR Hands runtime debugger.
///
/// Unity 6 + OpenXR + XR Hands.
///
/// Displays diagnostic information directly inside a referenced TMP_Text.
///
/// Reports:
/// - Unity / device information
/// - OpenXR enabled/disabled features
/// - XRHandSubsystem existence
/// - XRHandSubsystem running state
/// - Left / Right hand tracking state
/// - Handedness
/// - Root hand poses
/// - Joint availability
/// - Joint tracking states
/// - Joint poses
/// - Joint radius
/// - Joint velocities
/// - XR input devices
/// - Device characteristics
/// - isTracked
/// - trackingState
/// - device position / rotation
/// - all exposed XR InputFeatureUsages
///
/// SETUP:
///
/// 1. Add this component to a GameObject.
/// 2. Create a TextMeshPro or TextMeshProUGUI component.
/// 3. Drag it into Debug Text.
/// 4. Run on device.
/// 5. The text updates automatically.
///
/// The detailed report can get VERY long.
/// For headset debugging a world-space Canvas + ScrollRect works well.
/// </summary>
public class OpenXRHandsDebugger : MonoBehaviour
{
    // ================================================================
    // OUTPUT
    // ================================================================

    [Header("Text Output")]

    [Tooltip("TextMeshPro component receiving the debug report.")]
    [SerializeField]
    private TMP_Text debugText;

    [Tooltip(
        "If enabled, the text field contains the complete detailed report. " +
        "This includes individual joints and XR feature usages."
    )]
    [SerializeField]
    private bool fullReportInText = false;

    [Tooltip(
        "Include every OpenXR feature in the report. " +
        "Otherwise only hand-related features appear in the compact report."
    )]
    [SerializeField]
    private bool showOpenXRFeatures = true;

    [Tooltip("Show XR InputDevices.")]
    [SerializeField]
    private bool showXRDevices = true;

    [Tooltip("Show summary of joint availability.")]
    [SerializeField]
    private bool showJointSummary = true;

    [Tooltip(
        "Show every individual hand joint in the live text output."
    )]
    [SerializeField]
    private bool showEveryJoint = false;

    [Tooltip(
        "Show every feature exposed by XR InputDevices. " +
        "This can generate a lot of text."
    )]
    [SerializeField]
    private bool showAllDeviceFeatures = false;


    // ================================================================
    // REFRESH
    // ================================================================

    [Header("Refresh")]

    [Tooltip("How often the text report refreshes.")]
    [SerializeField]
    private float refreshInterval = 0.25f;

    [Tooltip(
        "How often to search for XRHandSubsystem if it hasn't appeared yet."
    )]
    [SerializeField]
    private float subsystemSearchInterval = 1f;


    // ================================================================
    // LOGGING
    // ================================================================

    [Header("Logging")]

    [Tooltip("Log when left/right hand tracking starts or stops.")]
    [SerializeField]
    private bool logHandTrackingChanges = true;

    [Tooltip("Log XR device connect / disconnect / config events.")]
    [SerializeField]
    private bool logDeviceChanges = true;


    // ================================================================
    // XR HANDS
    // ================================================================

    private XRHandSubsystem handSubsystem;

    private readonly List<XRHandSubsystem> handSubsystems =
        new List<XRHandSubsystem>();


    // ================================================================
    // XR INPUT
    // ================================================================

    private readonly List<XRInputSubsystem> xrInputSubsystems =
        new List<XRInputSubsystem>();

    private readonly List<InputDevice> xrDevices =
        new List<InputDevice>();

    private readonly List<InputDevice> nodeDevices =
        new List<InputDevice>();

    private readonly List<InputFeatureUsage> featureUsages =
        new List<InputFeatureUsage>();


    // ================================================================
    // INTERNAL STATE
    // ================================================================

    private float nextRefreshTime;
    private float nextSubsystemSearchTime;

    private bool previousLeftTracked;
    private bool previousRightTracked;

    private bool handTrackingStateInitialized;


    // ================================================================
    // PUBLIC ACCESS
    // ================================================================

    /// <summary>
    /// Allows another class to dynamically assign the debug text.
    /// </summary>
    public TMP_Text DebugText
    {
        get => debugText;
        set
        {
            debugText = value;
            RefreshText();
        }
    }

    /// <summary>
    /// Currently active XRHandSubsystem.
    /// Null means no subsystem was found.
    /// </summary>
    public XRHandSubsystem HandSubsystem => handSubsystem;

    public bool HasHandSubsystem => handSubsystem != null;

    public bool HandSubsystemRunning =>
        handSubsystem != null &&
        handSubsystem.running;

    public bool LeftHandTracked =>
        handSubsystem != null &&
        handSubsystem.leftHand.isTracked;

    public bool RightHandTracked =>
        handSubsystem != null &&
        handSubsystem.rightHand.isTracked;


    // ================================================================
    // UNITY
    // ================================================================

    private void OnEnable()
    {
        InputDevices.deviceConnected += OnDeviceConnected;
        InputDevices.deviceDisconnected += OnDeviceDisconnected;
        InputDevices.deviceConfigChanged += OnDeviceConfigChanged;

        FindHandSubsystem();

        RefreshText();
    }

    private void OnDisable()
    {
        InputDevices.deviceConnected -= OnDeviceConnected;
        InputDevices.deviceDisconnected -= OnDeviceDisconnected;
        InputDevices.deviceConfigChanged -= OnDeviceConfigChanged;
    }

    private void Update()
    {
        // XR initialization can happen after this component starts.
        if (handSubsystem == null &&
            Time.unscaledTime >= nextSubsystemSearchTime)
        {
            nextSubsystemSearchTime =
                Time.unscaledTime + subsystemSearchInterval;

            FindHandSubsystem();
        }

        CheckTrackingChanges();

        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime =
                Time.unscaledTime + refreshInterval;

            RefreshText();
        }
    }


    // ================================================================
    // FIND XR HAND SUBSYSTEM
    // ================================================================

    [ContextMenu("Re-scan XR Hand Subsystem")]
    public void FindHandSubsystem()
    {
        handSubsystems.Clear();

        SubsystemManager.GetSubsystems(handSubsystems);

        XRHandSubsystem selectedSubsystem = null;

        // Prefer running subsystem.
        for (int i = 0; i < handSubsystems.Count; i++)
        {
            XRHandSubsystem subsystem = handSubsystems[i];

            if (subsystem != null && subsystem.running)
            {
                selectedSubsystem = subsystem;
                break;
            }
        }

        // If none are running, still keep the first existing subsystem.
        if (selectedSubsystem == null &&
            handSubsystems.Count > 0)
        {
            selectedSubsystem = handSubsystems[0];
        }

        bool changed =
            selectedSubsystem != handSubsystem;

        handSubsystem = selectedSubsystem;

        if (changed)
        {
            handTrackingStateInitialized = false;

            if (handSubsystem != null)
            {
                Debug.Log(
                    "[OpenXRHandsDebugger] XRHandSubsystem found.\n" +
                    $"Type: {handSubsystem.GetType().FullName}\n" +
                    $"Running: {handSubsystem.running}"
                );
            }
            else
            {
                Debug.LogWarning(
                    "[OpenXRHandsDebugger] XRHandSubsystem NOT FOUND.\n\n" +

                    "Check:\n" +
                    "- XR Hands package installed\n" +
                    "- OpenXR loader enabled\n" +
                    "- OpenXR Hand Tracking feature enabled\n" +
                    "- XR session started\n" +
                    "- Runtime supports hand tracking"
                );
            }
        }

        RefreshText();
    }


    // ================================================================
    // TRACKING CHANGES
    // ================================================================

    private void CheckTrackingChanges()
    {
        if (handSubsystem == null)
            return;

        bool leftTracked =
            handSubsystem.leftHand.isTracked;

        bool rightTracked =
            handSubsystem.rightHand.isTracked;

        if (!handTrackingStateInitialized)
        {
            previousLeftTracked = leftTracked;
            previousRightTracked = rightTracked;

            handTrackingStateInitialized = true;

            return;
        }

        if (leftTracked != previousLeftTracked)
        {
            if (logHandTrackingChanges)
            {
                if (leftTracked)
                {
                    Debug.Log(
                        "[OpenXRHandsDebugger] LEFT HAND TRACKING ACQUIRED"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[OpenXRHandsDebugger] LEFT HAND TRACKING LOST"
                    );
                }
            }

            previousLeftTracked = leftTracked;
        }

        if (rightTracked != previousRightTracked)
        {
            if (logHandTrackingChanges)
            {
                if (rightTracked)
                {
                    Debug.Log(
                        "[OpenXRHandsDebugger] RIGHT HAND TRACKING ACQUIRED"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[OpenXRHandsDebugger] RIGHT HAND TRACKING LOST"
                    );
                }
            }

            previousRightTracked = rightTracked;
        }
    }


    // ================================================================
    // DEVICE EVENTS
    // ================================================================

    private void OnDeviceConnected(InputDevice device)
    {
        if (!logDeviceChanges)
            return;

        Debug.Log(
            "[OpenXRHandsDebugger] XR DEVICE CONNECTED\n" +
            GetDeviceShortDescription(device)
        );
    }

    private void OnDeviceDisconnected(InputDevice device)
    {
        if (!logDeviceChanges)
            return;

        Debug.LogWarning(
            "[OpenXRHandsDebugger] XR DEVICE DISCONNECTED\n" +
            GetDeviceShortDescription(device)
        );
    }

    private void OnDeviceConfigChanged(InputDevice device)
    {
        if (!logDeviceChanges)
            return;

        Debug.Log(
            "[OpenXRHandsDebugger] XR DEVICE CONFIG CHANGED\n" +
            GetDeviceShortDescription(device)
        );
    }


    // ================================================================
    // TEXT OUTPUT
    // ================================================================

    [ContextMenu("Refresh Text")]
    public void RefreshText()
    {
        if (debugText == null)
            return;

        if (fullReportInText)
        {
            debugText.text = GenerateFullReport();
        }
        else
        {
            debugText.text = GenerateCompactReport();
        }
    }


    // ================================================================
    // COMPACT REPORT
    // ================================================================

    public string GenerateCompactReport()
    {
        StringBuilder sb = new StringBuilder(8192);

        sb.AppendLine("<b>OPENXR HAND DEBUGGER</b>");
        sb.AppendLine("==============================");

        sb.AppendLine();

        sb.AppendLine(
            $"Unity: {Application.unityVersion}"
        );

        sb.AppendLine(
            $"Platform: {Application.platform}"
        );

        sb.AppendLine(
            $"Device: {SystemInfo.deviceModel}"
        );

        sb.AppendLine();

        AppendCompactOpenXR(sb);

        AppendCompactHandSubsystem(sb);

        AppendCompactHand(
            sb,
            "LEFT HAND",
            true
        );

        AppendCompactHand(
            sb,
            "RIGHT HAND",
            false
        );

        if (showJointSummary)
        {
            AppendJointSummary(sb);
        }

        if (showXRDevices)
        {
            AppendCompactDevices(sb);
        }

        if (showEveryJoint)
        {
            AppendAllJoints(
                sb,
                true,
                false
            );
        }

        if (showAllDeviceFeatures)
        {
            AppendXRDeviceInformation(
                sb,
                true
            );
        }

        return sb.ToString();
    }


    // ================================================================
    // FULL REPORT
    // ================================================================

    [ContextMenu("Dump FULL Report")]
    public void DumpFullReport()
    {
        string report = GenerateFullReport();

        Debug.Log(report);

        if (debugText != null)
        {
            debugText.text = report;
        }
    }

    public string GenerateFullReport()
    {
        StringBuilder sb = new StringBuilder(32768);

        sb.AppendLine(
            "==============================================="
        );

        sb.AppendLine(
            " OPENXR / XR HANDS FULL DIAGNOSTIC REPORT"
        );

        sb.AppendLine(
            "==============================================="
        );

        sb.AppendLine();

        AppendSystemInformation(sb);

        AppendOpenXRInformation(sb);

        AppendXRInputSubsystemInformation(sb);

        AppendXRDeviceInformation(
            sb,
            true
        );

        AppendXRHandSubsystemInformation(sb);

        AppendHandFullReport(
            sb,
            "LEFT HAND",
            true
        );

        AppendHandFullReport(
            sb,
            "RIGHT HAND",
            false
        );

        sb.AppendLine();

        sb.AppendLine(
            "==============================================="
        );

        sb.AppendLine(
            " END REPORT"
        );

        sb.AppendLine(
            "==============================================="
        );

        return sb.ToString();
    }


    // ================================================================
    // SYSTEM
    // ================================================================

    private void AppendSystemInformation(StringBuilder sb)
    {
        sb.AppendLine("UNITY / PLATFORM");
        sb.AppendLine("------------------------------");

        sb.AppendLine(
            $"Unity version: {Application.unityVersion}"
        );

        sb.AppendLine(
            $"Platform: {Application.platform}"
        );

        sb.AppendLine(
            $"Product: {Application.productName}"
        );

        sb.AppendLine(
            $"Device model: {SystemInfo.deviceModel}"
        );

        sb.AppendLine(
            $"Device name: {SystemInfo.deviceName}"
        );

        sb.AppendLine(
            $"Operating system: {SystemInfo.operatingSystem}"
        );

        sb.AppendLine(
            $"Graphics device: {SystemInfo.graphicsDeviceName}"
        );

        sb.AppendLine(
            $"Graphics API: {SystemInfo.graphicsDeviceType}"
        );

        sb.AppendLine();
    }


    // ================================================================
    // OPENXR
    // ================================================================

    private void AppendCompactOpenXR(StringBuilder sb)
    {
        sb.AppendLine("<b>OPENXR</b>");

        OpenXRSettings settings =
            OpenXRSettings.Instance;

        if (settings == null)
        {
            sb.AppendLine(
                "<color=red>OpenXRSettings: NULL</color>"
            );

            sb.AppendLine();

            return;
        }

        sb.AppendLine(
            "<color=green>OpenXRSettings: AVAILABLE</color>"
        );

        if (showOpenXRFeatures)
        {
            OpenXRFeature[] features =
                settings.GetFeatures();

            if (features != null)
            {
                for (int i = 0; i < features.Length; i++)
                {
                    OpenXRFeature feature = features[i];

                    if (feature == null)
                        continue;

                    if (!IsHandRelatedFeature(feature))
                        continue;

                    string state =
                        feature.enabled
                            ? "<color=green>ON</color>"
                            : "<color=red>OFF</color>";

                    sb.AppendLine(
                        $"  [{state}] {feature.name}"
                    );

                    sb.AppendLine(
                        $"      {feature.GetType().Name}"
                    );
                }
            }
        }

        sb.AppendLine();
    }

    private void AppendOpenXRInformation(StringBuilder sb)
    {
        sb.AppendLine("OPENXR");
        sb.AppendLine("------------------------------");

        OpenXRSettings settings =
            OpenXRSettings.Instance;

        if (settings == null)
        {
            sb.AppendLine(
                "OpenXRSettings.Instance: NULL"
            );

            sb.AppendLine();

            return;
        }

        sb.AppendLine(
            "OpenXRSettings.Instance: AVAILABLE"
        );

        OpenXRFeature[] features =
            settings.GetFeatures();

        if (features == null)
        {
            sb.AppendLine(
                "OpenXR features: NULL"
            );

            sb.AppendLine();

            return;
        }

        sb.AppendLine(
            $"OpenXR feature count: {features.Length}"
        );

        sb.AppendLine();

        for (int i = 0; i < features.Length; i++)
        {
            OpenXRFeature feature = features[i];

            if (feature == null)
                continue;

            sb.Append(
                feature.enabled
                    ? "[ENABLED ] "
                    : "[DISABLED] "
            );

            sb.Append(feature.name);

            sb.Append(
                " | "
            );

            sb.Append(
                feature.GetType().FullName
            );

            if (IsHandRelatedFeature(feature))
            {
                sb.Append(
                    "    <--- HAND RELATED"
                );
            }

            sb.AppendLine();
        }

        sb.AppendLine();
    }

    private bool IsHandRelatedFeature(
        OpenXRFeature feature)
    {
        if (feature == null)
            return false;

        string typeName =
            feature.GetType().FullName ?? "";

        string featureName =
            feature.name ?? "";

        string text =
            (
                featureName +
                " " +
                typeName
            ).ToLowerInvariant();

        return
            text.Contains("hand") ||
            text.Contains("palm") ||
            text.Contains("aim");
    }


    // ================================================================
    // XR HAND SUBSYSTEM
    // ================================================================

    private void AppendCompactHandSubsystem(
        StringBuilder sb)
    {
        sb.AppendLine(
            "<b>XR HAND SUBSYSTEM</b>"
        );

        handSubsystems.Clear();

        SubsystemManager.GetSubsystems(
            handSubsystems
        );

        sb.AppendLine(
            $"Instances: {handSubsystems.Count}"
        );

        if (handSubsystem == null)
        {
            sb.AppendLine(
                "<color=red>SUBSYSTEM NOT FOUND</color>"
            );

            sb.AppendLine();

            return;
        }

        sb.AppendLine(
            $"Type: {handSubsystem.GetType().Name}"
        );

        if (handSubsystem.running)
        {
            sb.AppendLine(
                "<color=green>Running: TRUE</color>"
            );
        }
        else
        {
            sb.AppendLine(
                "<color=red>Running: FALSE</color>"
            );
        }

        sb.AppendLine(
            $"Update flags: {handSubsystem.updateSuccessFlags}"
        );

        sb.AppendLine(
            $"Supported joints: {CountSupportedJoints()}"
        );

        sb.AppendLine();
    }

    private void AppendXRHandSubsystemInformation(
        StringBuilder sb)
    {
        sb.AppendLine(
            "XR HAND SUBSYSTEM"
        );

        sb.AppendLine(
            "------------------------------"
        );

        handSubsystems.Clear();

        SubsystemManager.GetSubsystems(
            handSubsystems
        );

        sb.AppendLine(
            $"Subsystem instances: {handSubsystems.Count}"
        );

        for (int i = 0;
             i < handSubsystems.Count;
             i++)
        {
            XRHandSubsystem subsystem =
                handSubsystems[i];

            if (subsystem == null)
                continue;

            sb.AppendLine(
                $"[{i}] {subsystem.GetType().FullName}"
            );

            sb.AppendLine(
                $"    Running: {subsystem.running}"
            );
        }

        sb.AppendLine();

        if (handSubsystem == null)
        {
            sb.AppendLine(
                "ACTIVE XRHandSubsystem: NONE"
            );

            sb.AppendLine();

            return;
        }

        sb.AppendLine(
            $"Active subsystem: {handSubsystem.GetType().FullName}"
        );

        sb.AppendLine(
            $"Running: {handSubsystem.running}"
        );

        sb.AppendLine(
            $"Update success flags: {handSubsystem.updateSuccessFlags}"
        );

        sb.AppendLine(
            $"Provider supported joints: {CountSupportedJoints()}"
        );

        sb.AppendLine();
    }


    // ================================================================
    // HAND SUMMARY
    // ================================================================

    private void AppendCompactHand(
        StringBuilder sb,
        string name,
        bool left)
    {
        sb.AppendLine(
            $"<b>{name}</b>"
        );

        if (handSubsystem == null)
        {
            sb.AppendLine(
                "<color=red>Unavailable</color>"
            );

            sb.AppendLine();

            return;
        }

        XRHand hand =
            left
                ? handSubsystem.leftHand
                : handSubsystem.rightHand;

        sb.AppendLine(
            $"Handedness: {hand.handedness}"
        );

        if (hand.isTracked)
        {
            sb.AppendLine(
                "<color=green>TRACKED: TRUE</color>"
            );
        }
        else
        {
            sb.AppendLine(
                "<color=red>TRACKED: FALSE</color>"
            );
        }

        int validPoses =
            CountAvailableJointPoses(hand);

        int supported =
            CountSupportedJoints();

        sb.AppendLine(
            $"Joint poses: {validPoses}/{supported}"
        );

        sb.AppendLine(
            $"Root pos: {FormatVector(hand.rootPose.position)}"
        );

        sb.AppendLine(
            $"Root rot: {FormatEuler(hand.rootPose.rotation)}"
        );

        sb.AppendLine();
    }


    // ================================================================
    // HAND FULL REPORT
    // ================================================================

    private void AppendHandFullReport(
        StringBuilder sb,
        string title,
        bool left)
    {
        sb.AppendLine(title);
        sb.AppendLine("------------------------------");

        if (handSubsystem == null)
        {
            sb.AppendLine(
                "XRHandSubsystem unavailable."
            );

            sb.AppendLine();

            return;
        }

        XRHand hand =
            left
                ? handSubsystem.leftHand
                : handSubsystem.rightHand;

        sb.AppendLine(
            $"Handedness: {hand.handedness}"
        );

        sb.AppendLine(
            $"Tracked: {hand.isTracked}"
        );

        sb.AppendLine(
            $"Root Position: {FormatVector(hand.rootPose.position)}"
        );

        sb.AppendLine(
            $"Root Rotation: {FormatQuaternion(hand.rootPose.rotation)}"
        );

        sb.AppendLine(
            $"Root Euler: {FormatEuler(hand.rootPose.rotation)}"
        );

        sb.AppendLine(
            $"Valid joint poses: " +
            $"{CountAvailableJointPoses(hand)}/" +
            $"{CountSupportedJoints()}"
        );

        sb.AppendLine();

        AppendHandJointDetails(
            sb,
            hand
        );

        sb.AppendLine();
    }


    // ================================================================
    // JOINT SUMMARY
    // ================================================================

    private void AppendJointSummary(
        StringBuilder sb)
    {
        if (handSubsystem == null)
            return;

        sb.AppendLine(
            "<b>JOINT DATA</b>"
        );

        AppendJointAvailabilityLine(
            sb,
            "LEFT",
            handSubsystem.leftHand
        );

        AppendJointAvailabilityLine(
            sb,
            "RIGHT",
            handSubsystem.rightHand
        );

        sb.AppendLine();
    }

    private void AppendJointAvailabilityLine(
        StringBuilder sb,
        string name,
        XRHand hand)
    {
        int supported =
            CountSupportedJoints();

        int poses = 0;
        int radii = 0;
        int linearVelocity = 0;
        int angularVelocity = 0;

        for (
            int i = XRHandJointID.BeginMarker.ToIndex();
            i < XRHandJointID.EndMarker.ToIndex();
            i++)
        {
            XRHandJointID id =
                XRHandJointIDUtility.FromIndex(i);

            XRHandJoint joint =
                hand.GetJoint(id);

            if (joint.TryGetPose(out _))
                poses++;

            if (joint.TryGetRadius(out _))
                radii++;

            if (joint.TryGetLinearVelocity(out _))
                linearVelocity++;

            if (joint.TryGetAngularVelocity(out _))
                angularVelocity++;
        }

        sb.AppendLine(
            $"{name}:"
        );

        sb.AppendLine(
            $"  supported: {supported}"
        );

        sb.AppendLine(
            $"  poses: {poses}"
        );

        sb.AppendLine(
            $"  radii: {radii}"
        );

        sb.AppendLine(
            $"  linear velocity: {linearVelocity}"
        );

        sb.AppendLine(
            $"  angular velocity: {angularVelocity}"
        );
    }


    // ================================================================
    // ALL JOINTS
    // ================================================================

    private void AppendAllJoints(
        StringBuilder sb,
        bool bothHands,
        bool detailed)
    {
        if (handSubsystem == null)
            return;

        AppendHandJointList(
            sb,
            "LEFT JOINTS",
            handSubsystem.leftHand,
            detailed
        );

        if (bothHands)
        {
            AppendHandJointList(
                sb,
                "RIGHT JOINTS",
                handSubsystem.rightHand,
                detailed
            );
        }
    }

    private void AppendHandJointList(
        StringBuilder sb,
        string title,
        XRHand hand,
        bool detailed)
    {
        sb.AppendLine(
            $"<b>{title}</b>"
        );

        for (
            int i = XRHandJointID.BeginMarker.ToIndex();
            i < XRHandJointID.EndMarker.ToIndex();
            i++)
        {
            XRHandJointID id =
                XRHandJointIDUtility.FromIndex(i);

            XRHandJoint joint =
                hand.GetJoint(id);

            bool supported =
                IsJointSupported(id);

            bool hasPose =
                joint.TryGetPose(out Pose pose);

            sb.Append(
                $"{id}: "
            );

            if (!supported)
            {
                sb.AppendLine(
                    "NOT SUPPORTED"
                );

                continue;
            }

            if (!hasPose)
            {
                sb.AppendLine(
                    $"NO POSE | {joint.trackingState}"
                );

                continue;
            }

            sb.AppendLine(
                $"{joint.trackingState} | {FormatVector(pose.position)}"
            );

            if (detailed)
            {
                sb.AppendLine(
                    $"    rot: {FormatEuler(pose.rotation)}"
                );
            }
        }

        sb.AppendLine();
    }


    // ================================================================
    // DETAILED JOINT DATA
    // ================================================================

    private void AppendHandJointDetails(
        StringBuilder sb,
        XRHand hand)
    {
        sb.AppendLine(
            "JOINTS:"
        );

        int supportedCount = 0;
        int poseCount = 0;
        int radiusCount = 0;
        int linearVelocityCount = 0;
        int angularVelocityCount = 0;

        for (
            int i = XRHandJointID.BeginMarker.ToIndex();
            i < XRHandJointID.EndMarker.ToIndex();
            i++)
        {
            XRHandJointID jointID =
                XRHandJointIDUtility.FromIndex(i);

            XRHandJoint joint =
                hand.GetJoint(jointID);

            bool supported =
                IsJointSupported(jointID);

            bool hasPose =
                joint.TryGetPose(out Pose pose);

            bool hasRadius =
                joint.TryGetRadius(out float radius);

            bool hasLinearVelocity =
                joint.TryGetLinearVelocity(
                    out Vector3 linearVelocity
                );

            bool hasAngularVelocity =
                joint.TryGetAngularVelocity(
                    out Vector3 angularVelocity
                );

            if (supported)
                supportedCount++;

            if (hasPose)
                poseCount++;

            if (hasRadius)
                radiusCount++;

            if (hasLinearVelocity)
                linearVelocityCount++;

            if (hasAngularVelocity)
                angularVelocityCount++;

            sb.AppendLine();

            sb.AppendLine(
                $"[{jointID}]"
            );

            sb.AppendLine(
                $"    Provider supports: {supported}"
            );

            sb.AppendLine(
                $"    Tracking state: {joint.trackingState}"
            );

            sb.AppendLine(
                $"    Pose available: {hasPose}"
            );

            if (hasPose)
            {
                sb.AppendLine(
                    $"    Position: {FormatVector(pose.position)}"
                );

                sb.AppendLine(
                    $"    Rotation: {FormatQuaternion(pose.rotation)}"
                );

                sb.AppendLine(
                    $"    Euler: {FormatEuler(pose.rotation)}"
                );
            }

            sb.AppendLine(
                $"    Radius available: {hasRadius}"
            );

            if (hasRadius)
            {
                sb.AppendLine(
                    $"    Radius: {radius:F6} m"
                );
            }

            sb.AppendLine(
                $"    Linear velocity available: {hasLinearVelocity}"
            );

            if (hasLinearVelocity)
            {
                sb.AppendLine(
                    $"    Linear velocity: {FormatVector(linearVelocity)}"
                );
            }

            sb.AppendLine(
                $"    Angular velocity available: {hasAngularVelocity}"
            );

            if (hasAngularVelocity)
            {
                sb.AppendLine(
                    $"    Angular velocity: {FormatVector(angularVelocity)}"
                );
            }
        }

        sb.AppendLine();

        sb.AppendLine(
            "JOINT TOTALS:"
        );

        sb.AppendLine(
            $"    Supported: {supportedCount}"
        );

        sb.AppendLine(
            $"    Pose available: {poseCount}"
        );

        sb.AppendLine(
            $"    Radius available: {radiusCount}"
        );

        sb.AppendLine(
            $"    Linear velocity available: {linearVelocityCount}"
        );

        sb.AppendLine(
            $"    Angular velocity available: {angularVelocityCount}"
        );
    }


    // ================================================================
    // JOINT HELPERS
    // ================================================================

    private int CountSupportedJoints()
    {
        if (handSubsystem == null)
            return 0;

        int count = 0;

        for (
            int i = XRHandJointID.BeginMarker.ToIndex();
            i < XRHandJointID.EndMarker.ToIndex();
            i++)
        {
            XRHandJointID id =
                XRHandJointIDUtility.FromIndex(i);

            if (IsJointSupported(id))
                count++;
        }

        return count;
    }

    private int CountAvailableJointPoses(
        XRHand hand)
    {
        int count = 0;

        for (
            int i = XRHandJointID.BeginMarker.ToIndex();
            i < XRHandJointID.EndMarker.ToIndex();
            i++)
        {
            XRHandJointID id =
                XRHandJointIDUtility.FromIndex(i);

            XRHandJoint joint =
                hand.GetJoint(id);

            if (joint.TryGetPose(out _))
                count++;
        }

        return count;
    }

    private bool IsJointSupported(
        XRHandJointID jointID)
    {
        if (handSubsystem == null)
            return false;

        int index =
            jointID.ToIndex();

        if (index < 0)
            return false;

        if (index >= handSubsystem.jointsInLayout.Length)
            return false;

        return handSubsystem.jointsInLayout[index];
    }


    // ================================================================
    // XR INPUT SUBSYSTEMS
    // ================================================================

    private void AppendXRInputSubsystemInformation(
        StringBuilder sb)
    {
        sb.AppendLine(
            "XR INPUT SUBSYSTEMS"
        );

        sb.AppendLine(
            "------------------------------"
        );

        xrInputSubsystems.Clear();

        SubsystemManager.GetSubsystems(
            xrInputSubsystems
        );

        sb.AppendLine(
            $"Count: {xrInputSubsystems.Count}"
        );

        for (int i = 0;
             i < xrInputSubsystems.Count;
             i++)
        {
            XRInputSubsystem subsystem =
                xrInputSubsystems[i];

            if (subsystem == null)
                continue;

            sb.AppendLine();

            sb.AppendLine(
                $"[{i}] {subsystem.GetType().FullName}"
            );

            sb.AppendLine(
                $"    Running: {subsystem.running}"
            );

            try
            {
                sb.AppendLine(
                    $"    Tracking origin: " +
                    $"{subsystem.GetTrackingOriginMode()}"
                );

                sb.AppendLine(
                    $"    Supported origins: " +
                    $"{subsystem.GetSupportedTrackingOriginModes()}"
                );
            }
            catch (Exception e)
            {
                sb.AppendLine(
                    $"    Tracking origin query failed: {e.Message}"
                );
            }
        }

        sb.AppendLine();
    }


    // ================================================================
    // XR DEVICES COMPACT
    // ================================================================

    private void AppendCompactDevices(
        StringBuilder sb)
    {
        xrDevices.Clear();

        InputDevices.GetDevices(
            xrDevices
        );

        sb.AppendLine(
            $"<b>XR DEVICES ({xrDevices.Count})</b>"
        );

        for (int i = 0;
             i < xrDevices.Count;
             i++)
        {
            InputDevice device =
                xrDevices[i];

            bool left =
                HasCharacteristic(
                    device,
                    InputDeviceCharacteristics.Left
                );

            bool right =
                HasCharacteristic(
                    device,
                    InputDeviceCharacteristics.Right
                );

            bool handTracking =
                HasCharacteristic(
                    device,
                    InputDeviceCharacteristics.HandTracking
                );

            bool headMounted =
                HasCharacteristic(
                    device,
                    InputDeviceCharacteristics.HeadMounted
                );

            // Only show relevant XR tracking devices.
            if (!left &&
                !right &&
                !handTracking &&
                !headMounted)
            {
                continue;
            }

            sb.AppendLine();

            sb.AppendLine(
                device.name
            );

            sb.AppendLine(
                $"  valid: {device.isValid}"
            );

            sb.AppendLine(
                $"  characteristics: {device.characteristics}"
            );

            sb.AppendLine(
                $"  left: {left}"
            );

            sb.AppendLine(
                $"  right: {right}"
            );

            sb.AppendLine(
                $"  hand tracking: {handTracking}"
            );

            if (device.TryGetFeatureValue(
                    CommonUsages.isTracked,
                    out bool tracked))
            {
                sb.AppendLine(
                    $"  tracked: {tracked}"
                );
            }
            else
            {
                sb.AppendLine(
                    "  tracked: N/A"
                );
            }

            if (device.TryGetFeatureValue(
                    CommonUsages.trackingState,
                    out InputTrackingState trackingState))
            {
                sb.AppendLine(
                    $"  tracking state: {trackingState}"
                );
            }
        }

        AppendXRNodeDeviceCompact(
            sb,
            XRNode.LeftHand
        );

        AppendXRNodeDeviceCompact(
            sb,
            XRNode.RightHand
        );

        sb.AppendLine();
    }

    private void AppendXRNodeDeviceCompact(
        StringBuilder sb,
        XRNode node)
    {
        nodeDevices.Clear();

        InputDevices.GetDevicesAtXRNode(
            node,
            nodeDevices
        );

        sb.AppendLine();

        sb.AppendLine(
            $"{node} XRNode devices: {nodeDevices.Count}"
        );

        for (int i = 0;
             i < nodeDevices.Count;
             i++)
        {
            InputDevice device =
                nodeDevices[i];

            sb.AppendLine(
                $"  {device.name}"
            );
        }
    }


    // ================================================================
    // XR DEVICES FULL
    // ================================================================

    private void AppendXRDeviceInformation(
        StringBuilder sb,
        bool includeFeatureUsages)
    {
        sb.AppendLine(
            "XR INPUT DEVICES"
        );

        sb.AppendLine(
            "------------------------------"
        );

        xrDevices.Clear();

        InputDevices.GetDevices(
            xrDevices
        );

        sb.AppendLine(
            $"Total devices: {xrDevices.Count}"
        );

        for (int i = 0;
             i < xrDevices.Count;
             i++)
        {
            InputDevice device =
                xrDevices[i];

            sb.AppendLine();

            sb.AppendLine(
                $"DEVICE [{i}]"
            );

            sb.AppendLine(
                $"    Name: {device.name}"
            );

            sb.AppendLine(
                $"    Manufacturer: {device.manufacturer}"
            );

            sb.AppendLine(
                $"    Serial: {device.serialNumber}"
            );

            sb.AppendLine(
                $"    Valid: {device.isValid}"
            );

            sb.AppendLine(
                $"    Characteristics: {device.characteristics}"
            );

            sb.AppendLine(
                $"    Left: " +
                $"{HasCharacteristic(device, InputDeviceCharacteristics.Left)}"
            );

            sb.AppendLine(
                $"    Right: " +
                $"{HasCharacteristic(device, InputDeviceCharacteristics.Right)}"
            );

            sb.AppendLine(
                $"    HandTracking: " +
                $"{HasCharacteristic(device, InputDeviceCharacteristics.HandTracking)}"
            );

            sb.AppendLine(
                $"    Controller: " +
                $"{HasCharacteristic(device, InputDeviceCharacteristics.Controller)}"
            );

            sb.AppendLine(
                $"    HeadMounted: " +
                $"{HasCharacteristic(device, InputDeviceCharacteristics.HeadMounted)}"
            );

            AppendKnownDeviceValues(
                sb,
                device
            );

            if (includeFeatureUsages)
            {
                AppendDeviceFeatureUsages(
                    sb,
                    device
                );
            }
        }

        sb.AppendLine();

        AppendXRNodeDevices(
            sb,
            XRNode.LeftHand
        );

        AppendXRNodeDevices(
            sb,
            XRNode.RightHand
        );

        sb.AppendLine();
    }

    private void AppendKnownDeviceValues(
        StringBuilder sb,
        InputDevice device)
    {
        if (device.TryGetFeatureValue(
                CommonUsages.isTracked,
                out bool isTracked))
        {
            sb.AppendLine(
                $"    isTracked: {isTracked}"
            );
        }
        else
        {
            sb.AppendLine(
                "    isTracked: NOT AVAILABLE"
            );
        }

        if (device.TryGetFeatureValue(
                CommonUsages.trackingState,
                out InputTrackingState trackingState))
        {
            sb.AppendLine(
                $"    trackingState: {trackingState}"
            );
        }
        else
        {
            sb.AppendLine(
                "    trackingState: NOT AVAILABLE"
            );
        }

        if (device.TryGetFeatureValue(
                CommonUsages.devicePosition,
                out Vector3 position))
        {
            sb.AppendLine(
                $"    devicePosition: {FormatVector(position)}"
            );
        }
        else
        {
            sb.AppendLine(
                "    devicePosition: NOT AVAILABLE"
            );
        }

        if (device.TryGetFeatureValue(
                CommonUsages.deviceRotation,
                out Quaternion rotation))
        {
            sb.AppendLine(
                $"    deviceRotation: {FormatQuaternion(rotation)}"
            );

            sb.AppendLine(
                $"    deviceEuler: {FormatEuler(rotation)}"
            );
        }
        else
        {
            sb.AppendLine(
                "    deviceRotation: NOT AVAILABLE"
            );
        }
    }


    // ================================================================
    // DEVICE FEATURE USAGES
    // ================================================================

    private void AppendDeviceFeatureUsages(
        StringBuilder sb,
        InputDevice device)
    {
        featureUsages.Clear();

        bool success =
            device.TryGetFeatureUsages(
                featureUsages
            );

        sb.AppendLine();

        sb.AppendLine(
            $"    FEATURE USAGES"
        );

        sb.AppendLine(
            $"    Query success: {success}"
        );

        sb.AppendLine(
            $"    Count: {featureUsages.Count}"
        );

        for (int i = 0;
             i < featureUsages.Count;
             i++)
        {
            InputFeatureUsage usage =
                featureUsages[i];

            string value =
                TryGetFeatureValueAsString(
                    device,
                    usage
                );

            sb.Append(
                $"        {usage.name}"
            );

            sb.Append(
                $" [{usage.type.Name}]"
            );

            if (!string.IsNullOrEmpty(value))
            {
                sb.Append(
                    $" = {value}"
                );
            }

            sb.AppendLine();
        }
    }

    private string TryGetFeatureValueAsString(
        InputDevice device,
        InputFeatureUsage usage)
    {
        try
        {
            if (usage.type == typeof(bool))
            {
                if (device.TryGetFeatureValue(
                        usage.As<bool>(),
                        out bool value))
                {
                    return value.ToString();
                }
            }

            if (usage.type == typeof(uint))
            {
                if (device.TryGetFeatureValue(
                        usage.As<uint>(),
                        out uint value))
                {
                    return value.ToString();
                }
            }

            if (usage.type == typeof(float))
            {
                if (device.TryGetFeatureValue(
                        usage.As<float>(),
                        out float value))
                {
                    return value.ToString("F4");
                }
            }

            if (usage.type == typeof(Vector2))
            {
                if (device.TryGetFeatureValue(
                        usage.As<Vector2>(),
                        out Vector2 value))
                {
                    return
                        $"({value.x:F4}, {value.y:F4})";
                }
            }

            if (usage.type == typeof(Vector3))
            {
                if (device.TryGetFeatureValue(
                        usage.As<Vector3>(),
                        out Vector3 value))
                {
                    return FormatVector(value);
                }
            }

            if (usage.type == typeof(Quaternion))
            {
                if (device.TryGetFeatureValue(
                        usage.As<Quaternion>(),
                        out Quaternion value))
                {
                    return FormatQuaternion(value);
                }
            }

            if (usage.type == typeof(InputTrackingState))
            {
                if (device.TryGetFeatureValue(
                        usage.As<InputTrackingState>(),
                        out InputTrackingState value))
                {
                    return value.ToString();
                }
            }
        }
        catch (Exception)
        {
            // Some platform-specific feature types cannot
            // be generically retrieved.
        }

        return "";
    }


    // ================================================================
    // XR NODE DEVICES
    // ================================================================

    private void AppendXRNodeDevices(
        StringBuilder sb,
        XRNode node)
    {
        nodeDevices.Clear();

        InputDevices.GetDevicesAtXRNode(
            node,
            nodeDevices
        );

        sb.AppendLine(
            $"{node} node device count: {nodeDevices.Count}"
        );

        for (int i = 0;
             i < nodeDevices.Count;
             i++)
        {
            InputDevice device =
                nodeDevices[i];

            sb.AppendLine(
                $"    {device.name}"
            );

            sb.AppendLine(
                $"        Valid: {device.isValid}"
            );

            sb.AppendLine(
                $"        Characteristics: {device.characteristics}"
            );
        }
    }


    // ================================================================
    // DEVICE HELPERS
    // ================================================================

    private bool HasCharacteristic(
        InputDevice device,
        InputDeviceCharacteristics characteristic)
    {
        return
            (device.characteristics & characteristic)
            == characteristic;
    }

    private string GetDeviceShortDescription(
        InputDevice device)
    {
        return
            $"Name: {device.name}\n" +
            $"Manufacturer: {device.manufacturer}\n" +
            $"Valid: {device.isValid}\n" +
            $"Characteristics: {device.characteristics}";
    }


    // ================================================================
    // FORMAT
    // ================================================================

    private string FormatVector(
        Vector3 value)
    {
        return
            $"({value.x:F4}, " +
            $"{value.y:F4}, " +
            $"{value.z:F4})";
    }

    private string FormatQuaternion(
        Quaternion value)
    {
        return
            $"({value.x:F4}, " +
            $"{value.y:F4}, " +
            $"{value.z:F4}, " +
            $"{value.w:F4})";
    }

    private string FormatEuler(
        Quaternion value)
    {
        Vector3 euler =
            value.eulerAngles;

        return FormatVector(euler);
    }
}