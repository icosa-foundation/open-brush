// Copyright 2022 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using TiltBrush.DocumentImport;
using UnityEngine;
using UnityEngine.Rendering;

namespace TiltBrush.MeshEditing
{
    // Runtime generation and regeneration path ported from the 3D Shapes branch.
    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;
        private void Awake() { m_Instance = this; }
        private void OnDestroy() { if (m_Instance == this) { m_Instance = null; } }

        public static EditableModelManager GetOrCreate()
        {
            if (m_Instance == null) { new GameObject("EditableModelManager").AddComponent<EditableModelManager>(); }
            return m_Instance;
        }

        public static PolyMesh Evaluate(GeometryRecipeData recipe)
        {
            recipe.GetSteps(out var generator, out var operations);
            return GeometryRecipeEvaluator.Build(new GeometryRecipe(ToStep(generator), operations.Select(ToStep)));
        }

        public static readonly Dictionary<string, GeometryRecipeData> RetainedDefinitions = new Dictionary<string, GeometryRecipeData>();
        public static readonly List<TiltEditableModels> RetainedInstances = new List<TiltEditableModels>();

        public static void ClearRetainedData()
        {
            RetainedDefinitions.Clear();
            RetainedInstances.Clear();
        }

        public static void LoadFromTilt(Dictionary<string, GeometryRecipeData> definitions, TiltEditableModels[] instances)
        {
            ClearRetainedData();
            if (definitions != null)
            {
                foreach (var definition in definitions)
                {
                    try { definition.Value.GetSteps(out _, out _); }
                    catch (Exception) { RetainedDefinitions.Add(definition.Key, definition.Value); }
                }
            }
            if (instances == null) { return; }
            foreach (var instance in instances)
            {
                // Validate an entire parallel-array record before creating any of its widgets.
                // On failure retain that record intact, avoiding partial creation and duplicate saves.
                GeometryRecipeData recipe;
                PolyMesh poly;
                try
                {
                    if (instance.AssetId == null || definitions == null || !definitions.TryGetValue(instance.AssetId, out recipe))
                    {
                        throw new NotSupportedException($"Missing editable model definition: {instance.AssetId ?? instance.FilePath}");
                    }
                    if (instance.ExtraData?.Count > 0) { throw new NotSupportedException("Unfamiliar editable model instance fields"); }
                    if (instance.RawTransforms == null || instance.RawTransforms.Any(transform => !transform.IsFinite()))
                    {
                        throw new ArgumentException("Invalid editable model transforms");
                    }
                    if (instance.LayerIds?.Any(layer => layer < 0) == true) { throw new ArgumentException("Invalid editable model layer"); }
                    poly = Evaluate(recipe);
                    if (poly.Vertices.Count == 0 || poly.Faces.Count == 0) { throw new InvalidOperationException("Empty editable geometry"); }
                }
                catch (Exception error)
                {
                    RetainedInstances.Add(instance);
                    if (instance.AssetId != null && definitions != null && definitions.TryGetValue(instance.AssetId, out var retained))
                    {
                        RetainedDefinitions[instance.AssetId] = retained;
                    }
                    Debug.LogWarning($"[DocumentImport] Editable model retained without rendering: {error.Message}");
                    OutputWindowScript.Error($"Editable model could not be displayed: {error.Message}");
                    continue;
                }
                for (int i = 0; i < instance.RawTransforms.Length; ++i)
                {
                    var canvas = App.Scene.GetOrCreateLayer(At(instance.LayerIds, i, 0));
                    var transform = instance.RawTransforms[i];
                    var widget = GetOrCreate().GeneratePolyMesh(poly, recipe, canvas.Pose * transform,
                        recordCommand: false, color: At(instance.Colors, i, Color.white), definitionId: instance.AssetId);
                    widget.SetCanvas(canvas);
                    widget.transform.localPosition = transform.translation;
                    widget.transform.localRotation = transform.rotation;
                    widget.SetSignedWidgetSize(transform.scale);
                    widget.Group = App.GroupManager.GetGroupFromId(At(instance.GroupIds, i, 0u));
                    if (At(instance.PinStates, i, false)) { widget.PinFromSave(); }
                }
            }
        }

        private static T At<T>(T[] values, int index, T fallback) => values != null && index < values.Length ? values[index] : fallback;

