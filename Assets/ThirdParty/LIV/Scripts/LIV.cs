using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace LIV.SDK.Unity
{
    [HelpURL("https://liv.tv/sdk-unity-docs")]
    [AddComponentMenu("LIV/LIV")]
    public class LIV : MonoBehaviour
    {
        /// <summary>
        /// triggered when the LIV SDK is activated by the LIV App and enabled by the game.
        /// </summary>
        public System.Action onActivate = null;
        /// <summary>
        /// triggered before the Mixed Reality camera is about to render.
        /// </summary>
        public System.Action<SDKRender> onPreRender = null;
        /// <summary>
        /// triggered before the LIV SDK starts rendering background image.
        /// </summary>
        public System.Action<SDKRender> onPreRenderBackground = null;
        /// <summary>
        /// triggered after the LIV SDK starts rendering background image.
        /// </summary>
        public System.Action<SDKRender> onPostRenderBackground = null;
        /// <summary>
        /// triggered before the LIV SDK starts rendering the foreground image.
        /// </summary>
        public System.Action<SDKRender> onPreRenderForeground = null;
        /// <summary>
        /// triggered after the LIV SDK starts rendering the foreground image.
        /// </summary>
        public System.Action<SDKRender> onPostRenderForeground = null;
        /// <summary>
        /// triggered after the Mixed Reality camera has finished rendering.
        /// </summary>
        public System.Action<SDKRender> onPostRender = null;
        /// <summary>
        /// triggered when the LIV SDK passthrough is activated by the LIV App.
        /// </summary>
        public System.Action onPassthroughActivated = null;
        /// <summary>
        /// triggered when the LIV SDK passthrough is deactivated by the LIV App.
        /// </summary>
        public System.Action onPassthroughDeactivated = null;
        /// <summary>
        /// triggered when the LIV SDK is deactivated by the LIV App or disabled by the game.
        /// </summary>
        public System.Action onDeactivate = null;
        /// <summary>
        /// triggered when the Volumetric capture SDK validation fails.
        /// </summary>
        public event Action<ValidationError, string> onValidationError = null;

        /// <summary>
        /// Tracking ID that identifies your game to the LIV backend, allowing you to get usage analytics
        /// </summary>
        public string trackingID {
            get {
                return SDKSettings.instance.trackingID;
            }
        }

        /// <summary>
        /// This is the topmost transform of your VR rig.
        /// </summary>
        /// <remarks>
        /// <para>When implementing VR locomotion(teleporting, joystick, etc),</para>
        /// <para>this is the GameObject that you should move around your scene.</para>
        /// <para>It represents the centre of the user’s playspace.</para>
        /// </remarks>
        [Tooltip("Topmost transform of your XR rig.")]
        [FormerlySerializedAs("TrackedSpaceOrigin")]
        [SerializeField] private Transform _stage = null;
        public Transform stage {
            get {
                return _stage;
            }
            set {
                _stage = value;
                CreateOrUpdateService();
            }
        }

        [Tooltip("This transform is an additional wrapper to the user’s playspace.")]
        [FormerlySerializedAs("StageTransform")]
        [SerializeField] Transform _stageTransform = null;

        /// <summary>
        /// This transform is an additional wrapper to the user’s playspace.
        /// </summary>
        /// <remarks>
        /// <para>It allows for user-controlled transformations for special camera effects & transitions.</para>
        /// <para>If a creator is using a static camera, this transformation can give the illusion of camera movement.</para>
        /// </remarks>
        public Transform stageTransform {
            get {
                return _stageTransform;
            }
            set {
                _stageTransform = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// This is the camera responsible for rendering the user’s HMD.
        /// </summary>
        /// <remarks>
        /// <para>The LIV SDK, by default clones this object to match your application’s rendering setup.</para>
        /// <para>You can use your own camera prefab should you want to!</para>
        /// </remarks>
        [Tooltip("Camera responsible for rendering the user’s HMD.")]
        [FormerlySerializedAs("HMDCamera")]
        [SerializeField] private Camera _HMDCamera = null;
        public Camera HMDCamera {
            get {
                return _HMDCamera;
            }
            set {
                _HMDCamera = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// Camera prefab for customized rendering.
        /// </summary>
        /// <remarks>
        /// <para>By default, LIV uses the HMD camera as a reference for the Mixed Reality camera.</para>
        /// <para>It is cloned and set up as a Mixed Reality camera.This approach works for most apps.</para>
        /// <para>However, some games can experience issues because of custom MonoBehaviours attached to this camera.</para>
        /// <para>You can use a custom camera prefab for those cases.</para>
        /// </remarks>
        [Tooltip("Camera prefab for customized rendering.")]
        [FormerlySerializedAs("MRCameraPrefab")]
        [SerializeField] private Camera _MRCameraPrefab = null;
        public Camera MRCameraPrefab {
            get {
                return _MRCameraPrefab;
            }
            set {
                _MRCameraPrefab = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// This option disables all standard Unity assets for the Mixed Reality rendering.
        /// </summary>
        /// <remarks>
        /// <para>Unity’s standard assets can interfere with the alpha channel that LIV needs to composite MR correctly.</para>
        /// </remarks>
        [Tooltip("This option disables all standard Unity assets for the Mixed Reality rendering.")]
        [FormerlySerializedAs("DisableStandardAssets")]
        [SerializeField] bool _disableStandardAssets = false;
        public bool disableStandardAssets {
            get {
                return _disableStandardAssets;
            }
            set {
                _disableStandardAssets = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// The layer mask defines exactly which object layers should be rendered in MR.
        /// </summary>
        /// <remarks>
        /// <para>You should use this to hide any in-game avatar that you’re using.</para>
        /// <para>LIV is meant to include the user’s body for you!</para>
        /// <para>Certain HMD-based effects should be disabled here too.</para>
        /// <para>Also, this can be used to render special effects or additional UI only to the MR camera.</para>
        /// <para>Useful for showing the player’s health, or current score!</para>
        /// </remarks>
        [Tooltip("The layer mask defines exactly which object layers should be rendered.")]
        [FormerlySerializedAs("SpectatorLayerMask")]
        [SerializeField] LayerMask _spectatorLayerMask = ~0;
        public LayerMask spectatorLayerMask {
            get {
                return _spectatorLayerMask;
            }
            set {
                _spectatorLayerMask = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// The layer mask works exactly as spectatorLayerMask but is activated only when passtrough MR
        /// is enabled in the LIV APP. Disable layers which contain background rendering in order to
        /// make passthrough rendering possible.
        /// </summary>
        /// <remarks>
        /// <para>Passthrough simulates augmented reality effect.</para>
        /// <para>Disable background rendering in order so LIV can replace it with camera footage.</para>
        /// <para>Certain HMD-based effects should be disabled here too.</para>
        /// </remarks>
        [Tooltip("The layer mask defines exactly which object layers should be rendered in passthrough MR.")]
        [SerializeField] private LayerMask _passthroughLayerMask = ~0;
        public LayerMask passthroughLayerMask {
            get {
                return _passthroughLayerMask;
            }
            set {
                _passthroughLayerMask = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// This is for removing unwanted scripts from the cloned MR camera.
        /// </summary>
        /// <remarks>
        /// <para>By default, we remove the AudioListener, Colliders and SteamVR scripts, as these are not necessary for rendering MR!</para>
        /// <para>The excluded string must match the name of the MonoBehaviour.</para>
        /// </remarks>
        [Tooltip("List for removing unwanted scripts from the cloned camera.")]
        [FormerlySerializedAs("ExcludeBehaviours")]
        [SerializeField]
        private string[] _excludeBehaviours = LivDescriptor.GetDefaultExcludeBehaviours();

        public string[] excludeBehaviours {
            get {
                return _excludeBehaviours;
            }
            set {
                _excludeBehaviours = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// Recovers corrupted alpha channel when using post-effects.
        /// </summary>
        ///
        [Tooltip("Recovers corrupted alpha channel when using post-effects.")]
        [FormerlySerializedAs("FixPostEffectsAlpha")]
        [SerializeField]
        private bool _fixPostEffectsAlpha = true;
        public bool fixPostEffectsAlpha {
            get {
                return _fixPostEffectsAlpha;
            }
            set {
                _fixPostEffectsAlpha = value;
                CreateOrUpdateService();
            }
        }

        /// <summary>
        /// Overrides alpha using depth buffer from opaque render pass, enable only when opaque render pass has corrupted alpha channel.
        /// </summary>
        [Tooltip("Overrides alpha using depth buffer from opaque render pass, enable only when opaque render pass has corrupted alpha channel.")]
        [SerializeField]
        private bool _overrideAlphaFromDepthBuffer = false;

        public bool overrideAlphaFromDepthBuffer
        {
            get
            {
                return _overrideAlphaFromDepthBuffer;
            }
            set
            {
                _overrideAlphaFromDepthBuffer = value;
                CreateOrUpdateService();
            }
        }

        public bool isActive {
            get {
                if (LivCaptureService.Service == null)
                    return false;

                return LivCaptureService.Service.isActive;
            }
        }

        public SDKRender render {
            get {
                if (LivCaptureService.Service == null)
                    return null;

                return LivCaptureService.Service.render;
            }
        }

        private void CreateOrUpdateService()
        {
            LivDescriptor livDescriptor = new LivDescriptor
            {
                trackingID = trackingID,
                stage = stage,
                stageTransform = stageTransform,
                HMDCamera = HMDCamera,
                cameraPrefab = MRCameraPrefab,
                disableStandardAssets = disableStandardAssets,
                spectatorLayerMask = spectatorLayerMask,
                passthroughLayerMask = passthroughLayerMask,
                excludeBehaviours = excludeBehaviours,
                fixPostEffectsAlpha = fixPostEffectsAlpha,
                overrideAlphaWithDepthBuffer = overrideAlphaFromDepthBuffer
            };

            // Update descriptor if service exists
            if (LivCaptureService.Service)
            {
                LivCaptureService.Service.descriptor = livDescriptor;
            }
            else
            {
                // create service
                var livResult = LivApi.CreateService(livDescriptor);
                if (!livResult.isOk) Debug.LogWarning(livResult.message);
            }
        }

        private void Awake()
        {
            CreateOrUpdateService();
        }

        private void OnEnable()
        {
            LivCaptureService service = LivCaptureService.Service;
            if (service == null)
                return;

            service.onActivate += OnLivActivate;
            service.onPreRender += OnLivPreRender;
            service.onPreRenderBackground += OnLivPreRenderBackground;
            service.onPostRenderBackground += OnLivPostRenderBackground;
            service.onPreRenderForeground += OnLivPreRenderForeground;
            service.onPostRenderForeground += OnLivPostRenderForeground;
            service.onPostRender += OnLivPostRender;
            service.onPassthroughActivated += OnLivPassthroughActivated;
            service.onPassthroughDeactivated += OnLivPassthroughDeactivated;
            service.onDeactivate += OnLivDeactivate;
            service.onValidationError += OnLivValidationError;
        }

        private void OnDisable()
        {
            LivCaptureService service = LivCaptureService.Service;
            if (service == null)
                return;

            service.onActivate -= OnLivActivate;
            service.onPreRender -= OnLivPreRender;
            service.onPreRenderBackground -= OnLivPreRenderBackground;
            service.onPostRenderBackground -= OnLivPostRenderBackground;
            service.onPreRenderForeground -= OnLivPreRenderForeground;
            service.onPostRenderForeground -= OnLivPostRenderForeground;
            service.onPostRender -= OnLivPostRender;
            service.onPassthroughActivated -= OnLivPassthroughActivated;
            service.onPassthroughDeactivated -= OnLivPassthroughDeactivated;
            service.onDeactivate -= OnLivDeactivate;
            service.onValidationError -= OnLivValidationError;
        }

        private void OnLivActivate()
        {
            if (onActivate != null)
                onActivate.Invoke();
        }

        private void OnLivPreRender(SDKRender livRenderer)
        {
            if (onPreRender != null)
                onPreRender.Invoke(livRenderer);
        }

        private void OnLivPreRenderBackground(SDKRender livRenderer)
        {
            if (onPreRenderBackground != null)
                onPreRenderBackground.Invoke(livRenderer);
        }

        private void OnLivPostRenderBackground(SDKRender livRenderer)
        {
            if (onPostRenderBackground != null)
                onPostRenderBackground.Invoke(livRenderer);
        }

        private void OnLivPreRenderForeground(SDKRender livRenderer)
        {
            if (onPreRenderForeground != null)
                onPreRenderForeground.Invoke(livRenderer);
        }

        private void OnLivPostRenderForeground(SDKRender livRenderer)
        {
            if (onPostRenderForeground != null)
                onPostRenderForeground.Invoke(livRenderer);
        }

        private void OnLivPostRender(SDKRender livRenderer)
        {
            if (onPostRender != null)
                onPostRender.Invoke(livRenderer);
        }

        private void OnLivPassthroughActivated()
        {
            if (onPassthroughActivated != null)
                onPassthroughActivated.Invoke();
        }

        private void OnLivPassthroughDeactivated()
        {
            if (onPassthroughDeactivated != null)
                onPassthroughDeactivated.Invoke();
        }

        private void OnLivDeactivate()
        {
            if (onDeactivate != null)
                onDeactivate.Invoke();
        }

        private void OnLivValidationError(ValidationError validationError, string validationErrorMessage)
        {
            if (onValidationError != null)
                onValidationError(validationError, validationErrorMessage);
        }
    }
}