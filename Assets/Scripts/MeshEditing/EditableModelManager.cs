using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;

        private struct EditableModelOperation
        {
            public int OperationType;
            public int Operation;
            public List<int> intParams;
            public List<float> floatParams;
            public List<string> stringParams;
        }
        
        private struct EditableModel
        {
            public PolyMesh polyMesh;
            public ColorMethods lastColorMethod;
            public List<EditableModelOperation> ops;
        }
        
        private Dictionary<string, EditableModel> m_EditableModels;

        void Awake()
        {
            m_Instance = this;
            if (m_EditableModels == null) m_EditableModels = new Dictionary<string, EditableModel>();
        }
        
        public void RegenerateMesh(EditableModelWidget widget, PolyMesh poly)
        {
            var id = widget.GetId();
            var emesh = m_EditableModels[id.guid];
            
            emesh.polyMesh = poly;
            m_EditableModels[id.guid] = emesh;
            
            var polyGo = id.gameObject;
            emesh = m_EditableModels[id.guid];
            var mat = polyGo.GetComponent<MeshRenderer>().material;
            var mesh = poly.BuildUnityMesh(colorMethod: emesh.lastColorMethod);
            UpdateMesh(polyGo, mesh, mat);
            UpdateEditableMeshRegistration(id, emesh.polyMesh, emesh.lastColorMethod);
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

        public void RegisterEditableMesh(GameObject modelGo, PolyMesh poly, ColorMethods colorMethod)
        {
            var id = modelGo.AddComponent<EditableModelId>();
            id.guid = Guid.NewGuid().ToString();
            UpdateEditableMeshRegistration(id, poly, colorMethod);
        }

        public void UpdateEditableMeshRegistration(EditableModelId id, PolyMesh poly)
        {
            var colMethod = GetColorMethod(id);
            UpdateEditableMeshRegistration(id, poly, colMethod);
        }

        public void UpdateEditableMeshRegistration(EditableModelId id, PolyMesh poly, ColorMethods colorMethod)
            {
            var emesh = new EditableModel();
            emesh.polyMesh = poly;
            emesh.lastColorMethod = colorMethod;
            id.guid = Guid.NewGuid().ToString();
            m_EditableModels[id.guid] = emesh;
        }

        public PolyMesh GetPolyMesh(EditableModelWidget widget)
        {
            return GetPolyMesh(widget.GetComponentInChildren<EditableModelId>());
        }
        
        public PolyMesh GetPolyMesh(EditableModelId id)
        {
            var guid = id.guid;
            return m_EditableModels[guid].polyMesh;
        }

        public ColorMethods GetColorMethod(EditableModelId id)
        {
            var guid = id.guid;
            return m_EditableModels[guid].lastColorMethod;
        }
    }
}