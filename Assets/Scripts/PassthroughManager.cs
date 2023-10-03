using UnityEngine;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
        public bool isHost;
#if OCULUS_SUPPORTED
        private OVRPassthroughLayer ovrPassthrough;
#endif // OCULUS_SUPPORTED 

        void Start()
        {
#if OCULUS_SUPPORTED
            ovrPassthrough = gameObject.AddComponent<OVRPassthroughLayer>();
            ovrPassthrough.overlayType = OVROverlay.OverlayType.Underlay;
#endif // OCULUS_SUPPORTED  
            OculusMRController.m_Instance.StartMRExperience(isHost);
        }
    }
}

