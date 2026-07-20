#if !LIV_UNIVERSAL_RENDER
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace LIV.SDK.Unity
{
    class FixPostEffectsPass : IDisposable
    {
        private CameraEvent _captureTextureEvent = CameraEvent.BeforeImageEffects;
        private CameraEvent _applyTextureEvent = CameraEvent.AfterEverything;

        // Captures texture before post-effects
        private CommandBuffer _captureTextureCommandBuffer = null;
        // Renders captured texture
        private CommandBuffer _applyTextureCommandBuffer = null;
        private RenderTexture tempRenderTexture = null;

        public FixPostEffectsPass()
        {
            _captureTextureCommandBuffer = new CommandBuffer();
            _applyTextureCommandBuffer = new CommandBuffer();

#if UNITY_EDITOR
            _captureTextureCommandBuffer.name = "LIV.captureTexture";
            _applyTextureCommandBuffer.name = "LIV.applyTexture";
#endif
        }

        public void Render(Camera camera, RenderTexture renderTarget, Mesh quadMesh, Material writeMaterial, bool fixColor)
        {
            if (tempRenderTexture != null)
                RenderTexture.ReleaseTemporary(tempRenderTexture);
            tempRenderTexture = RenderTexture.GetTemporary(renderTarget.width, renderTarget.height, 0, renderTarget.format);
#if UNITY_EDITOR
            tempRenderTexture.name = "LIV.TemporaryRenderTexture";
#endif
            _captureTextureCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
            camera.AddCommandBuffer(_captureTextureEvent, _captureTextureCommandBuffer);

            writeMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, fixColor ? (int)ColorWriteMask.All : (int)ColorWriteMask.Alpha);
            writeMaterial.mainTexture = tempRenderTexture;
            _applyTextureCommandBuffer.DrawMesh(quadMesh, Matrix4x4.identity, writeMaterial);
            camera.AddCommandBuffer(_applyTextureEvent, _applyTextureCommandBuffer);
        }

        public void Release(Camera camera)
        {
            camera.RemoveCommandBuffer(_captureTextureEvent, _captureTextureCommandBuffer);
            camera.RemoveCommandBuffer(_applyTextureEvent, _applyTextureCommandBuffer);

            _captureTextureCommandBuffer.Clear();
            _applyTextureCommandBuffer.Clear();

            if (tempRenderTexture != null)
            {
                RenderTexture.ReleaseTemporary(tempRenderTexture);
                tempRenderTexture = null;
            }
        }

        public void Dispose()
        {
            if (tempRenderTexture != null)
            {
                RenderTexture.ReleaseTemporary(tempRenderTexture);
                tempRenderTexture = null;
            }

            SDKUtils.DisposeObject<CommandBuffer>(ref _captureTextureCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyTextureCommandBuffer);
        }
    }

    public partial class SDKRender : System.IDisposable
    {
        // Renders the clip plane in the foreground texture
        private CommandBuffer _clipPlaneCommandBuffer = null;
        // Renders the clipped opaque content in to the foreground texture alpha
        private CommandBuffer _combineAlphaCommandBuffer = null;
        
        // Renders background and foreground in single render
        private CommandBuffer _optimizedRenderingCommandBuffer = null;

        private CameraEvent _clipPlaneCameraEvent = CameraEvent.AfterForwardOpaque;
        private CameraEvent _clipPlaneCombineAlphaCameraEvent = CameraEvent.AfterEverything;
        private CameraEvent _optimizedRenderingCameraEvent = CameraEvent.AfterForwardAlpha;

        // Clear material
        private Material _clipPlaneSimpleMaterial = null;
        // Transparent material for visual debugging
        private Material _clipPlaneSimpleDebugMaterial = null;
        // Tessellated height map clear material
        private Material _clipPlaneComplexMaterial = null;
        // Tessellated height map clear material for visual debugging
        private Material _clipPlaneComplexDebugMaterial = null;
        // Simple blit material
        private Material _writeMaterial = null;
        // Reveal opaque geometry in alpha channel
        private Material _writeOpaqueToAlphaMaterial = null;
        // Combine existing alpha channel with another texture alpha channel
        private Material _combineAlphaMaterial = null;
        
        // Enforce that forward rendering is being executed during deffered rendering
        private Material _forceForwardRenderingMaterial = null;

        private RenderTexture _backgroundRenderTexture = null;
        private RenderTexture _foregroundRenderTexture = null;
        private RenderTexture _optimizedRenderTexture = null;
        private RenderTexture _complexClipPlaneRenderTexture = null;

        private CaptureCameraSettings _captureCameraSettings = null;
        private FixPostEffectsPass _fixPostEffectsPass = null;
        private bool _isDisposed = false;
        public bool isDisposed {
            get {
                return _isDisposed; 
            }
        }

        public SDKRender(LivDescriptor livDescriptor)
        {
            _livDescriptor = livDescriptor;
            CreateAssets();
            CreateCamera();
        }

        public void Render()
        {
            UpdateBridgeResolution();
            UpdateBridgeInputFrame();
            SDKUtils.ApplyUserSpaceTransform(this);
            UpdateTextures();
            InvokePreRender();
            if (canRenderBackground) RenderBackground();
            if (canRenderForeground) RenderForeground();
            if (canRenderOptimized) RenderOptimized();
            IvokePostRender();
            SDKUtils.CreateBridgeOutputFrame(this);
            SDKBridge.IssuePluginEvent();
        }

        // Default render without any special changes
        private void RenderBackground()
        {
            if (!CreateCamera())
                return;

            bool debug = debugClipPlane;

            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, renderingLayerMask);
            _cameraInstance.targetTexture = _backgroundRenderTexture;

            // Fix alpha corruption by post processing
            bool fixPostEffects = overridePostProcessing || fixPostEffectsAlpha;
            if (fixPostEffects)
            {
                _fixPostEffectsPass.Render(cameraInstance, _backgroundRenderTexture, _quadMesh, _writeMaterial, overridePostProcessing);
            }

            _captureCameraSettings.Capture(_cameraInstance);
            if (isPassthroughEnabled)
            {
                _cameraInstance.clearFlags = CameraClearFlags.SolidColor;
                _cameraInstance.backgroundColor = Color.clear;
            }

            SDKShaders.StartRendering();
            SDKShaders.StartBackgroundRendering();
            InvokePreRenderBackground();
            if (debug) RenderDebugPreRender();
            _cameraInstance.Render();
            InvokePostRenderBackground();
            if (debug) RenderDebugPostRender(_backgroundRenderTexture);
            SendTextureToBridge(_backgroundRenderTexture, TEXTURE_ID.BACKGROUND_COLOR_BUFFER_ID);
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            if (fixPostEffects)
            {
                _fixPostEffectsPass.Release(cameraInstance);
            }

            _captureCameraSettings.Release(_cameraInstance);
        }

        // Extract the image which is in front of our clip plane
        // The compositing is heavily relying on the alpha channel, therefore we want to make sure it does
        // not get corrupted by the postprocessing or any shader
        private void RenderForeground()
        {
            if (!CreateCamera())
                return;

            bool debug = debugClipPlane;

            MonoBehaviour[] behaviours = null;
            bool[] wasBehaviourEnabled = null;
            if (disableStandardAssets) SDKUtils.DisableStandardAssets(_cameraInstance, ref behaviours, ref wasBehaviourEnabled);

            // Capture camera defaults
            _captureCameraSettings.Capture(_cameraInstance);
            Color capturedFogColor = RenderSettings.fogColor;

            // Make sure that fog does not corrupt alpha channel
            RenderSettings.fogColor = new Color(capturedFogColor.r, capturedFogColor.g, capturedFogColor.b, 0f);
            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, renderingLayerMask);
            _cameraInstance.clearFlags = CameraClearFlags.Color;
            _cameraInstance.backgroundColor = Color.clear;
            _cameraInstance.targetTexture = _foregroundRenderTexture;

            RenderTexture capturedAlphaRenderTexture = RenderTexture.GetTemporary(_foregroundRenderTexture.width, _foregroundRenderTexture.height, 0, _foregroundRenderTexture.format);
