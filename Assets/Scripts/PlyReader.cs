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
using UnityEngine;
using UnityEngine.Rendering;
using UObject = UnityEngine.Object;

namespace TiltBrush
{

    public class PlyReader
    {
        private readonly Material m_vertexColorMaterial;
        private readonly string m_path; // Full path to file
        private readonly List<string> m_warnings = new List<string>();
        private readonly ImportMaterialCollector m_collector;

        private List<string> warnings => m_warnings;

        public PlyReader(string path)
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
            mr.materials = new []
            {
                ModelCatalog.m_Instance.m_ObjLoaderPointCloudMaterial,
                ModelCatalog.m_Instance.m_ObjLoaderPointCloudInvisibleMaterial
            };
            
            Mesh mesh;
            mesh = ImportAsMesh(m_path);
            mf.mesh = mesh;
            var collider = go.AddComponent<BoxCollider>();
            collider.size = mesh.bounds.size;
            return (go, warnings.Distinct().ToList(), m_collector);
        }
        
        Mesh ImportAsMesh(string path)
        {
            try
            {
                var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                var header = ReadDataHeader(new StreamReader(stream));
                var body = ReadDataBody(header, new BinaryReader(stream));

                var mesh = new Mesh();
                mesh.name = Path.GetFileNameWithoutExtension(path);

                mesh.indexFormat = header.vertexCount > 65535 ?
                    IndexFormat.UInt32 : IndexFormat.UInt16;

                mesh.subMeshCount = 2;
                
                mesh.SetVertices(body.vertices);
                mesh.SetColors(body.colors);

                mesh.SetIndices(
                    Enumerable.Range(0, header.vertexCount).ToArray(),
                    MeshTopology.Points,
                    0
                );

                mesh.SetTriangles(new []
                    {
                        // top 031 135
                        body.cornerIndices[0], body.cornerIndices[3], body.cornerIndices[1],
                        body.cornerIndices[1], body.cornerIndices[3], body.cornerIndices[5],
                        // bottom 264 467
                        body.cornerIndices[2], body.cornerIndices[6], body.cornerIndices[4],
                        body.cornerIndices[4], body.cornerIndices[6], body.cornerIndices[7],
                        // left 536 567
                        body.cornerIndices[5], body.cornerIndices[3], body.cornerIndices[6],
                        body.cornerIndices[5], body.cornerIndices[6], body.cornerIndices[7],
                        // right 012 142
                        body.cornerIndices[0], body.cornerIndices[1], body.cornerIndices[2],
                        body.cornerIndices[1], body.cornerIndices[4], body.cornerIndices[2],
                        // front 032 236
                        body.cornerIndices[0], body.cornerIndices[3], body.cornerIndices[2],
                        body.cornerIndices[2], body.cornerIndices[3], body.cornerIndices[6],
                        // back 154 457
                        body.cornerIndices[1], body.cornerIndices[5], body.cornerIndices[4],
                        body.cornerIndices[4], body.cornerIndices[5], body.cornerIndices[7]
                    },
                    1);

                mesh.UploadMeshData(true);
                return mesh;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed importing " + path + ". " + e.Message);
                return null;
            }
        }
        
        enum DataProperty {
            Invalid,
            R8, G8, B8, A8,
            R16, G16, B16, A16,
            SingleX, SingleY, SingleZ,
            DoubleX, DoubleY, DoubleZ,
            Data8, Data16, Data32, Data64
        }
        
        class DataHeader
        {
            public List<DataProperty> properties = new List<DataProperty>();
            public int vertexCount = -1;
        }

        class DataBody
        {
            public List<Vector3> vertices;
            public List<Color32> colors;
            public Bounds bounds;
            public int[] cornerIndices; 
            public Vector3[] corners; 

            public DataBody(int vertexCount)
            {
                vertices = new List<Vector3>(vertexCount);
                colors = new List<Color32>(vertexCount);
                bounds = new Bounds();
                cornerIndices = new int[8];
                corners = new Vector3[8];
            }

