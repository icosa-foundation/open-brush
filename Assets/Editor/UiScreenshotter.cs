// Copyright 2023 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TiltBrush
{
    public class UiScreenshotter : Editor
    {
        private const float kBrushScreenshotTime = 0.5f;
        private const int kScreenshotSupersampling = 2;
        private const int kScreenshotMsaaSamples = 4;
        private const string kScreenshotOutputDirectory = "Support/Screenshots";
        private const string kLogPrefix = "_ui_screenshotter_20260520_";
        private const string kUrpPostLogPrefix = "[OB_URP_POST]";

        private enum BrushScreenshotRenderMode
        {
            Material,
            Wireframe
        }

        private static readonly string[] kWireframeWhiteColorProperties =
        {
            "_Color",
            "_MainColor",
            "_BaseColor",
            "_TintColor",
            "_SpecColor",
            "__SpecColor",
            "_Specular_Color",
            "_EmissionColor"
        };

        private struct MaterialColorOverride
        {
            public Material Material;
            public string PropertyName;
            public Color Color;

            public MaterialColorOverride(Material material, string propertyName, Color color)
            {
                Material = material;
                PropertyName = propertyName;
                Color = color;
            }
        }

        private static bool IsPlaying()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("You can only run this whilst in Play Mode");
                return false;
            }
            return true;
        }

        [MenuItem("Open Brush/Screenshots/Generate Brush Screenshots")]
        static void GenerateBrushScreenShots()
        {
            GenerateBrushScreenShots(enablePostProcessing: false, BrushScreenshotRenderMode.Material);
        }

        [MenuItem("Open Brush/Screenshots/Generate Brush Screenshots With Post Effects")]
        static void GenerateBrushScreenShotsWithPostEffects()
        {
            GenerateBrushScreenShots(enablePostProcessing: true, BrushScreenshotRenderMode.Material);
        }

        [MenuItem("Open Brush/Screenshots/Generate Brush Wireframe Screenshots")]
        static void GenerateBrushWireframeScreenShots()
        {
            GenerateBrushScreenShots(enablePostProcessing: false, BrushScreenshotRenderMode.Wireframe);
        }

        private static void GenerateBrushScreenShots(
            bool enablePostProcessing,
            BrushScreenshotRenderMode renderMode)
        {
            if (!IsPlaying()) return;

            if (renderMode == BrushScreenshotRenderMode.Wireframe)
            {
                enablePostProcessing = false;
            }

            SetupBlackEnvironment();

            DelayedGenerateBrushScreenShots(enablePostProcessing, renderMode);
        }

        [MenuItem("Open Brush/Screenshots/Generate Environment Screenshots")]
        static void GenerateEnvironmentScreenshots()
        {
            if (!IsPlaying()) return;
            DelayedGenerateEnvironmentScreenshots();
        }

        [MenuItem("Open Brush/Screenshots/Generate Panel Screenshots")]
        static void GeneratePanelScreenshots()
        {
            if (!IsPlaying()) return;

            SetupBlackEnvironment();

            foreach (BasePanel.PanelType panelType in (BasePanel.PanelType[])Enum.GetValues(typeof(BasePanel.PanelType)))
            {
                if (!PanelManager.m_Instance.IsPanelOpen(panelType))
                {
                    PanelManager.m_Instance.OpenPanel(panelType, TrTransform.T(new Vector3(0, 50, 2)));
                }
            }
            DelayedGeneratePanelScreenshots();
        }

        private static void SetupBlackEnvironment()
        {
            var blackGuid = Guid.Parse("580b4529-ac50-4fe9-b8d2-635765a14893");
            var env = EnvironmentCatalog.m_Instance.GetEnvironment(blackGuid);
            SceneSettings.m_Instance.SetDesiredPreset(env,
                keepSceneTransform: true, forceTransition: false, hasCustomLights: false, skipFade: true);
        }

        async static void DelayedGenerateEnvironmentScreenshots()
        {
            ApiMethods.ViewOnly();
            PanelManager.m_Instance.HideAllPanels();
            await Task.Delay(1000);
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 10, -5);
            cam.transform.rotation = Quaternion.identity;
            cam.fieldOfView = 110;
            cam.aspect = 1;
            foreach (var env in EnvironmentCatalog.m_Instance.AllEnvironments)
            {
                SceneSettings.m_Instance.SetDesiredPreset(env,
                    keepSceneTransform: true, forceTransition: false, hasCustomLights: false, skipFade: true);
                await Task.Delay(1000);
                SaveCurrentView(cam, $"environment-{env.Description}.png", 1024, 1024);
            }
        }

        async static void DelayedGenerateBrushScreenShots(
            bool enablePostProcessing,
            BrushScreenshotRenderMode renderMode)
        {
            await Task.Delay(3000);
            var cam = InitScreenshotCamera();

            var path = new List<TrTransform>();
            var origin = new Vector3(-1.25f, 100, 4);
            for (float i = 0; i < 3; i += 0.1f)
            {
                path.Add(TrTransform.T(new Vector3(i, Mathf.Sin(i * 5f) * (1 - i / 3), 0)));
            }

            var batchManager = App.Scene.ActiveCanvas.BatchManager;
            bool wasOneStrokePerBatch = batchManager.OneStrokePerBatch;
            bool wasForceDeterministicBirthTimeForExport = App.Config.m_ForceDeterministicBirthTimeForExport;
            bool setCameraConfigPostEffects = renderMode != BrushScreenshotRenderMode.Wireframe;
            bool wasPostEffects = CameraConfig.PostEffects;
            batchManager.OneStrokePerBatch = true;
            App.Config.m_ForceDeterministicBirthTimeForExport = true;
            if (setCameraConfigPostEffects)
            {
                CameraConfig.PostEffects = enablePostProcessing;
            }

            try
            {
                foreach (var brush in BrushCatalog.m_Instance.GetTagFilteredBrushList())
                {
                    if (!CanGenerateBrushScreenshot(brush))
                    {
                        continue;
                    }
                    PointerManager.m_Instance.SetBrushForAllPointers(brush);
                    await Task.Delay(100);
                    var strokes = DrawStrokes.DrawNestedTrList(
                        new List<IEnumerable<TrTransform>> { path },
                        TrTransform.T(origin));
                    SetFixedShaderTime(strokes, kBrushScreenshotTime);
                    batchManager.FlushMeshUpdates();
                    List<MaterialColorOverride> colorOverrides = null;
                    try
                    {
                        if (renderMode == BrushScreenshotRenderMode.Wireframe)
                        {
                            colorOverrides = SetBrushMaterialColors(strokes, Color.white);
                        }
                        SaveCurrentView(
                            cam,
                            GetBrushScreenshotFileName(brush, renderMode),
                            1024,
                            1024,
                            enablePostProcessing,
                            renderMode == BrushScreenshotRenderMode.Wireframe);
                    }
                    finally
                    {
                        RestoreBrushMaterialColors(colorOverrides);
                        DeleteStrokes(strokes);
                    }
                }
            }
            finally
            {
                App.Config.m_ForceDeterministicBirthTimeForExport = wasForceDeterministicBirthTimeForExport;
                if (setCameraConfigPostEffects)
                {
                    CameraConfig.PostEffects = wasPostEffects;
                }
                batchManager.OneStrokePerBatch = wasOneStrokePerBatch;
            }
        }

        private static bool CanGenerateBrushScreenshot(BrushDescriptor brush)
        {
            if (brush == null)
            {
                Debug.LogWarning($"{kLogPrefix} Skipping null brush descriptor.");
                return false;
            }

            try
            {
                Material material = brush.Material;
                if (material == null || !material)
                {
                    Debug.LogWarning(
                        $"{kLogPrefix} Skipping brush '{brush.name}' ({brush.m_DurableName}, {brush.m_Guid}) " +
                        "because its material is missing.");
                    return false;
                }
            }
            catch (MissingReferenceException exception)
            {
                Debug.LogWarning(
                    $"{kLogPrefix} Skipping brush '{brush.name}' ({brush.m_DurableName}, {brush.m_Guid}) " +
                    $"because its material reference is invalid: {exception.Message}");
                return false;
            }

            return true;
        }

        private static string GetBrushScreenshotFileName(
            BrushDescriptor brush,
            BrushScreenshotRenderMode renderMode)
        {
            string suffix = renderMode == BrushScreenshotRenderMode.Wireframe
                ? "-wireframe"
                : "";
            return $"brush-{brush.DurableName}{suffix}.png";
        }

        private static List<MaterialColorOverride> SetBrushMaterialColors(
            IEnumerable<Stroke> strokes,
            Color color)
        {
            var overrides = new List<MaterialColorOverride>();
            var seenMaterials = new HashSet<int>();
            foreach (var stroke in strokes)
            {
                if (stroke == null ||
                    stroke.m_BatchSubset == null ||
                    stroke.m_BatchSubset.m_ParentBatch == null)
                {
                    continue;
                }

                Material material = stroke.m_BatchSubset.m_ParentBatch.InstantiatedMaterial;
                if (material == null || !seenMaterials.Add(material.GetInstanceID()))
                {
                    continue;
                }

                foreach (string propertyName in kWireframeWhiteColorProperties)
                {
                    if (!material.HasColor(propertyName))
                    {
                        continue;
                    }
                    overrides.Add(new MaterialColorOverride(
                        material,
                        propertyName,
                        material.GetColor(propertyName)));
                    material.SetColor(propertyName, color);
                }
            }
            return overrides;
        }

        private static void RestoreBrushMaterialColors(IEnumerable<MaterialColorOverride> overrides)
        {
            if (overrides == null)
            {
                return;
            }

            foreach (var colorOverride in overrides)
            {
                if (colorOverride.Material != null &&
                    colorOverride.Material.HasColor(colorOverride.PropertyName))
                {
                    colorOverride.Material.SetColor(
                        colorOverride.PropertyName,
                        colorOverride.Color);
                }
            }
        }

        async static void DelayedGeneratePanelScreenshots()
        {
            await Task.Delay(3000);

            var cam = InitScreenshotCamera();

            int count = ((BasePanel.PanelType[])Enum.GetValues(typeof(BasePanel.PanelType))).Length;
            Debug.Log($"Starting {count} panel screenshots");
            for (var i = 0; i < count; i++)
            {
                var panelType = ((BasePanel.PanelType[])Enum.GetValues(typeof(BasePanel.PanelType)))[i];
                Debug.Log($"Screenshot {i}: {panelType}");
                TrTransform panelTr = TrTransform.T(new Vector3(-1.25f, 100, 4));
                if (PanelManager.m_Instance.IsPanelOpen(panelType))
                {
                    BasePanel panel = PanelManager.m_Instance.GetPanelByType(panelType);
                    panel.PanelGazeActive(true);
                    await Task.Delay(500);
                    var originalTransform = TrTransform.FromTransform(panel.transform);
                    panelTr.ToTransform(panel.transform);
                    panel.ResetReticleOffset();
                    SaveCurrentView(cam, $"panel-{panelType}.png", 1600, 1600);

                    // Try to open popups
                    FieldInfo fieldInfo = typeof(BasePanel).GetField("m_PanelPopUpMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    PopupMapKey[] popupMap = (PopupMapKey[])fieldInfo?.GetValue(panel);
                    if (popupMap != null)
                    {
                        foreach (var popup in popupMap)
                        {
                            var btn = panel.GetComponentsInChildren<OptionButton>()
                                .FirstOrDefault(x => x.m_Command == popup.m_Command);
                            if (btn == null)
                            {
                                Debug.LogWarning($"No button found for {popup.m_Command}");
                                continue;
                            }
                            Debug.Log($"Screenshop popup for {popup.m_Command}");
                            GameObject go = Instantiate(popup.m_PopUpPrefab,
                                btn.transform.position + new Vector3(.5f, 0, -0.25f), btn.transform.rotation);
                            go.transform.localScale = Vector3.one * 5;
                            var activePopUp = go.GetComponent<PopUpWindow>();
                            activePopUp.Init(panel.gameObject, "");
                            try
                            {
                                activePopUp.SetPopupCommandParameters(btn.m_CommandParam, btn.m_CommandParam2);
                            }
                            catch (NullReferenceException e) { }
                            SaveCurrentView(cam, $"panel-{panelType}_{btn.m_Command}.png", 1600, 1600);
                            go.transform.position = new Vector3(-100, 0, 0);
                            Destroy(go);
                        }
                    }
                    originalTransform.ToTransform(panel.transform);
                }
            }
        }

        private static Camera InitScreenshotCamera()
        {
            var cam = Camera.main;
            cam.transform.position = new Vector3(0, 100, 0);
            cam.transform.rotation = Quaternion.identity;
            return cam;
        }

        private static void SetFixedShaderTime(IEnumerable<Stroke> strokes, float time)
        {
            Vector4 timeValue = new Vector4(time / 20f, time, time * 2f, time * 3f);
            foreach (var stroke in strokes)
            {
                try
                {
                    var material = stroke.m_BatchSubset.m_ParentBatch.InstantiatedMaterial;
                    material.EnableKeyword("SHADER_SCRIPTING_ON");
                    if (!material.HasFloat("_TimeBlend") ||
                        !material.HasVector("_TimeOverrideValue"))
                    {
                        continue;
                    }
                    stroke.SetShaderFloat("_TimeBlend", 1f);
                    stroke.SetShaderVector(
                        "_TimeOverrideValue",
                        timeValue.x,
                        timeValue.y,
                        timeValue.z,
                        timeValue.w);
                }
                catch (StrokeShaderModifierException)
                {
                    // Static brushes do not expose the time override properties.
                }
            }
        }

        private static void DeleteStrokes(IEnumerable<Stroke> strokes)
        {
            foreach (var stroke in strokes)
            {
                SketchMemoryScript.m_Instance.RemoveMemoryObject(stroke);
                stroke.Uncreate();
            }
        }

        static void SaveCurrentView(
            Camera cameraToCapture,
            string fileName,
            int resWidth,
            int resHeight,
            bool? enablePostProcessing = null,
            bool renderWireframe = false)
        {
            int renderWidth = resWidth * kScreenshotSupersampling;
            int renderHeight = resHeight * kScreenshotSupersampling;
            RenderTextureFormat sourceFormat = enablePostProcessing == true
                ? RenderTextureFormat.DefaultHDR
                : RenderTextureFormat.ARGB32;
            RenderTexture rt = new RenderTexture(renderWidth, renderHeight, 24, sourceFormat)
            {
                antiAliasing = kScreenshotMsaaSamples,
                filterMode = FilterMode.Bilinear
            };
            RenderTexture downsampledRt = new RenderTexture(resWidth, resHeight, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear
            };
            Texture2D screenShot = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = cameraToCapture.targetTexture;
            bool previousAllowMsaa = cameraToCapture.allowMSAA;
            bool previousAllowHdr = cameraToCapture.allowHDR;
            UniversalAdditionalCameraData cameraData =
                cameraToCapture.GetComponent<UniversalAdditionalCameraData>();
            bool hadCameraData = cameraData != null;
            bool previousRenderPostProcessing = false;
            Transform previousVolumeTrigger = null;
            LayerMask previousVolumeLayerMask = default;
            try
            {
                if (enablePostProcessing == true && cameraData == null)
                {
                    cameraData = cameraToCapture.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    Debug.Log(
                        $"{kUrpPostLogPrefix} Added UniversalAdditionalCameraData to brush screenshot camera.");
                }

                if (cameraData != null && enablePostProcessing.HasValue)
                {
                    previousRenderPostProcessing = cameraData.renderPostProcessing;
                    previousVolumeTrigger = cameraData.volumeTrigger;
                    previousVolumeLayerMask = cameraData.volumeLayerMask;
                    cameraData.renderPostProcessing = enablePostProcessing.Value;
                    if (enablePostProcessing.Value)
                    {
                        cameraData.volumeTrigger = cameraToCapture.transform;
                        cameraData.volumeLayerMask = ~0;
                    }
                }

                cameraToCapture.allowMSAA = true;
                cameraToCapture.allowHDR = enablePostProcessing == true || previousAllowHdr;
                cameraToCapture.targetTexture = rt;
                screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
                RenderScreenshotCamera(cameraToCapture, renderWidth, renderHeight, renderWireframe);
                Graphics.Blit(rt, downsampledRt);
                RenderTexture.active = downsampledRt;
                screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                byte[] bytes = screenShot.EncodeToPNG();
                string outputDirectory = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    kScreenshotOutputDirectory);
                Directory.CreateDirectory(outputDirectory);
                string filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(filePath, bytes);
            }
            finally
            {
                cameraToCapture.targetTexture = previousTarget;
                cameraToCapture.allowMSAA = previousAllowMsaa;
                cameraToCapture.allowHDR = previousAllowHdr;
                if (cameraData != null && enablePostProcessing.HasValue)
                {
                    cameraData.renderPostProcessing = previousRenderPostProcessing;
                    cameraData.volumeTrigger = previousVolumeTrigger;
                    cameraData.volumeLayerMask = previousVolumeLayerMask;
                    if (!hadCameraData)
                    {
                        Destroy(cameraData);
                    }
                }
                RenderTexture.active = previousActive;
                if (screenShot != null)
                {
                    Destroy(screenShot);
                }
                Destroy(rt);
                Destroy(downsampledRt);
            }
        }

        private static void RenderScreenshotCamera(
            Camera cameraToCapture,
            int renderWidth,
            int renderHeight,
            bool renderWireframe)
        {
            if (!renderWireframe)
            {
                cameraToCapture.Render();
                return;
            }

            bool previousWireframe = GL.wireframe;
            try
            {
                GL.wireframe = true;
                cameraToCapture.Render();
            }
            finally
            {
                GL.wireframe = previousWireframe;
            }
        }
    }
}
