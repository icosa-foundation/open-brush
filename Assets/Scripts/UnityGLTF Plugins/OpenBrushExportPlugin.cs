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
        private const int kOpenBrushExporterContractVersion = 1;
        private const int kStaticExporterContractVersion = 1;

        private static int s_MeshFixtureExportDepth;

        private Dictionary<int, List<BatchSubset>> m_MeshBatchSubsets;
        private Dictionary<Mesh, TimestampSource> m_TimestampSources;
        private Dictionary<Batch, Mesh> m_OriginalBatchMeshes;
        private List<Mesh> m_TemporaryBatchMeshes;
        private List<Camera> m_CameraPathsCameras;
        private GameObject m_ThumbnailCamera;
        private bool m_WasUsingBatchedBrushes;
        private readonly List<Texture2D> m_BakedTextures = new List<Texture2D>();
        private Export.GlbExportMode m_ExportMode;

        private bool IsStaticExport => m_ExportMode == Export.GlbExportMode.Static;

        private readonly struct AdditiveTextureSource
        {
            public Texture Texture { get; }
            public Vector2 Scale { get; }
            public Vector2 Offset { get; }

            public AdditiveTextureSource(Texture texture, Vector2 scale, Vector2 offset)
            {
                Texture = texture;
                Scale = scale;
                Offset = offset;
            }
        }

        // Per-export state for additive brush emissive color modulation
        private GLTFRoot _gltfRoot;
        // Template additive material → emission gain
        private Dictionary<GLTFMaterial, float> _additiveBrushGains;
        // Template additive material → source texture and its Unity UV transform
        private Dictionary<GLTFMaterial, AdditiveTextureSource> _additiveBrushTextures;
        // (template material, stroke Color32) → index of per-colour clone in gltfRoot.Materials
        private Dictionary<(GLTFMaterial, Color32), int> _colorModulatedMaterials;
        // (source texture, colorKey) → cached atlas texture
        private Dictionary<(Texture, string), Texture2D> _atlasTextureCache;
        // (template material, colorKey) → index of atlas material clone in gltfRoot.Materials
        private Dictionary<(GLTFMaterial, string), int> _atlasMaterialCache;

        private const string kTimestampAttribute = "_TB_TIMESTAMP";

        public static IDisposable BeginIsolatedMeshFixtureExport()
        {
            ++s_MeshFixtureExportDepth;
            return new MeshFixtureExportScope();
        }

        private static bool IsIsolatedMeshFixtureExport => s_MeshFixtureExportDepth > 0;

        private sealed class MeshFixtureExportScope : IDisposable
        {
            private bool m_Disposed;

            public void Dispose()
            {
                if (m_Disposed) return;
                m_Disposed = true;
                --s_MeshFixtureExportDepth;
                Debug.Assert(s_MeshFixtureExportDepth >= 0);
            }
        }

        private readonly struct TimestampSource
        {
            public List<BatchSubset> BatchSubsets { get; }
            public string BatchName { get; }
            public Stroke Stroke { get; }

            private TimestampSource(
                List<BatchSubset> batchSubsets, string batchName, Stroke stroke)
            {
                BatchSubsets = batchSubsets;
                BatchName = batchName;
                Stroke = stroke;
            }

            public static TimestampSource ForBatch(
                List<BatchSubset> batchSubsets, string batchName)
            {
                return new TimestampSource(batchSubsets, batchName, null);
            }

            public static TimestampSource ForStroke(Stroke stroke)
            {
                return new TimestampSource(null, null, stroke);
            }
        }

        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            m_ExportMode = Export.CurrentGlbExportMode;
            Debug.Log($"[OB_GLB_PROFILE] Starting {m_ExportMode} GLB export");
            DestroyAtlasTextures();
            if (IsStaticExport)
            {
                _gltfRoot = gltfRoot;
                _additiveBrushGains = new Dictionary<GLTFMaterial, float>();
                _additiveBrushTextures =
                    new Dictionary<GLTFMaterial, AdditiveTextureSource>();
                _colorModulatedMaterials = new Dictionary<(GLTFMaterial, Color32), int>();
                _atlasTextureCache = new Dictionary<(Texture, string), Texture2D>();
                _atlasMaterialCache = new Dictionary<(GLTFMaterial, string), int>();
            }
            else
            {
                _gltfRoot = null;
                _additiveBrushGains = null;
                _additiveBrushTextures = null;
                _colorModulatedMaterials = null;
                _atlasTextureCache = null;
                _atlasMaterialCache = null;
            }
            m_MeshBatchSubsets = new Dictionary<int, List<BatchSubset>>();
            m_TimestampSources = new Dictionary<Mesh, TimestampSource>();
            m_OriginalBatchMeshes = new Dictionary<Batch, Mesh>();
            m_TemporaryBatchMeshes = new List<Mesh>();
            if (IsIsolatedMeshFixtureExport) return;

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
                cam.stereoTargetEye = StereoTargetEyeMask.None;
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
                    rotTimes[j] = t;
                    rotValues[j] = xf.rotation;
                }
                exporter.AddAnimationData(cam.gameObject, "rotation", anim, rotTimes, rotValues);

                var fovKnots = widget.WidgetScript.Path.FovKnots;
                var fovTimes = new float[fovKnots.Count];
                var fovValues = new object[fovKnots.Count];
                for (var j = 0; j < fovKnots.Count; j++)
                {
                    var knot = fovKnots[j];
                    var t = knot.PathT.T;
                    fovTimes[j] = t;
                    fovValues[j] = knot.CameraFov;
                }
                exporter.AddAnimationData(cam, "field of view", anim, fovTimes, fovValues);

                exporter.GetRoot().Animations.Add(anim);
            }
        }

        private void CleanupCameraPathsCameras()
        {
            if (m_CameraPathsCameras == null) return;

            foreach (var cam in m_CameraPathsCameras)
            {
                if (cam == null) continue;
                cam.enabled = false;
                Object.Destroy(cam.gameObject);
            }
            m_CameraPathsCameras.Clear();
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
                        if (mesh.vertexCount > 0)
                        {
                            var renderer = stroke.m_Object.GetComponent<Renderer>();
                            mesh = ProcessBrushMesh(
                                mesh, stroke.m_BrushGuid.ToString(), renderer?.sharedMaterial,
                                stroke.m_Object.transform.localToWorldMatrix, out _);
                            stroke.m_Object.GetComponent<MeshFilter>().sharedMesh = mesh;
                            stroke.m_Object.GetComponent<MeshFilter>().mesh = mesh;
                            if (App.UserConfig.Export.ExportStrokeTimestamp)
                            {
                                m_TimestampSources[mesh] = TimestampSource.ForStroke(stroke);
                            }
                        }
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
                    m_OriginalBatchMeshes[batch] = mf.sharedMesh;
                    if (mesh.vertexCount > 0)
                    {
                        int[] sourceVertexIndices;
                        mesh = ProcessBrushMesh(
                            mesh, brush.m_Guid.ToString(), brush.Material,
                            mf.transform.localToWorldMatrix, out sourceVertexIndices);
                        var exportSubsets = RemapBatchSubsets(
                            batch.m_Groups, sourceVertexIndices);
                        m_MeshBatchSubsets[mesh.GetHashCode()] = exportSubsets;
                        m_TemporaryBatchMeshes.Add(mesh);
                        mf.sharedMesh = mesh;
                        mf.mesh = mesh;
                        if (IsStaticExport)
                        {
                            // Static export can deduplicate brush materials because all
                            // per-stroke colour is represented in exported mesh/material data.
                            batch.gameObject.GetComponent<Renderer>().sharedMaterial = brush.Material;
                        }
                        if (App.UserConfig.Export.ExportStrokeTimestamp)
                        {
                            m_TimestampSources[mesh] =
                                TimestampSource.ForBatch(exportSubsets, batch.name);
                        }
                    }
                }
            }
        }

        private Mesh ProcessBrushMesh(
            Mesh mesh, string brushGuid, Material material, Matrix4x4 localToWorldMatrix,
            out int[] sourceVertexIndices)
        {
            sourceVertexIndices = null;
            if (IsStaticExport)
            {
                return BrushBaker.m_Instance.ProcessMeshForStaticExport(
                    mesh, brushGuid, material, localToWorldMatrix,
                    out sourceVertexIndices);
            }
            return BrushBaker.m_Instance.ProcessMesh(mesh, brushGuid);
        }

        private static List<BatchSubset> RemapBatchSubsets(
            IEnumerable<BatchSubset> subsets, int[] sourceVertexIndices)
        {
            if (sourceVertexIndices == null)
            {
                return subsets.ToList();
            }

            var remapped = new List<BatchSubset>();
            foreach (BatchSubset subset in subsets)
            {
                int sourceEnd = subset.m_StartVertIndex + subset.m_VertLength;
                int destinationStart = -1;
                int destinationLength = 0;
                for (int destination = 0; destination < sourceVertexIndices.Length; destination++)
                {
                    int source = sourceVertexIndices[destination];
                    if (source < subset.m_StartVertIndex || source >= sourceEnd)
                    {
                        continue;
                    }
                    if (destinationStart < 0)
                    {
                        destinationStart = destination;
                    }
                    else if (destination != destinationStart + destinationLength)
                    {
                        throw new InvalidOperationException(
                            "Faceted mesh vertices for a batch subset are not contiguous");
                    }
                    destinationLength++;
                }

                remapped.Add(new BatchSubset
                {
                    m_Stroke = subset.m_Stroke,
                    m_ParentBatch = subset.m_ParentBatch,
                    m_StartVertIndex =
                        destinationStart >= 0 ? destinationStart : sourceVertexIndices.Length,
                    m_VertLength = destinationLength,
                    m_Active = subset.m_Active,
                });
            }
            return remapped;
        }

        public override bool ShouldNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform)
        {
            if (transform.GetComponent<Batch>() != null)
            {
                var mf = transform.GetComponent<MeshFilter>();
                if (mf == null) return false;
                var mesh = mf.sharedMesh;
                if (mesh == null) return false;
                if (mesh.vertexCount == 0) return false;
            }

            if (transform.GetComponent<BaseBrushScript>() != null)
            {
                if (transform.gameObject.name.StartsWith("Preview "))
                {
                    return false;
                }
            }

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
            if (App.UserConfig.Export.KeepStrokes &&
                App.UserConfig.Export.ExportStrokeTimestamp)
            {
                var brush = transform.GetComponent<BaseBrushScript>();
                var mesh = transform.GetComponent<MeshFilter>()?.sharedMesh;
                if (brush?.Stroke != null && mesh != null && mesh.vertexCount > 0)
                {
                    m_TimestampSources[mesh] = TimestampSource.ForStroke(brush.Stroke);
                }
            }
            if (!App.UserConfig.Export.KeepStrokes)
            {
                // Register all batches so metadata and additive-material export can look up stroke data.
                var batch = transform.GetComponent<Batch>();
                var mf = transform.GetComponent<MeshFilter>();
                if (batch != null && mf != null &&
                    !m_MeshBatchSubsets.ContainsKey(mf.sharedMesh.GetHashCode()))
                {
                    m_MeshBatchSubsets[mf.sharedMesh.GetHashCode()] =
                        batch.m_Groups.ToList();
                }
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
                    if (m_OriginalBatchMeshes.TryGetValue(batch, out var originalMesh))
                    {
                        mf.sharedMesh = originalMesh;
                    }
                    if (IsStaticExport)
                    {
                        // Restore the per-batch material instance that runtime code depends on.
                        batch.gameObject.GetComponent<Renderer>().sharedMaterial =
                            batch.InstantiatedMaterial;
                    }
                }

                foreach (var mesh in m_TemporaryBatchMeshes)
                {
                    SafeDestroy(mesh);
                }
                m_TemporaryBatchMeshes.Clear();
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

            if (App.UserConfig.Export.ExportStrokeMetadata &&
                !App.UserConfig.Export.KeepStrokes)
            {
                if (m_MeshBatchSubsets.TryGetValue(
                    mesh.GetHashCode(), out List<BatchSubset> subsets))
                {
                    var batchInfo = new List<Dictionary<string, string>>();
                    foreach (var subset in subsets)
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

            if (!IsStaticExport || primitive.Material == null || _gltfRoot == null) return;
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
                if (!m_MeshBatchSubsets.TryGetValue(
                    mesh.GetHashCode(), out List<BatchSubset> subsetsForAtlas)) return;
                int cloneIdx = GetOrCreateAtlasMaterial(
                    mat, subsetsForAtlas, mesh, primitive, gain, exporter);
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
        private int GetOrCreateAtlasMaterial(
            GLTFMaterial source, List<BatchSubset> subsets, Mesh mesh,
            MeshPrimitive primitive, float gain, GLTFSceneExporter exporter)
        {
            // Build a tinted copy of the source brush texture for each stroke colour. glTF has
            // only one emissive texture slot, so the colour and source mask must be combined.
            var uniqueColors = subsets.Select(s => (Color32)s.m_Stroke.m_Color).Distinct().ToList();
            if (uniqueColors.Count == 0) return primitive.Material.Id;
            int N = uniqueColors.Count;
            string colorKey = ColorKey(uniqueColors);
            _additiveBrushTextures.TryGetValue(source, out var textureSource);

            // Reuse cached material clone if this (source, colorSet) was seen before —
            // but we still need to inject the per-primitive TEXCOORD accessor below.
            bool materialCached = _atlasMaterialCache.TryGetValue((source, colorKey), out int cachedIdx);

            // Get or create the atlas texture. A missing source texture naturally becomes a
            // one-pixel colour tile, preserving the previous solid-colour behaviour.
            var textureKey = (textureSource.Texture, colorKey);
            if (!_atlasTextureCache.TryGetValue(textureKey, out Texture2D atlas))
            {
                atlas = CreateTintedTextureAtlas(textureSource.Texture, uniqueColors);
                _atlasTextureCache[textureKey] = atlas;
            }

            int columns = Mathf.CeilToInt(Mathf.Sqrt(N));
            int rows = Mathf.CeilToInt(N / (float)columns);
            var colorIndices = uniqueColors
                .Select((color, colorIndex) => (color, colorIndex))
                .ToDictionary(pair => pair.color, pair => pair.colorIndex);
            Vector2[] sourceUvs = mesh.uv;

            // Remap the source UV into the tile for this stroke colour.
            int vertCount = mesh.vertexCount;
            var uv = new Vector2[vertCount];
            foreach (var subset in subsets)
            {
                int colorIdx = colorIndices[(Color32)subset.m_Stroke.m_Color];
                int tileX = colorIdx % columns;
                int tileY = colorIdx / columns;
                int end = Mathf.Min(subset.m_StartVertIndex + subset.m_VertLength, vertCount);
                for (int v = subset.m_StartVertIndex; v < end; v++)
                {
                    Vector2 sourceUv = sourceUvs.Length == vertCount
                        ? Vector2.Scale(sourceUvs[v], textureSource.Scale) + textureSource.Offset
                        : new Vector2(0.5f, 0.5f);
                    sourceUv.x = ApplyTextureWrap(sourceUv.x, textureSource.Texture);
                    sourceUv.y = ApplyTextureWrap(sourceUv.y, textureSource.Texture);
                    float atlasU = (tileX + sourceUv.x) / columns;
                    float atlasV = (tileY + sourceUv.y) / rows;
                    // UnityGLTF flips mesh UV V during export; match it for this custom accessor.
                    uv[v] = new Vector2(atlasU, 1f - atlasV);
                }
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

        private static Texture2D CreateTintedTextureAtlas(
            Texture sourceTexture, IReadOnlyList<Color32> colors)
        {
            int columns = Mathf.CeilToInt(Mathf.Sqrt(colors.Count));
            int rows = Mathf.CeilToInt(colors.Count / (float)columns);
            int maxAtlasSize = Mathf.Min(Mathf.Max(SystemInfo.maxTextureSize, 1), 4096);
            int sourceWidth = sourceTexture != null ? sourceTexture.width : 1;
            int sourceHeight = sourceTexture != null ? sourceTexture.height : 1;
            int tileWidth = Mathf.Max(1, Mathf.Min(sourceWidth, maxAtlasSize / columns));
            int tileHeight = Mathf.Max(1, Mathf.Min(sourceHeight, maxAtlasSize / rows));

            Texture2D readableSource = ReadTexture(sourceTexture, tileWidth, tileHeight);
            Color32[] sourcePixels = readableSource != null
                ? readableSource.GetPixels32()
                : new[] { new Color32(255, 255, 255, 255) };
            var atlas = new Texture2D(
                tileWidth * columns, tileHeight * rows, TextureFormat.RGBA32,
                mipChain: false, linear: false)
            {
                name = (sourceTexture != null ? sourceTexture.name : "Solid") +
                    "_StrokeColorAtlas",
                filterMode = sourceTexture != null ? sourceTexture.filterMode : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var atlasPixels = new Color32[atlas.width * atlas.height];
            for (int colorIndex = 0; colorIndex < colors.Count; colorIndex++)
            {
                int tileX = colorIndex % columns;
                int tileY = colorIndex / columns;
                Color32 tint = colors[colorIndex];
                for (int y = 0; y < tileHeight; y++)
                {
                    int sourceRow = y * tileWidth;
                    int atlasRow = (tileY * tileHeight + y) * atlas.width +
                        tileX * tileWidth;
                    for (int x = 0; x < tileWidth; x++)
                    {
                        Color32 pixel = sourcePixels[sourceRow + x];
                        atlasPixels[atlasRow + x] = new Color32(
                            (byte)(pixel.r * tint.r / 255),
                            (byte)(pixel.g * tint.g / 255),
                            (byte)(pixel.b * tint.b / 255),
                            pixel.a);
                    }
                }
            }
            atlas.SetPixels32(atlasPixels);
            atlas.Apply();
            SafeDestroy(readableSource);
            return atlas;
        }

        private static Texture2D ReadTexture(Texture source, int width, int height)
        {
            if (source == null) return null;

            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                temporary = RenderTexture.GetTemporary(
                    width, height, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Default);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                var readable = new Texture2D(
                    width, height, TextureFormat.RGBA32, false, false);
                readable.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                readable.Apply();
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null)
                    RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static float ApplyTextureWrap(float coordinate, Texture texture)
        {
            if (texture == null) return 0.5f;
            switch (texture.wrapMode)
            {
                case TextureWrapMode.Clamp:
                    return Mathf.Clamp01(coordinate);
                case TextureWrapMode.Mirror:
                    return Mathf.PingPong(coordinate, 1f);
                case TextureWrapMode.MirrorOnce:
                    return Mathf.PingPong(Mathf.Clamp(coordinate, -1f, 2f), 1f);
                default:
                    return Mathf.Repeat(coordinate, 1f);
            }
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
            if (mat.Extensions == null)
                mat.Extensions = new Dictionary<string, IExtension>();
            mat.Extensions[KHR_materials_emissive_strength_Factory.EXTENSION_NAME] =
                new KHR_materials_emissive_strength { emissiveStrength = strength };
        }

        public override void AfterMeshExport(
            GLTFSceneExporter exporter, Mesh mesh, GLTFMesh gltfMesh, int index)
        {
            if (!Application.isPlaying ||
                !App.UserConfig.Export.ExportStrokeTimestamp ||
                !m_TimestampSources.TryGetValue(mesh, out TimestampSource source))
            {
                return;
            }

            byte[] timestampData = source.Stroke != null
                ? CreateTimestampData(source.Stroke, mesh.vertexCount)
                : CreateTimestampData(
                    source.BatchSubsets, source.BatchName, mesh.vertexCount);
            if (timestampData == null)
            {
                return;
            }

            AccessorId timestampAccessor = exporter.ExportAccessor(
                timestampData,
                (uint)mesh.vertexCount,
                GLTFAccessorAttributeType.VEC3,
                GLTFComponentType.Float,
                null,
                null);
            timestampAccessor.Value.BufferView.Value.Target = BufferViewTarget.ArrayBuffer;

            foreach (MeshPrimitive primitive in gltfMesh.Primitives)
            {
                primitive.Attributes[kTimestampAttribute] = timestampAccessor;
            }
        }

        private static byte[] CreateTimestampData(
            IEnumerable<BatchSubset> subsets, string batchName, int vertexCount)
        {
            if (subsets == null || vertexCount == 0)
            {
                return null;
            }

            byte[] data = new byte[vertexCount * sizeof(float) * 3];
            foreach (BatchSubset subset in subsets)
            {
                if (subset.m_StartVertIndex < 0 || subset.m_VertLength < 0 ||
                    subset.m_StartVertIndex + subset.m_VertLength > vertexCount)
                {
                    Debug.LogWarning(
                        $"Cannot export timestamps for an invalid batch subset in {batchName}");
                    return null;
                }

                if (!WriteStrokeTimestamps(
                    data, subset.m_StartVertIndex, subset.m_VertLength, subset.m_Stroke))
                {
                    return null;
                }
            }
            return data;
        }

        private static byte[] CreateTimestampData(Batch batch, int vertexCount)
        {
            return batch == null
                ? null
                : CreateTimestampData(batch.m_Groups, batch.name, vertexCount);
        }

        private static byte[] CreateTimestampData(Stroke stroke, int vertexCount)
        {
            if (vertexCount == 0)
            {
                return null;
            }

            byte[] data = new byte[vertexCount * sizeof(float) * 3];
            return WriteStrokeTimestamps(data, 0, vertexCount, stroke) ? data : null;
        }

        // Matches the legacy exporter: x/y are the stroke endpoints in seconds and z is a
        // linear resampling of the control-point timestamps over the stroke's vertices.
        private static unsafe bool WriteStrokeTimestamps(
            byte[] data, int startVertex, int vertexCount, Stroke stroke)
        {
            PointerManager.ControlPoint[] controlPoints = stroke?.m_ControlPoints;
            if (controlPoints == null || controlPoints.Length == 0)
            {
                Debug.LogWarning("Cannot export timestamps for a stroke without control points");
                return false;
            }

            float startTime = controlPoints[0].m_TimestampMs * .001f;
            float endTime = controlPoints[controlPoints.Length - 1].m_TimestampMs * .001f;
            double controlPointFromVertex = vertexCount > 1
                ? (controlPoints.Length - 1) / ((double)vertexCount - 1)
                : 0;

            fixed (byte* dataBytes = data)
            {
                float* timestamps = (float*)dataBytes;
                for (int vertex = 0; vertex < vertexCount; ++vertex)
                {
                    double controlPointIndex = controlPointFromVertex * vertex;
                    int lowerIndex = (int)Math.Floor(controlPointIndex);
                    int upperIndex = Mathf.Min(lowerIndex + 1, controlPoints.Length - 1);
                    float t = (float)(controlPointIndex - lowerIndex);
                    float interpolatedTime = Mathf.LerpUnclamped(
                        controlPoints[lowerIndex].m_TimestampMs * .001f,
                        controlPoints[upperIndex].m_TimestampMs * .001f,
                        t);

                    int timestamp = (startVertex + vertex) * 3;
                    timestamps[timestamp] = startTime;
                    timestamps[timestamp + 1] = endTime;
                    timestamps[timestamp + 2] = interpolatedTime;
                }
            }
            return true;
        }

        void AddExtension(GLTFMaterial materialNode, IExtension ext, string name = null)
        {
            if (materialNode.Extensions == null)
                materialNode.Extensions = new Dictionary<string, IExtension>();
            materialNode.Extensions[name ?? EXT_blend_operations.EXTENSION_NAME] = ext;
        }

        public override void AfterMaterialExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Material material, GLTFMaterial materialNode)
        {
            // Only process Open Brush or Open Blocks materials
            string shaderName = material.shader.name;
            var textureBakeMode = BrushBaker.TextureBakeMode.None;
            var textureBakePass = 0;
            bool forceUnlit = false;

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

                if (IsStaticExport && BrushBaker.m_Instance != null &&
                    BrushBaker.m_Instance.TryGetTextureBakePolicy(
                        brush.m_Guid.ToString(), out var textureBakePolicy))
                {
                    textureBakeMode = textureBakePolicy.Mode;
                    textureBakePass = textureBakePolicy.BakePass;
                    forceUnlit = textureBakePolicy.ForceUnlit ||
                        textureBakeMode == BrushBaker.TextureBakeMode.UvUnlit;
                    string policyMessage = $"[OB_GLTF_BAKE] Brush {manifest.DurableName} uses texture bake mode {textureBakeMode}, pass {textureBakePass}: {textureBakePolicy.Reason}";
                    if (textureBakeMode == BrushBaker.TextureBakeMode.Unsupported)
                    {
                        Debug.LogWarning(policyMessage);
                    }
                    else
                    {
                        Debug.Log(policyMessage);
                    }
                }

                materialNode.Name = $"ob-{manifest.DurableName}";
                materialNode.DoubleSided = manifest.m_RenderBackfaces;
                var extras = materialNode.Extras as JObject ?? new JObject();
                if (!IsStaticExport)
                {
                    // TB_BrushGuid is an instruction to Open Brush-aware importers to restore
                    // the live brush shader. Static geometry must not trigger that behavior.
                    extras["TB_BrushGuid"] = manifest.m_Guid.ToString("D");
                }
                extras["TB_BrushName"] = manifest.DurableName;
                extras["TB_BlendMode"] = manifest.m_BlendMode.ToString();
                materialNode.Extras = extras;

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

                if (IsStaticExport &&
                    manifest.m_BlendMode == ExportableMaterialBlendMode.AdditiveBlend)
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
                        {
                            materialNode.EmissiveTexture = exporter.ExportTextureInfo(
                                emTex, GLTFSceneExporter.TextureMapType.Emissive);
                            _additiveBrushTextures[materialNode] = new AdditiveTextureSource(
                                emTex, material.GetTextureScale("_MainTex"),
                                material.GetTextureOffset("_MainTex"));
                        }
                    }
                }
                else if (IsStaticExport)
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

                if (IsStaticExport && shaderName == "Brush/Special/Unlit")
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

            if (IsStaticExport)
            {
                if (forceUnlit && !IsUnlitMaterial(material))
                {
                    exporter.ExportUnlit(materialNode, material);
                }
                BakeCustomShaderToPbr(
                    exporter, material, materialNode, textureBakeMode, textureBakePass);
            }
        }

        public override void AfterTextureExport(
            GLTFSceneExporter exporter, GLTFSceneExporter.UniqueTexture texture, int index,
            GLTFTexture textureNode)
        {
            if (texture.Texture != null && !string.IsNullOrEmpty(texture.Texture.name))
            {
                textureNode.Name = texture.Texture.name;
            }
        }

        public override void AfterSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            if (!Application.isPlaying) return;
            if (IsIsolatedMeshFixtureExport)
            {
                gltfRoot.Asset.Generator =
                    $"Open Brush UnityGLTF Mesh Fixture {App.Config.m_VersionNumber}." +
                    $"{App.Config.m_BuildStamp}";
                m_OriginalBatchMeshes?.Clear();
                m_TemporaryBatchMeshes?.Clear();
                return;
            }

            try
            {
                ExportCameraPaths(exporter);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error exporting camera paths: {e.Message}");
            }
            finally
            {
                CleanupCameraPathsCameras();
            }

            if (App.UserConfig.Export.ExportCustomSkybox)
            {
                GltfExportStandinManager.m_Instance.DestroySkyStandin();
            }

            gltfRoot.Asset.Generator = IsStaticExport
                ? $"Open Brush Static UnityGLTF Exporter {App.Config.m_VersionNumber}.{App.Config.m_BuildStamp})"
                : $"Open Brush UnityGLTF Exporter {App.Config.m_VersionNumber}.{App.Config.m_BuildStamp})";

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
            extras["TB_ExportProfile"] = IsStaticExport ? "static" : "openbrush";
            extras["TB_ExporterContractVersion"] = IsStaticExport
                ? kStaticExporterContractVersion
                : kOpenBrushExporterContractVersion;

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
            m_OriginalBatchMeshes?.Clear();
            m_TemporaryBatchMeshes?.Clear();
            foreach (var bakedTexture in m_BakedTextures)
            {
                SafeDestroy(bakedTexture);
            }
            m_BakedTextures.Clear();
            DestroyAtlasTextures();
            Debug.Log($"[OB_GLB_PROFILE] Completed {m_ExportMode} GLB export");
        }

        static readonly string[] kBaseColorProperties =
        {
            "_BaseColor", "_BaseColorFactor", "baseColorFactor", "_Color", "_TintColor"
        };

        static readonly string[] kBaseColorTextureProperties =
        {
            "_BaseMap", "_BaseColorTexture", "baseColorTexture", "_MainTex", "_ColorTexture"
        };

        static readonly string[] kNormalTextureProperties =
        {
            "_BumpMap", "_NormalMap", "_NormalTexture", "normalTexture"
        };

        static readonly string[] kOcclusionTextureProperties =
        {
            "_OcclusionMap", "_OcclusionTexture", "occlusionTexture", "_MaskMap"
        };

        static readonly string[] kEmissionColorProperties =
        {
            "_EmissionColor", "emissiveFactor", "_EmissiveFactor"
        };

        static readonly string[] kEmissionTextureProperties =
        {
            "_EmissionMap", "_EmissiveMap", "_EmissiveTexture", "_EmissiveColorMap",
            "emissiveTexture"
        };

        static readonly string[] kMetallicFactorProperties =
        {
            "_Metallic", "metallicFactor", "_MetallicFactor"
        };

        static readonly string[] kRoughnessFactorProperties =
        {
            "_Roughness", "roughnessFactor", "_RoughnessFactor"
        };

        static readonly string[] kSmoothnessFactorProperties =
        {
            "_Smoothness", "_Glossiness"
        };

        private void BakeCustomShaderToPbr(
            GLTFSceneExporter exporter, Material material, GLTFMaterial materialNode,
            BrushBaker.TextureBakeMode textureBakeMode, int textureBakePass)
        {
            if (materialNode == null)
            {
                return;
            }

            var pbr = materialNode.PbrMetallicRoughness ?? new PbrMetallicRoughness();
            bool pbrModified = materialNode.PbrMetallicRoughness == null;
            bool hasBakedBaseColor = false;

            if (pbr.BaseColorTexture == null && TryExportTexture(
                    exporter, material, kBaseColorTextureProperties,
                    GLTFSceneExporter.TextureMapType.BaseColor, out var baseColorTextureInfo))
            {
                pbr.BaseColorTexture = baseColorTextureInfo;
                pbrModified = true;
            }

            bool replaceBaseColorTexture =
                textureBakeMode == BrushBaker.TextureBakeMode.UvBaseColor ||
                textureBakeMode == BrushBaker.TextureBakeMode.UvUnlit ||
                textureBakeMode == BrushBaker.TextureBakeMode.PetalGradient;
            if ((pbr.BaseColorTexture == null || replaceBaseColorTexture) &&
                ShouldBakeBaseColorTexture(material, textureBakeMode))
            {
                var bakedTexture = BakeMaterialBaseColor(
                    material, textureBakeMode, textureBakePass);
                if (bakedTexture != null)
                {
                    var bakedInfo = ExportBakedTexture(exporter, material, bakedTexture);
                    if (bakedInfo != null)
                    {
                        pbr.BaseColorTexture = bakedInfo;
                        pbr.BaseColorFactor = ToGltfColor(Color.white);
                        hasBakedBaseColor = true;
                        pbrModified = true;
                        m_BakedTextures.Add(bakedTexture);
                    }
                    else
                    {
                        SafeDestroy(bakedTexture);
                    }
                }
            }

            if (!hasBakedBaseColor &&
                TryGetColor(material, out var baseColor, kBaseColorProperties))
            {
                pbr.BaseColorFactor = ToGltfColor(baseColor);
                pbrModified = true;
            }

            if (TryGetFloat(material, out var metallic, kMetallicFactorProperties))
            {
                pbr.MetallicFactor = Mathf.Clamp01(metallic);
                pbrModified = true;
            }

            if (TryGetFloat(material, out var roughness, kRoughnessFactorProperties))
            {
                pbr.RoughnessFactor = Mathf.Clamp01(roughness);
                pbrModified = true;
            }
            else if (TryGetFloat(material, out var smoothness, kSmoothnessFactorProperties))
            {
                pbr.RoughnessFactor = Mathf.Clamp01(1f - smoothness);
                pbrModified = true;
            }

            if (pbrModified)
            {
                materialNode.PbrMetallicRoughness = pbr;
            }

            if (materialNode.NormalTexture == null && TryExportNormalTexture(
                    exporter, material, kNormalTextureProperties, out var normalTexture))
            {
                materialNode.NormalTexture = normalTexture;
            }

            if (materialNode.OcclusionTexture == null && TryExportOcclusionTexture(
                    exporter, material, kOcclusionTextureProperties, out var occlusionTexture))
            {
                materialNode.OcclusionTexture = occlusionTexture;
            }

            bool addedEmissiveTexture = false;
            if (materialNode.EmissiveTexture == null && TryExportTexture(
                    exporter, material, kEmissionTextureProperties,
                    GLTFSceneExporter.TextureMapType.Emissive, out var emissiveTexture))
            {
                materialNode.EmissiveTexture = emissiveTexture;
                addedEmissiveTexture = true;
            }

            if (TryGetColor(material, out var emissiveColor, kEmissionColorProperties) &&
                emissiveColor.maxColorComponent > 0f)
            {
                materialNode.EmissiveFactor = ToGltfColor(emissiveColor);
            }
            else if (addedEmissiveTexture)
            {
                materialNode.EmissiveFactor = ToGltfColor(Color.white);
            }

            if (!materialNode.DoubleSided &&
                ((material.HasProperty("_Cull") && material.GetInt("_Cull") ==
                    (int)UnityEngine.Rendering.CullMode.Off) ||
                 (material.HasProperty("_CullMode") && material.GetInt("_CullMode") ==
                    (int)UnityEngine.Rendering.CullMode.Off)))
            {
                materialNode.DoubleSided = true;
            }

            if (IsUnlitMaterial(material) && materialNode.PbrMetallicRoughness != null)
            {
                // The UnityGLTF unlit plugin owns KHR_materials_unlit serialization.
                materialNode.PbrMetallicRoughness.MetallicFactor = 0;
                materialNode.PbrMetallicRoughness.RoughnessFactor = 1;
            }
        }

        private static bool ShouldBakeBaseColorTexture(
            Material material, BrushBaker.TextureBakeMode textureBakeMode)
        {
            if (textureBakeMode != BrushBaker.TextureBakeMode.UvBaseColor &&
                textureBakeMode != BrushBaker.TextureBakeMode.UvUnlit &&
                textureBakeMode != BrushBaker.TextureBakeMode.PetalGradient)
            {
                return false;
            }

            if (material == null || material.shader == null ||
                (!material.shader.name.StartsWith("Brush/", StringComparison.OrdinalIgnoreCase) &&
                 !material.shader.name.StartsWith("Blocks/", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        private static Texture2D BakeMaterialBaseColor(
            Material material, BrushBaker.TextureBakeMode textureBakeMode, int bakePass)
        {
            if (textureBakeMode == BrushBaker.TextureBakeMode.PetalGradient)
            {
                return BakePetalGradient(material);
            }

            const int textureSize = 512;
            RenderTexture renderTexture = null;
            Mesh bakeMesh = null;
            var previous = RenderTexture.active;
            bool matrixPushed = false;
            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    textureSize, textureSize, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);
                bakeMesh = CreateTextureBakeMesh();
                GL.PushMatrix();
                matrixPushed = true;
                GL.LoadOrtho();
                if (!material.SetPass(bakePass))
                {
                    Debug.LogWarning(
                        $"[OB_GLTF_BAKE] Shader pass {bakePass} is unavailable for material {material.name}");
                    return null;
                }
                Graphics.DrawMeshNow(bakeMesh, Matrix4x4.identity);
                var bakedTexture = new Texture2D(
                    textureSize, textureSize, TextureFormat.RGBA32, false, true)
                {
                    name = material.name + "_BakedBaseColor"
                };
                bakedTexture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
                bakedTexture.Apply();

                if (!HasVisibleBaseColorPixels(bakedTexture))
                {
                    Debug.LogWarning(
                        $"[OB_GLTF_BAKE] Ignoring unusable base color bake for material {material.name}");
                    SafeDestroy(bakedTexture);
                    return null;
                }

                return bakedTexture;
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"Failed to bake base color for material {material?.name}: {e.Message}");
                return null;
            }
            finally
            {
                if (matrixPushed)
                {
                    GL.PopMatrix();
                }
                RenderTexture.active = previous;
                SafeDestroy(bakeMesh);
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
        }

        private static Texture2D BakePetalGradient(Material material)
        {
            const int textureWidth = 512;
            const int textureHeight = 2;
            var texture = new Texture2D(
                textureWidth, textureHeight, TextureFormat.RGBA32, false, true)
            {
                name = $"{material.name}_BakedPetalGradient",
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[textureWidth * textureHeight];
            for (int x = 0; x < textureWidth; ++x)
            {
                float u = x / (textureWidth - 1f);
                float multiplier = Mathf.Lerp(0.6f, 1f, u);
                var color = new Color(multiplier, multiplier, multiplier, 1f);
                pixels[x] = color;
                pixels[x + textureWidth] = color;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Debug.Log($"[OB_GLTF_BAKE] Baked Petal UV.x gradient for material {material.name}");
            return texture;
        }

        private static Mesh CreateTextureBakeMesh()
        {
            var mesh = new Mesh
            {
                name = "OpenBrushTextureBakeMesh",
                vertices = new[]
                {
                    new Vector3(0, 0, 0),
                    new Vector3(0, 1, 0),
                    new Vector3(1, 0, 0),
                    new Vector3(1, 1, 0),
                },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                normals = new[]
                {
                    Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                },
                tangents = new[]
                {
                    new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1),
                    new Vector4(1, 0, 0, 1), new Vector4(1, 0, 0, 1),
                },
                triangles = new[] { 0, 1, 2, 2, 1, 3 },
            };
            mesh.SetUVs(0, new List<Vector4>
            {
                new Vector4(0, 0, 0, 0),
                new Vector4(0, 1, 0, 0),
                new Vector4(1, 0, 0, 0),
                new Vector4(1, 1, 0, 0),
            });
            mesh.SetUVs(1, new List<Vector4>
            {
                Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero,
            });
            mesh.SetUVs(2, new List<Vector4>
            {
                Vector4.zero, Vector4.zero, Vector4.zero, Vector4.zero,
            });
            mesh.RecalculateBounds();
            return mesh;
        }

        private static bool HasVisibleBaseColorPixels(Texture2D texture)
        {
            var pixels = texture.GetRawTextureData<Color32>();
            foreach (var pixel in pixels)
            {
                if (pixel.a > 1 && (pixel.r > 1 || pixel.g > 1 || pixel.b > 1))
                {
                    return true;
                }
            }
            return false;
        }

        private static TextureInfo ExportBakedTexture(
            GLTFSceneExporter exporter, Material material, Texture2D bakedTexture)
        {
            if (exporter == null || material == null || bakedTexture == null)
            {
                return null;
            }

            foreach (var property in kBaseColorTextureProperties)
            {
                if (!material.HasProperty(property)) continue;

                var previous = material.GetTexture(property);
                material.SetTexture(property, bakedTexture);
                try
                {
                    var exported = ExportTextureWithTransform(
                        exporter, material, bakedTexture, property,
                        GLTFSceneExporter.TextureMapType.BaseColor);
                    if (exported != null) return exported;
                }
                finally
                {
                    material.SetTexture(property, previous);
                }
            }

            material.SetTexture("_MainTex", bakedTexture);
            try
            {
                return ExportTextureWithTransform(
                    exporter, material, bakedTexture, "_MainTex",
                    GLTFSceneExporter.TextureMapType.BaseColor);
            }
            finally
            {
                material.SetTexture("_MainTex", null);
            }
        }

        private static bool IsUnlitMaterial(Material material)
        {
            if (material == null || material.shader == null) return false;

            string shaderName = material.shader.name;
            return shaderName.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   material.IsKeywordEnabled("_UNLIT") ||
                   (material.HasProperty("_UseLighting") &&
                    material.GetFloat("_UseLighting") < 0.5f) ||
                   (material.HasProperty("_EnableLighting") &&
                    material.GetFloat("_EnableLighting") < 0.5f);
        }

        private static bool TryGetFloat(
            Material material, out float value, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (!material.HasProperty(name)) continue;
                value = material.GetFloat(name);
                return true;
            }
            value = 0f;
            return false;
        }

        private static bool TryGetColor(
            Material material, out Color color, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                if (!material.HasProperty(name)) continue;
                color = material.GetColor(name);
                return true;
            }
            color = default;
            return false;
        }

        private static bool TryExportTexture(
            GLTFSceneExporter exporter, Material material, string[] propertyNames,
            string textureMapType, out TextureInfo textureInfo)
        {
            foreach (var name in propertyNames)
            {
                if (!material.HasProperty(name) || !(material.GetTexture(name) is Texture2D texture))
                {
                    continue;
                }

                textureInfo = ExportTextureWithTransform(
                    exporter, material, texture, name, textureMapType);
                if (textureInfo != null) return true;
            }
            textureInfo = null;
            return false;
        }

        private static bool TryExportNormalTexture(
            GLTFSceneExporter exporter, Material material, string[] propertyNames,
            out NormalTextureInfo textureInfo)
        {
            foreach (var name in propertyNames)
            {
                if (!material.HasProperty(name) || !(material.GetTexture(name) is Texture2D texture))
                {
                    continue;
                }

                var exported = ExportTextureWithTransform(
                    exporter, material, texture, name,
                    GLTFSceneExporter.TextureMapType.Normal);
                if (exported == null) continue;
                textureInfo = new NormalTextureInfo
                {
                    Index = exported.Index,
                    TexCoord = exported.TexCoord,
                    Extensions = exported.Extensions,
                    Extras = exported.Extras,
                    Scale = GetNormalScale(material)
                };
                return true;
            }
            textureInfo = null;
            return false;
        }

        private static bool TryExportOcclusionTexture(
            GLTFSceneExporter exporter, Material material, string[] propertyNames,
            out OcclusionTextureInfo textureInfo)
        {
            foreach (var name in propertyNames)
            {
                if (!material.HasProperty(name) || !(material.GetTexture(name) is Texture2D texture))
                {
                    continue;
                }

                var exported = ExportTextureWithTransform(
                    exporter, material, texture, name,
                    GLTFSceneExporter.TextureMapType.Occlusion);
                if (exported == null) continue;
                textureInfo = new OcclusionTextureInfo
                {
                    Index = exported.Index,
                    TexCoord = exported.TexCoord,
                    Extensions = exported.Extensions,
                    Extras = exported.Extras,
                    Strength = GetOcclusionStrength(material)
                };
                return true;
            }
            textureInfo = null;
            return false;
        }

        private static TextureInfo ExportTextureWithTransform(
            GLTFSceneExporter exporter, Material material, Texture texture,
            string propertyName, string textureMapType)
        {
            var exportSettings = exporter.GetExportSettingsForSlot(textureMapType);
            return exporter.ExportTextureInfoWithTextureTransform(
                material, texture, propertyName, exportSettings);
        }

        private static double GetNormalScale(Material material)
        {
            return TryGetFloat(material, out var scale, "_NormalScale", "_BumpScale", "normalScale")
                ? scale : 1.0f;
        }

        private static double GetOcclusionStrength(Material material)
        {
            return TryGetFloat(material, out var strength, "occlusionStrength", "_OcclusionStrength")
                ? Mathf.Clamp01(strength) : 1.0f;
        }

        private void DestroyAtlasTextures()
        {
            if (_atlasTextureCache == null) return;
            foreach (var texture in _atlasTextureCache.Values)
            {
                SafeDestroy(texture);
            }
            _atlasTextureCache.Clear();
        }

        private static GLTF.Math.Color ToGltfColor(Color color)
        {
            var linear = color.linear;
            return new GLTF.Math.Color(linear.r, linear.g, linear.b, color.a);
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
