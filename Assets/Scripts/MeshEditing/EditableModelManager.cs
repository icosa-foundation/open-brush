using System;
using System.Collections.Generic;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush.MeshEditing
{
    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;
        private struct EditableModel
        {
            public PolyMesh polyMesh;
            public PolyMesh.ColorMethods lastColorMethod;
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

        public void UpdateMesh(GameObject go, Mesh mesh, Material mat)
        {
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            var col = go.GetComponent<BoxCollider>();
            
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            
            mr.material = mat;
            mf.mesh = mesh;
            col.size = mesh.bounds.size;
        }

        public void RegisterEditableMesh(GameObject modelGo, PolyMesh poly, PolyMesh.ColorMethods colorMethod)
        {
            var id = modelGo.AddComponent<EditableModelId>();
            id.guid = Guid.NewGuid().ToString();
            UpdateEditableMeshRegistration(id, poly, colorMethod);

        }
        
        public void UpdateEditableMeshRegistration(EditableModelId id, PolyMesh poly, PolyMesh.ColorMethods colorMethod)
        {
            var emesh = new EditableModel();
            emesh.polyMesh = poly;
            emesh.lastColorMethod = colorMethod;
            id.guid = Guid.NewGuid().ToString();
            m_EditableModels[id.guid] = emesh;
        }

        public PolyMesh GetPolyMesh(EditableModelId id)
        {
            var guid = id.guid;
            return m_EditableModels[guid].polyMesh;
        }
        
        public PolyMesh.ColorMethods GetColorMethod(EditableModelId id)
        {
            var guid = id.guid;
            return m_EditableModels[guid].lastColorMethod;
        }
    }
}