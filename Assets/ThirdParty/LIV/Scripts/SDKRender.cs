#if !LIV_UNIVERSAL_RENDER
using UnityEngine;
using UnityEngine.Rendering;

namespace LIV.SDK.Unity
{
    public partial class SDKRender : System.IDisposable
    {
        // Renders the clip plane in the foreground texture
        private CommandBuffer _clipPlaneCommandBuffer = null;
        // Renders the clipped opaque content in to the foreground texture alpha
        private CommandBuffer _combineAlphaCommandBuffer = null;
        // Captures texture before post-effects
        private CommandBuffer _captureTextureCommandBuffer = null;
        // Renders captured texture
        private CommandBuffer _applyTextureCommandBuffer = null;
        // Renders background and foreground in single render
        private CommandBuffer _optimizedRenderingCommandBuffer = null;

        private CameraEvent _clipPlaneCameraEvent = CameraEvent.AfterForwardOpaque;
        private CameraEvent _clipPlaneCombineAlphaCameraEvent = CameraEvent.AfterEverything;
        private CameraEvent _captureTextureEvent = CameraEvent.BeforeImageEffects;
        private CameraEvent _applyTextureEvent = CameraEvent.AfterEverything;
        private CameraEvent _optimizedRenderingCameraEvent = CameraEvent.AfterEverything;

        // Tessellated quad
        private Mesh _clipPlaneMesh = null;
        // Clear material
        private Material _clipPlaneSimpleMaterial = null;
        // Transparent material for visual debugging
        private Material _clipPlaneSimpleDebugMaterial = null;
        // Tessellated height map clear material
        private Material _clipPlaneComplexMaterial = null;
        // Tessellated height map clear material for visual debugging
        private Material _clipPlaneComplexDebugMaterial = null;
        private Material _writeOpaqueToAlphaMaterial = null;
        private Material _combineAlphaMaterial = null;
        private Material _writeMaterial = null;
        private Material _forceForwardRenderingMaterial = null;
        
        private RenderTexture _backgroundRenderTexture = null;
        private RenderTexture _foregroundRenderTexture = null;
        private RenderTexture _optimizedRenderTexture = null;
        private RenderTexture _complexClipPlaneRenderTexture = null;

        Material GetClipPlaneMaterial(bool debugClipPlane, bool complexClipPlane, ColorWriteMask colorWriteMask)
        {
            Material output;

            if (complexClipPlane)
            {
                output = debugClipPlane ? _clipPlaneComplexDebugMaterial : _clipPlaneComplexMaterial;
                output.SetTexture(SDKShaders.LIV_CLIP_PLANE_HEIGHT_MAP_PROPERTY, _complexClipPlaneRenderTexture);
                output.SetFloat(SDKShaders.LIV_TESSELLATION_PROPERTY, _inputFrame.clipPlane.tesselation);
            }
            else
            {
                output = debugClipPlane ? _clipPlaneSimpleDebugMaterial : _clipPlaneSimpleMaterial;
            }

            output.SetInt(SDKShaders.LIV_COLOR_MASK, (int)colorWriteMask);
            return output;
        }

        Material GetGroundClipPlaneMaterial(bool debugClipPlane, ColorWriteMask colorWriteMask)
        {
            Material output;
            output = debugClipPlane ? _clipPlaneSimpleDebugMaterial : _clipPlaneSimpleMaterial;
            output.SetInt(SDKShaders.LIV_COLOR_MASK, (int)colorWriteMask);
            return output;
        }

        bool useDeferredRendering {
            get {
                return _cameraInstance.actualRenderingPath == RenderingPath.DeferredLighting ||
                _cameraInstance.actualRenderingPath == RenderingPath.DeferredShading;
            }
        }

        bool interlacedRendering {
            get {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.INTERLACED_RENDER);
            }
        }

