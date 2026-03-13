using System;
using System.Collections.Generic;
using System.Globalization;
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
        private List<Camera> m_CameraPathsCameras;
        private GameObject m_ThumbnailCamera;
        private bool m_WasUsingBatchedBrushes;

        // Per-export state for additive brush emissive color modulation
        private GLTFRoot _gltfRoot;
        // Template additive material → emission gain
        private Dictionary<GLTFMaterial, float> _additiveBrushGains;
        // (template material, stroke Color32) → index of per-colour clone in gltfRoot.Materials
        private Dictionary<(GLTFMaterial, Color32), int> _colorModulatedMaterials;
        // colorKey → cached atlas texture (colorKey = comma-separated sorted RRGGBB hex)
        private Dictionary<string, Texture2D> _atlasTextureCache;
        // (template material, colorKey) → index of atlas material clone in gltfRoot.Materials
        private Dictionary<(GLTFMaterial, string), int> _atlasMaterialCache;

        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            _gltfRoot = gltfRoot;
            _additiveBrushGains = new Dictionary<GLTFMaterial, float>();
            _colorModulatedMaterials = new Dictionary<(GLTFMaterial, Color32), int>();
            _atlasTextureCache = new Dictionary<string, Texture2D>();
            _atlasMaterialCache = new Dictionary<(GLTFMaterial, string), int>();
            _meshesToBatches = new Dictionary<int, Batch>();

            if (Application.isPlaying && App.UserConfig.Export.ExportCustomSkybox)
            {
                GltfExportStandinManager.m_Instance.CreateSkyStandin();
            }
            SelectionManager.m_Instance?.ClearActiveSelection();
            GenerateCameraPathsCameras();
            m_ThumbnailCamera = App.Instance.InstantiateThumbnailCamera();
            m_ThumbnailCamera.transform.SetParent(App.Scene.MainCanvas.transform, worldPositionStays: true);
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
                cam.enabled = false;
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
                m_WasUsingBatchedBrushes = App.Config.m_UseBatchedBrushes;
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
                        var strokeShaderName = BrushCatalog.m_Instance.GetBrush(stroke.m_BrushGuid)?.Material?.shader?.name;
                        if (strokeShaderName != "Brush/StandardSingleSided" &&
                            strokeShaderName != "Brush/StandardDoubleSided")
                            mesh.tangents = null;
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
            else
            {
                foreach (var batch in canvas.BatchManager.AllBatches())
                {
                    var brush = batch.Brush;
                    var mf = batch.gameObject.GetComponent<MeshFilter>();
                    Mesh mesh = new Mesh();
                    batch.Geometry.CopyToMesh(mesh);
                    if (mesh == null)
                    {
                        Debug.LogError($"No mesh found for brush {brush.name}");
                        continue;
                    }
                    batch.m_EditorDebugMesh = mf.sharedMesh;
                    mesh = BrushBaker.m_Instance.ProcessMesh(mesh, brush.m_Guid.ToString());
                    if (brush.Material.shader.name != "Brush/StandardSingleSided" &&
                        brush.Material.shader.name != "Brush/StandardDoubleSided")
                        mesh.tangents = null;
                    mf.sharedMesh = mesh;
                    mf.mesh = mesh;
                }
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
            if (!App.UserConfig.Export.KeepStrokes)
            {
                // Register all batches so AfterPrimitiveExport can look up stroke data
                var batch = transform.GetComponent<Batch>();
                var mf = transform.GetComponent<MeshFilter>();
                if (batch != null && mf != null)
                    _meshesToBatches[mf.sharedMesh.GetHashCode()] = batch;
            }
        }

        public void AfterLayerExport(Transform transform)
        {
            var canvas = transform.GetComponent<CanvasScript>();
            if (App.UserConfig.Export.KeepStrokes)
            {
                App.Config.m_UseBatchedBrushes = m_WasUsingBatchedBrushes;
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
            else
            {
                foreach (var batch in canvas.BatchManager.AllBatches())
                {
                    var mf = batch.gameObject.GetComponent<MeshFilter>();
                    mf.sharedMesh = batch.m_EditorDebugMesh;
                    batch.m_EditorDebugMesh = null;
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

            if (App.UserConfig.Export.ExportStrokeMetadata && App.UserConfig.Export.KeepStrokes)
            {
                Batch batch;
                if (_meshesToBatches.TryGetValue(mesh.GetHashCode(), out batch))
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
                    primitive.Extras = JToken.FromObject(new Dictionary<string, object>
                        { ["ICOSA_batchInfo"] = batchInfo });
                }
            }

            if (primitive.Material == null || _gltfRoot == null) return;
            var mat = _gltfRoot.Materials[primitive.Material.Id];
            if (!_additiveBrushGains.TryGetValue(mat, out float gain)) return;

            if (App.UserConfig.Export.KeepStrokes)
            {
                // Each primitive is one stroke with uniform vertex colour — use it directly.
                var colors = mesh.colors32;
                var strokeColor = colors.Length > 0 ? colors[0] : new Color32(255, 255, 255, 255);
                int cloneIdx = GetOrCreateColoredAdditiveMaterial(mat, strokeColor, gain, exporter);
                primitive.Material = new MaterialId { Id = cloneIdx, Root = _gltfRoot };
            }
            else
            {
                // Batch may contain strokes of different colours. Build a 1×N colour atlas and
                // inject it as TEXCOORD_7 so the emissive texture is sampled per-vertex.
                if (!_meshesToBatches.TryGetValue(mesh.GetHashCode(), out Batch batchForAtlas)) return;
                int cloneIdx = GetOrCreateAtlasMaterial(mat, batchForAtlas, mesh, primitive, gain, exporter);
                primitive.Material = new MaterialId { Id = cloneIdx, Root = _gltfRoot };
            }
        }

        // KeepStrokes=true: one clone per (material, strokeColor)
        private int GetOrCreateColoredAdditiveMaterial(GLTFMaterial source, Color32 strokeColor, float gain, GLTFSceneExporter exporter)
        {
            var key = (source, strokeColor);
            if (_colorModulatedMaterials.TryGetValue(key, out int existing)) return existing;

            var clone = CloneGltfMaterial(source);
            float r = strokeColor.r / 255f;
            float g = strokeColor.g / 255f;
            float b = strokeColor.b / 255f;
            clone.EmissiveFactor = new GLTF.Math.Color(r, g, b, 1f);
            if (gain > 1f) ApplyEmissiveStrength(clone, gain, exporter);

            _gltfRoot.Materials.Add(clone);
            int idx = _gltfRoot.Materials.Count - 1;
            _colorModulatedMaterials[key] = idx;
            return idx;
        }

        private static string ColorKey(List<Color32> colors)
        {
            return string.Join(",", colors.Select(c => $"{c.r:X2}{c.g:X2}{c.b:X2}"));
        }

        // KeepStrokes=false: one clone per (source material, color set), with TEXCOORD_n colour atlas injected
        private int GetOrCreateAtlasMaterial(GLTFMaterial source, Batch batch, Mesh mesh, MeshPrimitive primitive, float gain, GLTFSceneExporter exporter)
        {
            // Build colour atlas and per-vertex UV data
            var subsets = batch.m_Groups;
            var uniqueColors = subsets.Select(s => (Color32)s.m_Stroke.m_Color).Distinct().ToList();
            int N = uniqueColors.Count;
            string colorKey = ColorKey(uniqueColors);

            // Reuse cached material clone if this (source, colorSet) was seen before —
            // but we still need to inject the per-primitive TEXCOORD accessor below.
            bool materialCached = _atlasMaterialCache.TryGetValue((source, colorKey), out int cachedIdx);

            // Get or create the atlas texture
            if (!_atlasTextureCache.TryGetValue(colorKey, out Texture2D atlas))
            {
                atlas = new Texture2D(N, 1, TextureFormat.RGBA32, mipChain: false, linear: false);
                atlas.filterMode = FilterMode.Point;
                atlas.wrapMode = TextureWrapMode.Clamp;
                for (int i = 0; i < N; i++) atlas.SetPixel(i, 0, uniqueColors[i]);
                atlas.Apply();
                _atlasTextureCache[colorKey] = atlas;
            }

            // Per-vertex UV: U = texel centre for this stroke's colour, V = 0.5
            int vertCount = mesh.vertexCount;
            var uv = new Vector2[vertCount];
            foreach (var subset in subsets)
            {
                int colorIdx = uniqueColors.IndexOf((Color32)subset.m_Stroke.m_Color);
                float u = (colorIdx + 0.5f) / N;
                // GLTF UV origin is bottom-left; flip V (0.5 stays 0.5 for a 1-row atlas)
                int end = Mathf.Min(subset.m_StartVertIndex + subset.m_VertLength, vertCount);
                for (int v = subset.m_StartVertIndex; v < end; v++)
                    uv[v] = new Vector2(u, 0.5f);
            }

            // Build GLTF accessor for the UV data
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            var bytes = new byte[vertCount * 8];
            for (int i = 0; i < vertCount; i++)
            {
                if (uv[i].x < minX) minX = uv[i].x;
                if (uv[i].y < minY) minY = uv[i].y;
                if (uv[i].x > maxX) maxX = uv[i].x;
                if (uv[i].y > maxY) maxY = uv[i].y;
                Buffer.BlockCopy(BitConverter.GetBytes(uv[i].x), 0, bytes, i * 8,     4);
                Buffer.BlockCopy(BitConverter.GetBytes(uv[i].y), 0, bytes, i * 8 + 4, 4);
            }
            var accessorId = exporter.ExportAccessor(bytes, (uint)vertCount,
                GLTFAccessorAttributeType.VEC2, GLTFComponentType.Float,
                new List<double> { minX, minY }, new List<double> { maxX, maxY });

            // Find the next sequential TEXCOORD index after whatever the mesh already has
            int texCoordIndex = 0;
            while (primitive.Attributes.ContainsKey($"TEXCOORD_{texCoordIndex}"))
                texCoordIndex++;

            primitive.Attributes[$"TEXCOORD_{texCoordIndex}"] = accessorId;

            // Export atlas texture and build emissiveTexture pointing at that channel
            var atlasTexInfo = exporter.ExportTextureInfo(atlas, GLTFSceneExporter.TextureMapType.Emissive);
            atlasTexInfo.TexCoord = texCoordIndex;

            int materialIdx;
            if (materialCached)
            {
                materialIdx = cachedIdx;
            }
            else
            {
                var clone = CloneGltfMaterial(source);
                clone.EmissiveFactor = new GLTF.Math.Color(1f, 1f, 1f, 1f);
                clone.EmissiveTexture = atlasTexInfo;
                if (gain > 1f) ApplyEmissiveStrength(clone, gain, exporter);
                _gltfRoot.Materials.Add(clone);
                materialIdx = _gltfRoot.Materials.Count - 1;
                _atlasMaterialCache[(source, colorKey)] = materialIdx;
            }

            return materialIdx;
        }

        private static GLTFMaterial CloneGltfMaterial(GLTFMaterial src) => new GLTFMaterial
        {
            Name = src.Name,
            PbrMetallicRoughness = src.PbrMetallicRoughness,
            NormalTexture = src.NormalTexture,
            OcclusionTexture = src.OcclusionTexture,
            EmissiveTexture = src.EmissiveTexture,
            AlphaMode = src.AlphaMode,
            AlphaCutoff = src.AlphaCutoff,
            DoubleSided = src.DoubleSided,
            Extras = src.Extras,
            Extensions = src.Extensions != null
                ? new Dictionary<string, IExtension>(src.Extensions)
                : new Dictionary<string, IExtension>()
        };

        private static void ApplyEmissiveStrength(GLTFMaterial mat, float strength, GLTFSceneExporter exporter)
        {
            exporter.DeclareExtensionUsage(KHR_materials_emissive_strength_Factory.EXTENSION_NAME, false);
            mat.Extensions[KHR_materials_emissive_strength_Factory.EXTENSION_NAME] =
                new KHR_materials_emissive_strength { emissiveStrength = strength };
        }

        void AddExtension(GLTFMaterial materialNode, IExtension ext, string name = null)
        {
            if (materialNode.Extensions == null)
                materialNode.Extensions = new Dictionary<string, IExtension>();
            materialNode.Extensions[name ?? EXT_blend_operations.EXTENSION_NAME] = ext;
        }

        private PbrMetallicRoughness BuildBrushPbr(GLTFSceneExporter exporter, Material material)
        {
            var pbr = new PbrMetallicRoughness { MetallicFactor = 0f };

            if (material.HasProperty("_Color"))
            {
                var c = material.GetColor("_Color");
                pbr.BaseColorFactor = new GLTF.Math.Color(c.r, c.g, c.b, c.a);
            }
            else if (material.HasProperty("_TintColor"))
            {
                var c = material.GetColor("_TintColor");
                pbr.BaseColorFactor = new GLTF.Math.Color(c.r, c.g, c.b, c.a);
            }

            if (material.HasProperty("_MainTex"))
            {
                var tex = material.GetTexture("_MainTex");
                if (tex != null)
                    pbr.BaseColorTexture = exporter.ExportTextureInfo(tex, GLTFSceneExporter.TextureMapType.BaseColor);
            }

            // _Shininess is Unity smoothness [0,1]; roughness = 1 - smoothness
            pbr.RoughnessFactor = material.HasProperty("_Shininess")
                ? 1f - material.GetFloat("_Shininess")
                : 1f;

            return pbr;
        }

        public override void AfterMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
        {
            // Only process Open Brush or Open Blocks materials
            string shaderName = material.shader.name;

            if (shaderName.StartsWith("Brush/"))
            {
                var brushes = BrushCatalog.m_Instance.AllBrushes
                    .Where(b => b.Material.name == material.name.Replace("(Instance)", "").TrimEnd())
                    .ToList();

                switch (brushes.Count)
                {
                    case 0:
                        Debug.LogError($"No matching brush found for material {material.name}");
                        return;
                    case > 1:
                        Debug.LogWarning($"Multiple brushes with the same material name: {material.name}: {string.Join(", ", brushes.Select(b => b.name))}");
                        break;
                }

                var brush = brushes[0];
                var manifest = BrushCatalog.m_Instance.GetBrush(brush.m_Guid);

                materialNode.Name = $"ob-{manifest.DurableName}";
                materialNode.DoubleSided = manifest.m_RenderBackfaces;
                materialNode.Extras = new JObject { ["TB_BrushGuid"] = manifest.m_Guid.ToString("D") };
                materialNode.PbrMetallicRoughness = BuildBrushPbr(exporter, material);

                if (material.HasProperty("_BumpMap"))
                {
                    var bumpTex = material.GetTexture("_BumpMap");
                    if (bumpTex != null)
                        materialNode.NormalTexture = exporter.ExportNormalTextureInfo(
                            bumpTex, GLTFSceneExporter.TextureMapType.Normal, material);
                }

                switch (manifest.m_BlendMode)
                {
                    case ExportableMaterialBlendMode.AdditiveBlend:
                        exporter.DeclareExtensionUsage(EXT_blend_operations.EXTENSION_NAME, false);
                        AddExtension(materialNode, EXT_blend_operations.Add);
                        materialNode.AlphaMode = AlphaMode.BLEND;
                        break;
                    case ExportableMaterialBlendMode.AlphaMask:
                        materialNode.AlphaMode = AlphaMode.MASK;
                        if (material.HasProperty("_Cutoff"))
                            materialNode.AlphaCutoff = material.GetFloat("_Cutoff");
                        break;
                    case ExportableMaterialBlendMode.AlphaBlend:
                        materialNode.AlphaMode = AlphaMode.BLEND;
                        break;
                }

                if (manifest.m_BlendMode == ExportableMaterialBlendMode.AdditiveBlend)
                {
                    // Emissive colour comes from vertex colour, sampled per-primitive in
                    // AfterPrimitiveExport. Store gain here; emissive texture is set on clones.
                    float gain = manifest.m_EmissiveFactor;
                    if (gain <= 0f && material.HasProperty("_EmissionGain"))
                        gain = material.GetFloat("_EmissionGain");
                    if (gain <= 0f) gain = 1f;
                    _additiveBrushGains[materialNode] = gain;

                    // Set emissive texture on template now — KeepStrokes=true clones inherit it.
                    // KeepStrokes=false clones replace it with the per-batch colour atlas.
                    if (material.HasProperty("_MainTex"))
                    {
                        var emTex = material.GetTexture("_MainTex");
                        if (emTex != null)
                            materialNode.EmissiveTexture = exporter.ExportTextureInfo(
                                emTex, GLTFSceneExporter.TextureMapType.Emissive);
                    }
                }
                else
                {
                    float emissiveFactor = manifest.m_EmissiveFactor;
                    if (emissiveFactor <= 0f && material.HasProperty("_EmissionGain"))
                        emissiveFactor = material.GetFloat("_EmissionGain");
                    if (emissiveFactor > 0f)
                    {
                        float clamped = Mathf.Min(emissiveFactor, 1f);
                        materialNode.EmissiveFactor = new GLTF.Math.Color(clamped, clamped, clamped, 1f);
                        if (emissiveFactor > 1f)
                            ApplyEmissiveStrength(materialNode, emissiveFactor, exporter);
                    }
                }

                if (shaderName == "Brush/Special/Unlit")
                {
                    exporter.DeclareExtensionUsage(KHR_MaterialsUnlitExtensionFactory.EXTENSION_NAME, false);
                    AddExtension(materialNode, new KHR_MaterialsUnlitExtension(),
                        KHR_MaterialsUnlitExtensionFactory.EXTENSION_NAME);
                }
            }
            else if (shaderName.StartsWith("Blocks/"))
            {
                float r = material.color.r;
                float g = material.color.g;
                float b = material.color.b;
                float a = material.color.a;
                var pbr = new PbrMetallicRoughness
                {
                    BaseColorFactor = new GLTF.Math.Color(r, g, b, a),
                    MetallicFactor = 0.0f,
                    RoughnessFactor = material.HasProperty("_Shininess")
                        ? 1f - material.GetFloat("_Shininess")
                        : 1f
                };

                if (material.HasProperty("_MainTex"))
                {
                    var tex = material.GetTexture("_MainTex");
                    if (tex != null)
                        pbr.BaseColorTexture = exporter.ExportTextureInfo(
                            tex, GLTFSceneExporter.TextureMapType.BaseColor);
                }

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
        }

        public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            if (!Application.isPlaying) return;

            try
            {
                ExportCameraPaths(exporter);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting camera paths: {e.Message}");
            }

            if (App.UserConfig.Export.ExportCustomSkybox)
            {
                GltfExportStandinManager.m_Instance.DestroySkyStandin();
            }

            gltfRoot.Asset.Generator = $"Open Brush UnityGLTF Exporter {App.Config.m_VersionNumber}.{App.Config.m_BuildStamp})";

            JToken ColorToJString(Color c, bool includeAlpha = false) =>
                string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}" + (includeAlpha ? ", {3}" : ""), c.r, c.g, c.b, c.a);
            JToken Vector3ToJString(Vector3 c) => string.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2}", c.x, c.y, c.z);

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
            extras["TB_FogDensity"] = string.Format(CultureInfo.InvariantCulture, "{0}", settings.FogDensity);
            extras["TB_AmbientLightColor"] = ColorToJString(RenderSettings.ambientLight);
            for (int i = 0; i < App.Scene.GetNumLights(); i++)
            {
                var transform = App.Scene.GetLight(i).transform;
                Light unityLight = transform.GetComponent<Light>();
                Debug.Assert(unityLight != null);
                Color lightColor = unityLight.color * unityLight.intensity;
                lightColor.a = 1.0f;
                extras[$"TB_SceneLight{i}Color"] = ColorToJString(lightColor);
                Vector3 rot = transform.localEulerAngles;
                rot.y = 360 - rot.y; // Backwards compatibility
                rot.z = 0; // Roll is irrelevant for directional lights
                extras[$"TB_SceneLight{i}Rotation"] = Vector3ToJString(rot);
            }
            extras["TB_PoseTranslation"] = Vector3ToJString(pose.translation);
            extras["TB_PoseRotation"] = Vector3ToJString(pose.rotation.eulerAngles);
            extras["TB_PoseScale"] = string.Format(CultureInfo.InvariantCulture, "{0}", pose.scale);
            extras["TB_ExportedFromVersion"] = App.Config.m_VersionNumber;

            TrTransform cameraPose = SaveLoadScript.m_Instance.ReasonableThumbnail_SS;
            extras["TB_CameraTranslation"] = Vector3ToJString(cameraPose.translation);
            extras["TB_CameraRotation"] = Vector3ToJString(cameraPose.rotation.eulerAngles);

            // This is a new mode that solves the issue of finding a sane pivot for Orbit Camera Controller
            // And better suits Open Brush sketches
            extras["TB_FlyMode"] = "true";

            // Experimental
            // extras["TB_metadata"] = JObject.FromObject(metadata);
            gltfRoot.Extras = extras;

            Object.Destroy(m_ThumbnailCamera);
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
