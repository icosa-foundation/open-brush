using System;
using System.Collections.Generic;
using Polyhydra.Core;
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

        public struct EditableModel
        {
            public Color[] Colors { get; }
            public GeneratorTypes GeneratorType { get; }
            public PolyMesh PolyMesh { get; private set; }
            public ColorMethods ColorMethod { get; }
            public Dictionary<string, object> GeneratorParameters { get; }
            public List<Dictionary<string, object>> Operations { get; }

            public EditableModel(PolyMesh polyMesh, Color[] colors, ColorMethods colorMethod, GeneratorTypes type, Dictionary<string, object> generatorParameters)
            {
                GeneratorType = type;
                PolyMesh = polyMesh;
                Colors = (Color[])colors.Clone();
                ColorMethod = colorMethod;
                GeneratorParameters = generatorParameters;
                Operations = new List<Dictionary<string, object>>();
            }

            public EditableModel(PolyMesh polyMesh, Color[] colors, ColorMethods colorMethod,
                                 GeneratorTypes type, Dictionary<string, object> generatorParameters,
                                 List<Dictionary<string, object>> operations)
            {
                GeneratorType = type;
                PolyMesh = polyMesh;
                Colors = (Color[])colors.Clone();
                ColorMethod = colorMethod;
                GeneratorParameters = generatorParameters;
                Operations = operations;
            }

            public void SetPolyMesh(PolyMesh poly)
            {
                PolyMesh = poly;
            }
        }

        private Dictionary<string, EditableModel> m_EditableModels;
        [NonSerialized] public PreviewPolyhedron m_PreviewPolyhedron;
        public Dictionary<string, EditableModel> EditableModels => m_EditableModels;

        void Awake()
        {
            m_Instance = this;
            if (m_EditableModels == null) m_EditableModels = new Dictionary<string, EditableModel>();
        }

        public void RegenerateMesh(EditableModelWidget widget, PolyMesh poly)
        {
            var id = widget.GetId();
            var emesh = m_EditableModels[id.guid];

            emesh.SetPolyMesh(poly);
            m_EditableModels[id.guid] = emesh;

            var polyGo = id.gameObject;
            emesh = m_EditableModels[id.guid];
            var mat = polyGo.GetComponent<MeshRenderer>().material;
            var meshData = poly.BuildMeshData(colors: emesh.Colors, colorMethod: emesh.ColorMethod);
            var mesh = poly.BuildUnityMesh(meshData);
            UpdateMesh(polyGo, mesh, mat);
            emesh.SetPolyMesh(poly);
            m_EditableModels[id.guid] = emesh;
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

        public void RegisterEditableMesh(GameObject modelGo, PolyMesh poly, Color[] colors, ColorMethods colorMethod, GeneratorTypes type, Dictionary<string, object> parameters = null)
        {
            var id = modelGo.AddComponent<EditableModelId>();
            id.guid = Guid.NewGuid().ToString();
            var emesh = new EditableModel(poly, colors, colorMethod, type, parameters);
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
                                     Dictionary<string, object> parameters = null,
                                     List<Dictionary<string, object>> operations = null)
        {
            // Create Mesh from PolyMesh
            var mat = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;

            var meshData = poly.BuildMeshData(colors: colors, colorMethod: colMethod);
            var mesh = poly.BuildUnityMesh(meshData);

            // Create the EditableModel gameobject
            var polyGo = new GameObject();
            UpdateMesh(polyGo, mesh, mat);
            RegisterEditableMesh(polyGo, poly, colors, colMethod, generatorType, parameters);

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
    }
}