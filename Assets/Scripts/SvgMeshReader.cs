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


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VectorGraphics;
using UnityEngine;
using UObject = UnityEngine.Object;

namespace TiltBrush
{
    public class SvgMeshReader
    {
        private readonly Material m_vertexColorMaterial;
        private readonly string m_path; // Full path to file
        private readonly List<string> m_warnings = new ();
        private readonly ImportMaterialCollector m_collector;

        private List<string> warnings => m_warnings;

        public SvgMeshReader(string path)
        {
            m_path = path;
            var mDir = Path.GetDirectoryName(path);
            m_collector = new ImportMaterialCollector(mDir, m_path);
        }

        public (GameObject, List<string> warnings, ImportMaterialCollector) Import()
        {
            GameObject go = new GameObject();
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            var mat = RuntimeSVGImporter.MaterialForSVG(false);
            mr.materials = new[] {mat};
            m_collector.AddSvgIem(mr.materials[0]);
            Mesh mesh = ImportAsMesh(m_path);
            mf.mesh = mesh;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = mesh.bounds.size;
            return (go, warnings.Distinct().ToList(), m_collector);
        }


        Mesh ImportAsMesh(string path)
        {
            try
            {
                var importer = new RuntimeSVGImporter();
                var mesh = importer.ImportAsMesh(path);
                return mesh;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }
    }
} // namespace TiltBrush