#if UNITY_EDITOR
            capturedAlphaRenderTexture.name = "LIV.CapturedAlphaRenderTexture";
#endif

            if(_livDescriptor.overrideAlphaWithDepthBuffer)
            {
                // Render opaque pixels into alpha channel
                _clipPlaneCommandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _writeOpaqueToAlphaMaterial, 0, 0);
            }

            // Render clip plane
            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, renderComplexClipPlane, ColorWriteMask.All, ref _clipPlaneMaterialProperty), 0, 0, _clipPlaneMaterialProperty);

            // Render ground clip plane
            if (renderGroundClipPlane)
            {
                Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
                _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, false, ColorWriteMask.All, ref _groundPlaneMaterialProperty), 0, 0, _groundPlaneMaterialProperty);
            }

            // Copy alpha in to texture
            _clipPlaneCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);
            _cameraInstance.AddCommandBuffer(_clipPlaneCameraEvent, _clipPlaneCommandBuffer);

            // Fix alpha corruption by post processing
            bool fixPostEffects = overridePostProcessing || fixPostEffectsAlpha;
            if (fixPostEffects)
            {
                _fixPostEffectsPass.Render(cameraInstance, _backgroundRenderTexture, _quadMesh, _writeMaterial, overridePostProcessing);
            }

            // Combine captured alpha with result alpha
            _combineAlphaMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _combineAlphaMaterial.mainTexture = capturedAlphaRenderTexture;
            _combineAlphaCommandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _combineAlphaMaterial);
            _cameraInstance.AddCommandBuffer(_clipPlaneCombineAlphaCameraEvent, _combineAlphaCommandBuffer);

            // We need to force forward rendering to obtain transparency render events
            if (useDeferredRendering) 
                SDKUtils.ForceForwardRendering(cameraInstance, _clipPlaneMesh, _forceForwardRenderingMaterial);

            SDKShaders.StartRendering();
            SDKShaders.StartForegroundRendering();
            InvokePreRenderForeground();
            if (debug) RenderDebugPreRender();
            _cameraInstance.Render();
            InvokePostRenderForeground();
            if (debug) RenderDebugPostRender(_foregroundRenderTexture);
            SendTextureToBridge(_foregroundRenderTexture, TEXTURE_ID.FOREGROUND_COLOR_BUFFER_ID);
            _cameraInstance.targetTexture = null;
            SDKShaders.StopForegroundRendering();
            SDKShaders.StopRendering();

            if (fixPostEffects)
            {
                _fixPostEffectsPass.Release(cameraInstance);
            }

            _cameraInstance.RemoveCommandBuffer(_clipPlaneCameraEvent, _clipPlaneCommandBuffer);
            _cameraInstance.RemoveCommandBuffer(_clipPlaneCombineAlphaCameraEvent, _combineAlphaCommandBuffer);

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);

            _clipPlaneCommandBuffer.Clear();
            _combineAlphaCommandBuffer.Clear();

            // Revert camera defaults
            _captureCameraSettings.Release(_cameraInstance);
            RenderSettings.fogColor = capturedFogColor;

            SDKUtils.RestoreStandardAssets(ref behaviours, ref wasBehaviourEnabled);
        }

        // Renders a single camera in a single texture with occlusion only from opaque objects.
        // This is the most performant option for mixed reality.
        // It does not support any transparency in the foreground layer.
        private void RenderOptimized()
        {
            if (!CreateCamera())
                return;

            bool debug = debugClipPlane;

            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, renderingLayerMask);
            _cameraInstance.targetTexture = _optimizedRenderTexture;

            RenderTexture capturedAlphaRenderTexture = RenderTexture.GetTemporary(_optimizedRenderTexture.width, _optimizedRenderTexture.height, 0, _optimizedRenderTexture.format);
