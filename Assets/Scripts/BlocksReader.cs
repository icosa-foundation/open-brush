// Copyright 2026 The Open Brush Authors
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
using Icosa.OpenBlocks.FileFormat;
using Polyhydra.Core;
using TiltBrush.MeshEditing;
using UnityEngine;
using BlocksFace = Icosa.OpenBlocks.FileFormat.Face;
using BlocksMesh = Icosa.OpenBlocks.FileFormat.MMesh;
using BlocksVertex = Icosa.OpenBlocks.FileFormat.Vertex;

namespace TiltBrush
{
    /// Converts native Open Blocks files into the PolyMesh representation used by editable models.
    public class BlocksReader
    {
        private static readonly int[] kLegacyPalette =
        {
            0xBA68C8, 0x9C27B0, 0x673AB7, 0x80DEEA, 0x00BCD4, 0x039BE5,
            0xF8BBD0, 0xF06292, 0xF44336, 0x8BC34A, 0x4CAF50, 0x009688,
            0xFFEB3B, 0xFF9800, 0xFF5722, 0xCFD8DC, 0x78909C, 0x455A64,
            0xFFCC88, 0xDD9944, 0x795548, 0xFFFFFF, 0x9E9E9E, 0x1A1A1A,
        };

        private readonly string m_Path;

        public BlocksReader(string path)
        {
            m_Path = path;
        }

        public static bool IsSupportedExtension(string extension)
        {
            return string.Equals(extension, ".blocks", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".poly", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".peltzer", StringComparison.OrdinalIgnoreCase);
        }

        public (GameObject gameObject, List<string> warnings,
                ImportMaterialCollector collector, PolyMesh poly, PolyRecipe recipe) Import()
        {
            if (!BlocksFileFormat.LoadFromFile(m_Path, out PeltzerFile file) || file == null)
            {
                throw new InvalidDataException($"Could not deserialize native Blocks file: {m_Path}");
            }

            var (poly, recipe, warnings) = Convert(file, m_Path);
            var meshData = poly.BuildMeshData(colors: recipe.Colors, colorMethod: recipe.ColorMethod);
            var unityMesh = poly.BuildUnityMesh(meshData);
            var gameObject = new GameObject($"Blocks model: {m_Path}");
            EditableModelManager.m_Instance.UpdateMesh(
                gameObject, unityMesh, EditableModelManager.m_Instance.m_Materials[recipe.MaterialIndex]);

            var collector = new ImportMaterialCollector(Path.GetDirectoryName(m_Path), m_Path);
            collector.AddAllEditableModelMaterials();
            return (gameObject, warnings, collector, poly, recipe);
        }

        public static (PolyMesh poly, PolyRecipe recipe, List<string> warnings) Convert(
            PeltzerFile file, string sourceName = "native Blocks file")
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var warnings = new List<string>();
            var vertices = new List<Vector3>();
            var faces = new List<List<int>>();
            var faceTags = new List<HashSet<string>>();

            IEnumerable<BlocksMesh> blocksMeshes = file.meshes ?? new List<BlocksMesh>();
            foreach (BlocksMesh blocksMesh in blocksMeshes)
            {
                var vertexIndices = new Dictionary<int, int>();
                foreach (BlocksVertex vertex in blocksMesh.GetVertices())
                {
                    vertexIndices[vertex.id] = vertices.Count;
                    vertices.Add(blocksMesh.rotation * vertex.loc + blocksMesh.offset);
                }

                foreach (BlocksFace face in blocksMesh.GetFaces())
                {
                    var indices = new List<int>(face.vertexIds.Count);
                    bool hasMissingVertex = false;
                    foreach (int vertexId in face.vertexIds)
                    {
                        if (!vertexIndices.TryGetValue(vertexId, out int vertexIndex))
                        {
                            warnings.Add($"Mesh {blocksMesh.id}, face {face.id} references missing vertex {vertexId}.");
                            hasMissingVertex = true;
                            break;
                        }
                        indices.Add(vertexIndex);
                    }

                    if (hasMissingVertex || indices.Count < 3)
                    {
                        if (!hasMissingVertex)
                        {
                            warnings.Add($"Mesh {blocksMesh.id}, face {face.id} has fewer than three vertices.");
                        }
                        continue;
                    }

                    faces.Add(indices);
                    faceTags.Add(new HashSet<string>
                    {
                        ColorTagForMaterial(file.materials, face.properties.materialId)
                    });
                }
            }

            if (vertices.Count == 0 || faces.Count == 0)
            {
                throw new InvalidDataException(
                    $"Native Blocks file contains no usable geometry: {sourceName}");
            }

            CenterVerticesOnBounds(vertices);

            var poly = new PolyMesh(vertices, faces)
            {
                FaceTags = faceTags
            };
            var recipe = new PolyRecipe
            {
                GeneratorType = GeneratorTypes.GeometryData,
                Vertices = vertices,
                Faces = faces,
                FaceRoles = Enumerable.Repeat((int)Roles.New, faces.Count).ToList(),
                VertexRoles = Enumerable.Repeat((int)Roles.New, vertices.Count).ToList(),
                FaceTags = faceTags,
                Operators = new List<PreviewPolyhedron.OpDefinition>(),
                MaterialIndex = 0,
                ColorMethod = ColorMethods.ByTags,
                Colors = (Color[])PolyMesh.DefaultFaceColors.Clone(),
            };
            return (poly, recipe, warnings.Distinct().ToList());
        }

        private static void CenterVerticesOnBounds(List<Vector3> vertices)
        {
            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }

            Vector3 center = bounds.center;
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] -= center;
            }
        }

        private static string ColorTagForMaterial(List<PeltzerMaterial> materials, int materialId)
        {
            PeltzerMaterial embeddedMaterial = materials?
                .FirstOrDefault(material => material.materialId == materialId && material.color != 0);
            int rgb = embeddedMaterial != null
                ? embeddedMaterial.color & 0xFFFFFF
                : materialId >= 0 && materialId < kLegacyPalette.Length
                    ? kLegacyPalette[materialId]
                    : 0xFFFFFF;
            return $"#{rgb:X6}";
        }
    }
}