        bool canRenderBackground {
            get {
                if (interlacedRendering)
                {
                    // Render only if frame is even 
                    if (Time.frameCount % 2 != 0) return false;
                }
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.BACKGROUND_RENDER) && _backgroundRenderTexture != null;
            }
        }

        bool canRenderForeground {
            get {
                if (interlacedRendering)
                {
                    // Render only if frame is odd 
                    if (Time.frameCount % 2 != 1) return false;
                }
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FOREGROUND_RENDER) && _foregroundRenderTexture != null;
            }
        }

        bool canRenderOptimized {
            get {
                return SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OPTIMIZED_RENDER) && _optimizedRenderTexture != null; ;
            }
        }

        public SDKRender(LIV liv)
        {
            _liv = liv;
            CreateAssets();
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
            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, spectatorLayerMask);
            _cameraInstance.targetTexture = _backgroundRenderTexture;

            RenderTexture tempRenderTexture = null;

            bool overridePostProcessing = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OVERRIDE_POST_PROCESSING);
            if (overridePostProcessing)
            {
                tempRenderTexture = RenderTexture.GetTemporary(_backgroundRenderTexture.width, _backgroundRenderTexture.height, 0, _backgroundRenderTexture.format);
#if UNITY_EDITOR
                tempRenderTexture.name = "LIV.TemporaryRenderTexture";
#endif
                _captureTextureCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
                _applyTextureCommandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive);

                _cameraInstance.AddCommandBuffer(_captureTextureEvent, _captureTextureCommandBuffer);
                _cameraInstance.AddCommandBuffer(_applyTextureEvent, _applyTextureCommandBuffer);
            }

            SDKShaders.StartRendering();
            SDKShaders.StartBackgroundRendering();
            InvokePreRenderBackground();
            SendTextureToBridge(_backgroundRenderTexture, TEXTURE_ID.BACKGROUND_COLOR_BUFFER_ID);
            _cameraInstance.Render();
            InvokePostRenderBackground();
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            if (overridePostProcessing)
            {
                _cameraInstance.RemoveCommandBuffer(_captureTextureEvent, _captureTextureCommandBuffer);
                _cameraInstance.RemoveCommandBuffer(_applyTextureEvent, _applyTextureCommandBuffer);

                _captureTextureCommandBuffer.Clear();
                _applyTextureCommandBuffer.Clear();

                RenderTexture.ReleaseTemporary(tempRenderTexture);
            }
        }

        // Extract the image which is in front of our clip plane
        // The compositing is heavily relying on the alpha channel, therefore we want to make sure it does
        // not get corrupted by the postprocessing or any shader
        private void RenderForeground()
        {
            bool debugClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            bool renderComplexClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE);
            bool renderGroundClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.GROUND_CLIP_PLANE);
            bool overridePostProcessing = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OVERRIDE_POST_PROCESSING);
            bool fixPostEffectsAlpha = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FIX_FOREGROUND_ALPHA) | _liv.fixPostEffectsAlpha;

            MonoBehaviour[] behaviours = null;
            bool[] wasBehaviourEnabled = null;
            if (disableStandardAssets) SDKUtils.DisableStandardAssets(_cameraInstance, ref behaviours, ref wasBehaviourEnabled);

            // Capture camera defaults
            CameraClearFlags capturedClearFlags = _cameraInstance.clearFlags;
            Color capturedBgColor = _cameraInstance.backgroundColor;
            Color capturedFogColor = RenderSettings.fogColor;

            // Make sure that fog does not corrupt alpha channel
            RenderSettings.fogColor = new Color(capturedFogColor.r, capturedFogColor.g, capturedFogColor.b, 0f);
            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, spectatorLayerMask);
            _cameraInstance.clearFlags = CameraClearFlags.Color;
            _cameraInstance.backgroundColor = Color.clear;
            _cameraInstance.targetTexture = _foregroundRenderTexture;

            RenderTexture capturedAlphaRenderTexture = RenderTexture.GetTemporary(_foregroundRenderTexture.width, _foregroundRenderTexture.height, 0, _foregroundRenderTexture.format);
#if UNITY_EDITOR
            capturedAlphaRenderTexture.name = "LIV.CapturedAlphaRenderTexture";
