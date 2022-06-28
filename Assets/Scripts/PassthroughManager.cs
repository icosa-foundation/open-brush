using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
        void Start()
        {
#if OCULUS_SUPPORTED
                var passthrough = gameObject.AddComponent<OVRPassthroughLayer>();
                passthrough.overlayType = OVROverlay.OverlayType.Underlay;
#endif // OCULUS_SUPPORTED   
        }
    }
}

