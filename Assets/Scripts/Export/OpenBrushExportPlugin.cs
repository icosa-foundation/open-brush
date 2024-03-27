using System;
using System.Collections.Generic;
using System.Linq;
using GLTF.Schema;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;
using Object = UnityEngine.Object;

namespace TiltBrush
{
    public class OpenBrushExportPlugin : GLTFExportPlugin
    {
        public override string DisplayName => "Open Brush Export";
        public override string Description => "Handles Open Brush specific export logic.";
        public override bool EnabledByDefault => true;

        public override GLTFExportPluginContext CreateInstance(ExportContext context)
        {
            return new OpenBrushExportPluginConfig();
        }
    }

    public class OpenBrushExportPluginConfig : GLTFExportPluginContext
    {
        private Dictionary<int, Batch> _meshesToBatches;

        private List<string> _ignoreList = new()
        {
            "SnapGrid3D",
            "Preview Light"
        };

        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            SelectionManager.m_Instance.ClearActiveSelection();
            _meshesToBatches = new Dictionary<int, Batch>();
        }

        private Transform GetOrCreateGroupTransform(CanvasScript layer, int group)
        {
            if (layer.transform.childCount == 0)
            {
                var groupTransform = new GameObject($"_StrokeGroup_{group}").transform;
                groupTransform.parent = layer.transform;
                groupTransform.localPosition = Vector3.zero;
                groupTransform.localRotation = Quaternion.identity;
                groupTransform.localScale = Vector3.one;
                return groupTransform;
            }
            else
            {
                foreach (Transform child in layer.transform)
                {
                    if (child.name == $"_StrokeGroup_{group}")
                    {
                        return child;
                    }
                }
                var groupTransform = new GameObject($"_StrokeGroup_{group}").transform;
                groupTransform.parent = layer.transform;
                groupTransform.localPosition = Vector3.zero;
                groupTransform.localRotation = Quaternion.identity;
                groupTransform.localScale = Vector3.one;
                return groupTransform;
            }
        }

        public void BeforeLayerExport(Transform transform)
        {
            var canvas = transform.GetComponent<CanvasScript>();
            foreach (Transform child in transform)
            {
                if (_ignoreList.Contains(child.name))
                {
                    child.tag = "EditorOnly";
                }

                if (child.GetComponent<StencilWidget>() != null)
                {
                    child.tag = "EditorOnly";
                }
            }

            if (App.UserConfig.Export.KeepStrokes)
            {
                App.Config.m_UseBatchedBrushes = false;
                foreach (var batch in canvas.BatchManager.AllBatches())
                {
                    var subsets = batch.m_Groups.ToArray();
                    for (var i = 0; i < subsets.Length; i++)
                    {
                        var subset = subsets[i];
                        var stroke = subset.m_Stroke;
                        stroke.m_IntendedCanvas = stroke.Canvas;
                        if (stroke.m_Type != Stroke.Type.BatchedBrushStroke) continue;
                        stroke.Uncreate();
                        stroke.Recreate(null, canvas);
                        var mesh = stroke.m_Object.GetComponent<MeshFilter>().sharedMesh;
                        mesh = BrushBaker.m_Instance.ProcessMesh(mesh, stroke.m_BrushGuid.ToString());
                        stroke.m_Object.GetComponent<MeshFilter>().sharedMesh = mesh;
                        stroke.m_Object.GetComponent<MeshFilter>().mesh = mesh;
                        stroke.m_Object.name = $"{stroke.m_Object.name}_{i}";
                        if (App.UserConfig.Export.KeepGroups)
                        {
                            var group = stroke.Group.GetHashCode();
                            var groupTransform = GetOrCreateGroupTransform(canvas, group);
                            stroke.m_Object.transform.SetParent(groupTransform, true);
                        }
                    }
                    batch.tag = "EditorOnly";
                }
                canvas.BatchManager.FlushMeshUpdates();
            }
        }

        public override void BeforeNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            if (transform.GetComponent<CanvasScript>() != null)
            {
                BeforeLayerExport(transform);
            }

