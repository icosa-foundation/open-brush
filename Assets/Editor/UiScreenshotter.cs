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

namespace TiltBrush
{
    public class UiScreenshotter : Editor
    {
        private const float kBrushReferenceTime = 0.5f;
        private const float kBrushReferenceSize = 0.1125f;
        private const int kScreenshotSupersampling = 2;
        private const int kScreenshotMsaaSamples = 4;
        private const string kScreenshotOutputDirectory = "Support/Screenshots";
        private const string kPostEffectsDisabledDirectory = "brushes-postfx-disabled";
        private const string kPostEffectsEnabledDirectory = "brushes-postfx-enabled";
        private const string kWireframeDirectory = "brushes-wireframe";
        private const string kAaRawDirectory = "brushes-postfx-disabled-aa-raw";
        private const string kAaSupersampledDirectory =
            "brushes-postfx-disabled-aa-supersampled";
        private const string kAaFullDirectory = "brushes-postfx-disabled-aa-full";
        private const string kRainbowLod150Directory =
            "brushes-postfx-disabled-aa-rainbow-lod150";
        private const string kRainbowLodUnlimitedDirectory =
            "brushes-postfx-disabled-aa-rainbow-lod-unlimited";
        private const string kAaDiagnosticLogPrefix = "_brush_aa_diagnostic_20260901_";
        private const string kParityCaptureLogPrefix = "_brush_parity_capture_20260901_";
        private const string kToonOutlineLogPrefix = "_toon_outline_diagnostic_20260901_";
        private const string kToonOutlineDirectory = "brushes-toon-outline-diagnostic";
        private const string kMeshFixtureOutputDirectory = "Support/BrushFixtures";
        private static readonly Color kBrushReferenceColor =
            new Color32(51, 51, 230, 255);
        private static readonly HashSet<string> kAaDiagnosticBrushes = new HashSet<string>
        {
            "Electricity",
            "Marker",
            "Rainbow"
        };
        private static readonly HashSet<string> kToonOutlineDiagnosticBrushes = new HashSet<string>
        {
            "Toon",
            "TubeToonInverted"
        };

        private enum BrushScreenshotRenderMode
        {
            Material,
            Wireframe,
            ToonOutlineDiagnostic
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

        [MenuItem("Open Brush/Screenshots/Generate Brush AA Diagnostic Screenshots")]
        static void GenerateBrushAaDiagnosticScreenShots()
        {
            GenerateBrushScreenShots(
                enablePostProcessing: false,
                BrushScreenshotRenderMode.Material,
                captureAaDiagnostics: true);
        }

        [MenuItem("Open Brush/Screenshots/Generate Toon Outline Diagnostic Screenshots")]
        static void GenerateToonOutlineDiagnosticScreenShots()
        {
            GenerateBrushScreenShots(
                enablePostProcessing: false,
                BrushScreenshotRenderMode.ToonOutlineDiagnostic);
        }

        // Run in Play Mode. Writes one brush-<durable-name>.mesh.json fixture and,
        // when the stroke has geometry, one GLB per catalog brush to
        // Support/BrushFixtures. Each JSON file records the deterministic stroke
        // input, vertex layout, material state, finalized live mesh, and the mesh
        // after the configured BrushBaker mapping. The live mesh is the realtime
        // and .tilt reference; the actual UnityGLTF GLB is the end-to-end import
        // reference; the post-BrushBaker JSON is only a diagnostic intermediate.
        // Empty meshes are recorded as skipped and any stale GLB is removed.
        [MenuItem("Open Brush/Screenshots/Generate Brush Mesh Fixtures")]
        static void GenerateBrushMeshFixtures()
        {
            GenerateBrushScreenShots(
                enablePostProcessing: false,
                BrushScreenshotRenderMode.Material,
                captureScreenshots: false,
                captureMeshFixtures: true);
        }

