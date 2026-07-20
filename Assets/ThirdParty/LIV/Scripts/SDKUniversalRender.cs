#if LIV_UNIVERSAL_RENDER
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LIV.SDK.Unity
{
    class FixPostEffectsPass : IDisposable
    {
        private RenderPassEvent _captureTextureRenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        private RenderPassEvent _applyTextureRenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        // Captures texture before post-effects
        private SDKPass _captureTexturePass = null;
        // Renders captured texture
        private SDKPass _applyTexturePass = null;
        private RenderTexture tempRenderTexture = null;

        public FixPostEffectsPass()
        {
            _captureTexturePass = new SDKPass();
            _captureTexturePass.renderPassEvent = _captureTextureRenderPassEvent;
            _captureTexturePass.commandBuffer = new CommandBuffer();

            _applyTexturePass = new SDKPass();
            _applyTexturePass.renderPassEvent = _applyTextureRenderPassEvent;
            _applyTexturePass.commandBuffer = new CommandBuffer();

#if UNITY_EDITOR
            _captureTexturePass.commandBuffer.name = "LIV.captureTexture";
            _applyTexturePass.commandBuffer.name = "LIV.applyTexture";
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
            _captureTexturePass.commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, tempRenderTexture);
            SDKUniversalRenderFeature.AddPass(_captureTexturePass);

            writeMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, fixColor ? (int)ColorWriteMask.All : (int)ColorWriteMask.Alpha);
            _applyTexturePass.commandBuffer.Blit(tempRenderTexture, BuiltinRenderTextureType.CurrentActive, writeMaterial);
            SDKUniversalRenderFeature.AddPass(_applyTexturePass);
        }

        public void Release(Camera camera)
        {
            _captureTexturePass.commandBuffer.Clear();
            _applyTexturePass.commandBuffer.Clear();

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

            SDKUtils.DisposeObject<CommandBuffer>(ref _captureTexturePass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _applyTexturePass.commandBuffer);
        }
    }

    public partial class SDKRender : System.IDisposable
    {
        // Renders the clip plane in the foreground texture
        private SDKPass _clipPlanePass = null;
        // Renders the clipped opaque content in to the foreground texture alpha
        private SDKPass _combineAlphaPass = null;
        // Renders background and foreground in single render
        private SDKPass _optimizedRenderingPass = null;

        private RenderPassEvent _clipPlaneRenderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        private RenderPassEvent _addAlphaRenderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        private RenderPassEvent _optimizedRenderingPassEvent = RenderPassEvent.AfterRenderingTransparents;

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

        private UniversalAdditionalCameraData _universalAdditionalCameraData = null;
        private RenderTargetIdentifier _cameraColorTextureIdentifier = new RenderTargetIdentifier("_CameraColorTexture");

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

            bool debugClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            bool debug = debugClipPlane;

            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, renderingLayerMask);
            _cameraInstance.targetTexture = _backgroundRenderTexture;

            bool fixPostEffects = overridePostProcessing || fixPostEffectsAlpha;
            if (fixPostEffects)
            {
                _fixPostEffectsPass.Render(_cameraInstance, _backgroundRenderTexture, _quadMesh, _writeMaterial, overridePostProcessing);
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
            SendTextureToBridge(_backgroundRenderTexture, TEXTURE_ID.BACKGROUND_COLOR_BUFFER_ID);
            if (debug) RenderDebugPostRender(_backgroundRenderTexture);
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            if (fixPostEffects)
            {
                _fixPostEffectsPass.Release(_cameraInstance);
            }

            SDKUniversalRenderFeature.ClearPasses();
            _captureCameraSettings.Release(_cameraInstance);
        }

        // Extract the image which is in front of our clip plane
        // The compositing is heavily relying on the alpha channel, therefore we want to make sure it does
        // not get corrupted by the postprocessing or any shader
        private void RenderForeground()
        {
            if (!CreateCamera())
                return;

            bool debugClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            bool renderComplexClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE);
            bool renderGroundClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.GROUND_CLIP_PLANE);
            bool overridePostProcessing = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.OVERRIDE_POST_PROCESSING);
            bool fixPostEffectsAlpha = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.FIX_FOREGROUND_ALPHA) | _livDescriptor.fixPostEffectsAlpha;
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
                _clipPlanePass.commandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _writeOpaqueToAlphaMaterial, 0, 0);
            }

            // Render clip plane
            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            _clipPlanePass.commandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, renderComplexClipPlane, ColorWriteMask.All, ref _clipPlaneMaterialProperty), 0, 0, _clipPlaneMaterialProperty);

            // Render ground clip plane
            if (renderGroundClipPlane)
            {
                Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
                _clipPlanePass.commandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform,
                    GetClipPlaneMaterial(debugClipPlane, false, ColorWriteMask.All, ref _groundPlaneMaterialProperty), 0, 0, _groundPlaneMaterialProperty);
            }

            // Copy alpha in to texture
            _clipPlanePass.commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);
            _clipPlanePass.commandBuffer.SetRenderTarget(_cameraColorTextureIdentifier);
            SDKUniversalRenderFeature.AddPass(_clipPlanePass);

            // Fix alpha corruption by post processing
            bool fixPostEffects = overridePostProcessing || fixPostEffectsAlpha;
            if (fixPostEffects)
            {
                _fixPostEffectsPass.Render(_cameraInstance, _foregroundRenderTexture, _quadMesh, _writeMaterial, overridePostProcessing);
            }

            // Combine captured alpha with result alpha
            _combineAlphaMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _combineAlphaMaterial.mainTexture = capturedAlphaRenderTexture;
            _combineAlphaPass.commandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _combineAlphaMaterial);
            SDKUniversalRenderFeature.AddPass(_combineAlphaPass);

            if (useDeferredRendering) SDKUtils.ForceForwardRendering(cameraInstance, _clipPlaneMesh, _forceForwardRenderingMaterial);

            SDKShaders.StartRendering();
            SDKShaders.StartForegroundRendering();
            InvokePreRenderForeground();
            if (debug) RenderDebugPreRender();
            _cameraInstance.Render();
            InvokePostRenderForeground();
            SendTextureToBridge(_foregroundRenderTexture, TEXTURE_ID.FOREGROUND_COLOR_BUFFER_ID);
            if (debug) RenderDebugPostRender(_foregroundRenderTexture);
            _cameraInstance.targetTexture = null;
            SDKShaders.StopForegroundRendering();
            SDKShaders.StopRendering();

            if (fixPostEffects)
            {
                _fixPostEffectsPass.Release(_cameraInstance);
            }

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);

            _clipPlanePass.commandBuffer.Clear();
            _combineAlphaPass.commandBuffer.Clear();

            SDKUniversalRenderFeature.ClearPasses();

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

            bool debugClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.DEBUG_CLIP_PLANE);
            bool renderComplexClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.COMPLEX_CLIP_PLANE);
            bool renderGroundClipPlane = SDKUtils.FeatureEnabled(inputFrame.features, FEATURES.GROUND_CLIP_PLANE);

            SDKUtils.SetCamera(_cameraInstance, _cameraInstance.transform, _inputFrame, localToWorldMatrix, renderingLayerMask);
            _cameraInstance.targetTexture = _optimizedRenderTexture;

            RenderTexture capturedAlphaRenderTexture = RenderTexture.GetTemporary(_optimizedRenderTexture.width, _optimizedRenderTexture.height, 0, _optimizedRenderTexture.format);
