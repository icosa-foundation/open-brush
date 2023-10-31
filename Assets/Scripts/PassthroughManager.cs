using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if PICO_SUPPORTED
using Unity.XR.PXR;
#endif


namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
        void Start()
        {
#if OCULUS_SUPPORTED
                var passthrough = gameObject.AddComponent<OVRPassthroughLayer>();
                passthrough.overlayType = OVROverlay.OverlayType.Underlay;
#elif PICO_SUPPORTED
                PXR_Plugin.Boundary.UPxr_ShutdownSdkGuardianSystem();
                PXR_Boundary.EnableSeeThroughManual(true);
#else

#endif
        }
    }
}

