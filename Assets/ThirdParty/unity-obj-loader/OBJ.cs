using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.IO;

public class OBJ : MonoBehaviour {
	
	public string objPath;
	
	/* OBJ file tags */
	private const string O 	= "o";
	private const string G 	= "g";
	private const string V 	= "v";
	private const string VT = "vt";
	private const string VN = "vn";
	private const string F 	= "f";
	private const string MTL = "mtllib";
	private const string UML = "usemtl";

	/* MTL file tags */
	private const string NML = "newmtl";
	private const string NS = "Ns"; // Shininess
	private const string KA = "Ka"; // Ambient component (not supported)
	private const string KD = "Kd"; // Diffuse component
	private const string KS = "Ks"; // Specular component
	private const string D = "d"; 	// Transparency (not supported)
	private const string TR = "Tr";	// Same as 'd'
	private const string ILLUM = "illum"; // Illumination model. 1 - diffuse, 2 - specular
	private const string MAP_KA = "map_Ka"; // Ambient texture
	private const string MAP_KD = "map_Kd"; // Diffuse texture
	private const string MAP_KS = "map_Ks"; // Specular texture
	private const string MAP_KE = "map_Ke"; // Emissive texture
	private const string MAP_BUMP = "map_bump"; // Bump map texture
	private const string BUMP = "bump"; // Bump map texture
	
	private string basepath;
	private string mtllib;
	private GeometryBuffer buffer;

	public void BeginLoad()
	{
		buffer = new GeometryBuffer ();
		StartCoroutine (Load (objPath));
	}
	
	public IEnumerator Load(string path) {
		basepath = (path.IndexOf("/") == -1) ? "" : path.Substring(0, path.LastIndexOf("/") + 1);
		
		WWW loader = new WWW(path);
		yield return loader;
		SetGeometryData(loader.text);
		
		if(hasMaterials) {
			loader = new WWW(basepath + mtllib);
			Debug.Log("base path = "+basepath);
			Debug.Log("MTL path = "+(basepath + mtllib));
			yield return loader;
			if (loader.error != null) {
				Debug.LogError(loader.error);
			}
			else {
				SetMaterialData(loader.text);
			}
			
			foreach(MaterialData m in materialData) {
				if(m.diffuseTexPath != null) {
					WWW texloader = GetTextureLoader(m, m.diffuseTexPath);
					yield return texloader;
					if (texloader.error != null) {
						Debug.LogError(texloader.error);
					} else {
						m.diffuseTex = texloader.texture;
					}
				}
				if(m.bumpTexPath != null) {
					WWW texloader = GetTextureLoader(m, m.bumpTexPath);
					yield return texloader;
					if (texloader.error != null) {
						Debug.LogError(texloader.error);
					} else {
						m.bumpTex = texloader.texture;
					}
				}
			}
		}
		
		Build();

	}
	
	private WWW GetTextureLoader(MaterialData m, string texpath) {
		char[] separators = {'/', '\\'};
		string[] components = texpath.Split(separators);
		string filename = components[components.Length-1];
		string ext = Path.GetExtension(filename).ToLower();
		if (ext != ".png" && ext != ".jpg") {
			Debug.LogWarning("maybe unsupported texture format:"+ext);
		}
		WWW texloader = new WWW(basepath + filename);
		Debug.Log("texture path for material("+m.name+") = "+(basepath + filename));
		return texloader;
	}
	
	private void GetFaceIndicesByOneFaceLine(FaceIndices[] faces, string[] p, bool isFaceIndexPlus) {
		if (isFaceIndexPlus) {
			for(int j = 1; j < p.Length; j++) {
				string[] c = p[j].Trim().Split("/".ToCharArray());
				FaceIndices fi = new FaceIndices();
				// vertex
				int vi = ci(c[0]);
				fi.vi = vi-1;
				// uv
				if(c.Length > 1 && c[1] != "") {
					int vu = ci(c[1]);
					fi.vu = vu-1;
				}
				// normal
				if(c.Length > 2 && c[2] != "") {
					int vn = ci(c[2]);
					fi.vn = vn-1;
				}
				else { 
					fi.vn = -1;
				}
				faces[j-1] = fi;
			}
		}
		else { // for minus index
			int vertexCount = buffer.vertices.Count;
			int uvCount = buffer.uvs.Count;
			for(int j = 1; j < p.Length; j++) {
				string[] c = p[j].Trim().Split("/".ToCharArray());
				FaceIndices fi = new FaceIndices();
				// vertex
				int vi = ci(c[0]);
				fi.vi = vertexCount + vi;
				// uv
				if(c.Length > 1 && c[1] != "") {
					int vu = ci(c[1]);
					fi.vu = uvCount + vu;
				}
				// normal
				if(c.Length > 2 && c[2] != "") {
					int vn = ci(c[2]);
					fi.vn = vertexCount + vn;
				}
				else {
					fi.vn = -1;
				}
				faces[j-1] = fi;
			}
		}
	}

