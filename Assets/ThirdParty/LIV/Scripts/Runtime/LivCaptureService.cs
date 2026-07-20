using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;
using System;

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
#endif

namespace LIV.SDK.Unity
{
    public class LivCaptureService : MonoBehaviour
    {
        private static LivCaptureService _service = null;
        public static LivCaptureService Service {
            get {
                if (!_service)
                {
                    _service = FindObjectOfType<LivCaptureService>();
                }

                return _service;
            }
            private set {
                _service = value;
            }
        }

        /// <summary>
        /// triggered when the LIV SDK is activated by the LIV App and enabled by the game.
        /// </summary>
        public event System.Action onActivate = null;
        /// <summary>
        /// triggered before the Mixed Reality camera is about to render.
        /// </summary>
        public event System.Action<SDKRender> onPreRender = null;
        /// <summary>
        /// triggered before the LIV SDK starts rendering background image.
        /// </summary>
        public event System.Action<SDKRender> onPreRenderBackground = null;
        /// <summary>
        /// triggered after the LIV SDK starts rendering background image.
        /// </summary>
        public event System.Action<SDKRender> onPostRenderBackground = null;
        /// <summary>
        /// triggered before the LIV SDK starts rendering the foreground image.
        /// </summary>
        public event System.Action<SDKRender> onPreRenderForeground = null;
        /// <summary>
        /// triggered after the LIV SDK starts rendering the foreground image.
        /// </summary>
        public event System.Action<SDKRender> onPostRenderForeground = null;
        /// <summary>
        /// triggered after the Mixed Reality camera has finished rendering.
        /// </summary>
        public event System.Action<SDKRender> onPostRender = null;
        /// <summary>
        /// triggered when the LIV SDK passthrough is activated by the LIV App.
        /// </summary>
        public event System.Action onPassthroughActivated = null;
        /// <summary>
        /// triggered when the LIV SDK passthrough is deactivated by the LIV App.
        /// </summary>
        public event System.Action onPassthroughDeactivated = null;
        /// <summary>
        /// triggered when the LIV SDK is deactivated by the LIV App or disabled by the game.
        /// </summary>
        public event System.Action onDeactivate = null;
        /// <summary>
        /// triggered when the Volumetric capture SDK validation fails.
        /// </summary>
        public event Action<ValidationError, string> onValidationError = null;

        [Tooltip("Data needed to setup the LIV Capture.")]
        [SerializeField] private LivDescriptor _descriptor;
        /// <summary>
        /// Data needed to setup the Liv Capture.
        /// </summary>
        public LivDescriptor descriptor {
            get {
                return _descriptor;
            }
            set
            {
                ValidationError validationError;
                string error;
                if (!LivValidation.IsValid(value, out validationError, out error))
                {
                    Debug.LogError(error);
                    if (onValidationError != null)
                        onValidationError.Invoke(validationError, error);

                    // invalid descriptor do nothing
                    return;
                }
                
                // valid descriptor
                _descriptor = value;
                // invalidate sdk
                _wasReady = false;
            }
        }

        bool _isActive = false;
        /// <summary>
        /// Is the LIV SDK currently active.
        /// </summary>
        public bool isActive {
            get {
                return _isActive;
            }
        }

        private bool isReady {
            get {
                ValidationError validationError;
                string error;
                if (!descriptor.IsValid(out validationError, out error))
                {
                    Debug.LogWarning(error);
                    if(onValidationError != null)
                        onValidationError.Invoke(validationError, error);
                    return false;
                }

                SDKBridge.ErrorCode errorCode;
                return _isComponentEnabled && SDKBridge.IsConnected(out errorCode);
            }
        }

        private SDKRender _render = null;

        /// <summary>
        /// Script responsible for the MR rendering.
        /// </summary>
        public SDKRender render { get { return _render; } }

        private bool _wasReady = false;
        // unity component is not marked as disabled immediately when disabled, capturing manually.
        private bool _isComponentEnabled = false;
        private Coroutine _waitForEndOfFrameCoroutine;

        internal SDKBridge.ErrorCode InitializeService()
        {
            // This has been already initialized
            if (Service == this)
            {
                return SDKBridge.ErrorCode.OK;
            }

            // This is not current service, destroy
            if (Service && Service != this)
            {
                Debug.LogWarning("LIV: service has been already created.");
                Destroy(gameObject);
                return SDKBridge.ErrorCode.OK;
            }

            SDKBridge.ErrorCode errorCode = SDKBridge.CreateCaptureProtocol(_descriptor.GetCaptureProtocolType);
            if (errorCode != SDKBridge.ErrorCode.OK)
            {
                Debug.LogError($"LIV: Bridge Protocol initialization failed, {errorCode}");
                return errorCode;
            }

            // Set this as current service
            _service = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log(
              $"LIV: Initialize Service, " +
                    $"version: {LivApi.GetVersion()}\n" +
                    $"capture protocol: {_descriptor.GetCaptureProtocolType}\n" +
                    $"rendering backend: {LivApi.RenderingBackend()}\n" +
                    $"tracking ID: {descriptor.trackingID}\n"
              );
            return SDKBridge.ErrorCode.OK;
        }

        void OnEnable()
        {
            _isComponentEnabled = true;
            UpdateSDKReady();
        }

        void Update()
        {
            UpdateSDKReady();
        }

        void OnDisable()
        {
            _isComponentEnabled = false;
            UpdateSDKReady();
        }

        private void OnDestroy()
        {
            if (_service != this)
                return;

            _service = null;
            SDKBridge.DestroyCaptureProtocol();
            Debug.Log("LIV: Destroy Service");
        }

