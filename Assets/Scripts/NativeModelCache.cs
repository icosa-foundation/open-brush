// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TiltBrush.DocumentImport;
using UnityEngine;
using UnityGLTF;
using UnityGLTF.Plugins;
using Object = UnityEngine.Object;

namespace TiltBrush
{
    /// Import-session ownership of loaded source/variant models. The generated glTF files remain
    /// in the model library and use the normal local-model index on .tilt save/reload.
    internal sealed class NativeModelCache : IDisposable
    {
        private sealed class Source
        {
            public Model Model;
            public GltfAssetDocument Document;
            public Vector3 Center;
            public Vector3 Size;
        }
        private readonly Dictionary<string, ImportResource> m_IcosaResources = new Dictionary<string, ImportResource>(StringComparer.Ordinal);
        private readonly Dictionary<string, Source> m_Sources = new Dictionary<string, Source>(StringComparer.Ordinal);
        private readonly Dictionary<string, Model> m_Variants = new Dictionary<string, Model>(StringComparer.Ordinal);
        private readonly string m_RelativeDirectory = $"Native Imports/{Guid.NewGuid():N}";
        private readonly NativeAssetWorkspace m_Workspace;

        public NativeModelCache(NativeAssetWorkspace workspace) { m_Workspace = workspace; }

        public async Task<ImportResource> IcosaResource(string assetId)
        {
            if (m_IcosaResources.TryGetValue(assetId, out var resource)) { return resource; }
            var catalog = App.IcosaAssetCatalog;
            if (catalog == null) { throw new InvalidOperationException("Icosa catalog is unavailable"); }
            catalog.RequestModelLoad(assetId, "Maquette import");
            var deadline = DateTime.UtcNow.AddMinutes(5);
            while (catalog.IsLoading(assetId))
            {
                if (DateTime.UtcNow >= deadline) { throw new TimeoutException($"Icosa asset {assetId} timed out"); }
                await Task.Delay(50);
            }
            var model = catalog.GetModel(assetId);
            if (model == null || !model.m_Valid)
            {
                throw new IOException($"Could not load Icosa asset {assetId}: {model?.Error?.message ?? "asset unavailable"}");
            }
            // The catalog handles Poly's formats (including glTF 1). Export its loaded model to
            // the native import cache's glTF 2 representation, once per asset, so save/reload does
            // not depend on the gallery cache. We do not own or modify the catalog model.
            string directory = Path.Combine(App.ModelLibraryPath(), m_RelativeDirectory, "sources", Guid.NewGuid().ToString("N"));
            ExportSource(model, assetId, directory);
            string path = Path.Combine(directory, "source.gltf");
            resource = ImportResource.FromFile(path);
            m_Sources.Add(resource.Origin, new Source
            {
                Document = new GltfAssetDocument(path),
                Center = model.m_MeshBounds.center / model.UnitsPerMetre,
                Size = model.m_MeshBounds.size / model.UnitsPerMetre
            });
            m_IcosaResources.Add(assetId, resource);
            return resource;
        }

        public async Task<(Model Model, Vector3 CanvasOffset)> MaquetteModel(ImportResource resource,
            MaquettePrimitiveData primitive, MaquettePlacement placement, Color color, string materialId)
        {
            if (resource == null) { throw new FileNotFoundException($"Missing Maquette model: {primitive.Path}"); }
            var source = await GetSource(resource);
            float contentScale = 1;
            if (primitive.Type == "GooglePoly")
            {
                // Maquette's Poly importer uses FIT with desiredSize=1 and recenter=true.
                var size = source.Size;
                float longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (!(longest > 0) || float.IsInfinity(longest)) { throw new InvalidDataException("Icosa model has invalid bounds"); }
                contentScale = 1 / longest;
            }
            bool original = primitive.LoadOption == "Original";
            Color linear = color.linear;
            float[] appearance = original ? null : new[] { linear.r, linear.g, linear.b, linear.a };
            var colorMaterial = original ? null : MaquetteMaterialAdapter.ModelMaterial(materialId, appearance);
            var model = await GetVariant(resource, source, placement, -source.Center, appearance, original ? null : materialId,
                colorMaterial: colorMaterial, contentScale: contentScale);
            Vector3 offset = Vector3.zero;
            if (primitive.UsesLegacyMeshOrigin && primitive.Type != "GooglePoly")
            {
                // Maquette's v9 migration moved the handle to the model center while preserving
                // visible placement. Apply the equivalent offset after the full hierarchy linear map.
                var values = placement.GetIntrinsicTransform(new[] { 1f, 1f, 1f }, new[] { 0f, 0f, 0f }, 1);
                Matrix4x4 matrix = Matrix4x4.identity;
                for (int i = 0; i < 16; ++i) { matrix[i] = values[i]; }
                var r = placement.Rotation;
                offset = new Quaternion(r[0], r[1], r[2], r[3]) * matrix.MultiplyPoint3x4(source.Center);
            }
            return (model, offset);
        }

        public async Task<(Model Model, string[] Unmapped)> ShapesXrModel(ImportResource resource, NativePlacement placement,
            ShapesXrDocument document, ShapesXrObjectState state)
        {
            if (resource == null) { throw new FileNotFoundException("Missing ShapesXR model resource"); }
            var source = await GetSource(resource);
            // ShapesXR preserves the source model's authored origin and materials.
            var gltf = JObject.Parse(File.ReadAllText(source.Document.Path));
            var hidden = ShapesXrModelAdapter.HiddenLodNodes(gltf);
            var overrides = ShapesXrModelAdapter.MaterialOverrides(gltf,
                ShapesXrModelAdapter.ReadMaterialProperties(document, state), out var unmapped);
            var model = await GetVariant(resource, source, placement, Vector3.zero, null, null, hidden,
                ShapesXrMaterialAdapter.ReadObjectOpacity(state), overrides);
            return (model, unmapped);
        }

