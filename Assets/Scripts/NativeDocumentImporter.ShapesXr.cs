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
            Color color = ShapesXrColor(document, state, report);
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
                Transform = image.GeometryTransform, Tint = new[] { color.r, color.g, color.b }, BlendMode = ImageBlendMode.AlphaBlend
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
            if (!isText)
            {
                const string pending = "ShapesXR material appearance";
                report.UnmappedMaterials.TryGetValue(pending, out int count);
                report.UnmappedMaterials[pending] = count + 1;
            }
            Color color = isText ? new Color(155f / 255, 155f / 255, 155f / 255, 1) : Color.white;
            if (state.Properties["mid"] != null)
            {
                Guid id = ShapesXrDocument.ReadGuid(state.Properties["mid"]);
                if (!document.Entities.TryGetValue(id, out var material) || !material.Exists || material.Kind != 10)
                {
                    throw new InvalidDataException($"Missing ShapesXR material {id}");
                }
                if (material.Properties["m-c"] is JArray rgba)
                {
                    if (rgba.Count != 4 || rgba.Any(value => value.Type != JTokenType.Integer || (int)value < 0 || (int)value > 255))
                    {
                        throw new InvalidDataException("Invalid ShapesXR material color");
                    }
                    color = new Color((int)rgba[0] / 255f, (int)rgba[1] / 255f, (int)rgba[2] / 255f, (int)rgba[3] / 255f);
                }
                // Source text explicitly replaces Color32 alpha with the separate opacity.
                if (isText) { color.a = 1; }
                color.a *= (float?)material.Properties["m-o"] ?? 1;
            }
            color.a *= (float?)state.Properties["obj-vis"] ?? 1;
            if (float.IsNaN(color.a) || float.IsInfinity(color.a) || color.a < 0 || color.a > 1)
            {
                throw new InvalidDataException("Invalid ShapesXR material opacity");
            }
            return color;
        }
    }
}
