using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

namespace LIV.SDK.Unity
{    
    public partial class SDKRender : System.IDisposable
    {
        private LIV _liv = null;
        public LIV liv {
            get {
                return _liv;
            }
        }

        private SDKOutputFrame _outputFrame = SDKOutputFrame.empty;
        public SDKOutputFrame outputFrame {
            get {
                return _outputFrame;
            }
        }

        private SDKInputFrame _inputFrame = SDKInputFrame.empty;
        public SDKInputFrame inputFrame {
            get {
                return _inputFrame;
            }
        }

        private SDKResolution _resolution = SDKResolution.zero;
        public SDKResolution resolution {
            get {
                return _resolution;
            }
        }

        private Camera _cameraInstance = null;
        public Camera cameraInstance {
            get {
                return _cameraInstance;
            }
        }

        public Camera cameraReference {
            get {
                return _liv.MRCameraPrefab == null ? _liv.HMDCamera : _liv.MRCameraPrefab;
            }
        }

        public Camera hmdCamera {
            get {
                return _liv.HMDCamera;
            }
        }

        public Transform stage {
            get {
                return _liv.stage;
            }
        }

        public Transform stageTransform {
            get {
                return _liv.stageTransform;
            }
        }

        public Matrix4x4 stageLocalToWorldMatrix {
            get {
                return _liv.stage == null ? Matrix4x4.identity : _liv.stage.localToWorldMatrix;
            }
        }

        public Matrix4x4 localToWorldMatrix {
            get {
                return _liv.stageTransform == null ? stageLocalToWorldMatrix : _liv.stageTransform.localToWorldMatrix;
            }
        }

        public int spectatorLayerMask {
            get {
                return _liv.spectatorLayerMask;
            }
        }

        public bool disableStandardAssets {
            get {
                return _liv.disableStandardAssets;
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
            get {
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
                if(localTransform != null) worldToLocal = localTransform.worldToLocalMatrix;
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
        public void SetGroundPlane(float distance, Vector3 normal, bool useLocalSpace = false)
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

            SDKBridge.SetGroundPlane(new SDKPlane() { distance = outputDistance, normal = outputNormal });
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
            SDKBridge.UpdateInputFrame(ref _inputFrame);
        }

        private void UpdateBridgeResolution()
        {
            SDKBridge.GetResolution(ref _resolution);
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

            if (_cameraInstance != null)
            {
                // Near and far is always driven by game
                _inputFrame.pose.nearClipPlane = _cameraInstance.nearClipPlane;
                _inputFrame.pose.farClipPlane = _cameraInstance.farClipPlane;
            }

            SDKBridge.UpdateInputFrame(ref _inputFrame);
        }

        private void InvokePreRender()
        {
            if (_liv.onPreRender != null) _liv.onPreRender(this);
        }

        private void IvokePostRender()
        {
            if (_liv.onPostRender != null) _liv.onPostRender(this);
        }

        private void InvokePreRenderBackground()
        {
            if (_liv.onPreRenderBackground != null) _liv.onPreRenderBackground(this);
        }

        private void InvokePostRenderBackground()
        {
            if (_liv.onPostRenderBackground != null) _liv.onPostRenderBackground(this);
        }

        private void InvokePreRenderForeground()
        {
            if (_liv.onPreRenderForeground != null) _liv.onPreRenderForeground(this);
        }

        private void InvokePostRenderForeground()
        {
            if (_liv.onPostRenderForeground != null) _liv.onPostRenderForeground(this);
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
                Debug.LogError("LIV: Unable to create background texture!");
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
                Debug.LogError("LIV: Unable to create foreground texture!");
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
                Debug.LogError("LIV: Unable to create optimized texture!");
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
                Debug.LogError("LIV: Unable to create complex clip plane texture!");
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

        void SendTextureToBridge(RenderTexture texture, TEXTURE_ID id)
        {
            SDKBridge.AddTexture(new SDKTexture()
            {
                id = id,
                texturePtr = texture.GetNativeTexturePtr(),
                SharedHandle = System.IntPtr.Zero,
                device = SDKUtils.GetDevice(),
                dummy = 0,
                type = TEXTURE_TYPE.COLOR_BUFFER,
                format = TEXTURE_FORMAT.ARGB32,
                colorSpace = SDKUtils.GetColorSpace(texture),
                width = texture.width,
                height = texture.height
            });
        }
    }
}