#endif

            // Render opaque pixels into alpha channel
            _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, Matrix4x4.identity, _writeOpaqueToAlphaMaterial, 0, 0);

            // Render clip plane
            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, renderComplexClipPlane, ColorWriteMask.All), 0, 0);

            // Render ground clip plane
            if (renderGroundClipPlane)
            {
                Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
                _clipPlaneCommandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform,
                GetGroundClipPlaneMaterial(debugClipPlane, ColorWriteMask.All), 0, 0);
            }

            // Copy alpha in to texture
            _clipPlaneCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);
            _cameraInstance.AddCommandBuffer(_clipPlaneCameraEvent, _clipPlaneCommandBuffer);

            // Fix alpha corruption by post processing
            RenderTexture tempRenderTexture = null;
            if (overridePostProcessing || fixPostEffectsAlpha)
            {
                tempRenderTexture = RenderTexture.GetTemporary(_foregroundRenderTexture.width, _foregroundRenderTexture.height, 0, _foregroundRenderTexture.format);
#if UNITY_EDITOR
                tempRenderTexture.name = "LIV.TemporaryRenderTexture";
#endif
                _captureTextureCommandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
                _cameraInstance.AddCommandBuffer(_captureTextureEvent, _captureTextureCommandBuffer);

                _writeMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, overridePostProcessing ? (int)ColorWriteMask.All : (int)ColorWriteMask.Alpha);
                _applyTextureCommandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive, _writeMaterial);
                _cameraInstance.AddCommandBuffer(_applyTextureEvent, _applyTextureCommandBuffer);
            }

            // Combine captured alpha with result alpha
            _combineAlphaMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _combineAlphaCommandBuffer.Blit(capturedAlphaRenderTexture, BuiltinRenderTextureType.CurrentActive, _combineAlphaMaterial);
            _cameraInstance.AddCommandBuffer(_clipPlaneCombineAlphaCameraEvent, _combineAlphaCommandBuffer);

            if (useDeferredRendering) SDKUtils.ForceForwardRendering(cameraInstance, _clipPlaneMesh, _forceForwardRenderingMaterial);

            SDKShaders.StartRendering();
            SDKShaders.StartForegroundRendering();
            InvokePreRenderForeground();
            SendTextureToBridge(_foregroundRenderTexture, TEXTURE_ID.FOREGROUND_COLOR_BUFFER_ID);
            _cameraInstance.Render();
            InvokePostRenderForeground();
            _cameraInstance.targetTexture = null;
            SDKShaders.StopForegroundRendering();
            SDKShaders.StopRendering();

            if (overridePostProcessing || fixPostEffectsAlpha)
            {
                _cameraInstance.RemoveCommandBuffer(_captureTextureEvent, _captureTextureCommandBuffer);
                _cameraInstance.RemoveCommandBuffer(_applyTextureEvent, _applyTextureCommandBuffer);

                _captureTextureCommandBuffer.Clear();
                _applyTextureCommandBuffer.Clear();

                RenderTexture.ReleaseTemporary(tempRenderTexture);
            }

            _cameraInstance.RemoveCommandBuffer(_clipPlaneCameraEvent, _clipPlaneCommandBuffer);
            _cameraInstance.RemoveCommandBuffer(_clipPlaneCombineAlphaCameraEvent, _combineAlphaCommandBuffer);

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);

            _clipPlaneCommandBuffer.Clear();
            _combineAlphaCommandBuffer.Clear();

            // Revert camera defaults
            _cameraInstance.clearFlags = capturedClearFlags;
            _cameraInstance.backgroundColor = capturedBgColor;
            RenderSettings.fogColor = capturedFogColor;

            SDKUtils.RestoreStandardAssets(ref behaviours, ref wasBehaviourEnabled);
        }

        // Renders a single camera in a single texture with occlusion only from opaque objects.
        // This is the most performant option for mixed reality.
        // It does not support any transparency in the foreground layer.
        private void RenderOptimized()
        {
            bool debugClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            bool renderComplexClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE);
            bool renderGroundClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.GROUND_CLIP_PLANE);

            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, spectatorLayerMask);
            _cameraInstance.targetTexture = _optimizedRenderTexture;

            // Clear alpha channel
            _writeMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _optimizedRenderingCommandBuffer.Blit(BuiltinRenderTextureType.None, BuiltinRenderTextureType.CurrentActive, _writeMaterial);

            // Render opaque pixels into alpha channel            
            _writeOpaqueToAlphaMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _optimizedRenderingCommandBuffer.DrawMesh(_clipPlaneMesh, Matrix4x4.identity, _writeOpaqueToAlphaMaterial, 0, 0);

            // Render clip plane            
            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            _optimizedRenderingCommandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, renderComplexClipPlane, ColorWriteMask.Alpha), 0, 0);

            // Render ground clip plane            
            if (renderGroundClipPlane)
            {
                Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
                _optimizedRenderingCommandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform,
                    GetGroundClipPlaneMaterial(debugClipPlane, ColorWriteMask.Alpha), 0, 0);
            }

            _cameraInstance.AddCommandBuffer(CameraEvent.AfterEverything, _optimizedRenderingCommandBuffer);

            // TODO: this is just proprietary
            SDKShaders.StartRendering();
            SDKShaders.StartBackgroundRendering();
            InvokePreRenderBackground();
            SendTextureToBridge(_optimizedRenderTexture, TEXTURE_ID.OPTIMIZED_COLOR_BUFFER_ID);
            _cameraInstance.Render();
            InvokePostRenderBackground();
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            _cameraInstance.RemoveCommandBuffer(CameraEvent.AfterEverything, _optimizedRenderingCommandBuffer);
            _optimizedRenderingCommandBuffer.Clear();
        }

        private void CreateAssets()
        {
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

            GameObject cloneGO = (GameObject)Object.Instantiate(cameraReference.gameObject, _liv.stage);
            _cameraInstance = (Camera)cloneGO.GetComponent("Camera");

            SDKUtils.CleanCameraBehaviours(_cameraInstance, _liv.excludeBehaviours);

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

            _clipPlaneMesh = new Mesh();
            SDKUtils.CreateClipPlane(_clipPlaneMesh, 10, 10, true, 1000f);
            _clipPlaneSimpleMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_SIMPLE_SHADER));
            _clipPlaneSimpleDebugMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_SIMPLE_DEBUG_SHADER));
            _clipPlaneComplexMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_COMPLEX_SHADER));
            _clipPlaneComplexDebugMaterial = new Material(Shader.Find(SDKShaders.LIV_CLIP_PLANE_COMPLEX_DEBUG_SHADER));
            _writeOpaqueToAlphaMaterial = new Material(Shader.Find(SDKShaders.LIV_WRITE_OPAQUE_TO_ALPHA_SHADER));
            _combineAlphaMaterial = new Material(Shader.Find(SDKShaders.LIV_COMBINE_ALPHA_SHADER));
            _writeMaterial = new Material(Shader.Find(SDKShaders.LIV_WRITE_SHADER));
            _forceForwardRenderingMaterial = new Material(Shader.Find(SDKShaders.LIV_FORCE_FORWARD_RENDERING_SHADER));
            _clipPlaneCommandBuffer = new CommandBuffer();
            _combineAlphaCommandBuffer = new CommandBuffer();
            _captureTextureCommandBuffer = new CommandBuffer();
            _applyTextureCommandBuffer = new CommandBuffer();
            _optimizedRenderingCommandBuffer = new CommandBuffer();

