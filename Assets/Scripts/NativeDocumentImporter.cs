// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public sealed class NativeDocumentImportReport
    {
        public string Status { get; internal set; } = "running";
        public string Error { get; internal set; }
        public string SourceScene { get; internal set; }
        public int ProceduralObjects { get; internal set; }
        public int TextObjects { get; internal set; }
        public int ModelObjects { get; internal set; }
        public int ImageObjects { get; internal set; }
        public int LightObjects { get; internal set; }
        public int StrokeObjects { get; internal set; }
        public int AudioObjects { get; internal set; }
        public int HiddenObjects { get; internal set; }
        public int FontSubstitutions { get; internal set; }
        public int FailedObjects { get; internal set; }
        public Dictionary<string, int> UnsupportedObjects { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> UnsupportedActions { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> UnmappedMaterials { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> UnmappedEnvironment { get; } = new Dictionary<string, int>();
        public List<string> Details { get; } = new List<string>();

        internal void Detail(string message)
        {
            // Keep API responses and Editor logs bounded even for large source documents.
            if (Details.Count < 10) { Details.Add(message); }
        }
    }

    /// Native content conversion uses the ordinary widgets and their shared .tilt persistence.
    /// This imports one resolved state; source scene switching and animation are separate work.
    public static partial class NativeDocumentImporter
    {
        private static Task m_ImportTask;
        public static NativeDocumentImportReport LastReport { get; private set; }

        public static NativeDocumentImportReport BeginMaquette(string path, string contentDirectory = null, string userLibrary = null)
        {
            if (m_ImportTask != null && !m_ImportTask.IsCompleted) { throw new InvalidOperationException("A native import is already running"); }
            var report = new NativeDocumentImportReport();
            LastReport = report;
            m_ImportTask = RunMaquette(path, contentDirectory, userLibrary, report);
            return report;
        }

        private static async Task RunMaquette(string path, string contentDirectory, string userLibrary, NativeDocumentImportReport report)
        {
            try
            {
                await ImportMaquetteAsync(path, contentDirectory, userLibrary, report);
                report.Status = report.FailedObjects > 0 || report.FontSubstitutions > 0 ||
                                report.UnsupportedObjects.Count > 0 || report.UnmappedMaterials.Count > 0 ||
                                report.UnmappedEnvironment.Count > 0 ? "partial" : "completed";
            }
            catch (Exception exception)
            {
                report.Status = "failed";
                report.Error = exception.Message;
            }
            Debug.Log($"[OB_NATIVE_IMPORT] Maquette {report.Status}: procedural={report.ProceduralObjects}, text={report.TextObjects}, models={report.ModelObjects}, images={report.ImageObjects}, lights={report.LightObjects}, failed={report.FailedObjects}");
        }

        private static async Task ImportMaquetteAsync(string path, string contentDirectory, string userLibrary, NativeDocumentImportReport report)
        {
            var canvas = App.ActiveCanvas;
            if (canvas == null) { throw new InvalidOperationException("No canvas is available for native document import"); }
            var groups = new Dictionary<string, SketchGroupTag>(StringComparer.OrdinalIgnoreCase);
            CanvasScript hiddenCanvas = null;
            TMP_FontAsset font = null;
            string fontReference = null;
            bool fontPrepared = false;
            using (var document = new MaquetteDocument(path))
            using (var workspace = new NativeAssetWorkspace(document.Source, Path.Combine(Application.temporaryCachePath, "Native Imports")))
            using (var models = new NativeModelCache(workspace))
            {
                var scene = document.SelectScene();
                report.SourceScene = scene.Name;
                ImportMaquetteEnvironment(document, scene, canvas, report);
                userLibrary = userLibrary ?? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "Maquette");
                contentDirectory = contentDirectory ?? DefaultMaquetteContent();
                var assets = new MaquetteAssetResolver(document.Source, userLibrary, contentDirectory);
                var images = new NativeImageCache();
                foreach (var state in document.ReadComposedStates(scene))
                {
                    if (state.Entity?.Kind == MaquetteEntityKind.Group) { continue; }
                    if (state.Entity == null && state.Data["children"] is JArray children && children.Count > 0) { continue; }
                    if (state.Entity == null)
                    {
                        report.FailedObjects++;
                        report.Detail($"Missing entity {state.EntityId} for state {state.Id}");
                        continue;
                    }
                    if (state.Entity.Kind != MaquetteEntityKind.Procedural && state.Entity.Kind != MaquetteEntityKind.Text &&
                        state.Entity.Kind != MaquetteEntityKind.Primitive && state.Entity.Kind != MaquetteEntityKind.Light &&
                        state.Entity.Kind != MaquetteEntityKind.Stroke)
                    {
                        string kind = state.Entity.Kind.ToString();
                        report.UnsupportedObjects.TryGetValue(kind, out int count);
                        report.UnsupportedObjects[kind] = count + 1;
                        continue;
                    }
                    try
                    {
                        if (state.Entity.Kind == MaquetteEntityKind.Stroke)
                        {
                            var ribbon = MaquetteStrokeAdapter.Read(document, state, App.METERS_TO_UNITS);
                            var strokeCanvas = canvas;
                            if (!state.Visible)
                            {
                                if (hiddenCanvas == null)
                                {
                                    hiddenCanvas = App.Scene.AddLayerNow();
                                    hiddenCanvas.name = $"{scene.Name} (hidden)";
                                    hiddenCanvas.Pose = canvas.Pose;
                                }
                                strokeCanvas = hiddenCanvas;
                            }
                            var group = SketchGroupTag.None;
                            if (state.Parent != null && !groups.TryGetValue(state.Parent.Id, out group))
                            {
                                group = App.GroupManager.NewUnusedGroup();
                                groups.Add(state.Parent.Id, group);
                            }
                            var strokeColor = ReadColor(state.Data["color"]);
                            CreateNativeRibbon(ribbon, strokeCanvas, strokeColor, group);
                            report.StrokeObjects++;
                            if (!state.Visible) { report.HiddenObjects++; }
                            string material = MaquetteStrokeAdapter.ReadMaterialId(document, state);
                            if (material != "Unlit" && material != "DefaultUnlit")
                            {
                                string feature = $"Maquette ribbon material {material ?? "default"}";
                                report.UnmappedMaterials.TryGetValue(feature, out int count);
                                report.UnmappedMaterials[feature] = count + 1;
                            }
                            if (strokeColor.a < 1)
                            {
                                const string feature = "Maquette ribbon alpha blending";
                                report.UnmappedMaterials.TryGetValue(feature, out int count);
                                report.UnmappedMaterials[feature] = count + 1;
                            }
                            if ((bool?)state.Data["lockedForEdit"] == true)
                            {
                                const string feature = "Maquette per-stroke locking";
                                report.UnsupportedObjects.TryGetValue(feature, out int count);
                                report.UnsupportedObjects[feature] = count + 1;
                            }
                            continue;
                        }
                        GrabWidget widget;
                        Color color = ReadColor(state.Data["color"]);
                        if (state.Entity.Kind == MaquetteEntityKind.Light)
                        {
                            var data = MaquetteLightAdapter.Read(state, App.METERS_TO_UNITS);
                            var command = new CreateWidgetCommand(WidgetManager.m_Instance.LightWidgetPrefab,
                                canvas.Pose * Pose(data.Placement), forceTransform: true, skipIntroAnimation: true);
                            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                            var lightWidget = (LightWidget)command.Widget;
                            var light = lightWidget.SetLightType(data.IsSpot ? LightType.Spot : LightType.Point);
                            light.color = color;
                            light.intensity = data.Intensity;
                            light.range = data.Range * canvas.Pose.scale;
                            if (data.IsSpot) { light.spotAngle = data.SpotAngle; light.innerSpotAngle = 0; }
                            light.enabled = data.Enabled;
                            light.shadows = data.Shadows ? LightShadows.Hard : LightShadows.None;
                            lightWidget.RefreshLightGizmo();
                            command.SetWidgetCost(lightWidget.GetTiltMeterCost());
                            widget = lightWidget;
                        }
                        else if (state.Entity.Kind == MaquetteEntityKind.Procedural)
                        {
                            int version = (int)document.Sections["proceduralmeshes.json"]["version"];
                            var recipe = MaquetteGeometryAdapter.Convert(state.Entity.Data, state.GetBuilderScale(), version);
                            var placement = new MaquettePlacement(state);
                            recipe = placement.BakeLinearTransform(recipe);
                            var mesh = EditableModelManager.Evaluate(recipe);
                            widget = EditableModelManager.GetOrCreate().GeneratePolyMesh(mesh, recipe,
                                canvas.Pose * Pose(placement), color: color, skipIntroAnimation: true);
                            string material = (string)state.Data["materialId"];
                            if (!string.IsNullOrEmpty(material))
                            {
                                report.UnmappedMaterials.TryGetValue(material, out int count);
                                report.UnmappedMaterials[material] = count + 1;
                            }
                        }
                        else if (state.Entity.Kind == MaquetteEntityKind.Text)
                        {
                            var text = MaquetteTextAdapter.Read(state, id => assets.Text(id)?.ReadText(), App.METERS_TO_UNITS);
                            var layout = new TextWidgetLayout
                            {
                                FontSize = text.FontSize, FontSizeMin = MaquetteTextAdapter.FontSizeMin,
                                FontSizeMax = MaquetteTextAdapter.FontSizeMax, AutoSize = true,
                                FontStyle = text.FontStyle, Alignment = text.Alignment,
                                Wrapping = (int)TextWrappingModes.Normal, RichText = true,
                                Size = new Vector2(text.BoxSize[0], text.BoxSize[1]), Pivot = new Vector2(.5f, .5f),
                                GlyphTransform = text.GlyphTransform
                            };
                            layout.Validate();
                            if (!fontPrepared)
                            {
                                fontPrepared = true;
                                PrepareMaquetteFont(out font, out fontReference, report);
                            }
                            var command = new CreateWidgetCommand(WidgetManager.m_Instance.TextWidgetPrefab,
                                canvas.Pose * Pose(text.Placement), forceTransform: true, skipIntroAnimation: true);
                            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                            var textWidget = (TextWidget)command.Widget;
                            if (font != null) { textWidget.AssignFont(font, fontReference); }
                            else
                            {
                                textWidget.AssignFontReference(fontReference);
                                if (textWidget.HasUnresolvedFont) { report.FontSubstitutions++; }
                            }
                            textWidget.Text = text.Text;
                            textWidget.TextColor = color;
                            textWidget.StrokeColor = Color.clear;
                            textWidget.Mode = TextWidgetMode.TextMeshPro;
                            textWidget.ApplyLayout(layout);
                            command.SetWidgetCost(textWidget.GetTiltMeterCost());
                            widget = textWidget;
                        }
                        else
                        {
                            int version = (int)document.Sections["primitives.json"]["version"];
                            var primitive = assets.Primitive(state.Entity.Data, version);
                            if (primitive.Type == "ImageFile" || primitive.Type == "ImageFromWeb")
                            {
                                var data = MaquetteImageAdapter.Read(state, primitive, App.METERS_TO_UNITS);
                                var reference = images.Get(primitive, assets);
                                var pose = Pose(data.Placement);
                                pose.scale = data.Height;
                                var command = new CreateWidgetCommand(WidgetManager.m_Instance.ImageWidgetPrefab,
                                    canvas.Pose * pose, forceTransform: true, skipIntroAnimation: true);
                                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                                var imageWidget = (ImageWidget)command.Widget;
                                imageWidget.ReferenceImage = reference;
                                if (primitive.IgnoresImageTint) { color = Color.white; }
                                ImageBlendMode blend;
                                switch (data.MaterialId)
                                {
                                    case "TranslucentUnlit": blend = ImageBlendMode.AlphaBlend; break;
                                    case "Additive": blend = ImageBlendMode.Additive; break;
                                    case "Unlit": blend = ImageBlendMode.Opaque; break;
                                    default:
                                        blend = data.MaterialId == "TranslucentLit" ? ImageBlendMode.AlphaBlend : ImageBlendMode.Opaque;
                                        report.UnmappedMaterials.TryGetValue(data.MaterialId, out int count);
                                        report.UnmappedMaterials[data.MaterialId] = count + 1;
                                        break;
                                }
                                imageWidget.ImageColor = color;
                                imageWidget.TwoSided = true;
                                imageWidget.SetAspectRatioOverride(data.Width / data.Height);
                                imageWidget.BlendMode = blend;
                                imageWidget.LocalTransform = imageWidget.Canvas.Pose.inverse * canvas.Pose * pose;
                                command.SetWidgetCost(imageWidget.GetTiltMeterCost());
                                widget = imageWidget;
                            }
                            else
                            {
                                if (primitive.Type != "ModelFile")
                                {
                                    string kind = $"Primitive:{primitive.Type}";
                                    report.UnsupportedObjects.TryGetValue(kind, out int count);
                                    report.UnsupportedObjects[kind] = count + 1;
                                    continue;
                                }
                                var placement = new MaquettePlacement(state);
                                string materialId = (string)state.Data["materialId"];
                                var imported = await models.MaquetteModel(assets.Resolve(primitive.Path), primitive, placement, color, materialId);
                                if (canvas == null) { throw new InvalidOperationException("The import canvas was removed while loading a model"); }
                                var pose = Pose(placement);
                                pose.translation += imported.CanvasOffset * App.METERS_TO_UNITS;
                                pose.scale = App.METERS_TO_UNITS / imported.Model.UnitsPerMetre;
                                var command = new CreateWidgetCommand(WidgetManager.m_Instance.ModelWidgetPrefab,
                                    canvas.Pose * pose, forceTransform: true, skipIntroAnimation: true);
                                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                                var modelWidget = (ModelWidget)command.Widget;
                                modelWidget.Model = imported.Model;
                                modelWidget.Show(true);
                                command.SetWidgetCost(modelWidget.GetTiltMeterCost());
                                widget = modelWidget;
                                if (primitive.LoadOption != "Original" && !MaquetteMaterialAdapter.IsSupportedModelMaterial(materialId))
                                {
                                    report.UnmappedMaterials.TryGetValue(materialId, out int count);
                                    report.UnmappedMaterials[materialId] = count + 1;
                                }
                                if (primitive.LoadOption != "Original" &&
                                    (materialId == "TranslucentLit" || materialId == "TranslucentUnlit"))
                                {
                                    // Standard glTF alpha blending does not reproduce Maquette's
                                    // depth-writing transparent passes or its vertex-lit shader.
                                    string feature = materialId == "TranslucentLit" ?
                                        "Maquette translucent model depth writing and vertex lighting" :
                                        "Maquette translucent model depth writing and alpha discard";
                                    report.UnsupportedObjects.TryGetValue(feature, out int count);
                                    report.UnsupportedObjects[feature] = count + 1;
                                }
                                if (state.Data["castShadow"]?.Type == JTokenType.Boolean && !(bool)state.Data["castShadow"])
                                {
                                    const string feature = "Maquette model shadow casting";
                                    report.UnsupportedObjects.TryGetValue(feature, out int count);
                                    report.UnsupportedObjects[feature] = count + 1;
                                }
                                }
                        }
                        if (widget.Canvas != canvas) { widget.SetCanvas(canvas); }
                        widget.name = (string)state.Entity.Data["name"] ?? state.EntityId;
                        if (state.Parent != null)
                        {
                            if (!groups.TryGetValue(state.Parent.Id, out var group))
                            {
                                group = App.GroupManager.NewUnusedGroup();
                                groups.Add(state.Parent.Id, group);
                            }
                            widget.Group = group;
                        }
                        if (!state.Visible)
                        {
                            if (hiddenCanvas == null)
                            {
                                hiddenCanvas = App.Scene.AddLayerNow();
                                hiddenCanvas.name = $"{scene.Name} (hidden)";
                                hiddenCanvas.Pose = canvas.Pose;
                            }
                            widget.SetCanvas(hiddenCanvas);
                            report.HiddenObjects++;
                        }
                        if ((bool?)state.Data["lockedForEdit"] == true) { widget.PinFromSave(); }
                        if (state.Entity.Kind == MaquetteEntityKind.Procedural) { report.ProceduralObjects++; }
                        else if (state.Entity.Kind == MaquetteEntityKind.Text) { report.TextObjects++; }
                        else if (state.Entity.Kind == MaquetteEntityKind.Light) { report.LightObjects++; }
                        else if (widget is ImageWidget) { report.ImageObjects++; }
                        else { report.ModelObjects++; }
                    }
                    catch (Exception exception)
                    {
                        report.FailedObjects++;
                        report.Detail($"{state.Entity.Kind} {state.EntityId}: {exception.Message}");
                    }
                }
            }
            if (hiddenCanvas != null) { App.Scene.ToggleLayerVisibility(hiddenCanvas); }
        }

        private static string DefaultMaquetteContent()
        {
            string root = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrEmpty(root)) { return null; }
            string content = Path.Combine(root, "Steam", "steamapps", "common", "Microsoft Maquette", "MaquetteContent");
            return Directory.Exists(content) ? content : null;
        }

        private static TrTransform Pose(NativePlacement placement) => TrTransform.TR(
            new Vector3(placement.Position[0], placement.Position[1], placement.Position[2]) * App.METERS_TO_UNITS,
            new Quaternion(placement.Rotation[0], placement.Rotation[1], placement.Rotation[2], placement.Rotation[3]));

        private static Color ReadColor(JToken data) => data == null ? Color.white : new Color(
            (float?)data["r"] ?? 1, (float?)data["g"] ?? 1, (float?)data["b"] ?? 1, (float?)data["a"] ?? 1);

        private static void PrepareMaquetteFont(out TMP_FontAsset font, out string reference, NativeDocumentImportReport report)
        {
            // Use an already available asset or the user's own installed font. Never extract or
            // redistribute Maquette's atlas. Keep the unresolved identity when substitution is needed.
            const string sourceName = "SegoeUI_SemiBold SDF TextBox";
            reference = sourceName;
            font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(asset => asset.name == sourceName);
            if (font != null) { return; }
            string directory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Fonts);
            if (string.IsNullOrEmpty(directory)) { return; }
            string path = Path.Combine(directory, "seguisb.ttf");
            if (!File.Exists(path)) { return; }
            try
            {
                font = SvgTextUtils.ConvertOpenTypeToTMPro(path);
                if (font != null) { reference = path; }
            }
            catch (Exception exception) { report.Detail($"Segoe UI Semibold could not be loaded: {exception.Message}"); }
        }
    }
}