        private static void GenerateBrushScreenShots(
            bool enablePostProcessing,
            BrushScreenshotRenderMode renderMode,
            bool captureScreenshots = true,
            bool captureMeshFixtures = false,
            bool captureAaDiagnostics = false)
        {
            if (!IsPlaying()) return;

            if (captureMeshFixtures && BrushBaker.m_Instance == null)
            {
                Debug.LogError(
                    "[BrushMeshFixture] BrushBaker is not available in the active scene.");
                return;
            }

            if (renderMode == BrushScreenshotRenderMode.Wireframe)
            {
                enablePostProcessing = false;
            }

            SetupBlackEnvironment();

            DelayedGenerateBrushScreenShots(
                enablePostProcessing,
                renderMode,
                captureScreenshots,
                captureMeshFixtures,
                captureAaDiagnostics);
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
            BrushScreenshotRenderMode renderMode,
            bool captureScreenshots,
            bool captureMeshFixtures,
            bool captureAaDiagnostics)
        {
            await Task.Delay(3000);
            var cam = captureScreenshots ? InitScreenshotCamera() : null;
            bool overrideCameraBackground =
                cam != null && renderMode == BrushScreenshotRenderMode.ToonOutlineDiagnostic;
            CameraClearFlags previousClearFlags = default;
            Color previousBackgroundColor = default;
            if (overrideCameraBackground)
            {
                previousClearFlags = cam.clearFlags;
                previousBackgroundColor = cam.backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color32(128, 128, 128, 255);
            }

            if (captureScreenshots)
            {
                if (captureAaDiagnostics)
                {
                    Debug.Log(
                        $"{kAaDiagnosticLogPrefix} Generating raw, supersampled, and full-AA " +
                        "screenshots for Electricity, Marker, and Rainbow.");
                }
                else
                {
                    string screenshotDirectory = GetBrushScreenshotDirectory(
                        enablePostProcessing,
                        renderMode);
                    bool useParityCapture =
                        !enablePostProcessing &&
                        renderMode != BrushScreenshotRenderMode.Wireframe;
                    string captureSettings = useParityCapture
                        ? "1x resolution and 1x MSAA"
                        : $"{kScreenshotSupersampling}x resolution and " +
                          $"{kScreenshotMsaaSamples}x MSAA";
                    string logPrefix = renderMode == BrushScreenshotRenderMode.ToonOutlineDiagnostic
                        ? kToonOutlineLogPrefix
                        : kParityCaptureLogPrefix;
                    Debug.Log(
                        $"{logPrefix} Generating {renderMode} screenshots in " +
                        $"{screenshotDirectory} with post effects " +
                        $"{(enablePostProcessing ? "enabled" : "disabled")}, using " +
                        $"{captureSettings}.");
                }
            }

            var path = CreateBrushReferencePath();
            var origin = new Vector3(-1.25f, 100, 4);

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
                    if (captureAaDiagnostics &&
                        !kAaDiagnosticBrushes.Contains(brush.DurableName))
                    {
                        continue;
                    }
                    if (renderMode == BrushScreenshotRenderMode.ToonOutlineDiagnostic &&
                        !kToonOutlineDiagnosticBrushes.Contains(brush.DurableName))
                    {
                        continue;
                    }
                    PointerManager.m_Instance.SetBrushForAllPointers(brush);
                    await Task.Delay(100);
                    var colors = new List<Color> { kBrushReferenceColor };
                    float brushSize = Mathf.Clamp(
                        kBrushReferenceSize,
                        brush.m_BrushSizeRange.x,
                        brush.m_BrushSizeRange.y);
                    var strokes = DrawStrokes.DrawNestedTrList(
                        new List<IEnumerable<TrTransform>> { path },
                        TrTransform.T(origin),
                        colors,
                        brush: brush,
                        brushSize: brushSize);
                    await Task.Yield();
                    List<MaterialColorOverride> colorOverrides = null;
                    try
                    {
                        SetFixedShaderTime(strokes, kBrushReferenceTime);
                        batchManager.FlushMeshUpdates();
                        if (captureMeshFixtures)
                        {
                            BrushMeshFixtureWriter.WriteBrushFixture(
                                brush,
                                strokes,
                                kMeshFixtureOutputDirectory,
                                kBrushReferenceTime,
                                BrushBaker.m_Instance);
                        }
                        if (captureScreenshots && renderMode == BrushScreenshotRenderMode.Wireframe)
                        {
                            colorOverrides = SetBrushMaterialColors(strokes, Color.white);
                        }
                        if (captureScreenshots)
                        {
                            if (captureAaDiagnostics)
                            {
                                SaveBrushAaDiagnosticViews(
                                    cam,
                                    brush,
                                    enablePostProcessing,
                                    renderMode == BrushScreenshotRenderMode.Wireframe);
                            }
                            else
                            {
                                bool useParityCapture =
                                    !enablePostProcessing &&
                                    renderMode != BrushScreenshotRenderMode.Wireframe;
                                SaveCurrentView(
                                    cam,
                                    GetBrushScreenshotFileName(brush),
                                    1024,
                                    1024,
                                    enablePostProcessing,
                                    renderMode == BrushScreenshotRenderMode.Wireframe,
                                    GetBrushScreenshotDirectory(
                                        enablePostProcessing,
                                        renderMode),
                                    supersampling: useParityCapture
                                        ? 1
                                        : kScreenshotSupersampling,
                                    msaaSamples: useParityCapture
                                        ? 1
                                        : kScreenshotMsaaSamples);
                            }
                        }
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
                if (overrideCameraBackground)
                {
                    cam.clearFlags = previousClearFlags;
                    cam.backgroundColor = previousBackgroundColor;
                }
            }
        }