#if UNITY_EDITOR
            capturedAlphaRenderTexture.name = "LIV.CapturedAlphaRenderTexture";
#endif
            // Clear alpha channel
            _writeMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _optimizedRenderingCommandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _writeMaterial);

            // Render opaque pixels into alpha channel
            _writeOpaqueToAlphaMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);

            // Render opaque pixels into alpha channel
            _optimizedRenderingCommandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _writeOpaqueToAlphaMaterial, 0, 0);

            // Render clip plane
            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            _optimizedRenderingCommandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, renderComplexClipPlane, ColorWriteMask.Alpha, ref _clipPlaneMaterialProperty), 0, 0, _clipPlaneMaterialProperty);

            // Render ground clip plane
            if (renderGroundClipPlane)
            {
                Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
                _optimizedRenderingCommandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform,
                    GetClipPlaneMaterial(debugClipPlane, false, ColorWriteMask.Alpha, ref _groundPlaneMaterialProperty), 0, 0, _groundPlaneMaterialProperty);
            }

            _optimizedRenderingCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);

            _cameraInstance.AddCommandBuffer(_optimizedRenderingCameraEvent, _optimizedRenderingCommandBuffer);

            if (useDeferredRendering) SDKUtils.ForceForwardRendering(cameraInstance, _clipPlaneMesh, _forceForwardRenderingMaterial);

            _captureCameraSettings.Capture(_cameraInstance);
            if (isPassthroughEnabled)
            {
                _cameraInstance.clearFlags = CameraClearFlags.SolidColor;
                _cameraInstance.backgroundColor = Color.clear;
            }

            // TODO: this is just proprietary
            SDKShaders.StartRendering();
            SDKShaders.StartBackgroundRendering();
            InvokePreRenderBackground();
            if (debug) RenderDebugPreRender();
            _cameraInstance.Render();

            // Recover alpha
            RenderBuffer activeColorBuffer = Graphics.activeColorBuffer;
            RenderBuffer activeDepthBuffer = Graphics.activeDepthBuffer;
            Graphics.Blit(capturedAlphaRenderTexture, _optimizedRenderTexture, _writeMaterial);
            Graphics.SetRenderTarget(activeColorBuffer, activeDepthBuffer);

            InvokePostRenderBackground();
            if (debug) RenderDebugPostRender(_optimizedRenderTexture);
            SendTextureToBridge(_optimizedRenderTexture, TEXTURE_ID.OPTIMIZED_COLOR_BUFFER_ID);
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            _cameraInstance.RemoveCommandBuffer(_optimizedRenderingCameraEvent, _optimizedRenderingCommandBuffer);
            _optimizedRenderingCommandBuffer.Clear();

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);
            _captureCameraSettings.Release(_cameraInstance);
        }

        private bool CreateCamera()
        {
            // Camera is already created
            if (_cameraInstance != null)
                return true;

            // Camera reference does not exist
            if (cameraReference == null)
            {
                Debug.LogWarning("LIV Renderer: camera reference is missing!");
                return false;
            }

            bool cameraReferenceEnabled = cameraReference.enabled;
            if (cameraReferenceEnabled)
            {
                cameraReference.enabled = false;
            }
            bool cameraReferenceActive = cameraReference.gameObject.activeSelf;
            if (cameraReferenceActive)
            {
                cameraReference.gameObject.SetActive(false);
            }

            GameObject cloneGO = (GameObject)Object.Instantiate(cameraReference.gameObject, _livDescriptor.stage);
            _cameraInstance = cloneGO.GetComponent<Camera>();

            SDKUtils.CleanCameraBehaviours(_cameraInstance, _livDescriptor.excludeBehaviours);

            if (cameraReferenceActive != cameraReference.gameObject.activeSelf)
            {
                cameraReference.gameObject.SetActive(cameraReferenceActive);
            }
            if (cameraReferenceEnabled != cameraReference.enabled)
            {
                cameraReference.enabled = cameraReferenceEnabled;
            }

            _cameraInstance.name = "LIV Camera";
            if (_cameraInstance.tag == "MainCamera")
            {
                _cameraInstance.tag = "Untagged";
            }

            _cameraInstance.transform.localScale = Vector3.one;
            _cameraInstance.rect = new Rect(0, 0, 1, 1);
            _cameraInstance.depth = 0;
#if UNITY_5_4_OR_NEWER
            _cameraInstance.stereoTargetEye = StereoTargetEyeMask.None;
#endif
#if UNITY_5_6_OR_NEWER
            _cameraInstance.allowMSAA = false;
#endif
            _cameraInstance.enabled = false;
            _cameraInstance.gameObject.SetActive(true);

            return true;
        }

        private void CreateAssets()
        {
            _clipPlaneMesh = new Mesh();
            SDKUtils.CreateClipPlane(_clipPlaneMesh, 10, 10, true, 1000f);

            _quadMesh = new Mesh();
            SDKUtils.CreateQuad(_quadMesh);

            _boxMesh = new Mesh();
            SDKUtils.CreateBox(_boxMesh, Vector3.one);

            _clipPlaneSimpleMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_CLIP_PLANE_SIMPLE_SHADER));
            _clipPlaneSimpleDebugMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_CLIP_PLANE_SIMPLE_DEBUG_SHADER));
            _clipPlaneComplexMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_CLIP_PLANE_COMPLEX_SHADER));
            _clipPlaneComplexDebugMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_CLIP_PLANE_COMPLEX_DEBUG_SHADER));
            _writeMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_WRITE_SHADER));
            _writeOpaqueToAlphaMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_WRITE_OPAQUE_TO_ALPHA_SHADER));
            _combineAlphaMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_COMBINE_ALPHA_SHADER));
            _forceForwardRenderingMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_FORCE_FORWARD_RENDERING_SHADER));
            
            _clipPlaneMaterialProperty = new MaterialPropertyBlock();
            _clipPlaneMaterialProperty.SetColor(SDKShaders.LIV_COLOR, SDKShaders.GREEN_COLOR);
            _groundPlaneMaterialProperty = new MaterialPropertyBlock();
            _groundPlaneMaterialProperty.SetColor(SDKShaders.LIV_COLOR, SDKShaders.BLUE_COLOR);
            _hmdMaterialProperty = new MaterialPropertyBlock();
            _hmdMaterialProperty.SetColor(SDKShaders.LIV_COLOR, SDKShaders.RED_COLOR);

            _clipPlaneCommandBuffer = new CommandBuffer();
            _combineAlphaCommandBuffer = new CommandBuffer();
            _optimizedRenderingCommandBuffer = new CommandBuffer();

            _captureCameraSettings = new CaptureCameraSettings();
            _fixPostEffectsPass = new FixPostEffectsPass();

