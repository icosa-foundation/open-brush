using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.Hands;

public sealed class AndroidXRHandPermission : MonoBehaviour
{
    private const string HandPermission =
        "android.permission.HAND_TRACKING";

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(HandPermission))
        {
            var callbacks = new PermissionCallbacks();

            callbacks.PermissionGranted += OnPermissionGranted;
            callbacks.PermissionDenied += OnPermissionDenied;

            Permission.RequestUserPermission(
                HandPermission,
                callbacks
            );

            return;
        }
#endif

        Invoke(nameof(LogHandSubsystemStatus), 1f);
    }

    private void OnPermissionGranted(string permission)
    {
        Debug.Log($"Granted permission: {permission}");

        // The subsystem starts before permission is normally requested.
        // Relaunching the application after the first grant is recommended.
        Invoke(nameof(LogHandSubsystemStatus), 1f);
    }

    private void OnPermissionDenied(string permission)
    {
        Debug.LogError(
            $"Hand tracking permission denied: {permission}"
        );
    }

    private void LogHandSubsystemStatus()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        Debug.Log($"XRHandSubsystem count: {subsystems.Count}");

        foreach (XRHandSubsystem subsystem in subsystems)
        {
            Debug.Log(
                $"XR Hands running: {subsystem.running}, " +
                $"left tracked: {subsystem.leftHand.isTracked}, " +
                $"right tracked: {subsystem.rightHand.isTracked}"
            );
        }
    }
}