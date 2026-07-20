using UnityEngine;
using UnityEngine.Rendering;

namespace LIV.SDK.Unity
{
    public class CaptureCameraSettings
    {
        private CameraClearFlags _cameraClearFlags;
        private Color _cameraBackgroundColor;

        public void Capture(Camera camera)
        {
            _cameraClearFlags = camera.clearFlags;
            _cameraBackgroundColor = camera.backgroundColor;
        }

        public void Release(Camera camera)
        {
            camera.clearFlags = _cameraClearFlags;
            camera.backgroundColor = _cameraBackgroundColor;
        }
    }

    public partial class SDKRender : System.IDisposable
    {
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

        private LivDescriptor _livDescriptor;
        public LivDescriptor livDescriptor
        {
            get
            {
                return _livDescriptor;
            }
        }

        // quad
        private Mesh _quadMesh = null;
        // Tessellated quad
        private Mesh _clipPlaneMesh = null;
        // box
        private Mesh _boxMesh = null;
        // debug font
        private SDKFont _sdkFont = null;

        private MaterialPropertyBlock _clipPlaneMaterialProperty;
        private MaterialPropertyBlock _groundPlaneMaterialProperty;
        private MaterialPropertyBlock _hmdMaterialProperty;

        private SDKOutputFrame _outputFrame = SDKOutputFrame.empty;
        public SDKOutputFrame outputFrame
        {
            get
            {
                return _outputFrame;
            }
        }

        private SDKInputFrame _lastInputFrame = SDKInputFrame.empty;
        private SDKInputFrame _inputFrame = SDKInputFrame.empty;
        public SDKInputFrame inputFrame
        {
            get
            {
                return _inputFrame;
            }
        }

        private SDKResolution _resolution = SDKResolution.zero;
        public SDKResolution resolution
        {
            get
            {
                return _resolution;
            }
        }

        private Camera _cameraInstance = null;
        public Camera cameraInstance
        {
            get
            {
                return _cameraInstance;
            }
        }

        public Camera cameraReference
        {
            get
            {
                return _livDescriptor.cameraPrefab == null ? _livDescriptor.HMDCamera : _livDescriptor.cameraPrefab;
            }
        }

        public Camera hmdCamera
        {
            get
            {
                return _livDescriptor.HMDCamera;
            }
        }

        public Transform stage
        {
            get
            {
                return _livDescriptor.stage;
            }
        }

        public Transform stageTransform
        {
            get
            {
                return _livDescriptor.stageTransform;
            }
        }

        public Matrix4x4 stageLocalToWorldMatrix
        {
            get
            {
                return _livDescriptor.stage == null ? Matrix4x4.identity : _livDescriptor.stage.localToWorldMatrix;
            }
        }

        public Matrix4x4 localToWorldMatrix
        {
            get
            {
                return _livDescriptor.stageTransform == null ? stageLocalToWorldMatrix : _livDescriptor.stageTransform.localToWorldMatrix;
            }
        }

        public bool disableStandardAssets
        {
            get
            {
                return _livDescriptor.disableStandardAssets;
            }
        }

        public LayerMask renderingLayerMask
        {
            get
            {
                if (isPassthroughEnabled)
                    return _livDescriptor.passthroughLayerMask;
                return _livDescriptor.spectatorLayerMask;
            }
        }