        private static GeometryStep ToStep(GeometryStepData step)
        {
            var parameters = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var parameter in step.Parameters)
            {
                object value = parameter.Value;
                if (value is float[] components)
                {
                    switch (components.Length)
                    {
                        case 2: value = new Vector2(components[0], components[1]); break;
                        case 3: value = new Vector3(components[0], components[1], components[2]); break;
                        case 4: value = new Quaternion(components[0], components[1], components[2], components[3]); break;
                        default: throw new ArgumentException($"Invalid geometry vector: {parameter.Key}");
                    }
                }
                parameters.Add(parameter.Key, value);
            }
            return new GeometryStep(step.Type, parameters);
        }

        public EditableModelWidget GeneratePolyMesh(PolyMesh poly, GeometryRecipeData recipe, TrTransform tr,
            Quaternion? desiredEndForward = null, bool forceTransform = true,
            float snapGrid = 0, float snapAngle = 0, bool recordCommand = true, Color? color = null, string definitionId = null)
        {
            // Validate the complete authoring recipe before creating scene objects.
            recipe.GetSteps(out _, out _);
            Mesh mesh = BuildMesh(poly);
            var root = new GameObject("Editable geometry");
            var material = new Material((color ?? Color.white).a < 1
                ? ModelCatalog.m_Instance.m_ObjLoaderTransparentMaterial
                : ModelCatalog.m_Instance.m_ObjLoaderStandardMaterial);
            if (material.HasProperty("_BaseColor")) { material.SetColor("_BaseColor", color ?? Color.white); }
            if (material.HasProperty("_Color")) { material.SetColor("_Color", color ?? Color.white); }
            UpdateMesh(root, mesh, material);
            var model = new Model(Model.Location.Generated(definitionId ?? Guid.NewGuid().ToString()));
            model.TakeGeneratedMaterialOwnership(material);
            model.LoadEditableModel(root);
            if (!model.m_Valid)
            {
                model.ReleaseFromCatalog();
                throw new InvalidOperationException("Procedural model contains no usable geometry");
            }
            var command = new CreateWidgetCommand(WidgetManager.m_Instance.EditableModelWidgetPrefab,
                tr, desiredEndForward, forceTransform, snapGrid, snapAngle);
            if (recordCommand) { SketchMemoryScript.m_Instance.PerformAndRecordCommand(command); }
            else { command.Redo(); }
            var widget = command.Widget as EditableModelWidget;
            if (widget == null)
            {
                model.ReleaseFromCatalog();
                throw new InvalidOperationException("Editable model prefab did not create an EditableModelWidget");
            }
            widget.Model = model;
            model.ReleaseFromCatalog();
            widget.m_PolyRecipe = recipe;
            widget.m_PolyMesh = poly;
            widget.m_Color = color ?? Color.white;
            widget.Show(true);
            command.SetWidgetCost(widget.GetTiltMeterCost());
            return widget;
        }

        public void RegenerateMesh(EditableModelWidget widget, PolyMesh poly, Material material = null)
        {
            var root = widget.GetModelGameObject();
            UpdateMesh(root, BuildMesh(poly), material ?? root.GetComponent<MeshRenderer>().sharedMaterial);
            widget.TakeGeneratedMeshOwnership(root.GetComponent<MeshFilter>().sharedMesh);
            widget.RecalculateColliderBounds();
            widget.UpdateBatchInfo();
            widget.m_PolyMesh = poly;
        }

        public void UpdateMesh(GameObject root, Mesh mesh, Material material)
        {
            var filter = root.GetComponent<MeshFilter>() ?? root.AddComponent<MeshFilter>();
            var renderer = root.GetComponent<MeshRenderer>() ?? root.AddComponent<MeshRenderer>();
            var collider = root.GetComponent<BoxCollider>() ?? root.AddComponent<BoxCollider>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            collider.center = mesh.bounds.center;
            collider.size = mesh.bounds.size;
        }

        private static Mesh BuildMesh(PolyMesh poly)
        {
            var data = poly.BuildMeshData(colors: new[] { Color.white }, colorMethod: ColorMethods.ByIndex);
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            // Recipes use metres. Convert once, without preview normalization or random jitter.
            mesh.SetVertices(data.meshVertices.Select(vertex => vertex * App.METERS_TO_UNITS).ToList());
            mesh.SetNormals(data.meshNormals);
            mesh.SetUVs(0, data.meshUVs);
            mesh.SetColors(data.meshColors);
            mesh.SetTriangles(data.meshTriangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
