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
        Polygon = 3,
        
        Rotational = 4,
        Waterman = 5,
        Johnson = 6,
        ConwayString = 7,
        Wythoff = 8,
    }
    
    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;

        public struct EditableModelOperation
        {
            public PolyMesh.Operation Operation;
            public List<float> floatParams;
        }

        public struct EditableModel
        {
            public GeneratorTypes GeneratorType { get;}
            public PolyMesh PolyMesh { get; private set;}
            public ColorMethods ColorMethod { get;}
            public Dictionary<string, object> GeneratorParameters { get; set; }
            // private List<EditableModelOperation> Ops;

            public EditableModel(PolyMesh polyMesh, ColorMethods colorMethod, GeneratorTypes type, Dictionary<string, object> generatorParameters)
            {
                GeneratorType = type;
                PolyMesh = polyMesh;
                ColorMethod = colorMethod;
                GeneratorParameters = generatorParameters;
                // Ops = ops;
            }
            public void SetPolyMesh(PolyMesh poly)
            {
                PolyMesh = poly;
            }
        }
        
        private Dictionary<string, EditableModel> m_EditableModels;
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
            var mesh = poly.BuildUnityMesh(colorMethod: emesh.ColorMethod);
            UpdateMesh(polyGo, mesh, mat);
            UpdateEditableMeshEntry(id, emesh.PolyMesh);
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

        public void RegisterEditableMesh(GameObject modelGo, PolyMesh poly, ColorMethods colorMethod, GeneratorTypes type, Dictionary<string, object> parameters=null)
        {
            var id = modelGo.AddComponent<EditableModelId>();
            id.guid = Guid.NewGuid().ToString();
            var emesh = new EditableModel(poly, colorMethod, type, parameters);
            m_EditableModels[id.guid] = emesh;
        }

        private void UpdateEditableMeshEntry(EditableModelId id, PolyMesh poly)
        {
            var emesh = m_EditableModels[id.guid];
            emesh.SetPolyMesh(poly);
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
        
        public void GeneratePolyMesh(PolyMesh poly, TrTransform tr, ColorMethods colMethod, GeneratorTypes type, Dictionary<string, object> parameters=null)
        {
            // Create Mesh from PolyMesh
            var mat = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;
            var mesh = poly.BuildUnityMesh(colorMethod: colMethod);

            // Create the EditableModel gameobject 
            var polyGo = new GameObject();
            UpdateMesh(polyGo, mesh, mat);
            RegisterEditableMesh(polyGo, poly, colMethod, type, parameters);

            // Create the widget
            CreateWidgetCommand createCommand = new CreateWidgetCommand(
                WidgetManager.m_Instance.EditableModelWidgetPrefab, tr);
            SketchMemoryScript.m_Instance.PerformAndRecordCommand(createCommand);
            var widget = createCommand.Widget as EditableModelWidget;
            if (widget != null)
            {
                var model = new Model(Model.Location.Generated(polyGo.GetComponent<EditableModelId>()));
                model.LoadEditableModel(polyGo);
                widget.Model = model;
                widget.Show(true);
                createCommand.SetWidgetCost(widget.GetTiltMeterCost());
                
                // TODO Do we need to do this?
                // Also see _ImportModel 
                // WidgetManager.m_Instance.WidgetsDormant = false;
                // SketchControlsScript.m_Instance.EatGazeObjectInput();
                // SelectionManager.m_Instance.RemoveFromSelection(false);
            }
            else
            {
                Debug.LogWarning("Failed to create EditableModelWidget");
            }
        }
    }
}