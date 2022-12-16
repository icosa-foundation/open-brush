using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using Wave.Native;

namespace TiltBrush
{
    public class PassthroughManager : MonoBehaviour
    {
        private Color? recOriginalColor = null;
        private bool enablePassthrough = false;
        private float time = 0.5f;

        void Start()
        {
#if OCULUS_SUPPORTED
                var passthrough = gameObject.AddComponent<OVRPassthroughLayer>();
                passthrough.overlayType = OVROverlay.OverlayType.Underlay;
#endif
                enablePassthrough = true;
        }

        private void OnEnable()
        {
            enablePassthrough = true;
        }

        private void OnDisable()
        {
            DisablePassThrough();
        }

        private void OnApplicationPause(bool pause)
        {
            if (!pause)
            {
                EnablePassThrough();
            }
        }

        private void Update()
        {
            if (enablePassthrough)
            {
                if (time >= 0)
                {
                    time -= Time.deltaTime;
                }
                else
                {
                    time = 0.5f;
                    enablePassthrough = false;
                    EnablePassThrough();
                }
            }
        }

        private bool WVR_enablePassthrough(bool enable)
        {
            Debug.Log("[PassthroughManager] WVR_enablePassthrough(): " + enable);
            if (enable)
            {
                WVR_Result result = Interop.WVR_ShowPassthroughUnderlay(true);
                if (result != WVR_Result.WVR_Success)
                    Debug.Log("[PassthroughManager] WVR_ShowPassthroughUnderlay(true): fail");
                return result == WVR_Result.WVR_Success;
            }
            else
            {
                WVR_Result result = Interop.WVR_ShowPassthroughUnderlay(false);
                if (result != WVR_Result.WVR_Success)
                    Debug.Log("[PassthroughManager] WVR_ShowPassthroughUnderlay(false): fail");
            }

            return false;
        }

        public bool EnablePassThrough()
        {
            Debug.Log("[PassthroughManager] EnablePassThrough");
            Camera.main.clearFlags = CameraClearFlags.SolidColor;

            if (recOriginalColor == null)
                recOriginalColor = Camera.main.backgroundColor;

            Camera.main.backgroundColor = Color.white * 0;

            return WVR_enablePassthrough(true);
        }

        public void DisablePassThrough()
        {
            Debug.Log("[PassthroughManager] DisablePassThrough");
            Camera.main.clearFlags = CameraClearFlags.Skybox;

            if (recOriginalColor.HasValue)
                Camera.main.backgroundColor = recOriginalColor.Value;
            else
                Camera.main.backgroundColor = new Color(49f / 255f, 77f / 255f, 121f / 255f, 5f / 255f);

            WVR_enablePassthrough(false);
        }
    }
}