        // Screenshots and mesh fixtures must use the same stroke input so the
        // rendered reference image corresponds to the captured fixture geometry.
        private static List<TrTransform> CreateBrushReferencePath()
        {
            const int pointCount = 36;
            var path = new List<TrTransform>(pointCount);
            for (int index = 0; index < pointCount; ++index)
            {
                float t = index / (pointCount - 1f);
                float x = index <= 22
                    ? index * 0.1f
                    : 2.2f - (index - 22) * 0.075f;
                var position = new Vector3(
                    x,
                    0.55f * Mathf.Sin(index * 0.47f) + 0.012f * index,
                    0.38f * Mathf.Sin(index * 0.31f) + 0.009f * index);
                if (index == 10)
                {
                    position = path[path.Count - 1].translation +
                        new Vector3(0.00001f, 0.00004f, -0.00002f);
                }

                var orientation = Quaternion.Euler(
                    28f * Mathf.Sin(index * 0.23f),
                    65f * t,
                    140f * t + 18f * Mathf.Sin(index * 0.41f));
                float pressure = 0.25f + 0.75f *
                    (0.5f + 0.5f * Mathf.Sin(index * 0.37f - Mathf.PI * 0.5f));
                path.Add(TrTransform.TRS(position, orientation, pressure));
            }
            return path;
        }

        private static string GetBrushScreenshotFileName(BrushDescriptor brush)
        {
            return $"brush-{brush.DurableName}.png";
        }

