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
using Polyhydra.Core;
using UnityEngine;
using UObject = UnityEngine.Object;
namespace TiltBrush
{

    public class OffReader
    {
        public const int MAX_VERTS_PER_MESH = 65534;

        private readonly Material m_vertexColorMaterial;
        private readonly string m_path; // Full path to file
        private readonly List<string> m_warnings = new List<string>();
        private readonly ImportMaterialCollector m_collector;

        private List<string> warnings => m_warnings;

        public OffReader(string path)
        {
            m_vertexColorMaterial = ModelCatalog.m_Instance.m_ObjLoaderVertexColorMaterial;
            m_path = path;
            var mDir = Path.GetDirectoryName(path);
            m_collector = new ImportMaterialCollector(mDir, m_path);
        }

        public (GameObject, List<string> warnings, ImportMaterialCollector) Import()
        {
            GameObject go = new GameObject();
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            Mesh mesh;
            using (StreamReader reader = new StreamReader(m_path))
            {
                var poly = new PolyMesh(reader);
                mesh = poly.BuildUnityMesh(colorMethod: PolyMesh.ColorMethods.ByTags);
                mf.mesh = mesh;
                mr.material = m_vertexColorMaterial;
            }
            var collider = go.AddComponent<BoxCollider>();
            collider.size = mesh.bounds.size;
            return (go, warnings.Distinct().ToList(), m_collector);
        }

    }
} // namespace TiltBrush