            if (!App.UserConfig.Export.KeepStrokes && App.UserConfig.Export.ExportStrokeMetadata)
            {
                // We'll need a way to find the batch for each mesh later
                var batch = transform.GetComponent<Batch>();
                var mf = transform.GetComponent<MeshFilter>();
                if (batch != null && mf != null)
                {
                    var mesh = mf.sharedMesh;
                    _meshesToBatches[mesh.GetHashCode()] = batch;
                }
            }
        }

        public void AfterLayerExport(Transform transform)
        {
            var canvas = transform.GetComponent<CanvasScript>();
            App.Config.m_UseBatchedBrushes = true;
            if (App.UserConfig.Export.KeepStrokes)
            {
                foreach (var brushScript in canvas.transform.GetComponentsInChildren<BaseBrushScript>())
                {
                    var stroke = brushScript.Stroke;
                    if (stroke == null || stroke.m_Type != Stroke.Type.BrushStroke) continue;
                    var strokeGo = stroke.m_Object;
                    stroke.InvalidateCopy();
                    stroke.Uncreate();
                    stroke.Recreate(null, canvas);
                    if (stroke.m_BatchSubset != null)
                    {
                        stroke.m_BatchSubset.m_ParentBatch.transform.tag = "Untagged";
                    }
                    SafeDestroy(strokeGo);
                }
                canvas.BatchManager.FlushMeshUpdates();

                if (App.UserConfig.Export.KeepStrokes)
                {
                    foreach (Transform child in canvas.transform)
                    {
                        if (child.name.StartsWith($"_StrokeGroup_"))
                        {
                            SafeDestroy(child.gameObject);
                        }
                    }
                }
            }
        }

        public override void AfterNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            if (transform.GetComponent<CanvasScript>() != null)
            {
                AfterLayerExport(transform);
            }

            if (App.UserConfig.Export.KeepStrokes && App.UserConfig.Export.ExportStrokeMetadata)
            {
                var brush = transform.GetComponent<BaseBrushScript>();
                if (brush != null)
                {
                    Stroke stroke = brush.Stroke;
                    if (stroke != null && node.Mesh != null)
                    {
                        if (App.UserConfig.Export.ExportStrokeTimestamp)
                        {
                            var strokeInfo = new Dictionary<string, string>();
                            strokeInfo["HeadTimestampMs"] = stroke.HeadTimestampMs.ToString();
                            strokeInfo["TailTimestampMs"] = stroke.TailTimestampMs.ToString();
                            strokeInfo["Group"] = stroke.Group.GetHashCode().ToString();
                            strokeInfo["Seed"] = stroke.m_Seed.ToString();
                            strokeInfo["Color"] = stroke.m_Color.ToString();
                            var primitiveExtras = new Dictionary<string, Dictionary<string, string>>
                            {
                                ["ICOSA_strokeInfo"] = strokeInfo
                            };

                            node.Mesh.Value.Extras = JToken.FromObject(primitiveExtras);
                        }
                    }
                }
            }
            else
            {
                try
                {
                    if (node.Name.StartsWith("Batch_"))
                    {
                        var parts = node.Name.Split("_");
                        Guid brushGuid = new Guid(parts.Last());
                        string brushName = BrushCatalog.m_Instance.GetBrush(brushGuid).DurableName;
                        brushName = brushName.Replace(" ", "_").ToLower();
                        node.Name = $"brush_{brushName}_{parts[1]}";
                        node.Mesh.Value.Name = $"brush_{brushName}_{parts[1]}";
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to rename node {node.Name} based on brush guid: {e.Message}");
                }
            }
        }

        public override void AfterPrimitiveExport(GLTFSceneExporter exporter, Mesh mesh, MeshPrimitive primitive, int index)
        {
            if (App.UserConfig.Export.ExportStrokeMetadata)
            {
                if (App.UserConfig.Export.KeepStrokes)
                {
                    Batch batch;
                    var result = _meshesToBatches.TryGetValue(mesh.GetHashCode(), out batch);
                    if (result)
                    {
                        var batchInfo = new List<Dictionary<string, string>>();
                        foreach (var subset in batch.m_Groups)
                        {
                            var subsetInfo = new Dictionary<string, string>();
                            subsetInfo["StartVertIndex"] = subset.m_StartVertIndex.ToString();
                            subsetInfo["VertLength"] = subset.m_VertLength.ToString();
                            subsetInfo["HeadTimestampMs"] = subset.m_Stroke.HeadTimestampMs.ToString();
                            subsetInfo["TailTimestampMs"] = subset.m_Stroke.TailTimestampMs.ToString();
                            subsetInfo["Group"] = subset.m_Stroke.Group.GetHashCode().ToString();
                            subsetInfo["Seed"] = subset.m_Stroke.m_Seed.ToString();
                            subsetInfo["Color"] = subset.m_Stroke.m_Color.ToString();
                            batchInfo.Add(subsetInfo);
                        }
                        var primitiveExtras = new Dictionary<string, List<Dictionary<string, string>>>
                        {
                            ["ICOSA_batchInfo"] = batchInfo
                        };
                        primitive.Extras = JToken.FromObject(primitiveExtras);
                    }
                }
            }
        }

        void AddExtension(GLTFMaterial materialNode, IExtension blend)
        {
            if (materialNode.Extensions == null)
                materialNode.Extensions = new Dictionary<string, IExtension>();
            materialNode.Extensions.Add(EXT_blend_operations.EXTENSION_NAME, blend);
        }

        public override void AfterMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
        {
            // Only handle brush materials
            if (!material.shader.name.StartsWith("Brush/")) return;

            // Strip the (Instance) suffix from the material node name
            materialNode.Name = materialNode.Name.Replace("(Instance)", "").Trim();
            materialNode.Name = $"ob-{materialNode.Name}";

            var brush = BrushCatalog.m_Instance.AllBrushes.FirstOrDefault(b => b.DurableName == materialNode.Name);

            if (brush != null && brush.BlendMode == ExportableMaterialBlendMode.AdditiveBlend)
            {
                AddExtension(materialNode, EXT_blend_operations.Add);
            }
        }

        public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            gltfRoot.Asset.Generator = $"Open Brush UnityGLTF Exporter {App.Config.m_VersionNumber}.{App.Config.m_BuildStamp})";
        }

        private static void SafeDestroy(Object o)
        {
            if (!o) return;
            if (Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }
    }
}
