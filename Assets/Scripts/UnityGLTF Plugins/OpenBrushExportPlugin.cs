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
        private Dictionary<Batch, Mesh> m_OriginalBatchMeshes;
        private List<Mesh> m_TemporaryBatchMeshes;
        private List<Camera> m_CameraPathsCameras;
        private GameObject m_ThumbnailCamera;
        private bool m_WasUsingBatchedBrushes;
        private readonly List<Texture2D> m_BakedTextures = new List<Texture2D>();

        public override void BeforeSceneExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot)
        {
            if (Application.isPlaying && App.UserConfig.Export.ExportCustomSkybox)
            {
                GltfExportStandinManager.m_Instance.CreateSkyStandin();
            }
            SelectionManager.m_Instance?.ClearActiveSelection();
            _meshesToBatches = new Dictionary<int, Batch>();
            m_OriginalBatchMeshes = new Dictionary<Batch, Mesh>();
            m_TemporaryBatchMeshes = new List<Mesh>();
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
                            mesh = BrushBaker.m_Instance.ProcessMesh(mesh, stroke.m_BrushGuid.ToString());
                            stroke.m_Object.GetComponent<MeshFilter>().sharedMesh = mesh;
                            stroke.m_Object.GetComponent<MeshFilter>().mesh = mesh;
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
                        mesh = BrushBaker.m_Instance.ProcessMesh(mesh, brush.m_Guid.ToString());
                        m_TemporaryBatchMeshes.Add(mesh);
                        mf.sharedMesh = mesh;
                        mf.mesh = mesh;
                    }
                }
            }
        }

        public override bool ShouldNodeExport(GLTFSceneExporter exporter, GLTFRoot gltfRoot, Transform transform)
        {
            var batch = transform.GetComponent<Batch>();
            if (batch != null)
            {
                var mesh = transform.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.vertexCount == 0)
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
            // Only process Open Brush or Open Blocks materials
            // Use shaderName to determine if this is the case
            string shaderName = material.shader.name;

            if (shaderName.StartsWith("Brush/"))
            {

                // TODO - This assumes that every brush has a unique material with a unique name
                // Currently, this is true, but it may not always be the case
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
                // Do we need to override the regular UnityGLTF logic here?
                materialNode.DoubleSided = manifest.m_RenderBackfaces;

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

            if (App.UserConfig.Export.BakeCustomShadersToPbr)
            {
                BakeCustomShaderToPbr(exporter, material, materialNode);
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
            finally
            {
                CleanupCameraPathsCameras();
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
            m_OriginalBatchMeshes?.Clear();
            m_TemporaryBatchMeshes?.Clear();
            foreach (var bakedTexture in m_BakedTextures)
            {
                SafeDestroy(bakedTexture);
            }
            m_BakedTextures.Clear();
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
            GLTFSceneExporter exporter, Material material, GLTFMaterial materialNode)
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

            if (pbr.BaseColorTexture == null && ShouldBakeBaseColorTexture(material))
            {
                var bakedTexture = BakeMaterialBaseColor(material);
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
            else if (!hasBakedBaseColor && materialNode.PbrMetallicRoughness == null)
            {
                pbr.BaseColorFactor = ToGltfColor(material.color);
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

            if (materialNode.EmissiveTexture == null && TryExportTexture(
                    exporter, material, kEmissionTextureProperties,
                    GLTFSceneExporter.TextureMapType.Emissive, out var emissiveTexture))
            {
                materialNode.EmissiveTexture = emissiveTexture;
            }

            if (TryGetColor(material, out var emissiveColor, kEmissionColorProperties) &&
                emissiveColor.maxColorComponent > 0f)
            {
                materialNode.EmissiveFactor = ToGltfColor(emissiveColor);
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

        private static bool ShouldBakeBaseColorTexture(Material material)
        {
            if (material == null || material.shader == null ||
                (!material.shader.name.StartsWith("Brush/", StringComparison.OrdinalIgnoreCase) &&
                 !material.shader.name.StartsWith("Blocks/", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            foreach (var property in kBaseColorTextureProperties)
            {
                if (material.HasProperty(property) && material.GetTexture(property) != null)
                {
                    return false;
                }
            }
            return true;
        }

        private static Texture2D BakeMaterialBaseColor(Material material)
        {
            const int textureSize = 512;
            RenderTexture renderTexture = null;
            var previous = RenderTexture.active;
            try
            {
                renderTexture = RenderTexture.GetTemporary(
                    textureSize, textureSize, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear);
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);
                Graphics.Blit(Texture2D.whiteTexture, renderTexture, material);
                var bakedTexture = new Texture2D(
                    textureSize, textureSize, TextureFormat.RGBA32, false, true)
                {
                    name = material.name + "_BakedBaseColor"
                };
                bakedTexture.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
                bakedTexture.Apply();
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
                RenderTexture.active = previous;
                if (renderTexture != null)
                {
                    RenderTexture.ReleaseTemporary(renderTexture);
                }
            }
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