            public void AddPoint(
                float x, float y, float z,
                byte r, byte g, byte b, byte a
                )
            {
                var point = new Vector3(x, y, z);
                vertices.Add(point);
                if (!bounds.Contains(point))
                {
                    bounds.Encapsulate(point);
                    int idx = vertices.Count - 1;
                        
                    if (point.x >= corners[0].x && point.y >= corners[0].y && point.z >= corners[0].z)
                    { // rtf
                        cornerIndices[0] = idx;
                        corners[0] = point;
                    }
                    if (point.x >= corners[1].x && point.y >= corners[1].y && point.z <= corners[1].z)
                    { // rtb
                        cornerIndices[1] = idx;
                        corners[1] = point;
                    }
                    if (point.x >= corners[2].x && point.y <= corners[2].y && point.z >= corners[2].z)
                    { // rbf
                        cornerIndices[2] = idx;
                        corners[2] = point;
                    }
                    if (point.x <= corners[3].x && point.y >= corners[3].y && point.z >= corners[3].z)
                    { // ltf
                        cornerIndices[3] = idx;
                        corners[3] = point;
                    }
                    if (point.x >= corners[4].x && point.y <= corners[4].y && point.z <= corners[4].z)
                    { // rbb
                        cornerIndices[4] = idx;
                        corners[4] = point;
                    }
                    if (point.x <= corners[5].x && point.y >= corners[5].y && point.z <= corners[5].z)
                    { // ltb
                        cornerIndices[5] = idx;
                        corners[5] = point;
                    }
                    if (point.x <= corners[6].x && point.y <= corners[6].y && point.z >= corners[6].z)
                    { // lbf
                        cornerIndices[6] = idx;
                        corners[6] = point;
                    }
                    if (point.x <= corners[7].x && point.y <= corners[7].y && point.z <= corners[7].z)
                    { // lbb
                        cornerIndices[7] = idx;
                        corners[7] = point;
                    }
                }
                colors.Add(new Color32(r, g, b, a));
            }
        }
        
        static int GetPropertySize(DataProperty p)
        {
            switch (p)
            {
                case DataProperty.R8: return 1;
                case DataProperty.G8: return 1;
                case DataProperty.B8: return 1;
                case DataProperty.A8: return 1;
                case DataProperty.R16: return 2;
                case DataProperty.G16: return 2;
                case DataProperty.B16: return 2;
                case DataProperty.A16: return 2;
                case DataProperty.SingleX: return 4;
                case DataProperty.SingleY: return 4;
                case DataProperty.SingleZ: return 4;
                case DataProperty.DoubleX: return 8;
                case DataProperty.DoubleY: return 8;
                case DataProperty.DoubleZ: return 8;
                case DataProperty.Data8: return 1;
                case DataProperty.Data16: return 2;
                case DataProperty.Data32: return 4;
                case DataProperty.Data64: return 8;
            }
            return 0;
        }


        DataHeader ReadDataHeader(StreamReader reader)
        {
            var data = new DataHeader();
            var readCount = 0;

            // Magic number line ("ply")
            var line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "ply")
                throw new ArgumentException("Magic number ('ply') mismatch.");

            // Data format: check if it's binary/little endian.
            line = reader.ReadLine();
            readCount += line.Length + 1;
            if (line != "format binary_little_endian 1.0")
                throw new ArgumentException(
                    "Invalid data format ('" + line + "'). " +
                    "Should be binary/little endian.");

