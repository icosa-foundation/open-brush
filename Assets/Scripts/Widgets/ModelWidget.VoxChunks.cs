// Copyright 2026 The Open Brush Authors
// Licensed under the Apache License, Version 2.0 (the "License");
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TiltBrush
{
    public partial class ModelWidget
    {
        private VoxChunkRenderer m_VoxChunks;
        private bool m_VoxSnapGhostDirty;
        private readonly Dictionary<Material, Material> m_VoxKeywordMaterials = new();

        protected override void InitiateSnapping()
        {
            if (HasVoxRegionSelection) return;
            if (m_VoxSnapGhostDirty && m_ModelInstance != null)
            {
                // The ghost is a child of the model instance; detach it before
                // cloning so it cannot recursively clone its own old hierarchy.
                if (m_SnapGhost != null)
                {
                    m_SnapGhost.SetParent(null);
                    m_SnapGhost.gameObject.SetActive(false);
                    Destroy(m_SnapGhost.gameObject);
                }
                InitSnapGhost(m_ModelInstance, m_ModelInstance);
                m_VoxSnapGhostDirty = false;
            }
            base.InitiateSnapping();
        }

        protected override void CloneInitialMaterials(GrabWidget other)
        {
            if (!IsVoxModel()) { base.CloneInitialMaterials(other); return; }
            m_WidgetRenderers = GetComponentsInChildren<Renderer>(true);
            var previous = m_InitialMaterials;
            m_InitialMaterials = new Dictionary<Renderer, Material[]>();
            m_NewMaterials = new Dictionary<Renderer, Material[]>();
            for (int i = 0; i < m_WidgetRenderers.Length; ++i)
            {
                var renderer = m_WidgetRenderers[i];
                bool chunk = m_VoxChunks != null && renderer.GetComponent<VoxChunkTag>() != null &&
                    renderer.transform.IsChildOf(m_ModelInstance) && (m_SnapGhost == null || !renderer.transform.IsChildOf(m_SnapGhost));
                var materials = chunk ? m_VoxChunks.Materials :
                    previous != null && previous.TryGetValue(renderer, out var original) ? original : renderer.sharedMaterials;
                m_InitialMaterials[renderer] = materials;
                if (chunk)
                {
                    m_NewMaterials[renderer] = materials;
                    renderer.sharedMaterials = materials;
                    continue;
                }
                var instances = new Material[materials.Length];
                for (int j = 0; j < materials.Length; ++j)
                {
                    if (materials[j] == null) continue;
                    if (!m_VoxKeywordMaterials.TryGetValue(materials[j], out var instance))
                    {
                        instance = new Material(materials[j]);
                        m_VoxKeywordMaterials.Add(materials[j], instance);
                    }
                    instances[j] = instance;
                }
                m_NewMaterials[renderer] = instances;
                renderer.sharedMaterials = instances;
            }
        }

        private void RegisterVoxChunkMaterials()
        {
            m_InitialMaterials ??= new Dictionary<Renderer, Material[]>();
            m_NewMaterials ??= new Dictionary<Renderer, Material[]>();
            var chunks = new HashSet<Renderer>(m_VoxChunks.Renderers);
            foreach (var renderer in chunks)
            {
                m_InitialMaterials[renderer] = m_VoxChunks.Materials;
                m_NewMaterials[renderer] = m_VoxChunks.Materials;
            }
            foreach (var renderer in m_InitialMaterials.Keys.Where(renderer => renderer == null).ToArray())
            {
                m_InitialMaterials.Remove(renderer);
                m_NewMaterials.Remove(renderer);
            }
            m_WidgetRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in m_WidgetRenderers)
                if (!m_NewMaterials.ContainsKey(renderer))
                {
                    m_InitialMaterials[renderer] = renderer.sharedMaterials;
                    m_NewMaterials[renderer] = renderer.sharedMaterials;
                }
        }

        private void ReleaseVoxChunks()
        {
            if (m_VoxChunks == null && m_VoxKeywordMaterials.Count == 0) return;
            if (m_InitialMaterials != null)
                foreach (var pair in m_InitialMaterials)
                    if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            m_VoxChunks?.Dispose();
            m_VoxChunks = null;
            m_VoxSnapGhostDirty = false;
            foreach (var material in m_VoxKeywordMaterials.Values) DestroyOwnedVoxMaterial(material);
            m_VoxKeywordMaterials.Clear();
            m_InitialMaterials = m_NewMaterials = null;
            m_WidgetRenderers = null;
        }
    }
}