#if UNITY_EDITOR
            capturedAlphaRenderTexture.name = "LIV.CapturedAlphaRenderTexture";
#endif
            // Clear alpha channel
            _writeMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _optimizedRenderingPass.commandBuffer.DrawMesh(_quadMesh, Matrix4x4.identity, _writeMaterial);

            // Render opaque pixels into alpha channel            
            _writeOpaqueToAlphaMaterial.SetInt(SDKShaders.LIV_COLOR_MASK, (int)ColorWriteMask.Alpha);
            _optimizedRenderingPass.commandBuffer.DrawMesh(_clipPlaneMesh, Matrix4x4.identity, _writeOpaqueToAlphaMaterial, 0, 0);

            // Render clip plane            
            Matrix4x4 clipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.clipPlane.transform;
            _optimizedRenderingPass.commandBuffer.DrawMesh(_clipPlaneMesh, clipPlaneTransform,
                GetClipPlaneMaterial(debugClipPlane, renderComplexClipPlane, ColorWriteMask.Alpha, ref _clipPlaneMaterialProperty), 0, 0, _clipPlaneMaterialProperty);

            // Render ground clip plane            
            if (renderGroundClipPlane)
            {
                Matrix4x4 groundClipPlaneTransform = localToWorldMatrix * (Matrix4x4)_inputFrame.groundClipPlane.transform;
                _optimizedRenderingPass.commandBuffer.DrawMesh(_clipPlaneMesh, groundClipPlaneTransform,
                    GetClipPlaneMaterial(debugClipPlane, false, ColorWriteMask.Alpha, ref _groundPlaneMaterialProperty), 0, 0, _groundPlaneMaterialProperty);
            }

            // Copy alpha in to texture
            _optimizedRenderingPass.commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, capturedAlphaRenderTexture);
            _optimizedRenderingPass.commandBuffer.SetRenderTarget(_cameraColorTextureIdentifier);
            SDKUniversalRenderFeature.AddPass(_optimizedRenderingPass);

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
            if (debugClipPlane) RenderDebugPreRender();
            _cameraInstance.Render();

            // Recover alpha
            RenderBuffer activeColorBuffer = Graphics.activeColorBuffer;
            RenderBuffer activeDepthBuffer = Graphics.activeDepthBuffer;
            Graphics.Blit(capturedAlphaRenderTexture, _optimizedRenderTexture, _writeMaterial);
            Graphics.SetRenderTarget(activeColorBuffer, activeDepthBuffer);
            if (debugClipPlane) RenderDebugPostRender(_optimizedRenderTexture);
            InvokePostRenderBackground();
            SendTextureToBridge(_optimizedRenderTexture, TEXTURE_ID.OPTIMIZED_COLOR_BUFFER_ID);
            _cameraInstance.targetTexture = null;
            SDKShaders.StopBackgroundRendering();
            SDKShaders.StopRendering();

            RenderTexture.ReleaseTemporary(capturedAlphaRenderTexture);

            _optimizedRenderingPass.commandBuffer.Clear();
            _combineAlphaPass.commandBuffer.Clear();

            SDKUniversalRenderFeature.ClearPasses();
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
                Debug.LogWarning("LIV URP Renderer: camera reference is missing!");
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

            UpdateUniversalAdditionalCameraData(ref _universalAdditionalCameraData, _cameraInstance);
            AddRenderFeature(SDKUniversalRenderFeature.instance);

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
            _writeOpaqueToAlphaMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_WRITE_OPAQUE_TO_ALPHA_SHADER));
            _combineAlphaMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_COMBINE_ALPHA_SHADER));
            _writeMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_WRITE_SHADER));
            _forceForwardRenderingMaterial = new Material(SDKShaders.GetShader(SDKShaders.LIV_FORCE_FORWARD_RENDERING_SHADER));

            _clipPlanePass = new SDKPass();
            _clipPlanePass.renderPassEvent = _clipPlaneRenderPassEvent;
            _clipPlanePass.commandBuffer = new CommandBuffer();

            _combineAlphaPass = new SDKPass();
            _combineAlphaPass.renderPassEvent = _addAlphaRenderPassEvent;
            _combineAlphaPass.commandBuffer = new CommandBuffer();

            _optimizedRenderingPass = new SDKPass();
            _optimizedRenderingPass.renderPassEvent = _optimizedRenderingPassEvent;
            _optimizedRenderingPass.commandBuffer = new CommandBuffer();

            _clipPlaneMaterialProperty = new MaterialPropertyBlock();
            _clipPlaneMaterialProperty.SetColor(SDKShaders.LIV_COLOR, SDKShaders.GREEN_COLOR);
            _groundPlaneMaterialProperty = new MaterialPropertyBlock();
            _groundPlaneMaterialProperty.SetColor(SDKShaders.LIV_COLOR, SDKShaders.BLUE_COLOR);
            _hmdMaterialProperty = new MaterialPropertyBlock();
            _hmdMaterialProperty.SetColor(SDKShaders.LIV_COLOR, SDKShaders.RED_COLOR);

            _captureCameraSettings = new CaptureCameraSettings();
            _fixPostEffectsPass = new FixPostEffectsPass();

