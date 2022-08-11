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
using Polyhydra.Core;
using TiltBrushToolkit;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    public enum GeneratorTypes
    {
        FileSystem = 0,
        GeometryData = 1,
        Grid = 2,
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

        private EditableModel m_CurrentModel;
        private Dictionary<string, EditableModel> m_EditableModels;
        public Dictionary<string, EditableModel> EditableModels => m_EditableModels;

        // List of widgets that should be updated whenever we rebuild the preview
        public HashSet<EditableModelWidget> LinkedWidgets;

        public static EditableModel CurrentModel
        {
            get
            {
                if (m_Instance.m_CurrentModel == null)
                {
                    m_Instance.m_CurrentModel = new EditableModel(GeneratorTypes.Uniform);
                }
                return m_Instance.m_CurrentModel;
            }
            set => m_Instance.m_CurrentModel = value;
        }

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

            if (m_EditableModels == null) m_EditableModels = new Dictionary<string, EditableModel>();
            if (LinkedWidgets == null) LinkedWidgets = new HashSet<EditableModelWidget>();
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

        public void UpdateEditableModel(EditableModelWidget widget, EditableModel emodel)
        {
            var id = widget.GetId();
            m_EditableModels[id.guid] = emodel;
        }

        public void RegenerateMesh(EditableModelWidget widget, PolyMesh poly, Material mat = null)
        {
            var id = widget.GetId();
            var emodel = m_EditableModels[id.guid];
            if (mat == null) mat = id.gameObject.GetComponent<MeshRenderer>().material;
            var meshData = poly.BuildMeshData(colors: emodel.Colors, colorMethod: emodel.ColorMethod);
            var mesh = poly.BuildUnityMesh(meshData);
            UpdateMesh(id.gameObject, mesh, mat);
            emodel.SetPolyMesh(poly);
            m_EditableModels[id.guid] = emodel;
        }

        public void RecordOperation(EditableModelWidget widget, Dictionary<string, object> parameters)
        {
            var id = widget.GetId();
            var emesh = m_EditableModels[id.guid];
            emesh.Operations.Add(parameters);
        }

        public void RemoveLastOperation(EditableModelWidget widget)
        {
            var id = widget.GetId();
            var emesh = m_EditableModels[id.guid];
            emesh.Operations.RemoveAt(emesh.Operations.Count - 1);
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

        public void RegisterEditableMesh(
            GameObject modelGo, PolyMesh poly,
            Color[] colors, ColorMethods colorMethod, int materialIndex,
            GeneratorTypes type, Dictionary<string, object> parameters = null)
        {
            var id = modelGo.AddComponent<EditableModelId>();
            id.guid = Guid.NewGuid().ToString();
            var emesh = new EditableModel(poly, colors, colorMethod, materialIndex, type, parameters);
            m_EditableModels[id.guid] = emesh;
        }

        public PolyMesh GetPolyMesh(EditableModelWidget widget)
        {
            return GetPolyMesh(widget.GetComponentInChildren<EditableModelId>());
        }

        public PolyMesh GetPolyMesh(EditableModelId id)
        {
            var guid = id.guid;
            return m_EditableModels[guid].PolyMesh;
        }

        public ColorMethods GetColorMethod(EditableModelId id)
        {
            var guid = id.guid;
            return m_EditableModels[guid].ColorMethod;
        }

        public void GeneratePolyMesh(PolyMesh poly, TrTransform tr,
                                     ColorMethods colMethod,
                                     GeneratorTypes generatorType,
                                     Color[] colors = null,
                                     int materialIndex = 0,
                                     Dictionary<string, object> parameters = null,
                                     List<Dictionary<string, object>> operations = null)
        {
            // Create Mesh from PolyMesh
            // var mat = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;
            var mat = m_Materials[materialIndex];
            var meshData = poly.BuildMeshData(colors: colors, colorMethod: colMethod);
            var mesh = poly.BuildUnityMesh(meshData);

            // Create the EditableModel gameobject
            var polyGo = new GameObject();
            UpdateMesh(polyGo, mesh, mat);
            RegisterEditableMesh(polyGo, poly, colors, colMethod, materialIndex, generatorType, parameters);

            // Create the widget
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.EditableModelWidgetPrefab, tr, spawnAtEnd: true);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var widget = createCommand.Widget as EditableModelWidget;
            if (widget != null)
            {
                var model = new Model(Model.Location.Generated(polyGo.GetComponent<EditableModelId>()));
                model.LoadEditableModel(polyGo);
                widget.Model = model;
                widget.Show(true);
                foreach (var op in operations)
                {
                    RecordOperation(widget, op);
                }
                createCommand.SetWidgetCost(widget.GetTiltMeterCost());
            }
            else
            {
                Debug.LogWarning("Failed to create EditableModelWidget");
            }
        }

        public static StencilWidget AddCustomGuide(PolyMesh poly, TrTransform tr)
        {
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.GetStencilPrefab(StencilType.Custom), tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var stencilWidget = createCommand.Widget as StencilWidget;
            poly = poly.ConvexHull();
            var meshData = poly.BuildMeshData(colorMethod: ColorMethods.ByRole);
            Mesh mesh = poly.BuildUnityMesh(meshData);
            var collider = stencilWidget.GetComponentInChildren<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.GetComponentInChildren<MeshFilter>().mesh = mesh;
            return stencilWidget;
        }

        public void CloneEditableModel(EditableModelWidget clone)
        {
            var id = clone.GetComponentInChildren<EditableModelId>(true);
            var prevId = id.guid;
            id.guid = Guid.NewGuid().ToString();
            m_EditableModels[id.guid] = m_EditableModels[prevId];
        }
        public static void UpdateWidgetFromPolyMesh(EditableModelWidget widget, PolyMesh poly)
        {
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(
                new ModifyPolyCommand(widget, poly, EditableModelManager.CurrentModel)
            );
        }
    }

}