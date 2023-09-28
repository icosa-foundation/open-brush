using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
#if OCULUS_SUPPORTED
        private OVRPassthroughLayer ovrPassthrough;
#endif // OCULUS_SUPPORTED 
        private OVRSceneManager ovrSceneManager;


        void Start()
        {
#if OCULUS_SUPPORTED
            ovrPassthrough = gameObject.AddComponent<OVRPassthroughLayer>();
            ovrPassthrough.overlayType = OVROverlay.OverlayType.Underlay;
#endif // OCULUS_SUPPORTED   
                
            ovrSceneManager = GetComponent<OVRSceneManager>();
            ovrSceneManager.SceneModelLoadedSuccessfully += SceneModelLoaded;
            LoadScene();
        }

// Oculus Methods

        void RequestScenePermission()
        {
            const string permissionString = "com.oculus.permission.USE_SCENE";
            bool hasUserAuthorizedPermission = UnityEngine.Android.Permission.HasUserAuthorizedPermission(permissionString);
            if (!hasUserAuthorizedPermission) {
                UnityEngine.Android.Permission.RequestUserPermission(permissionString);
            }
        }

        void LoadScene()
        {
            ovrSceneManager.LoadSceneModel();
        }

        void SceneModelLoaded()
        {

        }

    }
}