	private void SetGeometryData(string data) {
		string[] lines = data.Split("\n".ToCharArray());
		Regex regexWhitespaces = new Regex(@"\s+");
		bool isFirstInGroup = true;
		bool isFaceIndexPlus = true;
		for(int i = 0; i < lines.Length; i++) {
			string l = lines[i].Trim();
			
			if(l.IndexOf("#") != -1) { // comment line
				continue;
			}
			string[] p = regexWhitespaces.Split(l);
			switch(p[0]) {
				case O:
					buffer.PushObject(p[1].Trim());
					isFirstInGroup = true;
					break;
				case G:
					string groupName = null;
					if (p.Length >= 2) {
						groupName = p[1].Trim();
					}
					isFirstInGroup = true;
					buffer.PushGroup(groupName);
					break;
				case V:
					buffer.PushVertex( new Vector3( cf(p[1]), cf(p[2]), cf(p[3]) ) );
					break;
				case VT:
					buffer.PushUV(new Vector2( cf(p[1]), cf(p[2]) ));
					break;
				case VN:
					buffer.PushNormal(new Vector3( cf(p[1]), cf(p[2]), cf(p[3]) ));
					break;
				case F:
					FaceIndices[] faces = new FaceIndices[p.Length-1];
					if (isFirstInGroup) {
						isFirstInGroup = false;
						string[] c = p[1].Trim().Split("/".ToCharArray());
						isFaceIndexPlus = (ci(c[0]) >= 0);
					}
					GetFaceIndicesByOneFaceLine(faces, p, isFaceIndexPlus);
					if (p.Length == 4) {
						buffer.PushFace(faces[0]);
						buffer.PushFace(faces[1]);
						buffer.PushFace(faces[2]);
					}
					else if (p.Length == 5) {
						buffer.PushFace(faces[0]);
						buffer.PushFace(faces[1]);
						buffer.PushFace(faces[3]);
						buffer.PushFace(faces[3]);
						buffer.PushFace(faces[1]);
						buffer.PushFace(faces[2]);
					}
					else {
						Debug.LogWarning("face vertex count :"+(p.Length-1)+" larger than 4:");
					}
					break;
				case MTL:
					mtllib = l.Substring(p[0].Length+1).Trim();
					break;
				case UML:
					buffer.PushMaterialName(p[1].Trim());
					break;
			}
		}
		
		// buffer.Trace();
	}

	private float cf(string v) {
		try {
			return float.Parse(v);
		}
		catch(Exception e) {
			print(e);
			return 0;
		}
	}
	
	private int ci(string v) {
		try {
			return int.Parse(v);
		}
		catch(Exception e) {
			print(e);
			return 0;
		}
	}
	
	private bool hasMaterials {
		get {
			return mtllib != null;
		}
	}
	
	/* ############## MATERIALS */
	private List<MaterialData> materialData;
	private class MaterialData {
		public string name;
		public Color ambient;
		public Color diffuse;
		public Color specular;
		public float shininess;
		public float alpha;
		public int illumType;
		public string diffuseTexPath;
		public string bumpTexPath;
		public Texture2D diffuseTex;
		public Texture2D bumpTex;
	}
	
	private void SetMaterialData(string data) {
		string[] lines = data.Split("\n".ToCharArray());
		
		materialData = new List<MaterialData>();
		MaterialData current = new MaterialData();
		Regex regexWhitespaces = new Regex(@"\s+");
		
		for(int i = 0; i < lines.Length; i++) {
			string l = lines[i].Trim();
			
			if(l.IndexOf("#") != -1) l = l.Substring(0, l.IndexOf("#"));
			string[] p = regexWhitespaces.Split(l);
			if (p[0].Trim() == "") continue;

			switch(p[0]) {
				case NML:
					current = new MaterialData();
					current.name = p[1].Trim();
					materialData.Add(current);
					break;
				case KA:
					current.ambient = gc(p);
					break;
				case KD:
					current.diffuse = gc(p);
					break;
				case KS:
					current.specular = gc(p);
					break;
				case NS:
					current.shininess = cf(p[1]) / 1000;
					break;
				case D:
				case TR:
					current.alpha = cf(p[1]);
					break;
				case MAP_KD:
					current.diffuseTexPath = p[p.Length-1].Trim();
					break;
				case MAP_BUMP:
				case BUMP:
					BumpParameter(current, p);
					break;
				case ILLUM:
					current.illumType = ci(p[1]);
					break;
				default:
					Debug.Log("this line was not processed :" +l );
					break;
			}
		}	
	}
	
