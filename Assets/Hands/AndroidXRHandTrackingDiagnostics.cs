using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.XR.Hands;

namespace TiltBrush
{
    /// <summary>
    /// Diagnostics only. Never opens an Android permission dialog.
    /// </summary>
    public class AndroidXRHandTrackingDiagnostics : MonoBehaviour
    {
        private const string HandPermission = "android.permission.HAND_TRACKING";
        private readonly List<XRHandSubsystem> m_Subsystems = new();

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log(
                "ANDROIDXR_HAND_PERMISSION granted=" +
                Permission.HasUserAuthorizedPermission(HandPermission));
#else
            Debug.Log("ANDROIDXR_HAND_PERMISSION editor/non-Android");
#endif
        }

        private void Update()
        {
            if (Time.frameCount % 120 != 0)
                return;

            m_Subsystems.Clear();
            SubsystemManager.GetSubsystems(m_Subsystems);

            foreach (var subsystem in m_Subsystems)
            {
                Debug.Log(
                    "ANDROIDXR_XRHANDS " +
                    $"running={subsystem.running} " +
                    $"L={subsystem.leftHand.isTracked} " +
                    $"R={subsystem.rightHand.isTracked}");
            }
        }
    }
}