#if UNITY_EDITOR
            _quadMesh.name = "LIV.quad";
            _clipPlaneMesh.name = "LIV.clipPlane";
            _clipPlaneSimpleMaterial.name = "LIV.clipPlaneSimple";
            _clipPlaneSimpleDebugMaterial.name = "LIV.clipPlaneSimpleDebug";
            _clipPlaneComplexMaterial.name = "LIV.clipPlaneComplex";
            _clipPlaneComplexDebugMaterial.name = "LIV.clipPlaneComplexDebug";
            _writeMaterial.name = "LIV.write";
            _writeOpaqueToAlphaMaterial.name = "LIV.writeOpaqueToAlpha";
            _combineAlphaMaterial.name = "LIV.combineAlpha";
            _forceForwardRenderingMaterial.name = "LIV.forceForwardRendering";
            _clipPlaneCommandBuffer.name = "LIV.renderClipPlanes";
            _combineAlphaCommandBuffer.name = "LIV.foregroundCombineAlpha";
            _optimizedRenderingCommandBuffer.name = "LIV.optimizedRendering";
#endif
        }

        private void DestroyAssets()
        {
            if (_cameraInstance != null)
            {
                Object.Destroy(_cameraInstance.gameObject);
                _cameraInstance = null;
            }

            SDKUtils.DestroyObject<Mesh>(ref _quadMesh);
            SDKUtils.DestroyObject<Mesh>(ref _clipPlaneMesh);
            SDKUtils.DestroyObject<Mesh>(ref _boxMesh);

            SDKUtils.DestroyObject<Material>(ref _clipPlaneSimpleMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneSimpleDebugMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneComplexMaterial);
            SDKUtils.DestroyObject<Material>(ref _clipPlaneComplexDebugMaterial);
            SDKUtils.DestroyObject<Material>(ref _writeOpaqueToAlphaMaterial);
            SDKUtils.DestroyObject<Material>(ref _combineAlphaMaterial);
            SDKUtils.DestroyObject<Material>(ref _writeMaterial);
            SDKUtils.DestroyObject<Material>(ref _forceForwardRenderingMaterial);

            SDKUtils.DisposeObject<CommandBuffer>(ref _clipPlaneCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _combineAlphaCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _optimizedRenderingCommandBuffer);

            SDKUtils.DestroyTexture(ref _backgroundRenderTexture);
            SDKUtils.DestroyTexture(ref _foregroundRenderTexture);
            SDKUtils.DestroyTexture(ref _optimizedRenderTexture);
            SDKUtils.DestroyTexture(ref _complexClipPlaneRenderTexture);

            _fixPostEffectsPass.Dispose();
        }

        public void Dispose()
        {
            if (isPassthroughEnabled)
            {
                if (onPassthroughDeactivated != null)
                {
                    onPassthroughDeactivated.Invoke();
                }
            }

            ReleaseBridgePoseControl();
            DestroyAssets();
            DisposeDebug();

            _isDisposed = true;
        }
    }
}
#endif