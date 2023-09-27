using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
#if OCULUS_SUPPORTED
        private OVRPassthroughLayer ovrPassthrough;
        private OVRSceneManager ovrSceneManager;
#endif // OCULUS_SUPPORTED

        void Start()
        {
#if OCULUS_SUPPORTED
            ovrPassthrough = gameObject.AddComponent<OVRPassthroughLayer>();
            ovrPassthrough.overlayType = OVROverlay.OverlayType.Underlay;
                
            ovrSceneManager = gameObject.AddComponent<OVRSceneManager>();
#endif // OCULUS_SUPPORTED   
        }

// Oculus Methods
#if OCULUS_SUPPORTED
        void RequestScenePermission()
        {
            const string permissionString = "com.oculus.permission.USE_SCENE";
            bool hasUserAuthorizedPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(permissionString);
            if (!hasUserAuthorizedPermission) {
                UnityEngine.Android.Permission.RequestUserPermission(permissionString);
            }
        }
#endif // OCULUS_SUPPORTED
    }
}