	private Material GetMaterial(MaterialData md) {
		Material m;
		
		if(md.illumType == 2) {
			string shaderName = (md.bumpTex != null)? "Bumped Specular" : "Specular";
			m =  new Material(Shader.Find(shaderName));
			m.SetColor("_SpecColor", md.specular);
			m.SetFloat("_Shininess", md.shininess);
		} else {
			string shaderName = (md.bumpTex != null)? "Bumped Diffuse" : "Diffuse";
			m =  new Material(Shader.Find(shaderName));
		}

		if(md.diffuseTex != null) {
			m.SetTexture("_MainTex", md.diffuseTex);
		}
		else {
			m.SetColor("_Color", md.diffuse);
		}
		if(md.bumpTex != null) m.SetTexture("_BumpMap", md.bumpTex);
		
		m.name = md.name;
		
		return m;
	}
	
	private class BumpParamDef {
		public string optionName;
		public string valueType;
		public int valueNumMin;
		public int valueNumMax;
		public BumpParamDef(string name, string type, int numMin, int numMax) {
			this.optionName = name;
			this.valueType = type;
			this.valueNumMin = numMin;
			this.valueNumMax = numMax;
		}
	}

	private void BumpParameter(MaterialData m, string[] p) {
		Regex regexNumber = new Regex(@"^[-+]?[0-9]*\.?[0-9]+$");
		
		var bumpParams = new Dictionary<String, BumpParamDef>();
		bumpParams.Add("bm",new BumpParamDef("bm","string", 1, 1));
		bumpParams.Add("clamp",new BumpParamDef("clamp", "string", 1,1));
		bumpParams.Add("blendu",new BumpParamDef("blendu", "string", 1,1));
		bumpParams.Add("blendv",new BumpParamDef("blendv", "string", 1,1));
		bumpParams.Add("imfchan",new BumpParamDef("imfchan", "string", 1,1));
		bumpParams.Add("mm",new BumpParamDef("mm", "string", 1,1));
		bumpParams.Add("o",new BumpParamDef("o", "number", 1,3));
		bumpParams.Add("s",new BumpParamDef("s", "number", 1,3));
		bumpParams.Add("t",new BumpParamDef("t", "number", 1,3));
		bumpParams.Add("texres",new BumpParamDef("texres", "string", 1,1));
		int pos = 1;
		string filename = null;
		while (pos < p.Length) {
			if (!p[pos].StartsWith("-")) {
				filename = p[pos];
				pos++;
				continue;
			}
			// option processing
			string optionName = p[pos].Substring(1);
			pos++;
			if (!bumpParams.ContainsKey(optionName)) {
				continue;
			}
			BumpParamDef def = bumpParams[optionName];
			ArrayList args = new ArrayList();
			int i=0;
			bool isOptionNotEnough = false;
			for (;i<def.valueNumMin ; i++, pos++) {
				if (pos >= p.Length) {
					isOptionNotEnough = true;
					break;
				}
				if (def.valueType == "number") {
					Match match = regexNumber.Match(p[pos]);
					if (!match.Success) {
						isOptionNotEnough = true;
						break;
					}
				}
				args.Add(p[pos]);
			}
			if (isOptionNotEnough) {
				Debug.Log("bump variable value not enough for option:"+optionName+" of material:"+m.name);
				continue;
			}
			for (;i<def.valueNumMax && pos < p.Length ; i++, pos++) {
				if (def.valueType == "number") {
					Match match = regexNumber.Match(p[pos]);
					if (!match.Success) {
						break;
					}
				}
				args.Add(p[pos]);
			}
			// TODO: some processing of options
			Debug.Log("found option: "+optionName+" of material: "+m.name+" args: "+String.Concat(args.ToArray()));
		}
		if (filename != null) {
			m.bumpTexPath = filename;
		}
	}
	
	private Color gc(string[] p) {
		return new Color( cf(p[1]), cf(p[2]), cf(p[3]) );
	}

	private void Build() {
		Dictionary<string, Material> materials = new Dictionary<string, Material>();
		
		if(hasMaterials) {
			foreach(MaterialData md in materialData) {
				if (materials.ContainsKey(md.name)) {
					Debug.LogWarning("duplicate material found: "+ md.name+ ". ignored repeated occurences");
					continue;
				}
				materials.Add(md.name, GetMaterial(md));
			}
		} else {
			materials.Add("default", new Material(Shader.Find("VertexLit")));
		}
		
		GameObject[] ms = new GameObject[buffer.numObjects];
		
		if(buffer.numObjects == 1) {
			gameObject.AddComponent(typeof(MeshFilter));
			gameObject.AddComponent(typeof(MeshRenderer));
			ms[0] = gameObject;
		} else if(buffer.numObjects > 1) {
			for(int i = 0; i < buffer.numObjects; i++) {
				GameObject go = new GameObject();
				go.transform.parent = gameObject.transform;
				go.AddComponent(typeof(MeshFilter));
				go.AddComponent(typeof(MeshRenderer));
				ms[i] = go;
			}
		}
		
		buffer.PopulateMeshes(ms, materials);
	}
}
