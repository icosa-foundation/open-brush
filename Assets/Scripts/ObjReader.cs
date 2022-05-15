// Copyright 2022 The Open Brush Authors
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using ObjLoader.Loader.Loaders;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace TiltBrush
{

    public class ObjReader
    {
        public const int MAX_VERTS_PER_MESH = 65534;

        private readonly Material m_vertexColorMaterial;
        private readonly string m_path; // Full path to file
        private readonly List<string> m_warnings = new List<string>();
        private readonly ImportMaterialCollector m_collector;

        private List<string> warnings => m_warnings;

        public ObjReader(string path)
        {
            m_vertexColorMaterial = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;
            m_path = path;
            var mDir = Path.GetDirectoryName(path);
            m_collector = new ImportMaterialCollector(mDir, m_path);
        }

        public (GameObject, List<string> warnings, ImportMaterialCollector) Import(bool editable)
        {
            GameObject go = new GameObject($"Obj model: {m_path}");
            
            var objLoaderFactory = new ObjLoaderFactory();
            var objLoader = objLoaderFactory.Create();
            
            using (StreamReader reader = new StreamReader(m_path))
            {
                var result = objLoader.Load(reader.BaseStream);
                var verts = result.Vertices.Select(v=>new Vector3(v.X, v.Y, v.Z));
                var faceIndices = result
                    .Groups
                    .SelectMany(g => g.Faces)
                    .Select(f => f._vertices.Select(v=>v.VertexIndex-1));
                var poly = new PolyMesh(verts, faceIndices);
                var meshData = poly.BuildMeshData(colorMethod: ColorMethods.ByTags);
                var mesh = poly.BuildUnityMesh(meshData);
                if (editable)
                {
                    poly.MergeCoplanarFaces(0.01f);
                    EditableModelManager.m_Instance.UpdateMesh(go, mesh, m_vertexColorMaterial);
                    EditableModelManager.m_Instance.RegisterEditableMesh(go, poly, ColorMethods.ByTags, GeneratorTypes.FileSystem);
                }
                else
                {
                    EditableModelManager.m_Instance.UpdateMesh(go, mesh, m_vertexColorMaterial);
                }
            }
            return (go, warnings.Distinct().ToList(), m_collector);
        }

    }
} // namespace TiltBrush
