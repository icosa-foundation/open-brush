// Copyright 2022 The Open Brush Authors
// Licensed under the Apache License, Version 2.0. See LICENSE for details.

using System.Linq;
using Polyhydra.Core;
using TiltBrush.DocumentImport;
using TiltBrush.MeshEditing;
using UnityEngine;

namespace TiltBrush
{
    // Runtime subset ported from the 3D Shapes branch. The authoring recipe now uses
    // the shared generator/operations contract rather than the branch's old serializer.
    public class EditableModelWidget : ModelWidget
    {
        public GeometryRecipeData m_PolyRecipe;
        public PolyMesh m_PolyMesh;
        public Color m_Color = Color.white;
        private Mesh m_OwnedGeneratedMesh;

        internal void TakeGeneratedMeshOwnership(Mesh mesh)
        {
            Mesh previousMesh = m_OwnedGeneratedMesh;
            m_OwnedGeneratedMesh = mesh;
            if (previousMesh != null && previousMesh != mesh) { Destroy(previousMesh); }
        }

        public override void OnPreDestroy()
        {
            if (m_OwnedGeneratedMesh != null) { Destroy(m_OwnedGeneratedMesh); }
            m_OwnedGeneratedMesh = null;
            base.OnPreDestroy();
        }

        public override GrabWidget Clone() => Clone(transform.position, transform.rotation, m_Size);

        public override GrabWidget Clone(Vector3 position, Quaternion rotation, float size)
        {
            var poly = m_PolyMesh.Duplicate();
            poly.ScalingFactor = m_PolyMesh.ScalingFactor;
            var clone = EditableModelManager.GetOrCreate().GeneratePolyMesh(poly,
                new GeometryRecipeData(m_PolyRecipe.ToJson()), TrTransform.TRS(position, rotation, size), color: m_Color);
            clone.m_PreviousCanvas = m_PreviousCanvas;
            clone.transform.SetParent(transform.parent, true);
            clone.SetSignedWidgetSize(size);
            clone.CloneInitialMaterials(this);
            HierarchyUtils.RecursivelySetLayer(clone.transform, gameObject.layer);
            clone.TrySetCanvasKeywordsFromObject(transform);
            return clone;
        }

        protected override void CloneInitialMaterials(GrabWidget other)
        {
            m_WidgetRenderers = GetComponentsInChildren<Renderer>()
                .Where(renderer => renderer.gameObject != GetModelGameObject()).ToArray();
            m_InitialMaterials = m_WidgetRenderers.ToDictionary(renderer => renderer, renderer => renderer.sharedMaterials);
            m_NewMaterials = m_WidgetRenderers.ToDictionary(renderer => renderer, renderer => renderer.materials);
        }

        public GameObject GetModelGameObject() => GetComponentInChildren<ObjModelScript>().gameObject;
    }
}