        private IEnumerator WaitForUnityEndOfFrame()
        {
            while (Application.isPlaying && _isComponentEnabled)
            {
                yield return new WaitForEndOfFrame();
                if (isActive)
                {
                    _render.Render();
                }
            }
        }

        private void UpdateSDKReady()
        {
            bool ready = isReady;
            if (ready != _wasReady)
            {
                OnSDKReadyChanged(ready);
                _wasReady = ready;
            }
        }

        private void OnSDKReadyChanged(bool value)
        {
            if (value)
            {
                OnSDKActivate();
            }
            else
            {
                OnSDKDeactivate();
            }
        }

        private void OnSDKActivate()
        {
            Debug.Log("LIV: Service connected, setting up Mixed Reality!");
            SubmitApplicationOutput(descriptor.trackingID);
            CreateAssets();
            StartRenderCoroutine();
            _isActive = true;
            if (onActivate != null) 
                onActivate.Invoke();
        }

        private void OnSDKDeactivate()
        {
            Debug.Log("LIV: Service disconnected, cleaning up Mixed Reality.");
            if (onDeactivate != null) 
                onDeactivate.Invoke();
            StopRenderCoroutine();
            DestroyAssets();
            _isActive = false;
        }

        private void CreateAssets()
        {
            DestroyAssets();
            _render = new SDKRender(_descriptor);
            _render.onPreRender += OnLivCameraPreRender;
            _render.onPreRenderBackground += OnLivCameraPreRenderBackground;
            _render.onPostRenderBackground += OnLivCameraPostRenderBackground;
            _render.onPreRenderForeground += OnLivCameraPreRenderForeground;
            _render.onPostRenderForeground += OnLivCameraPostRenderForeground;
            _render.onPostRender += OnLivCameraPostRender;
            _render.onPassthroughActivated += OnPassthroughActivated;
            _render.onPassthroughDeactivated += OnPassthroughDeactivated;
        }

        private void DestroyAssets()
        {
            if (_render != null)
            {
                _render.Dispose();
                _render.onPreRender -= OnLivCameraPreRender;
                _render.onPreRenderBackground -= OnLivCameraPreRenderBackground;
                _render.onPostRenderBackground -= OnLivCameraPostRenderBackground;
                _render.onPreRenderForeground -= OnLivCameraPreRenderForeground;
                _render.onPostRenderForeground -= OnLivCameraPostRenderForeground;
                _render.onPostRender -= OnLivCameraPostRender;
                _render.onPassthroughActivated -= OnPassthroughActivated;
                _render.onPassthroughDeactivated -= OnPassthroughDeactivated;
                _render = null;
            }
        }

        private void StartRenderCoroutine()
        {
            StopRenderCoroutine();
            _waitForEndOfFrameCoroutine = StartCoroutine(WaitForUnityEndOfFrame());
        }

        private void StopRenderCoroutine()
        {
            if (_waitForEndOfFrameCoroutine != null)
            {
                StopCoroutine(_waitForEndOfFrameCoroutine);
                _waitForEndOfFrameCoroutine = null;
            }
        }

        private static void SubmitApplicationOutput(string trackingID)
        {
            SDKApplicationOutput output = SDKApplicationOutput.empty;
            output.supportedFeatures = FEATURES.BACKGROUND_RENDER |
                                        FEATURES.FOREGROUND_RENDER |
                                        FEATURES.OVERRIDE_POST_PROCESSING |
                                        FEATURES.FIX_FOREGROUND_ALPHA;

            output.sdkID = trackingID;
            output.sdkVersion = SDKConstants.SDK_VERSION;
            output.engineName = SDKConstants.ENGINE_NAME;
            output.engineVersion = Application.unityVersion;
            output.applicationName = Application.productName;
            output.applicationVersion = Application.version;
            output.graphicsAPI = SystemInfo.graphicsDeviceType.ToString();
#if UNITY_2017_2_OR_NEWER
            output.xrDeviceName = XRSettings.loadedDeviceName;
#endif
            SDKBridge.SubmitApplicationOutput(output);
        }

        private void OnLivCameraPreRender(SDKRender obiRenderer)
        {
            if (onPreRender != null) 
                onPreRender.Invoke(obiRenderer);
        }

        private void OnLivCameraPreRenderBackground(SDKRender obiRenderer)
        {
            if (onPreRenderBackground != null) 
                onPreRenderBackground.Invoke(obiRenderer);
        }

        private void OnLivCameraPostRenderBackground(SDKRender obiRenderer)
        {
            if (onPostRenderBackground != null) 
                onPostRenderBackground.Invoke(obiRenderer);
        }

        private void OnLivCameraPreRenderForeground(SDKRender obiRenderer)
        {
            if (onPreRenderForeground != null) 
                onPreRenderForeground.Invoke(obiRenderer);
        }

        private void OnLivCameraPostRenderForeground(SDKRender obiRenderer)
        {
            if (onPostRenderForeground != null) 
                onPostRenderForeground.Invoke(obiRenderer);
        }

        private void OnLivCameraPostRender(SDKRender obiRenderer)
        {
            if (onPostRender != null) 
                onPostRender.Invoke(obiRenderer);
        }

        private void OnPassthroughActivated()
        {
            if (onPassthroughActivated != null) 
                onPassthroughActivated.Invoke();
        }

        private void OnPassthroughDeactivated()
        {
            if (onPassthroughDeactivated != null) 
                onPassthroughDeactivated.Invoke();
        }
    }
}