        private async Task<Model> GetVariant(ImportResource resource, Source source, NativePlacement placement,
            Vector3 contentOffset, float[] appearance, string materialId, IEnumerable<int> excludedNodes = null, float opacity = 1,
            IReadOnlyList<GltfMaterialOverride> materialOverrides = null, JObject colorMaterial = null, float contentScale = 1)
        {
            string key = new JObject
            {
                ["source"] = resource.Origin,
                ["transform"] = new JArray(placement.GetIntrinsicTransform(new[] { 1f, 1f, 1f }, new[] { 0f, 0f, 0f }, 1)),
                ["offset"] = new JArray(contentOffset.x, contentOffset.y, contentOffset.z),
                ["contentScale"] = contentScale,
                ["excludedNodes"] = excludedNodes == null ? null : new JArray(excludedNodes),
                ["color"] = appearance == null ? null : new JArray(appearance),
                ["material"] = materialId,
                ["opacity"] = opacity,
                ["materialOverrides"] = materialOverrides == null ? null : new JArray(System.Linq.Enumerable.Select(materialOverrides, value => value.ToJson())),
                ["colorMaterial"] = colorMaterial?.DeepClone()
            }.ToString(Formatting.None);
            if (!m_Variants.TryGetValue(key, out var model))
            {
                string relative = $"{m_RelativeDirectory}/variants/{Guid.NewGuid():N}.gltf";
                string path = Path.Combine(App.ModelLibraryPath(), relative);
                var transforms = new List<GeometryStepData>();
                if (contentScale != 1)
                {
                    var normalization = new NativePlacement(new[]
                    {
                        (System.Numerics.Vector3.Zero, System.Numerics.Quaternion.Identity,
                            System.Numerics.Vector3.One * contentScale)
                    });
                    transforms.AddRange(normalization.LinearTransforms);
                }
                transforms.AddRange(placement.LinearTransforms);
                source.Document.WriteVariant(path, transforms,
                    new[] { contentOffset.x, contentOffset.y, contentOffset.z }, appearance, excludedNodes, opacity, materialOverrides, colorMaterial);
                model = new Model(relative);
                try
                {
                    await model.LoadModelAsync();
                    if (!model.m_Valid) { throw new InvalidDataException($"Cached model could not be loaded: {model.Error?.message}"); }
                    m_Variants.Add(key, model);
                }
                catch { model.ReleaseFromCatalog(); throw; }
            }
            return model;
        }

        private async Task<Source> GetSource(ImportResource resource)
        {
            if (m_Sources.TryGetValue(resource.Origin, out var source)) { return source; }
            string path = m_Workspace.LocalPath(resource);
            var model = new Model(Model.Location.FilePath(path));
            try
            {
                await model.LoadModelAsync();
                if (!model.m_Valid) { throw new InvalidDataException($"Source model could not be loaded: {model.Error?.message}"); }
                string directory = Path.Combine(App.ModelLibraryPath(), m_RelativeDirectory, "sources", Guid.NewGuid().ToString("N"));
                string extension = Path.GetExtension(path);
                GltfAssetDocument document;
                if (extension.Equals(".gltf", StringComparison.OrdinalIgnoreCase) || extension.Equals(".glb", StringComparison.OrdinalIgnoreCase))
                {
                    document = GltfAssetDocument.CopySource(resource, path, directory);
                }
                else
                {
                    ExportSource(model, resource.Name, directory);
                    document = new GltfAssetDocument(Path.Combine(directory, "source.gltf"));
                }
                source = new Source
                {
                    Model = model, Document = document,
                    Center = model.m_MeshBounds.center / model.UnitsPerMetre,
                    Size = model.m_MeshBounds.size / model.UnitsPerMetre
                };
                m_Sources.Add(resource.Origin, source);
                return source;
            }
            catch { model.ReleaseFromCatalog(); throw; }
        }

        private static void ExportSource(Model model, string name, string directory)
        {
            Directory.CreateDirectory(directory);
            var root = new GameObject("Imported source");
            root.transform.localScale = Vector3.one / model.UnitsPerMetre;
            var settings = ScriptableObject.CreateInstance<GLTFSettings>();
            var materials = ScriptableObject.CreateInstance<MaterialExtensionsExport>();
            var unlit = ScriptableObject.CreateInstance<UnlitMaterialsExport>();
            try
            {
                var clone = Object.Instantiate(model.m_ModelParent.gameObject, root.transform, false);
                clone.name = name;
                clone.SetActive(true);
                settings.ExportPlugins.Add(materials);
                settings.ExportPlugins.Add(unlit);
                settings.ExportAnimations = false;
                settings.BakeSkinnedMeshes = true;
                settings.ExportDisabledGameObjects = false;
                settings.UseMainCameraVisibility = false;
                settings.TryExportTexturesFromDisk = false;
                settings.ExportVertexColors = true;
                var exporter = new GLTFSceneExporter(new[] { root.transform }, new ExportContext(settings));
                exporter.SaveGLTFandBin(directory, "source");
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(settings);
                Object.Destroy(materials);
                Object.Destroy(unlit);
            }
        }

        public void Dispose()
        {
            foreach (var model in m_Variants.Values) { model.ReleaseFromCatalog(); }
            foreach (var source in m_Sources.Values) { source.Model?.ReleaseFromCatalog(); }
            m_Variants.Clear();
            m_Sources.Clear();
            m_IcosaResources.Clear();
        }
    }
}
