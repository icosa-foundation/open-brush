// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
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
                foreach (var state in scene.Objects.Values.Where(value => value.HasSnapshot))
                {
                    if (!(state.Properties["obj-pmugs"] is JArray))
                    {
                        const string kind = "ShapesXR object preset";
                        report.UnsupportedObjects.TryGetValue(kind, out int count);
                        report.UnsupportedObjects[kind] = count + 1;
                        report.Detail($"Unsupported ShapesXR preset on {state.Entity.Id}");
                        continue;
                    }
                    try
                    {
                        var placement = state.Placement();
                        var recipe = placement.BakeLinearTransform(ShapesXrGeometryAdapter.Panel(state.Properties));
                        Color color = ShapesXrColor(document, state, report);
                        var mesh = EditableModelManager.Evaluate(recipe);
                        var widget = EditableModelManager.GetOrCreate().GeneratePolyMesh(mesh, recipe,
                            canvas.Pose * Pose(placement), color: color, skipIntroAnimation: true);
                        if (widget.Canvas != canvas) { widget.SetCanvas(canvas); }
                        widget.name = $"ShapesXR panel {state.Entity.Id}";
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
                        report.ProceduralObjects++;
                    }
                    catch (Exception exception)
                    {
                        report.FailedObjects++;
                        report.Detail($"ShapesXR object {state.Entity.Id}: {exception.Message}");
                    }
                }
                report.Status = report.FailedObjects > 0 || report.UnsupportedObjects.Count > 0 ||
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
            Debug.Log($"[OB_NATIVE_IMPORT] ShapesXR {report.Status}: procedural={report.ProceduralObjects}, failed={report.FailedObjects}");
        }

        private static Color ShapesXrColor(ShapesXrDocument document, ShapesXrObjectState state,
            NativeDocumentImportReport report)
        {
            // Color and opacity fit the existing editable-model representation. Keep the report
            // partial until source shading, textures and secondary material slots are supported.
            const string pending = "ShapesXR material appearance";
            report.UnmappedMaterials.TryGetValue(pending, out int count);
            report.UnmappedMaterials[pending] = count + 1;
            Color color = Color.white;
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
