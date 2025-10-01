using System;
using System.Collections.Generic;
using System.IO;
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

    // Data structures for canonical brush data
    [Serializable]
    public class CanonicalBrushData
    {
        public string guid;
        public string[] legacyGuids;
        public string name;
        public int blendMode;
        public bool enableCull;
        public DefaultParams defaultParams;
        public TextureData textures;
        public List<UnityMaterialData> unityMaterials;
    }

    [Serializable]
    public class DefaultParams
    {
        public Dictionary<string, float> floats;
        public Dictionary<string, float[]> colors;
    }

    [Serializable]
    public class TextureData
    {
        public Dictionary<string, string> names;
    }

    [Serializable]
    public class UnityMaterialData
    {
        public string name;
        public string shaderName;
        public Dictionary<string, float> floatOverrides;
        public Dictionary<string, ColorData> colorOverrides;
        public Dictionary<string, string> textureGuids;
    }

    [Serializable]
    public class ColorData
    {
        public float r, g, b, a;
    }

    [Serializable]
    public class CanonicalBrushDatabase
    {
        public Dictionary<string, CanonicalBrushData> brushes;
    }

    public class OpenBrushExportPluginConfig : GLTFExportPluginContext
    {
        private Dictionary<int, Batch> _meshesToBatches;
        private List<Camera> m_CameraPathsCameras;
        private CanonicalBrushDatabase _canonicalBrushData;

        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            if (Application.isPlaying && App.UserConfig.Export.ExportCustomSkybox)
            {
                GltfExportStandinManager.m_Instance.CreateSkyStandin();
            }
            SelectionManager.m_Instance?.ClearActiveSelection();
            _meshesToBatches = new Dictionary<int, Batch>();
            LoadCanonicalBrushData();
            GenerateCameraPathsCameras();
        }

        private void LoadCanonicalBrushData()
        {
            try
            {
                string canonicalDataPath = Path.Combine(Application.dataPath, "..", "canonical_brushes.json");
                if (File.Exists(canonicalDataPath))
                {
                    string jsonData = File.ReadAllText(canonicalDataPath);
                    var jsonObject = JObject.Parse(jsonData);
                    var brushesObj = jsonObject["brushes"] as JObject;

                    if (brushesObj != null)
                    {
                        _canonicalBrushData = new CanonicalBrushDatabase
                        {
                            brushes = new Dictionary<string, CanonicalBrushData>()
                        };

                        foreach (var prop in brushesObj.Properties())
                        {
                            var brushData = prop.Value.ToObject<CanonicalBrushData>();
                            _canonicalBrushData.brushes[prop.Name] = brushData;
                        }

                        Debug.Log($"Loaded canonical brush data: {_canonicalBrushData.brushes.Count} brushes");
                    }
                }
                else
                {
                    Debug.LogWarning($"Canonical brush data not found at {canonicalDataPath}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load canonical brush data: {e.Message}");
                Debug.LogError($"Stack trace: {e.StackTrace}");
            }
        }

        private void GenerateCameraPathsCameras()
        {
            if (!Application.isPlaying) return;
            m_CameraPathsCameras = new List<Camera>();
            var cameraPathWidgets = WidgetManager.m_Instance.CameraPathWidgets.ToArray();
            for (var i = 0; i < cameraPathWidgets.Length; i++)
            {
                var widget = cameraPathWidgets[i];
                var layer = widget.m_WidgetScript.Canvas;
                var go = GameObject.Instantiate(new GameObject(), layer.transform);
                go.name = $"CameraPath_{i}_{widget.m_WidgetScript.name}";
                var cam = go.AddComponent<Camera>();
                m_CameraPathsCameras.Add(cam);
            }
        }

        private void ExportCameraPaths(GLTFSceneExporter exporter)
        {
            var cameraPathWidgets = WidgetManager.m_Instance.CameraPathWidgets.ToArray();
            for (var i = 0; i < cameraPathWidgets.Length; i++)
            {
                var cam = m_CameraPathsCameras[i];
                var widget = cameraPathWidgets[i];

                GLTFAnimation anim = new GLTFAnimation();
                anim.Name = cam.gameObject.name;

                var posKnots = widget.WidgetScript.Path.PositionKnots;
                var posTimes = new float[posKnots.Count];
                var posValues = new object[posKnots.Count];
                for (var j = 0; j < posKnots.Count; j++)
                {
                    var knot = posKnots[j];
                    var xf = knot.KnotXf;
                    var t = knot.PathT.T;
                    posTimes[j] = t;
                    posValues[j] = xf.position;
                }
                exporter.AddAnimationData(cam.gameObject, "translation", anim, posTimes, posValues);

                var rotKnots = widget.WidgetScript.Path.RotationKnots;
                var rotTimes = new float[rotKnots.Count];
                var rotValues = new object[rotKnots.Count];
                for (var j = 0; j < rotKnots.Count; j++)
                {
                    var knot = rotKnots[j];
                    var xf = knot.KnotXf;
                    var t = knot.PathT.T;
                    posTimes[j] = t;
                    posValues[j] = xf.rotation;
                }
                exporter.AddAnimationData(cam.gameObject, "rotation", anim, posTimes, posValues);

                var fovKnots = widget.WidgetScript.Path.FovKnots;
                var fovTimes = new float[fovKnots.Count];
                var fovValues = new object[fovKnots.Count];
                for (var j = 0; j < fovKnots.Count; j++)
                {
                    var knot = fovKnots[j];
                    var xf = knot.KnotXf;
                    var t = knot.PathT.T;
                    posTimes[j] = t;
                    posValues[j] = xf.rotation;
                }
                exporter.AddAnimationData(cam, "field of view", anim, fovTimes, fovValues);

                exporter.GetRoot().Animations.Add(anim);
                GameObject.Destroy(cam);
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

        public override bool ShouldNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform)
        {
            Type[] excludedTypes =
            {
                typeof(SnapGrid3D),
                typeof(StencilWidget),
                typeof(CameraPathWidget)
            };
            bool hasExcludedComponent = excludedTypes.Any(t => transform.GetComponent(t) != null);
            bool excludedName = false; // TODO
            return !hasExcludedComponent && !excludedName;
        }

        public override void BeforeNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform, Node node)
        {
            if (transform.GetComponent<CanvasScript>() != null)
            {
                BeforeLayerExport(transform);
            }
            if (!Application.isPlaying) return;
            if (!App.UserConfig.Export.KeepStrokes &&
                App.UserConfig.Export.ExportStrokeMetadata)
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

            if (!Application.isPlaying) return;
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
            if (!Application.isPlaying) return;
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
            string shaderName = material.shader.name;

            if (shaderName.StartsWith("Brush/"))
            {
                var brushes = BrushCatalog.m_Instance.AllBrushes
                    .Where(b => b.Material.name == material.name.Replace("(Instance)", "").TrimEnd())
                    .ToList();

                if (brushes.Count == 0)
                {
                    Debug.LogError($"No matching brush found for material {material.name}");
                    return;
                }
                if (brushes.Count > 1)
                {
                    Debug.LogWarning($"Multiple brushes with the same material name: {material.name}");
                }

                var brush = brushes[0];
                var manifest = BrushCatalog.m_Instance.GetBrush(brush.m_Guid);
                string brushGuid = brush.m_Guid.ToString();

                materialNode.Name = $"ob-{manifest.DurableName}";
                materialNode.DoubleSided = manifest.m_RenderBackfaces;

                // Apply blend mode from manifest
                switch (manifest.m_BlendMode)
                {
                    case ExportableMaterialBlendMode.AdditiveBlend:
                        AddExtension(materialNode, EXT_blend_operations.Add);
                        materialNode.AlphaMode = AlphaMode.BLEND;
                        break;
                    case ExportableMaterialBlendMode.AlphaMask:
                        materialNode.AlphaMode = AlphaMode.MASK;
                        break;
                    case ExportableMaterialBlendMode.AlphaBlend:
                        materialNode.AlphaMode = AlphaMode.BLEND;
                        break;
                }

                // Use canonical brush data for enhanced PBR parameters
                // Try current GUID first, then search for it in legacy GUIDs
                CanonicalBrushData canonicalBrush = null;
                if (_canonicalBrushData?.brushes != null)
                {
                    // Try direct lookup by current GUID
                    if (_canonicalBrushData.brushes.ContainsKey(brushGuid))
                    {
                        canonicalBrush = _canonicalBrushData.brushes[brushGuid];
                    }
                    else
                    {
                        // Search for this GUID in legacyGuids arrays
                        foreach (var kvp in _canonicalBrushData.brushes)
                        {
                            var brush_data = kvp.Value;
                            if (brush_data.legacyGuids != null && brush_data.legacyGuids.Contains(brushGuid))
                            {
                                canonicalBrush = brush_data;
                                break;
                            }
                        }
                    }
                }

                if (canonicalBrush != null)
                {
                    ApplyCanonicalBrushData(material, materialNode, canonicalBrush, exporter, gltfRoot);
                }
                else
                {
                    Debug.LogWarning($"Canonical brush data not found for {manifest.DurableName} ({brushGuid})");
                }
            }
            else if (shaderName.StartsWith("Blocks/"))
            {
                ApplyBlocksMaterialData(material, materialNode, shaderName);
            }
        }

        private void ApplyCanonicalBrushData(Material material, GLTFMaterial materialNode, CanonicalBrushData brushData, GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            // Find the matching Unity material variant from canonical data
            string cleanMaterialName = material.name.Replace("(Instance)", "").TrimEnd();
            UnityMaterialData matData = brushData.unityMaterials?.FirstOrDefault(m => m.name == cleanMaterialName);

            // Merge default params with material overrides
            var floatParams = new Dictionary<string, float>(brushData.defaultParams?.floats ?? new Dictionary<string, float>());
            var colorParams = new Dictionary<string, float[]>(brushData.defaultParams?.colors ?? new Dictionary<string, float[]>());

            if (matData != null)
            {
                // Apply float overrides
                if (matData.floatOverrides != null)
                {
                    foreach (var kvp in matData.floatOverrides)
                    {
                        floatParams[kvp.Key.TrimStart('_')] = kvp.Value;
                    }
                }

                // Apply color overrides
                if (matData.colorOverrides != null)
                {
                    foreach (var kvp in matData.colorOverrides)
                    {
                        colorParams[kvp.Key.TrimStart('_')] = new float[] { kvp.Value.r, kvp.Value.g, kvp.Value.b, kvp.Value.a };
                    }
                }
            }

            // Initialize PBR if not already set
            if (materialNode.PbrMetallicRoughness == null)
            {
                materialNode.PbrMetallicRoughness = new PbrMetallicRoughness();
            }

            // Apply Metallic
            if (floatParams.ContainsKey("Metallic"))
            {
                materialNode.PbrMetallicRoughness.MetallicFactor = floatParams["Metallic"];
            }

            // Apply Roughness (derived from Glossiness or Shininess)
            if (floatParams.ContainsKey("Glossiness"))
            {
                materialNode.PbrMetallicRoughness.RoughnessFactor = 1.0 - floatParams["Glossiness"];
            }
            else if (floatParams.ContainsKey("Shininess"))
            {
                // Convert Shininess to Roughness using standard formula
                materialNode.PbrMetallicRoughness.RoughnessFactor = Mathf.Sqrt(2f / (floatParams["Shininess"] * 100f + 2f));
            }

            // Apply Base Color
            if (colorParams.ContainsKey("Color"))
            {
                var c = colorParams["Color"];
                materialNode.PbrMetallicRoughness.BaseColorFactor = new GLTF.Math.Color(c[0], c[1], c[2], c[3]);
            }

            // Apply Alpha Cutoff for masked materials
            if (floatParams.ContainsKey("Cutoff") && materialNode.AlphaMode == AlphaMode.MASK)
            {
                materialNode.AlphaCutoff = floatParams["Cutoff"];
            }

            // Apply Emission
            if (colorParams.ContainsKey("EmissionColor"))
            {
                var ec = colorParams["EmissionColor"];
                float emissionGain = floatParams.ContainsKey("EmissionGain") ? floatParams["EmissionGain"] : 1.0f;
                materialNode.EmissiveFactor = new GLTF.Math.Color(ec[0] * emissionGain, ec[1] * emissionGain, ec[2] * emissionGain, 1);
            }

            // Handle textures from Unity material
            // UnityGLTF may not export textures for brush materials, so we do it manually

            // Normal Map (_BumpMap → NormalTexture)
            if (material.HasProperty("_BumpMap"))
            {
                var bumpTex = material.GetTexture("_BumpMap");
                if (bumpTex != null && materialNode.NormalTexture == null)
                {
                    // Export with NormalChannel conversion (Unity AG format → glTF RGB format)
                    var normalSettings = new GLTFSceneExporter.TextureExportSettings
                    {
                        isValid = true,
                        conversion = GLTFSceneExporter.TextureExportSettings.Conversion.NormalChannel
                    };
                    var textureInfo = exporter.ExportTextureInfo(bumpTex, "_BumpMap", normalSettings);
                    if (textureInfo != null)
                    {
                        materialNode.NormalTexture = new NormalTextureInfo
                        {
                            Index = textureInfo.Index
                        };

                        // Apply normal scale if available
                        if (floatParams.ContainsKey("BumpScale"))
                        {
                            materialNode.NormalTexture.Scale = floatParams["BumpScale"];
                        }
                    }
                }
            }

            // Occlusion Map (_OcclusionMap → OcclusionTexture)
            if (material.HasProperty("_OcclusionMap"))
            {
                var occlusionTex = material.GetTexture("_OcclusionMap");
                if (occlusionTex != null && materialNode.OcclusionTexture == null)
                {
                    var textureInfo = exporter.ExportTextureInfo(occlusionTex, "_OcclusionMap");
                    if (textureInfo != null)
                    {
                        materialNode.OcclusionTexture = new OcclusionTextureInfo
                        {
                            Index = textureInfo.Index
                        };

                        // Apply occlusion strength if available
                        if (floatParams.ContainsKey("OcclusionStrength"))
                        {
                            materialNode.OcclusionTexture.Strength = floatParams["OcclusionStrength"];
                        }
                    }
                }
            }

            // Emission Map (_EmissionMap → EmissiveTexture)
            if (material.HasProperty("_EmissionMap"))
            {
                var emissionTex = material.GetTexture("_EmissionMap");
                if (emissionTex != null && materialNode.EmissiveTexture == null)
                {
                    var textureInfo = exporter.ExportTextureInfo(emissionTex, "_EmissionMap");
                    if (textureInfo != null)
                    {
                        materialNode.EmissiveTexture = textureInfo;
                    }
                }
            }

            // Log texture status for debugging
            string normalStatus = materialNode.NormalTexture != null ? "YES" : "NO";
            string occlusionStatus = materialNode.OcclusionTexture != null ? "YES" : "NO";
            string emissionStatus = materialNode.EmissiveTexture != null ? "YES" : "NO";
            Debug.Log($"Applied canonical data to {brushData.name}: Metallic={materialNode.PbrMetallicRoughness.MetallicFactor}, Roughness={materialNode.PbrMetallicRoughness.RoughnessFactor}, Normal={normalStatus}, Occlusion={occlusionStatus}, Emission={emissionStatus}");
        }

        private void ApplyBlocksMaterialData(Material material, GLTFMaterial materialNode, string shaderName)
        {
            float r = material.color.r;
            float g = material.color.g;
            float b = material.color.b;
            float a = material.color.a;

            var pbr = new PbrMetallicRoughness
            {
                BaseColorFactor = new GLTF.Math.Color(r, g, b, a),
                MetallicFactor = 0.0f,
                RoughnessFactor = Mathf.Sqrt(2f / (material.GetFloat("_Shininess") + 2f))
            };

            if (shaderName == "Blocks/BlocksGlass")
            {
                materialNode.AlphaMode = AlphaMode.BLEND;
                materialNode.DoubleSided = true;
            }
            else if (shaderName == "Blocks/BlocksGem")
            {
                materialNode.AlphaMode = AlphaMode.BLEND;
            }

            materialNode.PbrMetallicRoughness = pbr;
        }

        public override void AfterTextureExport(GLTFSceneExporter exporter, GLTFSceneExporter.UniqueTexture texture, int index, GLTFTexture tex)
        {
            // Use the Unity texture's original name if available
            // This gives better filenames like "OilPaint-...-MainTex.png" instead of "image74.png"
            if (texture.Texture != null && !string.IsNullOrEmpty(texture.Texture.name))
            {
                tex.Name = texture.Texture.name;
            }
        }

        public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            if (!Application.isPlaying) return;

            ExportCameraPaths(exporter);
            if (App.UserConfig.Export.ExportCustomSkybox)
            {
                GltfExportStandinManager.m_Instance.DestroySkyStandin();
            }

            gltfRoot.Asset.Generator = $"Open Brush UnityGLTF Exporter {App.Config.m_VersionNumber}.{App.Config.m_BuildStamp})";

            JToken ColorToJString(Color c) => $"{c.r}, {c.g}, {c.b}, {c.a}";
            JToken Vector3ToJString(Vector3 c) => $"{c.x}, {c.y}, {c.z}";

            var metadata = new SketchSnapshot().GetSketchMetadata();

            var settings = SceneSettings.m_Instance;
            Environment env = settings.GetDesiredPreset();
            var extras = new JObject();

            var pose = metadata.SceneTransformInRoomSpace;
            extras["TB_EnvironmentGuid"] = env.m_Guid.ToString("D");
            extras["TB_Environment"] = env.Description;
            extras["TB_UseGradient"] = settings.InGradient ? "true" : "false";
            extras["TB_SkyColorA"] = ColorToJString(settings.SkyColorA);
            extras["TB_SkyColorB"] = ColorToJString(settings.SkyColorB);
            Matrix4x4 exportFromUnity = AxisConvention.GetFromUnity(AxisConvention.kGltf2);
            extras["TB_SkyGradientDirection"] = Vector3ToJString(
                exportFromUnity * (settings.GradientOrientation * Vector3.up));
            extras["TB_FogColor"] = ColorToJString(settings.FogColor);
            extras["TB_FogDensity"] = settings.FogDensity;
            Vector3 gltfPoseTranslation = pose.translation;
            gltfPoseTranslation.x = -gltfPoseTranslation.x; // Flip X for GLTF
            extras["TB_PoseTranslation"] = Vector3ToJString(gltfPoseTranslation);
            extras["TB_PoseRotation"] = Vector3ToJString(pose.rotation.eulerAngles);
            extras["TB_PoseScale"] = pose.scale;
            extras["TB_ExportedFromVersion"] = App.Config.m_VersionNumber;

            TrTransform cameraPose = SaveLoadScript.m_Instance.ReasonableThumbnail_SS;
            // TODO - this seemed like a sensible alternative, but doesn't seem to work
            // TODO - We should also export a real GLTF camera object
            // TrTransform cameraPose = SketchControlsScript.m_Instance.GetSaveIconTool().LastSaveCameraRigState.GetLossyTrTransform();

            Vector3 gltfCamTranslation = cameraPose.translation;
            gltfCamTranslation.x = -gltfCamTranslation.x; // Flip X for GLTF
            extras["TB_CameraTranslation"] = Vector3ToJString(gltfCamTranslation);
            extras["TB_CameraRotation"] = Vector3ToJString(cameraPose.rotation.eulerAngles);
            // Experimental
            // extras["TB_metadata"] = JObject.FromObject(metadata);
            gltfRoot.Extras = extras;
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