        private static string GetBrushScreenshotDirectory(
            bool enablePostProcessing,
            BrushScreenshotRenderMode renderMode)
        {
            if (renderMode == BrushScreenshotRenderMode.Wireframe)
            {
                return kWireframeDirectory;
            }
            if (renderMode == BrushScreenshotRenderMode.ToonOutlineDiagnostic)
            {
                return kToonOutlineDirectory;
            }
            return enablePostProcessing
                ? kPostEffectsEnabledDirectory
                : kPostEffectsDisabledDirectory;
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
                            catch (NullReferenceException) { }
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
                    if (!material.HasFloat("_TimeBlend") ||
                        !material.HasVector("_TimeOverrideValue"))
                    {
                        continue;
                    }
                    material.EnableKeyword("SHADER_SCRIPTING_ON");
                    material.SetFloat("_TimeBlend", 1f);
                    material.SetVector("_TimeOverrideValue", timeValue);
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

        private static void SaveBrushAaDiagnosticViews(
            Camera cameraToCapture,
            BrushDescriptor brush,
            bool enablePostProcessing,
            bool renderWireframe)
        {
            string fileName = GetBrushScreenshotFileName(brush);
            SaveCurrentView(
                cameraToCapture,
                fileName,
                1024,
                1024,
                enablePostProcessing,
                renderWireframe,
                kAaRawDirectory,
                supersampling: 1,
                msaaSamples: 1);
            SaveCurrentView(
                cameraToCapture,
                fileName,
                1024,
                1024,
                enablePostProcessing,
                renderWireframe,
                kAaSupersampledDirectory,
                supersampling: 2,
                msaaSamples: 1);
            SaveCurrentView(
                cameraToCapture,
                fileName,
                1024,
                1024,
                enablePostProcessing,
                renderWireframe,
                kAaFullDirectory,
                supersampling: 2,
                msaaSamples: 4);

            if (brush.DurableName == "Rainbow")
            {
                int previousMaximumLod = Shader.globalMaximumLOD;
                try
                {
                    Debug.Log($"{kAaDiagnosticLogPrefix} Rainbow current global maximum LOD: {previousMaximumLod}. Capturing forced LOD 150 and unrestricted LOD.");
                    Shader.globalMaximumLOD = 150;
                    SaveCurrentView(
                        cameraToCapture,
                        fileName,
                        1024,
                        1024,
                        enablePostProcessing,
                        renderWireframe,
                        kRainbowLod150Directory,
                        supersampling: 1,
                        msaaSamples: 1);

                    Shader.globalMaximumLOD = 99999999;
                    SaveCurrentView(
                        cameraToCapture,
                        fileName,
                        1024,
                        1024,
                        enablePostProcessing,
                        renderWireframe,
                        kRainbowLodUnlimitedDirectory,
                        supersampling: 1,
                        msaaSamples: 1);
                }
                finally
                {
                    Shader.globalMaximumLOD = previousMaximumLod;
                }
            }
        }

        private static readonly Type[] kBuiltInPostEffectComponents =
        {
            typeof(RenderWrapper),
            typeof(MobileBloom),
            typeof(SENaturalBloomAndDirtyLens),
            typeof(TiltShift),
            typeof(Kino.Vignette)
        };

        private static List<KeyValuePair<Behaviour, bool>> SetBuiltInPostEffectsEnabled(
            Camera cameraToCapture, bool enabled)
        {
            var previousStates = new List<KeyValuePair<Behaviour, bool>>();
            if (cameraToCapture == null)
            {
                return previousStates;
            }

            foreach (Type componentType in kBuiltInPostEffectComponents)
            {
                var component = cameraToCapture.GetComponent(componentType) as Behaviour;
                if (component == null)
                {
                    continue;
                }
                previousStates.Add(new KeyValuePair<Behaviour, bool>(component, component.enabled));
                component.enabled = enabled;
            }
            return previousStates;
        }

        private static void RestoreBuiltInPostEffects(
            IEnumerable<KeyValuePair<Behaviour, bool>> previousStates)
        {
            foreach (var previousState in previousStates)
            {
                if (previousState.Key != null)
                {
                    previousState.Key.enabled = previousState.Value;
                }
            }
        }

        private static void SetKeyword(string keyword, bool enabled)
        {
            if (enabled)
            {
                Shader.EnableKeyword(keyword);
            }
            else
            {
                Shader.DisableKeyword(keyword);
            }
        }

        static void SaveCurrentView(
            Camera cameraToCapture,
            string fileName,
            int resWidth,
            int resHeight,
            bool? enablePostProcessing = null,
            bool renderWireframe = false,
            string outputSubdirectory = null,
            int supersampling = kScreenshotSupersampling,
            int msaaSamples = kScreenshotMsaaSamples)
        {
            int renderWidth = resWidth * supersampling;
            int renderHeight = resHeight * supersampling;
            RenderTexture rt = new RenderTexture(renderWidth, renderHeight, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = msaaSamples,
                filterMode = FilterMode.Bilinear
            };
            RenderTexture downsampledRt = supersampling > 1
                ? new RenderTexture(resWidth, resHeight, 0, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Bilinear
                }
                : null;
            Texture2D screenShot = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = cameraToCapture.targetTexture;
            bool previousAllowMsaa = cameraToCapture.allowMSAA;
            bool previousHdrSimple = Shader.IsKeywordEnabled("HDR_SIMPLE");
            bool previousHdrEmulated = Shader.IsKeywordEnabled("HDR_EMULATED");
            List<KeyValuePair<Behaviour, bool>> previousPostEffectStates = null;
            try
            {
                if (enablePostProcessing.HasValue)
                {
                    previousPostEffectStates = SetBuiltInPostEffectsEnabled(
                        cameraToCapture, enablePostProcessing.Value);
                    if (!enablePostProcessing.Value)
                    {
                        Shader.DisableKeyword("HDR_SIMPLE");
                        Shader.DisableKeyword("HDR_EMULATED");
                    }
                }

                cameraToCapture.allowMSAA = msaaSamples > 1;
                cameraToCapture.targetTexture = rt;
                screenShot = new Texture2D(resWidth, resHeight, TextureFormat.RGB24, false);
                RenderScreenshotCamera(cameraToCapture, renderWidth, renderHeight, renderWireframe);
                if (downsampledRt != null)
                {
                    Graphics.Blit(rt, downsampledRt);
                }
                RenderTexture.active = downsampledRt != null ? downsampledRt : rt;
                screenShot.ReadPixels(new Rect(0, 0, resWidth, resHeight), 0, 0);
                byte[] bytes = screenShot.EncodeToPNG();
                string outputDirectory = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    kScreenshotOutputDirectory);
                if (!string.IsNullOrEmpty(outputSubdirectory))
                {
                    outputDirectory = Path.Combine(
                        outputDirectory,
                        outputSubdirectory);
                }
                Directory.CreateDirectory(outputDirectory);
                string filePath = Path.Combine(outputDirectory, fileName);
                File.WriteAllBytes(filePath, bytes);
            }
            finally
            {
                cameraToCapture.targetTexture = previousTarget;
                cameraToCapture.allowMSAA = previousAllowMsaa;
                if (previousPostEffectStates != null)
                {
                    RestoreBuiltInPostEffects(previousPostEffectStates);
                }
                SetKeyword("HDR_SIMPLE", previousHdrSimple);
                SetKeyword("HDR_EMULATED", previousHdrEmulated);
                RenderTexture.active = previousActive;
                if (screenShot != null)
                {
                    Destroy(screenShot);
                }
                Destroy(rt);
                if (downsampledRt != null)
                {
                    Destroy(downsampledRt);
                }
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
