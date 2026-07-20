using System;
using UnityEngine;

namespace LIV.SDK.Unity
{
    public enum ValidationError
    {
        OK = 0,
        MISSING_HMD,
        EMPTY_LAYER_MASK,
        MISSING_STAGE,
        INVALID_TRACKING_ID,
        INITIALIZATION_FAILED,
        INVALID_PLATFORM
    }

    public static class LivValidation
    {
        public const string ERROR_MESSAGE_MISSING_HMD = "LIV: HMD Camera is a required parameter!";
        public const string ERROR_MESSAGE_EMPTY_LAYER_MASK = "LIV: The spectator layer mask is set empty. Is this correct?";
        public const string ERROR_MESSAGE_MISSING_STAGE = "LIV: Tracked space origin is a required parameter!";
        public const string ERROR_MESSAGE_INVALID_TRACKING_ID = "LIV: Tracking id is a required parameter!";

        /// <summary>
        /// Is the curret LIV SDK setup valid.
        /// </summary>
        public static bool IsValid(this LivDescriptor descriptor, out ValidationError validationError, out string error)
        {
            if (descriptor.HMDCamera == null)
            {
                validationError = ValidationError.MISSING_HMD;
                error = ERROR_MESSAGE_MISSING_HMD;
                return false;
            }

            if (descriptor.spectatorLayerMask == 0)
            {
                validationError = ValidationError.EMPTY_LAYER_MASK;
                error = ERROR_MESSAGE_EMPTY_LAYER_MASK;
                return false;
            }

            if (descriptor.stage == null)
            {
                validationError = ValidationError.MISSING_STAGE;
                error = ERROR_MESSAGE_MISSING_STAGE;
                return false;
            }

            if (string.IsNullOrEmpty(descriptor.trackingID))
            {
                validationError = ValidationError.INVALID_TRACKING_ID;
                error = ERROR_MESSAGE_INVALID_TRACKING_ID;
                return false;
            }

            error = null;
            validationError = ValidationError.OK;
            return true;
        }
    }

    public struct LivDescriptor
    {
        public string trackingID;
        public Transform stage;
        public Transform stageTransform;
        public Camera HMDCamera;
        public Camera cameraPrefab;
        public bool disableStandardAssets;
        public LayerMask spectatorLayerMask;
        public LayerMask passthroughLayerMask;
        public string[] excludeBehaviours;
        public bool fixPostEffectsAlpha;
        public bool overrideAlphaWithDepthBuffer;

        internal SDKBridge.CaptureProtocolType GetCaptureProtocolType
        {
            get
            {
                return SDKSettings.instance.captureProtocolType;
            }
        }

        public LivDescriptor(string trackingID, Transform stage, Transform stageTransform, Camera HMDCamera, Camera cameraPrefab, bool disableStandardAssets, LayerMask spectatorLayerMask, LayerMask passthroughLayerMask, string[] excludeBehaviours, bool fixPostEffectsAlpha, bool overrideAlphaWithDepthBuffer)
        {
            this.trackingID = trackingID;
            this.stage = stage;
            this.stageTransform = stageTransform;
            this.HMDCamera = HMDCamera;
            this.cameraPrefab = cameraPrefab;
            this.disableStandardAssets = disableStandardAssets;
            this.spectatorLayerMask = spectatorLayerMask;
            this.passthroughLayerMask = passthroughLayerMask;
            this.excludeBehaviours = excludeBehaviours;
            this.fixPostEffectsAlpha = fixPostEffectsAlpha;
            this.overrideAlphaWithDepthBuffer = overrideAlphaWithDepthBuffer;
        }

        public static string[] GetDefaultExcludeBehaviours()
        {
            return new string[]
            {
                typeof(AudioListener).Name,
                typeof(Collider).Name,
                "SteamVR_Camera",
                "SteamVR_Fade",
                "SteamVR_ExternalCamera"
            };
        }

        public Camera cameraReference
        {
            get
            {
                return cameraPrefab == null
                    ? HMDCamera
                    : cameraPrefab;
            }
        }

        public Matrix4x4 stageLocalToWorldMatrix
        {
            get
            {
                return stage == null
                    ? Matrix4x4.identity
                    : stage.localToWorldMatrix;
            }
        }

        public Matrix4x4 stageWorldToLocalMatrix
        {
            get
            {
                return stage == null
                    ? Matrix4x4.identity
                    : stage.worldToLocalMatrix;
            }
        }
    }
}