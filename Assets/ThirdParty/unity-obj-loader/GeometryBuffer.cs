using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GeometryBuffer {

	private List<ObjectData> objects;
	public List<Vector3> vertices;
	public List<Vector2> uvs;
	public List<Vector3> normals;
	public int unnamedGroupIndex = 1; // naming index for unnamed group. like "Unnamed-1"
	
	private ObjectData current;
	private class ObjectData {
		public string name;
		public List<GroupData> groups;
		public List<FaceIndices> allFaces;
		public int normalCount;
		public ObjectData() {
			groups = new List<GroupData>();
			allFaces = new List<FaceIndices>();
			normalCount = 0;
		}
	}
	
	private GroupData curgr;
	private class GroupData {
		public string name;
		public string materialName;
		public List<FaceIndices> faces;
		public GroupData() {
			faces = new List<FaceIndices>();
		}
		public bool isEmpty { get { return faces.Count == 0; } }
	}
	
	public GeometryBuffer() {
		objects = new List<ObjectData>();
		ObjectData d = new ObjectData();
		d.name = "default";
		objects.Add(d);
		current = d;
		
		GroupData g = new GroupData();
		g.name = "default";
		d.groups.Add(g);
		curgr = g;
		
		vertices = new List<Vector3>();
		uvs = new List<Vector2>();
		normals = new List<Vector3>();
	}
	
	public void PushObject(string name) {
		//Debug.Log("Adding new object " + name + ". Current is empty: " + isEmpty);
		if(isEmpty) objects.Remove(current);
		
		ObjectData n = new ObjectData();
		n.name = name;
		objects.Add(n);
		
		GroupData g = new GroupData();
		g.name = "default";
		n.groups.Add(g);
		
		curgr = g;
		current = n;
	}
	
	public void PushGroup(string name) {
		if(curgr.isEmpty) current.groups.Remove(curgr);
		GroupData g = new GroupData();
		if (name == null) {
			name = "Unnamed-"+unnamedGroupIndex;
			unnamedGroupIndex++;
		}
		g.name = name;
		current.groups.Add(g);
		curgr = g;
	}
	
	public void PushMaterialName(string name) {
		//Debug.Log("Pushing new material " + name + " with curgr.empty=" + curgr.isEmpty);
		if(!curgr.isEmpty) PushGroup(name);
		if(curgr.name == "default") curgr.name = name;
		curgr.materialName = name;
	}
	
	public void PushVertex(Vector3 v) {
		vertices.Add(v);
	}
	
	public void PushUV(Vector2 v) {
		uvs.Add(v);
	}
	
	public void PushNormal(Vector3 v) {
		normals.Add(v);
	}
	
	public void PushFace(FaceIndices f) {
		curgr.faces.Add(f);
		current.allFaces.Add(f);
		if (f.vn >= 0) {
			current.normalCount++;
		}
	}
	
	public void Trace() {
		Debug.Log("OBJ has " + objects.Count + " object(s)");
		Debug.Log("OBJ has " + vertices.Count + " vertice(s)");
		Debug.Log("OBJ has " + uvs.Count + " uv(s)");
		Debug.Log("OBJ has " + normals.Count + " normal(s)");
		foreach(ObjectData od in objects) {
			Debug.Log(od.name + " has " + od.groups.Count + " group(s)");
			foreach(GroupData gd in od.groups) {
				Debug.Log(od.name + "/" + gd.name + " has " + gd.faces.Count + " faces(s)");
			}
		}
		
	}
	
	public int numObjects { get { return objects.Count; } }	
	public bool isEmpty { get { return vertices.Count == 0; } }
	public bool hasUVs { get { return uvs.Count > 0; } }
	public bool hasNormals { get { return normals.Count > 0; } }
	
	public static int MAX_VERTICES_LIMIT_FOR_A_MESH = 64999;
	
	public void PopulateMeshes(GameObject[] gs, Dictionary<string, Material> mats) {
		if(gs.Length != numObjects) return; // Should not happen unless obj file is corrupt...
		Debug.Log("PopulateMeshes GameObjects count:"+gs.Length);
		for(int i = 0; i < gs.Length; i++) {
			ObjectData od = objects[i];
			bool objectHasNormals = (hasNormals && od.normalCount > 0);
			
			if(od.name != "default") gs[i].name = od.name;
			Debug.Log("PopulateMeshes object name:"+od.name);
			
			Vector3[] tvertices = new Vector3[od.allFaces.Count];
			Vector2[] tuvs = new Vector2[od.allFaces.Count];
			Vector3[] tnormals = new Vector3[od.allFaces.Count];
		
			int k = 0;
			foreach(FaceIndices fi in od.allFaces) {
				if (k >= MAX_VERTICES_LIMIT_FOR_A_MESH) {
					Debug.LogWarning("maximum vertex number for a mesh exceeded for object:"  + gs[i].name);
					break;
				}
				tvertices[k] = vertices[fi.vi];
				if(hasUVs) tuvs[k] = uvs[fi.vu];
				if(hasNormals && fi.vn >= 0) tnormals[k] = normals[fi.vn];
				k++;
			}
		
			Mesh m = (gs[i].GetComponent(typeof(MeshFilter)) as MeshFilter).mesh;
			m.vertices = tvertices;
			if(hasUVs) m.uv = tuvs;
			if(objectHasNormals) m.normals = tnormals;
			
			if(od.groups.Count == 1) {
				Debug.Log("PopulateMeshes only one group: "+od.groups[0].name);
				GroupData gd = od.groups[0];
				string matName = (gd.materialName != null) ? gd.materialName : "default"; // MAYBE: "default" may not enough.
				if (mats.ContainsKey(matName)) {
					gs[i].GetComponent<Renderer>().material = mats[matName];
					Debug.Log("PopulateMeshes mat:"+matName+" set.");
				}
				else {
					Debug.LogWarning("PopulateMeshes mat:"+matName+" not found.");
				}
				int[] triangles = new int[gd.faces.Count];
				for(int j = 0; j < triangles.Length; j++) triangles[j] = j;
				
				m.triangles = triangles;
				
			} else {
				int gl = od.groups.Count;
				Material[] materials = new Material[gl];
				m.subMeshCount = gl;
				int c = 0;
				
				Debug.Log("PopulateMeshes group count:"+gl);
				for(int j = 0; j < gl; j++) {
					string matName = (od.groups[j].materialName != null) ? od.groups[j].materialName : "default"; // MAYBE: "default" may not enough.
					if (mats.ContainsKey(matName)) {
						materials[j] = mats[matName];
						Debug.Log("PopulateMeshes mat:"+matName+" set.");
					}
					else {
						Debug.LogWarning("PopulateMeshes mat:"+matName+" not found.");
					}
					
					int[] triangles = new int[od.groups[j].faces.Count];
					int l = od.groups[j].faces.Count + c;
					int s = 0;
					for(; c < l; c++, s++) triangles[s] = c;
					m.SetTriangles(triangles, j);
				}
				
				gs[i].GetComponent<Renderer>().materials = materials;
			}
			if (!objectHasNormals) {
				m.RecalculateNormals();
			}
		}
	}
}



