        public bool isPassthroughEnabled
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.ENABLE_PASSTHROUGH);
            }
        }

        /// <summary>
        /// Is LIV Avatar mode enabled and avatar toggled on?
        /// </summary>
        public bool IsAvatarEnabled
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.AVATAR_ENABLED);
            }
        }

        bool useDeferredRendering
        {
            get
            {
                if (_cameraInstance == null)
                    return false;

                return _cameraInstance.actualRenderingPath == RenderingPath.DeferredLighting ||
                       _cameraInstance.actualRenderingPath == RenderingPath.DeferredShading;
            }
        }

        bool interlacedRendering
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.INTERLACED_RENDER);
            }
        }

        bool canRenderBackground
        {
            get
            {
                if (interlacedRendering)
                {
                    // Render only if frame is even 
                    if (Time.frameCount % 2 != 0) return false;
                }
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.BACKGROUND_RENDER) && _backgroundRenderTexture != null;
            }
        }

        bool canRenderForeground
        {
            get
            {
                if (interlacedRendering)
                {
                    // Render only if frame is odd 
                    if (Time.frameCount % 2 != 1) return false;
                }
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FOREGROUND_RENDER) && _foregroundRenderTexture != null;
            }
        }

        bool canRenderOptimized
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OPTIMIZED_RENDER) && _optimizedRenderTexture != null; ;
            }
        }

        bool debugClipPlane
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            }
        }

        bool renderComplexClipPlane
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE);
            }
        }

        bool renderGroundClipPlane
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.GROUND_CLIP_PLANE);
            }
        }

        bool overridePostProcessing
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OVERRIDE_POST_PROCESSING);
            }
        }

        bool fixPostEffectsAlpha
        {
            get
            {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FIX_FOREGROUND_ALPHA) |
                       _livDescriptor.fixPostEffectsAlpha;
            }
        }

        private SDKPose _requestedPose = SDKPose.empty;
        private int _requestedPoseFrameIndex = 0;

        /// <summary>
        /// Detect if the game can actually change the pose during this frame.
        /// </summary>
        /// <remarks>
        /// <para>Because other applications can take over the pose, the game has to know if it can take over the pose or not.</para>        
        /// </remarks>
        /// <example>
        /// <code>
        /// public class CanControlCameraPose : MonoBehaviour
        /// {
        ///     [SerializeField] LIV.SDK.Unity.LIV _liv;
        ///
        ///     private void Update()
        ///     {
        ///         if(_liv.isActive) 
        ///         {
        ///             Debug.Log(_liv.render.canSetPose);
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool canSetPose
        {
            get
            {
                if (_inputFrame.frameid == 0) return false;
                return _inputFrame.priority.pose <= (sbyte)PRIORITY.GAME;
            }
        }

        /// <summary>
        /// Control camera pose by calling this method each frame. The pose is released when you stop calling it.
        /// </summary>
        /// <remarks>
        /// <para>By default the pose is set in worldspace, turn on local space for using the stage relative space instead.</para>        
        /// </remarks>
        /// <example>
        /// <code>
        /// public class ControlCameraPose : MonoBehaviour
        /// {
        ///     [SerializeField] LIV.SDK.Unity.LIV _liv;
        ///     [SerializeField] float _fov = 60f;
        ///
        ///     private void Update()
        ///     {
        ///         if(_liv.isActive) 
        ///         {
        ///             _liv.render.SetPose(transform.position, transform.rotation, _fov);
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public bool SetPose(Vector3 position, Quaternion rotation, float verticalFieldOfView = 60f, bool useLocalSpace = false)
        {
            if (_inputFrame.frameid == 0) return false;
            SDKPose inputPose = _inputFrame.pose;
            float aspect = 1f;
            if (_resolution.height > 0)
            {
                aspect = (float)_resolution.width / (float)_resolution.height;
            }

            if (!useLocalSpace)
            {
                Matrix4x4 worldToLocal = Matrix4x4.identity;
                Transform localTransform = stageTransform == null ? stage : stageTransform;
                if (localTransform != null) worldToLocal = localTransform.worldToLocalMatrix;
                position = worldToLocal.MultiplyPoint(position);
                rotation = SDKUtils.RotateQuaternionByMatrix(worldToLocal, rotation);
            }

            _requestedPose = new SDKPose()
            {
                localPosition = position,
                localRotation = rotation,
                verticalFieldOfView = verticalFieldOfView,
                projectionMatrix = Matrix4x4.Perspective(verticalFieldOfView, aspect, inputPose.nearClipPlane, inputPose.farClipPlane)
            };

            _requestedPoseFrameIndex = Time.frameCount;
            return _inputFrame.priority.pose <= (sbyte)PRIORITY.GAME;
        }

        /// <summary>
        /// Set the game ground plane.
        /// </summary>
        /// <remarks>
        /// <para>If you wisth to use local space coordinates use local space instead. 
        /// The local space has to be relative to stage or stage transform if set.
        /// </para>
        /// </remarks>        
        public SDKBridge.ErrorCode SetGroundPlane(float distance, Vector3 normal, bool useLocalSpace = false)
        {
            float outputDistance = distance;
            Vector3 outputNormal = normal;

            if (!useLocalSpace)
            {
                Transform localTransform = stageTransform == null ? stage : stageTransform;
                Matrix4x4 worldToLocal = localTransform.worldToLocalMatrix;
                Vector3 localPosition = worldToLocal.MultiplyPoint(normal * distance);
                outputNormal = worldToLocal.MultiplyVector(normal);
                outputDistance = -Vector3.Dot(normal, localPosition);
            }

            return SDKBridge.SetGroundPlane(new SDKPlane() { distance = outputDistance, normal = outputNormal });
        }

        /// <summary>
        /// Set the game ground plane.
        /// </summary>
        /// <remarks>
        /// <para>If you wisth to use local space coordinates use local space instead. 
        /// The local space has to be relative to stage or stage transform if set.
        /// </para>
        /// </remarks>        
        public void SetGroundPlane(Plane plane, bool useLocalSpace = false)
        {
            SetGroundPlane(plane.distance, plane.normal, useLocalSpace);
        }

        /// <summary>
        /// Set the game ground plane.
        /// </summary>
        /// <remarks>
        /// <para>The transform up vector defines the normal of the plane and the position defines the distance.
        /// By default, the transform uses world space coordinates. If you wisth to use local space coordinates
        /// use local space instead. The local space has to be relative to stage or stage transform if set.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// public class SetGround : MonoBehaviour 
        /// {
        ///     [SerializeField] LIV.SDK.Unity.LIV _liv = null;
        /// 
        ///     void Update () 
        ///     {
        ///         if(_liv.isActive)
        ///         {        
        ///             _liv.render.SetGroundPlane(transform);
        ///         }
        ///     }
        /// }
        /// </code>
        /// </example>
        public void SetGroundPlane(Transform transform, bool useLocalSpace = false)
        {
            if (transform == null) return;
            Quaternion rotation = useLocalSpace ? transform.localRotation : transform.rotation;
            Vector3 position = useLocalSpace ? transform.localPosition : transform.position;
            Vector3 normal = rotation * Vector3.up;
            SetGroundPlane(-Vector3.Dot(normal, position), normal, useLocalSpace);
        }

        private void ReleaseBridgePoseControl()
        {
            _inputFrame.ReleaseControl();
            // Propagate release control.
            UpdateInputFrame();
        }

        private SDKBridge.ErrorCode UpdateBridgeResolution()
        {
            return SDKBridge.GetResolution(ref _resolution);
        }

        private void UpdateBridgeInputFrame()
        {
            if (_requestedPoseFrameIndex == Time.frameCount)
            {
                _inputFrame.ObtainControl();
                _inputFrame.pose = _requestedPose;
                _requestedPose = SDKPose.empty;
            }
            else
            {
                _inputFrame.ReleaseControl();
            }

            // store last input frame
            _lastInputFrame = _inputFrame;
            UpdateInputFrame();

            if (cameraReference != null)
            {
                // Near and far is always driven by game!
                _inputFrame.pose.nearClipPlane = cameraReference.nearClipPlane;
                _inputFrame.pose.farClipPlane = cameraReference.farClipPlane;
            }

            bool wasPassthroughEnabled = SDKUtils.FeatureEnabled(_lastInputFrame.features, FEATURES.ENABLE_PASSTHROUGH);
            bool isPassTroughEnabled = SDKUtils.FeatureEnabled(_inputFrame.features, FEATURES.ENABLE_PASSTHROUGH);

            if (!wasPassthroughEnabled && isPassTroughEnabled)
            {
                if (onPassthroughActivated != null)
                    onPassthroughActivated.Invoke();
            }

            if (wasPassthroughEnabled && !isPassTroughEnabled)
            {
                if (onPassthroughDeactivated != null)
                    onPassthroughDeactivated.Invoke();
            }
        }

        private SDKBridge.ErrorCode UpdateInputFrame()
        {
            SDKBridge.ErrorCode errorCode = SDKBridge.UpdateInputFrame(ref _inputFrame);
            if (errorCode != SDKBridge.ErrorCode.OK)
            {
                return errorCode;
            }

            return SDKBridge.ErrorCode.OK;
        }

        private void InvokePreRender()
        {
            if (onPreRender != null) onPreRender(this);
        }

        private void IvokePostRender()
        {
            if (onPostRender != null)
                onPostRender(this);
        }

        private void InvokePreRenderBackground()
        {
            if (onPreRenderBackground != null)
                onPreRenderBackground(this);
        }

        private void InvokePostRenderBackground()
        {
            if (onPostRenderBackground != null)
                onPostRenderBackground(this);
        }

        private void InvokePreRenderForeground()
        {
            if (onPreRenderForeground != null)
                onPreRenderForeground(this);
        }

        private void InvokePostRenderForeground()
        {
            if (onPostRenderForeground != null)
                onPostRenderForeground(this);
        }

        private void CreateBackgroundTexture()
        {
            if (SDKUtils.CreateTexture(ref _backgroundRenderTexture, _resolution.width, _resolution.height, 24, RenderTextureFormat.ARGB32))
            {
#if UNITY_EDITOR
                _backgroundRenderTexture.name = "LIV.BackgroundRenderTexture";
#endif               
            }
            else
            {
                Debug.LogError("LIV Render: Unable to create background texture!");
            }
        }

        private void CreateForegroundTexture()
        {
            if (SDKUtils.CreateTexture(ref _foregroundRenderTexture, _resolution.width, _resolution.height, 24, RenderTextureFormat.ARGB32))
            {
#if UNITY_EDITOR
                _foregroundRenderTexture.name = "LIV.ForegroundRenderTexture";
#endif
            }
            else
            {
                Debug.LogError("LIV Render: Unable to create foreground texture!");
            }
        }

        private void CreateOptimizedTexture()
        {
            if (SDKUtils.CreateTexture(ref _optimizedRenderTexture, _resolution.width, _resolution.height, 24, RenderTextureFormat.ARGB32))
            {
#if UNITY_EDITOR
                _optimizedRenderTexture.name = "LIV.OptimizedRenderTexture";
#endif               
            }
            else
            {
                Debug.LogError("LIV Render: Unable to create optimized texture!");
            }
        }

        private void CreateComplexClipPlaneTexture()
        {
            if (SDKUtils.CreateTexture(ref _complexClipPlaneRenderTexture, _inputFrame.clipPlane.width, _inputFrame.clipPlane.height, 0, RenderTextureFormat.ARGB32))
            {
#if UNITY_EDITOR
                _complexClipPlaneRenderTexture.name = "LIV.ComplexClipPlaneRenderTexture";
#endif
            }
            else
            {
                Debug.LogError("LIV Render: Unable to create complex clip plane texture!");
            }
        }

        private void UpdateTextures()
        {
            if (SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.BACKGROUND_RENDER))
            {
                if (
                    _backgroundRenderTexture == null ||
                    _backgroundRenderTexture.width != _resolution.width ||
                    _backgroundRenderTexture.height != _resolution.height
                )
                {
                    CreateBackgroundTexture();
                }
            }
            else
            {
                SDKUtils.DestroyTexture(ref _backgroundRenderTexture);
            }

            if (SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FOREGROUND_RENDER))
            {
                if (
                    _foregroundRenderTexture == null ||
                    _foregroundRenderTexture.width != _resolution.width ||
                    _foregroundRenderTexture.height != _resolution.height
                )
                {
                    CreateForegroundTexture();
                }
            }
            else
            {
                SDKUtils.DestroyTexture(ref _foregroundRenderTexture);
            }

            if (SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OPTIMIZED_RENDER))
            {
                if (
                    _optimizedRenderTexture == null ||
                    _optimizedRenderTexture.width != _resolution.width ||
                    _optimizedRenderTexture.height != _resolution.height
                )
                {
                    CreateOptimizedTexture();
                }
            }
            else
            {
                SDKUtils.DestroyTexture(ref _optimizedRenderTexture);
            }

            if (SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE))
            {
                if (
                    _complexClipPlaneRenderTexture == null ||
                    _complexClipPlaneRenderTexture.width != _inputFrame.clipPlane.width ||
                    _complexClipPlaneRenderTexture.height != _inputFrame.clipPlane.height
                )
                {
                    CreateComplexClipPlaneTexture();
                }
            }
            else
            {
                SDKUtils.DestroyTexture(ref _complexClipPlaneRenderTexture);
            }
        }

        SDKBridge.ErrorCode SendTextureToBridge(RenderTexture texture, TEXTURE_ID id)
        {
            return SDKBridge.AddTexture(texture, id);
        }

        Material GetClipPlaneMaterial(bool debugClipPlane, bool complexClipPlane, ColorWriteMask colorWriteMask, ref MaterialPropertyBlock materialPropertyBlock)
        {
            Material output;

            if (complexClipPlane)
            {
                output = debugClipPlane ? _clipPlaneComplexDebugMaterial : _clipPlaneComplexMaterial;
                materialPropertyBlock.SetTexture(SDKShaders.LIV_CLIP_PLANE_HEIGHT_MAP_PROPERTY, _complexClipPlaneRenderTexture);
                materialPropertyBlock.SetFloat(SDKShaders.LIV_TESSELLATION_PROPERTY, _inputFrame.clipPlane.tesselation);
            }
            else
            {
                output = debugClipPlane ? _clipPlaneSimpleDebugMaterial : _clipPlaneSimpleMaterial;
            }

            output.SetInt(SDKShaders.LIV_COLOR_MASK, (int)colorWriteMask);
            return output;
        }

        void RenderFrameStamps(RenderTexture renderTexture)
        {
            float aspect = (float)renderTexture.width / (float)renderTexture.height;
            int height = 50;
            int width = (int)(height * aspect);
            if (_sdkFont == null) _sdkFont = new SDKFont(width, height);
            _sdkFont.Resize(width, height);
            _sdkFont.Clear();
            string frameCount = Time.frameCount.ToString();

            System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(Time.realtimeSinceStartup);
            string timeStamp = string.Format("{0:00}:{1:00}:{2:00}:{3:000}", timeSpan.Hours, timeSpan.Minutes, timeSpan.Seconds, timeSpan.Milliseconds);
            _sdkFont.SetText(0, 0, frameCount);
            _sdkFont.SetText(width - frameCount.Length, 0, frameCount);
            _sdkFont.SetText(0, height - 1, string.Format("{0} {1}", frameCount, timeStamp));
            _sdkFont.SetText(width - frameCount.Length, height - 1, frameCount);
            _sdkFont.Apply();

            Graphics.Blit(null, renderTexture, _sdkFont.fontMaterial);
        }

        void RenderDebugHMD()
        {
            Graphics.DrawMesh(_boxMesh,
                _livDescriptor.HMDCamera.transform.localToWorldMatrix * Matrix4x4.Scale(Vector3.one * 0.1f),
                _clipPlaneSimpleDebugMaterial,
                0,
                _cameraInstance,
                0,
                _hmdMaterialProperty,
                false,
                false,
                false);
        }

        void RenderDebugPreRender()
        {
            RenderDebugHMD();
        }

        void RenderDebugPostRender(RenderTexture renderTexture)
        {
            RenderBuffer activeColorBuffer = Graphics.activeColorBuffer;
            RenderBuffer activeDepthBuffer = Graphics.activeDepthBuffer;
            RenderFrameStamps(renderTexture);
            Graphics.SetRenderTarget(activeColorBuffer, activeDepthBuffer);
        }

        protected void DisposeDebug()
        {
            if (_sdkFont != null)
            {
                _sdkFont.Dispose();
                _sdkFont = null;
            }
        }
    }
}