#if UNITY_EDITOR
            _clipPlaneMesh.name = "LIV.clipPlane";
            _clipPlaneSimpleMaterial.name = "LIV.clipPlaneSimple";
            _clipPlaneSimpleDebugMaterial.name = "LIV.clipPlaneSimpleDebug";
            _clipPlaneComplexMaterial.name = "LIV.clipPlaneComplex";
            _clipPlaneComplexDebugMaterial.name = "LIV.clipPlaneComplexDebug";
            _writeOpaqueToAlphaMaterial.name = "LIV.writeOpaqueToAlpha";
            _combineAlphaMaterial.name = "LIV.combineAlpha";
            _writeMaterial.name = "LIV.write";
            _forceForwardRenderingMaterial.name = "LIV.forceForwardRendering";
            _clipPlaneCommandBuffer.name = "LIV.renderClipPlanes";
            _combineAlphaCommandBuffer.name = "LIV.foregroundCombineAlpha";
            _captureTextureCommandBuffer.name = "LIV.captureTexture";
            _applyTextureCommandBuffer.name = "LIV.applyTexture";
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

            SDKUtils.DestroyObject<Mesh>(ref _clipPlaneMesh);
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
            SDKUtils.DisposeObject<CommandBuffer>(ref _captureTextureCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyTextureCommandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _optimizedRenderingCommandBuffer);
        }

        public void Dispose()
        {
            ReleaseBridgePoseControl();
            DestroyAssets();
            SDKUtils.DestroyTexture(ref _backgroundRenderTexture);
            SDKUtils.DestroyTexture(ref _foregroundRenderTexture);
            SDKUtils.DestroyTexture(ref _optimizedRenderTexture);
            SDKUtils.DestroyTexture(ref _complexClipPlaneRenderTexture);
        }
    }
}
#endif