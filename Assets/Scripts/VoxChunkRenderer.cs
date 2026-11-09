// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    internal sealed class VoxChunkRenderer : IDisposable
    {
        private sealed class Entry
        {
            internal Mesh Mesh;
            internal long Version;
            internal VoxChunkGrid.Chunk Chunk;
            internal ulong Signature;
        }
        private readonly Transform m_Root;
        private readonly Material m_Default;
        private readonly ushort m_BatchId;
        private readonly Dictionary<RuntimeVoxDocument.VoxelDefinition, Dictionary<Vector3Int, Entry>> m_Cache = new();
        private readonly HashSet<Mesh> m_Owned = new();
        private readonly Dictionary<RuntimeVoxDocument.RuntimeModel, Dictionary<Vector3Int, MeshFilter>> m_Nodes = new();
        private readonly Dictionary<RuntimeVoxDocument.RuntimeModel, Transform> m_ModelRoots = new();
        private bool m_Initialized;
        private readonly Dictionary<int, RuntimeVoxDocument.RuntimeMaterial> m_MaterialDefinitions = new();
        private readonly Color32[] m_Palette = new Color32[256];
        private VoxMaterialSet m_Set;
        private Material[] m_Materials;
        internal Material[] Materials => m_Materials;
        internal VoxMaterialSet Layout => m_Set;
        internal IEnumerable<Renderer> Renderers => m_Nodes.Values.SelectMany(nodes => nodes.Values)
            .Where(filter => filter != null).Select(filter => filter.GetComponent<Renderer>());

        internal VoxChunkRenderer(Transform root, Material defaultMaterial, ushort batchId)
        {
            m_Root = root;
            m_Default = defaultMaterial;
            m_BatchId = batchId;
        }

        internal void Update(RuntimeVoxDocument document, IEnumerable<string> keywords)
        {
            foreach (var model in m_Nodes.Keys.Where(model => !document.ContainsModel(model)).ToArray())
            {
                m_ModelRoots[model].gameObject.SetActive(false);
                Destroy(m_ModelRoots[model].gameObject);
                m_ModelRoots.Remove(model);
                m_Nodes.Remove(model);
            }
            bool initial = m_Set == null;
            bool rebuildMaterials = m_Set == null || m_MaterialDefinitions.Count != document.Materials.Count ||
                document.Materials.Any(pair => !m_MaterialDefinitions.TryGetValue(pair.Key, out var old) || !ReferenceEquals(old, pair.Value));
            rebuildMaterials |= !initial && document.Materials.Any(pair =>
                (pair.Value.Type == RuntimeVoxDocument.MaterialType.Glass && m_Palette[pair.Key - 1].a != document.Palette[pair.Key - 1].a) ||
                (pair.Value.Type == RuntimeVoxDocument.MaterialType.Emit && !m_Palette[pair.Key - 1].Equals(document.Palette[pair.Key - 1])));
            bool[] changedColors = new bool[256];
            for (int i = 0; i < 256; ++i) changedColors[i] = !initial && !m_Palette[i].Equals(document.Palette[i]);
            if (rebuildMaterials)
            {
                ReleaseMaterials();
                m_Set = new VoxMaterialSet(document, m_Default);
                m_Materials = m_Set.Materials.Select(material => new Material(material)).ToArray();
                foreach (var material in m_Materials)
                    foreach (string keyword in keywords) material.EnableKeyword(keyword);
                m_MaterialDefinitions.Clear();
                foreach (var pair in document.Materials) m_MaterialDefinitions.Add(pair.Key, pair.Value);
            }
            var updated = new HashSet<RuntimeVoxDocument.VoxelDefinition>();
            foreach (var model in document.Models)
            {
                Transform root = GetModelRoot(document, model);
                TrTransform pose = VoxMeshBuilder.GetModelTransform(model);
                root.localPosition = pose.translation;
                root.localRotation = pose.rotation;
                root.gameObject.SetActive(model.IsVisible);
                if (!m_Nodes.TryGetValue(model, out var nodes))
                {
                    nodes = root.GetComponentsInChildren<VoxChunkTag>(true).ToDictionary(tag => tag.Coordinate,
                        tag => tag.GetComponent<MeshFilter>());
                    m_Nodes.Add(model, nodes);
                    var legacy = root.GetComponent<MeshRenderer>();
                    if (legacy != null) legacy.enabled = false;
                    var legacyFilter = root.GetComponent<MeshFilter>();
                    if (legacyFilter != null) legacyFilter.sharedMesh = null;
                }
                if (!m_Cache.TryGetValue(model.Definition, out var meshes))
                {
                    meshes = new Dictionary<Vector3Int, Entry>();
                    m_Cache.Add(model.Definition, meshes);
                }
                bool update = updated.Add(model.Definition);
                foreach (var pair in model.Definition.Grid.Chunks)
                {
                    bool colors = false;
                    foreach (byte i in pair.Value.UsedPalette) colors |= changedColors[i - 1];
                    if (!meshes.TryGetValue(pair.Key, out var mesh))
                    {
                        mesh = new Entry();
                        if (nodes.TryGetValue(pair.Key, out var existing) && existing != null &&
                            unchecked((ulong)existing.GetComponent<VoxChunkTag>().Signature) ==
                                model.Definition.Grid.MeshHash(pair.Key, document.Palette, m_Set.PaletteSubmeshIndices, m_Set.PaletteOccludesFaces))
                        {
                            mesh.Mesh = existing.sharedMesh;
                            mesh.Version = pair.Value.Version;
                            mesh.Chunk = pair.Value;
                            mesh.Signature = unchecked((ulong)existing.GetComponent<VoxChunkTag>().Signature);
                        }
                        meshes.Add(pair.Key, mesh);
                    }
                    if (update && (mesh.Mesh == null || !ReferenceEquals(mesh.Chunk, pair.Value) ||
                        mesh.Version != pair.Value.Version || (!initial && rebuildMaterials) || colors))
                    {
                        Mesh replacement = new VoxMeshBuilder().GenerateChunkMesh(model, pair.Key, document.Palette,
                            m_Set.PaletteSubmeshIndices, m_Set.Materials.Count, m_Set.PaletteOccludesFaces);
                        if (mesh.Mesh != null && m_Owned.Remove(mesh.Mesh)) Destroy(mesh.Mesh);
                        mesh.Mesh = replacement;
                        mesh.Signature = model.Definition.Grid.MeshHash(pair.Key, document.Palette,
                            m_Set.PaletteSubmeshIndices, m_Set.PaletteOccludesFaces);
                        mesh.Version = pair.Value.Version;
                        mesh.Chunk = pair.Value;
                        m_Owned.Add(replacement);
                    }
                    bool created = !nodes.TryGetValue(pair.Key, out var filter) || filter == null;
                    if (created)
                    {
                        var obj = new GameObject($"Chunk_{pair.Key.x}_{pair.Key.y}_{pair.Key.z}");
                        obj.transform.SetParent(root, false);
                        obj.layer = root.gameObject.layer;
                        filter = obj.AddComponent<MeshFilter>();
                        obj.AddComponent<MeshRenderer>();
                        obj.AddComponent<VoxChunkTag>().Coordinate = pair.Key;
                        var block = new MaterialPropertyBlock();
                        block.SetFloat("_BatchID", m_BatchId);
                        obj.GetComponent<MeshRenderer>().SetPropertyBlock(block);
                        nodes[pair.Key] = filter;
                    }
                    if (filter.sharedMesh != mesh.Mesh)
                    {
                        filter.sharedMesh = mesh.Mesh;
                        filter.GetComponent<VoxChunkTag>().Signature = unchecked((long)mesh.Signature);
                    }
                    if (created || rebuildMaterials) filter.GetComponent<MeshRenderer>().sharedMaterials = m_Materials;
                }
                foreach (var key in nodes.Keys.Where(key => !model.Definition.Grid.Chunks.ContainsKey(key)).ToArray())
                {
                    if (nodes[key] != null)
                    {
                        nodes[key].gameObject.SetActive(false);
                        Destroy(nodes[key].gameObject);
                    }
                    nodes.Remove(key);
                }
                if (update)
                    foreach (var key in meshes.Keys.Where(key => !model.Definition.Grid.Chunks.ContainsKey(key)).ToArray())
                    {
                        if (meshes[key].Mesh != null && m_Owned.Remove(meshes[key].Mesh)) Destroy(meshes[key].Mesh);
                        meshes.Remove(key);
                    }
            }
            foreach (var definition in m_Cache.Keys.Where(definition => !updated.Contains(definition)).ToArray())
            {
                foreach (var entry in m_Cache[definition].Values)
                    if (entry.Mesh != null && m_Owned.Remove(entry.Mesh)) Destroy(entry.Mesh);
                m_Cache.Remove(definition);
            }
            if (!m_Initialized)
            {
                // A split document can adopt only part of the catalog prefab.
                // Remove model roots that were not claimed during reconciliation.
                foreach (var child in m_Root.Cast<Transform>().Where(child =>
                    child.name.StartsWith("Model_", StringComparison.Ordinal) &&
                    !m_ModelRoots.ContainsValue(child)).ToArray())
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
            Array.Copy(document.Palette, m_Palette, 256);
            m_Initialized = true;
        }

        private Transform GetModelRoot(RuntimeVoxDocument document, RuntimeVoxDocument.RuntimeModel model)
        {
            if (m_ModelRoots.TryGetValue(model, out var root)) return root;
            int index = 0;
            while (!ReferenceEquals(document.Models[index], model)) ++index;
            string prefix = $"Model_{index}_";
            if (!m_Initialized)
                foreach (Transform child in m_Root)
                    if (child.name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        m_ModelRoots.Add(model, child);
                        return child;
                    }
            var obj = new GameObject($"{prefix}{model.Name}");
            obj.transform.SetParent(m_Root, false);
            obj.layer = m_Root.gameObject.layer;
            m_ModelRoots.Add(model, obj.transform);
            return obj.transform;
        }

        private void ReleaseMaterials()
        {
            if (m_Materials != null) foreach (var material in m_Materials) Destroy(material);
            if (m_Set != null) foreach (var material in m_Set.OwnedMaterials) Destroy(material);
        }
        public void Dispose()
        {
            foreach (var mesh in m_Owned) Destroy(mesh);
            m_Owned.Clear();
            ReleaseMaterials();
        }
        private static void Destroy(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
