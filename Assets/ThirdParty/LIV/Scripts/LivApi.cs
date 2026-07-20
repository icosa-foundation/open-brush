using UnityEngine;

namespace LIV.SDK.Unity
{
    public static class LivApi
    {
        public static LivResult<LivCaptureService, ValidationError> CreateService(LivDescriptor descriptor, bool setActive = true)
        {
            if (LivCaptureService.Service)
            {
                // Service already exists, update only descriptor
                LivCaptureService.Service.descriptor = descriptor;
                Debug.LogWarning("LIV: service already created.");
                return LivResult<LivCaptureService, ValidationError>.Ok(LivCaptureService.Service);
            }

            return BuildService(descriptor, setActive);
        }

        public static string GetVersion()
        {
            return SDKConstants.SDK_VERSION;
        }

        public static string RenderingBackend()
        {
#if LIV_UNIVERSAL_RENDER
          return "urp";
#else
          return "legacy";
#endif
        }

        public static void DestroyService()
        {
            if (!LivCaptureService.Service)
                return;

            GameObject.Destroy(LivCaptureService.Service.gameObject);
        }

        internal static LivResult<LivCaptureService, ValidationError> BuildService(LivDescriptor descriptor, bool setActive = true)
        {
#if (UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN) && UNITY_64
            ValidationError validationOutput;
            string errorMessage;
            if (!LivValidation.IsValid(descriptor, out validationOutput, out errorMessage))
            {
                Debug.LogError(errorMessage);
                return LivResult<LivCaptureService, ValidationError>.Error(validationOutput, errorMessage);
            }

            Debug.Log($"LIV: Setting up mixed reality based on {descriptor.HMDCamera.name}");

            var livGameObject = new GameObject("LIV");
            livGameObject.gameObject.SetActive(false);

            var livCaptureService = livGameObject.AddComponent<LivCaptureService>();
            livCaptureService.descriptor = descriptor;
            SDKBridge.ErrorCode initializationErrorCode = livCaptureService.InitializeService();
            if (initializationErrorCode != SDKBridge.ErrorCode.OK)
            {
                GameObject.Destroy(livGameObject);
                return LivResult<LivCaptureService, ValidationError>.Error(ValidationError.INITIALIZATION_FAILED,
                    initializationErrorCode.ToString());
            }

            livGameObject.gameObject.SetActive(setActive);

            Debug.Log($"LIV: instance created successfully with stage: {descriptor.stage.name}");
            return LivResult<LivCaptureService, ValidationError>.Ok(livCaptureService);
#else
            return LivResult<LivCaptureService, ValidationError>.Error(ValidationError.INVALID_PLATFORM, $"LIV: Invalid build platform selected, please target Windows x64 to use LIV.");
#endif
        }
    }
}