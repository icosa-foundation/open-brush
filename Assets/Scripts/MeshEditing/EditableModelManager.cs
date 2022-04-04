using System;
using System.Collections.Generic;
using System.Linq;
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
        
        public void RegenerateMesh(GameObject widgetGameObject, PolyMesh poly)
        {
            SetPolyMesh(widgetGameObject, poly);
            var id = widgetGameObject.GetComponentInChildren<EditableModelId>();
            var polyMeshGameObject = id.gameObject;
            var emesh = m_EditableModels[id.guid];
            var mat = polyMeshGameObject.GetComponent<MeshRenderer>().material;
            GenerateMesh(polyMeshGameObject, emesh.polyMesh, mat, emesh.lastColorMethod, true);
        }

        public void GenerateMesh(GameObject go, PolyMesh poly, Material mat, PolyMesh.ColorMethods colorMethod, bool editable)
        {
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            mr.material = mat;
            mf.mesh = poly.BuildUnityMesh(colorMethod: colorMethod);
            
            var col = go.AddComponent<BoxCollider>();
            col.size = mf.mesh.bounds.size;
            var mcol = go.AddComponent<MeshCollider>();
            mcol.sharedMesh = mf.mesh;

            if (editable)
            {
                var emesh = new EditableModel();
                emesh.polyMesh = poly;
                emesh.lastColorMethod = colorMethod;
                var id = go.GetComponent<EditableModelId>();
                if (id==null)
                {
                    id = go.AddComponent<EditableModelId>();
                    id.guid = Guid.NewGuid().ToString();
                    m_EditableModels[id.guid] = emesh;
                }
            }
        }
        
        public PolyMesh GetPolyMesh(GameObject go)
        {
            var guid = go.GetComponentInChildren<EditableModelId>().guid;
            return m_EditableModels[guid].polyMesh;
        }
        
        public PolyMesh.ColorMethods GetColorMethod(GameObject go)
        {
            var guid = go.GetComponentInChildren<EditableModelId>().guid;
            return m_EditableModels[guid].lastColorMethod;
        }
        
        public void SetPolyMesh(GameObject widgetGameobject, PolyMesh polyMesh)
        {
            var guid = widgetGameobject.GetComponentInChildren<EditableModelId>().guid;
            var emesh = m_EditableModels[guid];
            emesh.polyMesh = polyMesh;
            m_EditableModels[guid] = emesh;
        }
    }
}