            // Read header contents.
            for (var skip = false;;)
            {
                // Read a line and split it with white space.
                line = reader.ReadLine();
                readCount += line.Length + 1;
                if (line == "end_header") break;
                var col = line.Split();

                // Element declaration (unskippable)
                if (col[0] == "element")
                {
                    if (col[1] == "vertex")
                    {
                        data.vertexCount = Convert.ToInt32(col[2]);
                        skip = false;
                    }
                    else
                    {
                        // Don't read elements other than vertices.
                        skip = true;
                    }
                }

                if (skip) continue;

                // Property declaration line
                if (col[0] == "property")
                {
                    var prop = DataProperty.Invalid;

                    // Parse the property name entry.
                    switch (col[2])
                    {
                        case "red"  : prop = DataProperty.R8; break;
                        case "green": prop = DataProperty.G8; break;
                        case "blue" : prop = DataProperty.B8; break;
                        case "alpha": prop = DataProperty.A8; break;
                        case "x"    : prop = DataProperty.SingleX; break;
                        case "y"    : prop = DataProperty.SingleY; break;
                        case "z"    : prop = DataProperty.SingleZ; break;
                    }

                    // Check the property type.
                    if (col[1] == "char" || col[1] == "uchar" ||
                        col[1] == "int8" || col[1] == "uint8")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data8;
                        else if (GetPropertySize(prop) != 1)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "short" || col[1] == "ushort" ||
                             col[1] == "int16" || col[1] == "uint16")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data16; break;
                            case DataProperty.R8: prop = DataProperty.R16; break;
                            case DataProperty.G8: prop = DataProperty.G16; break;
                            case DataProperty.B8: prop = DataProperty.B16; break;
                            case DataProperty.A8: prop = DataProperty.A16; break;
                        }
                        if (GetPropertySize(prop) != 2)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int"   || col[1] == "uint"   || col[1] == "float" ||
                             col[1] == "int32" || col[1] == "uint32" || col[1] == "float32")
                    {
                        if (prop == DataProperty.Invalid)
                            prop = DataProperty.Data32;
                        else if (GetPropertySize(prop) != 4)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else if (col[1] == "int64"  || col[1] == "uint64" ||
                             col[1] == "double" || col[1] == "float64")
                    {
                        switch (prop)
                        {
                            case DataProperty.Invalid: prop = DataProperty.Data64; break;
                            case DataProperty.SingleX: prop = DataProperty.DoubleX; break;
                            case DataProperty.SingleY: prop = DataProperty.DoubleY; break;
                            case DataProperty.SingleZ: prop = DataProperty.DoubleZ; break;
                        }
                        if (GetPropertySize(prop) != 8)
                            throw new ArgumentException("Invalid property type ('" + line + "').");
                    }
                    else
                    {
                        throw new ArgumentException("Unsupported property type ('" + line + "').");
                    }

                    data.properties.Add(prop);
                }
            }

            // Rewind the stream back to the exact position of the reader.
            reader.BaseStream.Position = readCount;

            return data;
        }

        DataBody ReadDataBody(DataHeader header, BinaryReader reader)
        {
            var data = new DataBody(header.vertexCount);

            float x = 0, y = 0, z = 0;
            Byte r = 255, g = 255, b = 255, a = 255;

            for (var i = 0; i < header.vertexCount; i++)
            {
                foreach (var prop in header.properties)
                {
                    switch (prop)
                    {
                        case DataProperty.R8: r = reader.ReadByte(); break;
                        case DataProperty.G8: g = reader.ReadByte(); break;
                        case DataProperty.B8: b = reader.ReadByte(); break;
                        case DataProperty.A8: a = reader.ReadByte(); break;

                        case DataProperty.R16: r = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.G16: g = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.B16: b = (byte)(reader.ReadUInt16() >> 8); break;
                        case DataProperty.A16: a = (byte)(reader.ReadUInt16() >> 8); break;

                        case DataProperty.SingleX: x = reader.ReadSingle(); break;
                        case DataProperty.SingleY: y = reader.ReadSingle(); break;
                        case DataProperty.SingleZ: z = reader.ReadSingle(); break;

                        case DataProperty.DoubleX: x = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleY: y = (float)reader.ReadDouble(); break;
                        case DataProperty.DoubleZ: z = (float)reader.ReadDouble(); break;

                        case DataProperty.Data8: reader.ReadByte(); break;
                        case DataProperty.Data16: reader.BaseStream.Position += 2; break;
                        case DataProperty.Data32: reader.BaseStream.Position += 4; break;
                        case DataProperty.Data64: reader.BaseStream.Position += 8; break;
                    }
                }

                data.AddPoint(x, y, z, r, g, b, a);
            }

            return data;
        }


    }
} // namespace TiltBrush
