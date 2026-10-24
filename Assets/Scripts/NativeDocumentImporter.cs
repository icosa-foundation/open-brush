// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    public sealed class NativeDocumentImportReport
    {
        public string SourceScene { get; internal set; }
        public int ProceduralObjects { get; internal set; }
        public int TextObjects { get; internal set; }
        public int HiddenObjects { get; internal set; }
        public int FontSubstitutions { get; internal set; }
        public int FailedObjects { get; internal set; }
        public Dictionary<string, int> UnsupportedObjects { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> UnmappedMaterials { get; } = new Dictionary<string, int>();
        public List<string> Details { get; } = new List<string>();

        internal void Detail(string message)
        {
            // Keep API responses and Editor logs bounded even for large source documents.
            if (Details.Count < 10) { Details.Add(message); }
        }
    }

    /// Native content conversion uses the ordinary widgets and their shared .tilt persistence.
    /// This imports one resolved state; source scene switching and animation are separate work.
    public static class NativeDocumentImporter
    {
        public static NativeDocumentImportReport ImportMaquette(string path)
        {
            var report = new NativeDocumentImportReport();
            var canvas = App.ActiveCanvas;
            if (canvas == null) { throw new InvalidOperationException("No canvas is available for native document import"); }
            var groups = new Dictionary<string, SketchGroupTag>(StringComparer.OrdinalIgnoreCase);
            CanvasScript hiddenCanvas = null;
            TMP_FontAsset font = null;
            string fontReference = null;
            bool fontPrepared = false;
            using (var document = new MaquetteDocument(path))
            {
                var scene = document.SelectScene();
                report.SourceScene = scene.Name;
                var assets = new MaquetteAssetResolver(document.Source);
                foreach (var state in document.ReadStates(scene))
                {
                    if (state.Entity?.Kind == MaquetteEntityKind.Group) { continue; }
                    if (state.Entity == null && state.Data["children"] is JArray children && children.Count > 0) { continue; }
                    if (state.Entity == null)
                    {
                        report.FailedObjects++;
                        report.Detail($"Missing entity {state.EntityId} for state {state.Id}");
                        continue;
                    }
                    if (state.Entity.Kind != MaquetteEntityKind.Procedural && state.Entity.Kind != MaquetteEntityKind.Text)
                    {
                        string kind = state.Entity.Kind.ToString();
                        report.UnsupportedObjects.TryGetValue(kind, out int count);
                        report.UnsupportedObjects[kind] = count + 1;
                        continue;
                    }
                    try
                    {
                        GrabWidget widget;
                        Color color = ReadColor(state.Data["color"]);
                        if (state.Entity.Kind == MaquetteEntityKind.Procedural)
                        {
                            int version = (int)document.Sections["proceduralmeshes.json"]["version"];
                            var recipe = MaquetteGeometryAdapter.Convert(state.Entity.Data, state.GetBuilderScale(), version);
                            var placement = new MaquettePlacement(state);
                            recipe = placement.BakeLinearTransform(recipe);
                            var mesh = EditableModelManager.Evaluate(recipe);
                            widget = EditableModelManager.GetOrCreate().GeneratePolyMesh(mesh, recipe,
                                canvas.Pose * Pose(placement), color: color);
                            string material = (string)state.Data["materialId"];
                            if (!string.IsNullOrEmpty(material))
                            {
                                report.UnmappedMaterials.TryGetValue(material, out int count);
                                report.UnmappedMaterials[material] = count + 1;
                            }
                        }
                        else
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
                                canvas.Pose * Pose(text.Placement), forceTransform: true);
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
                        else { report.TextObjects++; }
                    }
                    catch (Exception exception)
                    {
                        report.FailedObjects++;
                        report.Detail($"{state.Entity.Kind} {state.EntityId}: {exception.Message}");
                    }
                }
            }
            if (hiddenCanvas != null) { App.Scene.ToggleLayerVisibility(hiddenCanvas); }
            return report;
        }

        private static TrTransform Pose(MaquettePlacement placement) => TrTransform.TR(
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
