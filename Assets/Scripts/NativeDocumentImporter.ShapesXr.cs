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
    public static partial class NativeDocumentImporter
    {
        public static NativeDocumentImportReport BeginShapesXr(string path)
        {
            if (m_ImportTask != null && !m_ImportTask.IsCompleted) { throw new InvalidOperationException("A native import is already running"); }
            var report = new NativeDocumentImportReport();
            LastReport = report;
            m_ImportTask = RunShapesXr(path, report);
            return report;
        }

        private static async Task RunShapesXr(string path, NativeDocumentImportReport report)
        {
            // Establish the running task before starting scene work on the Unity thread.
            await Task.Yield();
            CanvasScript hiddenCanvas = null;
            try
            {
                var canvas = App.ActiveCanvas;
                if (canvas == null) { throw new InvalidOperationException("No canvas is available for native document import"); }
                var document = new ShapesXrDocument(path);
                var scene = new ShapesXrScene(document);
                report.SourceScene = scene.Name;
                var groups = new Dictionary<Guid, SketchGroupTag>();
                using var assets = new NativeShapesXrAssets();
                using var models = new NativeModelCache(assets.Workspace);
                var images = new NativeImageCache();
                foreach (var state in scene.Objects.Values.Where(value => value.HasSnapshot))
                {
                    if (state.IsGroup) { continue; }
                    bool isText = state.Properties["obj-tx"]?.Type == JTokenType.String;
                    bool isResource = state.Properties["resi"] != null;
                    if (!isText && !isResource && !(state.Properties["obj-pmugs"] is JArray))
                    {
                        const string kind = "ShapesXR object preset";
                        report.UnsupportedObjects.TryGetValue(kind, out int count);
                        report.UnsupportedObjects[kind] = count + 1;
                        report.Detail($"Unsupported ShapesXR preset on {state.Entity.Id}");
                        continue;
                    }
                    try
                    {
                        GrabWidget widget;
                        if (isText)
                        {
                            widget = CreateShapesXrText(document, state, canvas, report);
                        }
                        else if (isResource)
                        {
                            var resource = await assets.Get(ShapesXrDocument.ReadGuid(state.Properties["resi"]));
                            if (canvas == null) { throw new InvalidOperationException("The import canvas was removed while downloading an asset"); }
                            if (resource.Data.Kind == ShapesXrResourceKind.Image)
                            {
                                widget = CreateShapesXrImage(document, state, canvas, images.Get(resource.Resource), report);
                            }
                            else
                            {
                                var placement = state.Placement();
                                var model = await models.ShapesXrModel(resource.Resource, placement);
                                if (canvas == null) { throw new InvalidOperationException("The import canvas was removed while loading a model"); }
                                var pose = Pose(placement);
                                pose.scale = App.METERS_TO_UNITS / model.UnitsPerMetre;
                                var command = new CreateWidgetCommand(WidgetManager.m_Instance.ModelWidgetPrefab,
                                    canvas.Pose * pose, forceTransform: true, skipIntroAnimation: true);
                                SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
                                var modelWidget = (ModelWidget)command.Widget;
                                modelWidget.Model = model;
                                modelWidget.Show(true);
                                command.SetWidgetCost(modelWidget.GetTiltMeterCost());
                                widget = modelWidget;
                                const string pending = "ShapesXR model material overrides";
                                report.UnmappedMaterials.TryGetValue(pending, out int count);
                                report.UnmappedMaterials[pending] = count + 1;
                            }
                        }
                        else
                        {
                            var placement = state.Placement();
                            var recipe = placement.BakeLinearTransform(ShapesXrGeometryAdapter.Panel(state.Properties));
                            Color color = ShapesXrColor(document, state, report);
                            var mesh = EditableModelManager.Evaluate(recipe);
                            widget = EditableModelManager.GetOrCreate().GeneratePolyMesh(mesh, recipe,
                                canvas.Pose * Pose(placement), color: color, skipIntroAnimation: true);
                        }
                        if (widget.Canvas != canvas) { widget.SetCanvas(canvas); }
                        widget.name = $"ShapesXR {(isText ? "text" : isResource ? "asset" : "panel")} {state.Entity.Id}";
                        if (state.Parent != null)
                        {
                            if (!groups.TryGetValue(state.ParentId, out var group))
                            {
                                group = App.GroupManager.NewUnusedGroup();
                                groups.Add(state.ParentId, group);
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
                        if (state.Locked) { widget.PinFromSave(); }
                        if (isText) { report.TextObjects++; }
                        else if (widget is ImageWidget) { report.ImageObjects++; }
                        else if (widget is ModelWidget && !(widget is EditableModelWidget)) { report.ModelObjects++; }
                        else { report.ProceduralObjects++; }
                    }
                    catch (NotSupportedException exception)
                    {
                        report.UnsupportedObjects.TryGetValue(exception.Message, out int count);
                        report.UnsupportedObjects[exception.Message] = count + 1;
                        report.Detail($"ShapesXR object {state.Entity.Id}: {exception.Message}");
                    }
                    catch (Exception exception)
                    {
                        report.FailedObjects++;
                        report.Detail($"ShapesXR object {state.Entity.Id}: {exception.Message}");
                    }
                }
                report.Status = report.FailedObjects > 0 || report.FontSubstitutions > 0 || report.UnsupportedObjects.Count > 0 ||
                                report.UnmappedMaterials.Count > 0 ? "partial" : "completed";
            }
            catch (Exception exception)
            {
                report.Status = "failed";
                report.Error = exception.Message;
            }
            finally
            {
                if (hiddenCanvas != null) { App.Scene.ToggleLayerVisibility(hiddenCanvas); }
            }
            Debug.Log($"[OB_NATIVE_IMPORT] ShapesXR {report.Status}: procedural={report.ProceduralObjects}, text={report.TextObjects}, failed={report.FailedObjects}");
        }

        private static ImageWidget CreateShapesXrImage(ShapesXrDocument document, ShapesXrObjectState state,
            CanvasScript canvas, ReferenceImage reference, NativeDocumentImportReport report)
        {
            var image = ShapesXrImageAdapter.Read(state, App.METERS_TO_UNITS);
            var appearance = ShapesXrMaterialAdapter.Read(document, state, ShapesXrMaterialUsage.Image);
            Color color = new Color(appearance.Color[0], appearance.Color[1], appearance.Color[2], appearance.Color[3]);
            ImageBlendMode blend;
            switch (appearance.Blend)
            {
                case "opaque": blend = ImageBlendMode.Opaque; break;
                case "cutout":
                    blend = ImageBlendMode.Cutout;
                    UnmappedShapesXrMaterial(report, "ShapesXR image alpha-mask threshold");
                    break;
                case "additive": blend = ImageBlendMode.Additive; break;
                case "multiply":
                    blend = ImageBlendMode.AlphaBlend;
                    UnmappedShapesXrMaterial(report, "ShapesXR image multiply blend");
                    break;
                default: blend = ImageBlendMode.AlphaBlend; break;
            }
            if (appearance.Lit) { UnmappedShapesXrMaterial(report, "ShapesXR lit image"); }
            if (appearance.Passthrough) { UnmappedShapesXrMaterial(report, "ShapesXR image passthrough"); }
            foreach (string property in appearance.UnmappedProperties) { UnmappedShapesXrMaterial(report, $"ShapesXR image {property}"); }
            var pose = Pose(image.Placement);
            pose.scale = image.Height;
            var command = new CreateWidgetCommand(WidgetManager.m_Instance.ImageWidgetPrefab,
                canvas.Pose * pose, forceTransform: true, skipIntroAnimation: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
            var widget = (ImageWidget)command.Widget;
            widget.ReferenceImage = reference;
            widget.Opacity = color.a;
            widget.TwoSided = true;
            widget.ApplyLayout(new ImageWidgetLayout
            {
                Transform = image.GeometryTransform, Tint = new[] { color.r, color.g, color.b }, BlendMode = blend
            });
            widget.LocalTransform = widget.Canvas.Pose.inverse * canvas.Pose * pose;
            command.SetWidgetCost(widget.GetTiltMeterCost());
            return widget;
        }

        private static TextWidget CreateShapesXrText(ShapesXrDocument document, ShapesXrObjectState state,
            CanvasScript canvas, NativeDocumentImportReport report)
        {
            var text = ShapesXrTextAdapter.Read(state, App.METERS_TO_UNITS);
            var layout = new TextWidgetLayout
            {
                FontSize = text.FontSize, AutoSize = text.AutoSize,
                FontSizeMin = ShapesXrTextAdapter.FontSizeMin, FontSizeMax = ShapesXrTextAdapter.FontSizeMax,
                FontStyle = text.FontStyle, Alignment = text.Alignment,
                Wrapping = (int)TextWrappingModes.Normal, RichText = true,
                Size = new Vector2(text.BoxSize[0], text.BoxSize[1]), Pivot = new Vector2(0, 1),
                GlyphTransform = text.GlyphTransform
            };
            layout.Validate();
            var pose = Pose(text.Placement);
            pose.translation = new Vector3(text.Position[0], text.Position[1], text.Position[2]) * App.METERS_TO_UNITS;
            Color color = ShapesXrColor(document, state, report, isText: true);
            var command = new CreateWidgetCommand(WidgetManager.m_Instance.TextWidgetPrefab,
                canvas.Pose * pose, forceTransform: true, skipIntroAnimation: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(command);
            var widget = (TextWidget)command.Widget;
            widget.AssignFontReference(ShapesXrTextAdapter.FontResource);
            if (widget.HasUnresolvedFont) { report.FontSubstitutions++; }
            widget.Text = text.Text;
            widget.TextColor = color;
            widget.StrokeColor = Color.clear;
            widget.Mode = TextWidgetMode.TextMeshPro;
            widget.ApplyLayout(layout);
            command.SetWidgetCost(widget.GetTiltMeterCost());
            return widget;
        }

        private static Color ShapesXrColor(ShapesXrDocument document, ShapesXrObjectState state,
            NativeDocumentImportReport report, bool isText = false)
        {
            // Color and opacity fit the existing editable-model representation. Keep the report
            // partial until source shading, textures and secondary material slots are supported.
            if (!isText) { UnmappedShapesXrMaterial(report, "ShapesXR material appearance"); }
            var appearance = ShapesXrMaterialAdapter.Read(document, state, isText ? ShapesXrMaterialUsage.Text : ShapesXrMaterialUsage.Panel);
            return new Color(appearance.Color[0], appearance.Color[1], appearance.Color[2], appearance.Color[3]);
        }

        private static void UnmappedShapesXrMaterial(NativeDocumentImportReport report, string feature)
        {
            report.UnmappedMaterials.TryGetValue(feature, out int count);
            report.UnmappedMaterials[feature] = count + 1;
        }
    }
}
