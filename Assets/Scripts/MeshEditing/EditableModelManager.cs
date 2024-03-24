// Copyright 2022 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using TiltBrushToolkit;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    public enum GeneratorTypes
    {
        FileSystem = 0,
        GeometryData = 1,

        RegularGrids = 2,
        CatalanGrids = 10,
        OneUniformGrids = 11,
        TwoUniformGrids = 12,
        DurerGrids = 13,

        Shapes = 3,

        Radial = 4,
        Waterman = 5,
        Johnson = 6,
        ConwayString = 7,
        Uniform = 8,
        Various = 9,
    }

    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;
        public Material[] m_Materials;
        [NonSerialized] public Dictionary<Material, DynamicExportableMaterial> m_ExportableMaterials;

        void Awake()
        {
            // Taking editable model screenshots uses EditableModelManager
            // but doesn't have an App object - so catch the exception
            try
            {
                App.InitShapeRecipesPath();
            }
            catch (NullReferenceException)
            {
                Debug.LogWarning($"Failed to Init Shape Recipes Path");
            }

            m_Instance = this;

            CreateExportableMaterials();
        }

        private void CreateExportableMaterials()
        {

            Guid MakeDeterministicUniqueName(int data, string data2)
            {
                return GuidUtils.Uuid5(GuidUtils.Uuid5(
                        Guid.Empty, "internal"),
                    string.Format("{0}_{1}", data, data2)
                );
            }

            m_ExportableMaterials = new Dictionary<Material, DynamicExportableMaterial>();

            for (var i = 0; i < m_Materials.Length; i++)
            {
                var mat = m_Materials[i];
                BrushDescriptor parent = null;
                float metallic = 1;
                float gloss = 1;
                Color color = Color.white;

                switch (i)
                {
                    case 0: // Shiny
                        parent = TbtSettings.Instance.m_PbrOpaqueDoubleSided.descriptor;
                        metallic = 0.9f;
                        gloss = 0.1f;
                        break;
                    case 1: // Matte
                        parent = TbtSettings.Instance.m_PbrOpaqueDoubleSided.descriptor;
                        metallic = 0.03f;
                        gloss = 0.04f;
                        break;
                    case 2: // Unlit
                        parent = TbtSettings.Instance.m_PbrOpaqueDoubleSided.descriptor;
                        metallic = 0;
                        gloss = 0;
                        break;
                    case 3: // Diamond
                        parent = TbtSettings.Instance.m_PbrBlendDoubleSided.descriptor;
                        metallic = 0.82f;
                        gloss = 0.1f;
                        break;
                    case 4: // Metal
                        parent = TbtSettings.Instance.m_PbrOpaqueDoubleSided.descriptor;
                        metallic = 0.825f;
                        gloss = 0.825f;
                        break;
                    case 5: // Edged
                        parent = TbtSettings.Instance.m_PbrOpaqueDoubleSided.descriptor;
                        metallic = 0.9f;
                        gloss = 0.05f;
                        break;
                }

                var iem = new DynamicExportableMaterial(
                    parent: parent,
                    durableName: mat.name,
                    uniqueName: MakeDeterministicUniqueName(i, mat.name),
                    uriBase: "internal")
                {
                    BaseColorFactor = color,
                    BaseColorTex = null,
                    MetallicFactor = metallic,
                    RoughnessFactor = gloss,
                };
                m_ExportableMaterials.Add(mat, iem);
            }
        }

        public void RegenerateMesh(EditableModelWidget widget, PolyMesh poly, Material mat = null)
        {
            var go = widget.GetModelGameObject();
            if (mat == null) mat = widget.m_PolyRecipe.CurrentMaterial;
            var meshData = poly.BuildMeshData(colors: widget.m_PolyRecipe.Colors, colorMethod: widget.m_PolyRecipe.ColorMethod);
            var mesh = poly.BuildUnityMesh(meshData);
            UpdateMesh(go, mesh, mat);
            widget.m_PolyMesh = poly;
        }

        public void UpdateMesh(GameObject polyGo, Mesh mesh, Material mat)
        {
            var mf = polyGo.GetComponent<MeshFilter>();
            var mr = polyGo.GetComponent<MeshRenderer>();
            var col = polyGo.GetComponent<BoxCollider>();

            if (mf == null) mf = polyGo.AddComponent<MeshFilter>();
            if (mr == null) mr = polyGo.AddComponent<MeshRenderer>();
            if (col == null) col = polyGo.AddComponent<BoxCollider>();

            mr.material = mat;
            mf.mesh = mesh;
            col.size = mesh.bounds.size;
        }


        public EditableModelWidget GeneratePolyMesh(PolyMesh poly, PolyRecipe polyRecipe, TrTransform tr)
        {
            var meshData = poly.BuildMeshData(colors: polyRecipe.Colors, colorMethod: polyRecipe.ColorMethod);
            return GeneratePolyMesh(poly, polyRecipe, tr, meshData);
        }

        public EditableModelWidget GeneratePolyMesh(PolyMesh poly, PolyRecipe polyRecipe, TrTransform tr, PolyMesh.MeshData meshData)
        {
            // Create Mesh from PolyMesh
            // var mat = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;
            var mat = m_Materials[polyRecipe.MaterialIndex];
            var mesh = poly.BuildUnityMesh(meshData);

            // Create the EditableModel gameobject
            var polyGo = new GameObject();
            UpdateMesh(polyGo, mesh, mat);

            // Create the widget

            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.EditableModelWidgetPrefab, tr, forceTransform: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var widget = createCommand.Widget as EditableModelWidget;
            if (widget != null)
            {
                var model = new Model(Model.Location.Generated(Guid.NewGuid().ToString()));
                model.LoadEditableModel(polyGo);
                widget.Model = model;
                widget.m_PolyRecipe = polyRecipe.Clone();
                widget.m_PolyMesh = poly;
                widget.Show(true);
                createCommand.SetWidgetCost(widget.GetTiltMeterCost());
            }
            else
            {
                Debug.LogWarning("Failed to create EditableModelWidget");
            }
            return widget;
        }

        public static StencilWidget AddCustomGuide(PolyMesh poly, TrTransform tr)
        {
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(StencilType.Custom), tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var stencilWidget = createCommand.Widget as StencilWidget;
            SetCustomStencil(stencilWidget, poly);
            return stencilWidget;
        }

        public static void SetCustomStencil(StencilWidget stencilWidget, PolyMesh poly)
        {
            poly = poly.ConvexHull();
            var meshData = poly.BuildMeshData(colorMethod: ColorMethods.ByRole);
            Mesh mesh = poly.BuildUnityMesh(meshData);
            var collider = stencilWidget.GetComponentInChildren<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.GetComponentInChildren<MeshFilter>().mesh = mesh;
        }

        public static void UpdateWidgetFromPolyMesh(EditableModelWidget widget, PolyMesh poly, PolyRecipe polyRecipe)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPolyCommand(widget, poly, polyRecipe)
            );
        }
    }

}