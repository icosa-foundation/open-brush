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
            _meshesToBatches = new Dictionary<int, Batch>();

            if (App.UserConfig.Export.KeepStrokes)
            {
                App.Config.m_UseBatchedBrushes = false;
                SelectionManager.m_Instance.ClearActiveSelection();
                foreach (var stroke in SketchMemoryScript.m_Instance.GetAllUnselectedActiveStrokes())
                {
                    var batch = stroke.m_BatchSubset.m_ParentBatch.transform;
                    stroke.Uncreate();
                    // Uncreate doesn't destroy immediately, so ensure we don't also export the batch mesh
                    batch.tag = "EditorOnly";
                    batch.gameObject.SetActive(false);
                    stroke.Recreate();
                    if (App.UserConfig.Export.KeepGroups)
                    {
                        var group = stroke.Group.GetHashCode();
                        var groupTransform = GetOrCreateGroupTransform(stroke.Canvas, group);
                        stroke.m_Object.transform.SetParent(groupTransform, true);
                    }
                }
            }
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
                    if (child.name == $"Group_{group}")
                    {
                        return child;
                    }
                }
                var groupTransform = new GameObject($"Group_{group}").transform;
                groupTransform.parent = layer.transform;
                groupTransform.localPosition = Vector3.zero;
                groupTransform.localRotation = Quaternion.identity;
                groupTransform.localScale = Vector3.one;
                return groupTransform;
            }
        }

        public override void BeforeNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            foreach (Transform child in transform)
            {
                if (_ignoreList.Contains(child.name))
                {
                    child.tag = "EditorOnly";
                }
            }

            // Hide Batched strokes
            // Not sure why these still exist after Uncreate/Recreate?
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

        public override void AfterNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            if (App.UserConfig.Export.KeepStrokes && App.UserConfig.Export.ExportStrokeMetadata)
            {
                var brush = transform.GetComponent<BaseBrushScript>();
                if (brush != null)
                {
                    Stroke stroke = brush.Stroke;
                    if (stroke != null)
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
                if (!App.UserConfig.Export.KeepStrokes)
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

            var brush = BrushCatalog.m_Instance.AllBrushes.FirstOrDefault(b => b.DurableName == materialNode.Name);

            if (brush != null && brush.BlendMode == ExportableMaterialBlendMode.AdditiveBlend)
            {
                AddExtension(materialNode, EXT_blend_operations.Add);
            }
        }

        public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            if (App.UserConfig.Export.KeepStrokes)
            {
                App.Config.m_UseBatchedBrushes = true;
                foreach (var stroke in SketchMemoryScript.m_Instance.GetAllUnselectedActiveStrokes())
                {
                    stroke.Uncreate();
                    stroke.Recreate();
                    stroke.m_BatchSubset.m_ParentBatch.transform.tag = "Untagged";
                }
                if (App.UserConfig.Export.KeepGroups)
                {
                    foreach (var layer in App.Scene.LayerCanvases)
                    {
                        foreach (Transform child in layer.transform)
                        {
                            if (child.name.StartsWith($"_StrokeGroup_"))
                            {
                                SafeDestroy(child.gameObject);
                            }
                        }
                    }
                }
            }

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