#if UNITY_EDITOR
            _quadMesh.name = "LIV.quad";
            _clipPlaneMesh.name = "LIV.clipPlane";
            _clipPlaneSimpleMaterial.name = "LIV.clipPlaneSimple";
            _clipPlaneSimpleDebugMaterial.name = "LIV.clipPlaneSimpleDebug";
            _clipPlaneComplexMaterial.name = "LIV.clipPlaneComplex";
            _clipPlaneComplexDebugMaterial.name = "LIV.clipPlaneComplexDebug";
            _writeOpaqueToAlphaMaterial.name = "LIV.writeOpaqueToAlpha";
            _combineAlphaMaterial.name = "LIV.combineAlpha";
            _writeMaterial.name = "LIV.write";
            _forceForwardRenderingMaterial.name = "LIV.forceForwardRendering";
            _clipPlanePass.commandBuffer.name = "LIV.renderClipPlanes";
            _combineAlphaPass.commandBuffer.name = "LIV.foregroundCombineAlpha";
            _optimizedRenderingPass.commandBuffer.name = "LIV.optimizedRendering";
#endif
        }

        static void UpdateUniversalAdditionalCameraData(ref UniversalAdditionalCameraData universalAdditionalCameraData, Camera cameraInstance)
        {
            if (universalAdditionalCameraData == null)
                universalAdditionalCameraData = cameraInstance.GetComponent<UniversalAdditionalCameraData>();

            if (universalAdditionalCameraData != null)
            {
                universalAdditionalCameraData.antialiasing = AntialiasingMode.None;
                universalAdditionalCameraData.antialiasingQuality = AntialiasingQuality.Low;
                universalAdditionalCameraData.dithering = false;
            }
        }

        static ScriptableRendererData GetScriptableRenderData(UniversalRenderPipelineAsset urpAsset)
        {
            ScriptableRendererData[] scriptableRendererData = (ScriptableRendererData[])typeof(UniversalRenderPipelineAsset)
                .GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(urpAsset);

            if (scriptableRendererData == null || scriptableRendererData.Length == 0)
                return null;

            return scriptableRendererData[0];
        }

        static SDKUniversalRenderFeature AddRenderFeature(SDKUniversalRenderFeature sdkUniversalRenderFeature)
        {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Debug.LogError("LIV URP Renderer: cannot add render feature. No render pipeline found!");
                return null;
            }

            ScriptableRendererData scriptableRendererData = GetScriptableRenderData(urpAsset);
            if (scriptableRendererData.rendererFeatures.Contains(sdkUniversalRenderFeature))
                return sdkUniversalRenderFeature;

            scriptableRendererData.rendererFeatures.Add(sdkUniversalRenderFeature);
            scriptableRendererData.SetDirty();

            Debug.Log("LIV URP Renderer: AddRenderFeature");
            return sdkUniversalRenderFeature;
        }

        static void RemoveRenderFeature(SDKUniversalRenderFeature sdkUniversalRenderFeature)
        {
            UniversalRenderPipelineAsset urpAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urpAsset == null)
            {
                Debug.LogError("LIV URP Renderer: cannot remove render feature. No render pipeline found!");
                return;
            }

            ScriptableRendererData scriptableRendererData = GetScriptableRenderData(urpAsset);
            scriptableRendererData.rendererFeatures.Remove(sdkUniversalRenderFeature);
            scriptableRendererData.SetDirty();

            Debug.Log("LIV URP Renderer: RemoveRenderFeature");
        }

        private void DestroyAssets()
        {
            RemoveRenderFeature(SDKUniversalRenderFeature.instance);

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

            SDKUtils.DisposeObject<CommandBuffer>(ref _clipPlanePass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _combineAlphaPass.commandBuffer);
            SDKUtils.DisposeObject<CommandBuffer>(ref _optimizedRenderingPass.commandBuffer);

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