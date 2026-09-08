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
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityGLTF;
using Object = UnityEngine.Object;

namespace TiltBrush
{
    /// Writes the exact stroke input and live Unity mesh used by UiScreenshotter,
    /// plus the mesh after the export BrushBaker mapping. The JSON is intentionally
    /// renderer-neutral so downstream implementations can compare typed arrays.
    internal static class BrushMeshFixtureWriter
    {
        private const int kSchemaVersion = 1;
        private const string kLogPrefix = "[BrushMeshFixture]";
        private const float kHullPointTolerance = 1e-5f;
        private const float kHullPlaneTolerance = 1e-5f;
        private const float kHullNormalDotTolerance = 1f - 1e-6f;

        private sealed class HullTriangle
        {
            internal Vector3 Normal;
            internal float PlaneDistance;
            internal Vector3[] Vertices;
            internal string[] VertexKeys;
        }

        internal static string WriteBrushFixture(
            BrushDescriptor brush,
            IEnumerable<Stroke> strokes,
            string relativeOutputDirectory,
            float fixedShaderTimeSeconds,
            BrushBaker brushBaker)
        {
            if (brush == null) throw new ArgumentNullException(nameof(brush));
            if (strokes == null) throw new ArgumentNullException(nameof(strokes));
            if (brushBaker == null) throw new ArgumentNullException(nameof(brushBaker));

            List<Stroke> strokeList = strokes.Where(stroke => stroke != null).ToList();
            if (strokeList.Count == 0)
            {
                throw new InvalidOperationException(
                    $"{kLogPrefix} {brush.DurableName} produced no strokes.");
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDirectory = Path.Combine(projectRoot, relativeOutputDirectory);
            Directory.CreateDirectory(outputDirectory);
            string sanitizedBrushName = SanitizeFileName(brush.DurableName);

            string brushGuid = brush.UniqueName.ToString("D");
            var fixture = new JObject
            {
                ["schemaVersion"] = kSchemaVersion,
                ["brushGuid"] = brushGuid,
                ["durableName"] = brush.DurableName,
                ["fixedShaderTimeSeconds"] = fixedShaderTimeSeconds,
                ["coordinateSystem"] =
                    "Unity X-right, Y-up, Z-forward; mesh positions are mesh-local",
            };
            var strokeFixtures = new JArray();
            fixture["strokes"] = strokeFixtures;

            for (int strokeIndex = 0; strokeIndex < strokeList.Count; ++strokeIndex)
            {
                Stroke stroke = strokeList[strokeIndex];
                GeometryPool.VertexLayout layout = GetVertexLayout(stroke, brush);
                Mesh liveMesh = CreateLiveMesh(stroke);
                Mesh postBrushBakerMesh = null;
                try
                {
                    liveMesh.RecalculateBounds();
                    postBrushBakerMesh = Object.Instantiate(liveMesh);
                    postBrushBakerMesh.name = $"{liveMesh.name}-post-brush-baker";

                    BrushBaker.ComputeShaderMapping mapping;
                    bool mappingFound = brushBaker.TryGetMapping(brushGuid, out mapping);
                    bool processAttempted = mappingFound && mapping.computeShader != null;
                    if (processAttempted)
                    {
                        brushBaker.ProcessMesh(postBrushBakerMesh, brushGuid);
                    }
                    postBrushBakerMesh.RecalculateBounds();
                    string glbFileName = strokeList.Count == 1
                        ? $"brush-{sanitizedBrushName}.glb"
                        : $"brush-{sanitizedBrushName}-stroke-{strokeIndex}.glb";
                    JObject exportedGlb = WriteExportedGlb(
                        postBrushBakerMesh,
                        GetStrokeMaterial(stroke),
                        outputDirectory,
                        glbFileName,
                        brushGuid);

                    var strokeFixture = new JObject
                    {
                        ["input"] = BuildStrokeInput(stroke),
                        ["vertexLayout"] = BuildVertexLayout(layout),
                        ["material"] = BuildMaterial(GetStrokeMaterial(stroke)),
                        ["brushBaker"] = BuildBrushBakerMapping(
                            mappingFound,
                            processAttempted,
                            mapping,
                            brushBaker.squeezeAmount),
                        ["live"] = BuildMeshStage(liveMesh, layout),
                        ["postBrushBaker"] = BuildMeshStage(postBrushBakerMesh, layout),
                        ["exportedGlb"] = exportedGlb,
                    };
                    if (IsHullBrush(brush))
                    {
                        strokeFixture["polygonFaces"] = BuildHullPolygonFaces(liveMesh);
                    }
                    strokeFixtures.Add(strokeFixture);
                }
                finally
                {
                    if (postBrushBakerMesh != null) Object.DestroyImmediate(postBrushBakerMesh);
                    Object.DestroyImmediate(liveMesh);
                }
            }

            string fileName = $"brush-{sanitizedBrushName}.mesh.json";
            string outputPath = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(
                outputPath,
                fixture.ToString(Formatting.Indented),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Debug.Log($"{kLogPrefix} wrote {outputPath}");
            return outputPath;
        }

        private static JObject WriteExportedGlb(
            Mesh postBrushBakerMesh,
            Material material,
            string outputDirectory,
            string fileName,
            string brushGuid)
        {
            string outputPath = Path.Combine(outputDirectory, fileName);
            if (postBrushBakerMesh.vertexCount == 0 || postBrushBakerMesh.GetIndexCount(0) == 0)
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                return new JObject
                {
                    ["file"] = JValue.CreateNull(),
                    ["skipped"] = true,
                    ["reason"] = "The deterministic stroke produced an empty mesh.",
                    ["sourceStage"] = "postBrushBaker",
                    ["exporter"] = "UnityGLTF with OpenBrushExportPlugin",
                };
            }

            var exportRoot = new GameObject($"Batch_0_{brushGuid}");
            try
            {
                exportRoot.AddComponent<MeshFilter>().sharedMesh = postBrushBakerMesh;
                exportRoot.AddComponent<MeshRenderer>().sharedMaterial = material;
                var context = new ExportContext(App.Config.m_UnityGLTFSettings);
                var exporter = new GLTFSceneExporter(exportRoot.transform, context);
                using (OpenBrushExportPluginConfig.BeginIsolatedMeshFixtureExport(
                    BrushCatalog.m_Instance.GetBrush(new Guid(brushGuid))))
                {
                    exporter.SaveGLB(outputDirectory, fileName);
                }
                return new JObject
                {
                    ["file"] = fileName,
                    ["byteLength"] = new FileInfo(outputPath).Length,
                    ["sourceStage"] = "postBrushBaker",
                    ["exporter"] = "UnityGLTF with OpenBrushExportPlugin",
                };
            }
            finally
            {
                Object.DestroyImmediate(exportRoot);
            }
        }

        internal static JObject BuildMeshStageForTesting(
            Mesh mesh,
            GeometryPool.VertexLayout layout)
        {
            return BuildMeshStage(mesh, layout);
        }

        internal static JObject BuildHullPolygonFacesForTesting(Mesh mesh)
        {
            return BuildHullPolygonFaces(mesh);
        }

        private static bool IsHullBrush(BrushDescriptor brush)
        {
            if (brush.m_BrushPrefab == null) return false;
            return brush.m_BrushPrefab.GetComponent<HullBrush>() != null ||
                brush.m_BrushPrefab.GetComponent<ConcaveHullBrush>() != null;
        }

        private static JObject BuildHullPolygonFaces(Mesh mesh)
        {
            Vector3[] meshVertices = mesh.vertices;
            int[] indices = mesh.triangles;
            Vector3 center = mesh.bounds.center;
            var triangles = new List<HullTriangle>();
            var edgeTriangles = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            var triangleKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index + 2 < indices.Length; index += 3)
            {
                Vector3 p0 = meshVertices[indices[index]];
                Vector3 p1 = meshVertices[indices[index + 1]];
                Vector3 p2 = meshVertices[indices[index + 2]];
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                if (normal.sqrMagnitude <= 1e-12f) continue;
                normal.Normalize();
                if (Vector3.Dot(normal, center - p0) > 0f) normal = -normal;

                var vertexKeys = new[]
                {
                    HullPointKey(p0),
                    HullPointKey(p1),
                    HullPointKey(p2),
                };
                string[] sortedVertexKeys = (string[])vertexKeys.Clone();
                Array.Sort(sortedVertexKeys, StringComparer.Ordinal);
                string triangleKey = string.Join("|", sortedVertexKeys);
                if (!triangleKeys.Add(triangleKey)) continue;
                int triangleIndex = triangles.Count;
                triangles.Add(new HullTriangle
                {
                    Normal = normal,
                    PlaneDistance = Vector3.Dot(normal, p0),
                    Vertices = new[] { p0, p1, p2 },
                    VertexKeys = vertexKeys,
                });
                AddHullEdge(edgeTriangles, vertexKeys[0], vertexKeys[1], triangleIndex);
                AddHullEdge(edgeTriangles, vertexKeys[1], vertexKeys[2], triangleIndex);
                AddHullEdge(edgeTriangles, vertexKeys[2], vertexKeys[0], triangleIndex);
            }

            var adjacency = new List<HashSet<int>>(triangles.Count);
            for (int index = 0; index < triangles.Count; ++index)
            {
                adjacency.Add(new HashSet<int>());
            }
            foreach (List<int> edgeMembers in edgeTriangles.Values)
            {
                for (int first = 0; first < edgeMembers.Count; ++first)
                {
                    for (int second = first + 1; second < edgeMembers.Count; ++second)
                    {
                        int a = edgeMembers[first];
                        int b = edgeMembers[second];
                        if (!HullTrianglesAreCoplanar(triangles[a], triangles[b])) continue;
                        adjacency[a].Add(b);
                        adjacency[b].Add(a);
                    }
                }
            }

            var faces = new List<JObject>();
            var visited = new bool[triangles.Count];
            for (int start = 0; start < triangles.Count; ++start)
            {
                if (visited[start]) continue;
                var pending = new Stack<int>();
                var component = new List<int>();
                pending.Push(start);
                visited[start] = true;
                while (pending.Count > 0)
                {
                    int current = pending.Pop();
                    component.Add(current);
                    foreach (int neighbor in adjacency[current])
                    {
                        if (visited[neighbor]) continue;
                        visited[neighbor] = true;
                        pending.Push(neighbor);
                    }
                }

                HullTriangle firstTriangle = triangles[component[0]];
                var uniqueVertices = new Dictionary<string, Vector3>(StringComparer.Ordinal);
                var componentEdges = new Dictionary<string, Tuple<string, string, int>>(
                    StringComparer.Ordinal);
                foreach (int triangleIndex in component)
                {
                    HullTriangle triangle = triangles[triangleIndex];
                    for (int vertex = 0; vertex < 3; ++vertex)
                    {
                        uniqueVertices[triangle.VertexKeys[vertex]] = triangle.Vertices[vertex];
                    }
                    CountHullBoundaryEdge(componentEdges, triangle.VertexKeys[0], triangle.VertexKeys[1]);
                    CountHullBoundaryEdge(componentEdges, triangle.VertexKeys[1], triangle.VertexKeys[2]);
                    CountHullBoundaryEdge(componentEdges, triangle.VertexKeys[2], triangle.VertexKeys[0]);
                }
                var boundaryVertexKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (Tuple<string, string, int> edge in componentEdges.Values)
                {
                    if (edge.Item3 != 1) continue;
                    boundaryVertexKeys.Add(edge.Item1);
                    boundaryVertexKeys.Add(edge.Item2);
                }
                List<Vector3> faceVertices = boundaryVertexKeys
                    .Select(key => uniqueVertices[key])
                    .ToList();
                faceVertices.Sort(CompareHullPoints);
                faces.Add(new JObject
                {
                    ["normal"] = Vector3Array(firstTriangle.Normal),
                    ["planeDistance"] = firstTriangle.PlaneDistance,
                    ["vertices"] = new JArray(faceVertices.Select(Vector3Array)),
                    ["sourceTriangleCount"] = component.Count,
                });
            }

            faces.Sort((a, b) => StringComparer.Ordinal.Compare(
                HullFaceSortKey(a), HullFaceSortKey(b)));
            return new JObject
            {
                ["definition"] =
                    "edge-connected coplanar components of the finalized live hull mesh",
                ["pointTolerance"] = kHullPointTolerance,
                ["planeTolerance"] = kHullPlaneTolerance,
                ["normalDotTolerance"] = kHullNormalDotTolerance,
                ["faceCount"] = faces.Count,
                ["faces"] = new JArray(faces),
            };
        }

        private static void AddHullEdge(
            Dictionary<string, List<int>> edgeTriangles,
            string first,
            string second,
            int triangleIndex)
        {
            string edgeKey = StringComparer.Ordinal.Compare(first, second) <= 0
                ? $"{first}|{second}"
                : $"{second}|{first}";
            if (!edgeTriangles.TryGetValue(edgeKey, out List<int> members))
            {
                members = new List<int>();
                edgeTriangles.Add(edgeKey, members);
            }
            members.Add(triangleIndex);
        }

        private static void CountHullBoundaryEdge(
            Dictionary<string, Tuple<string, string, int>> edges,
            string first,
            string second)
        {
            string low;
            string high;
            if (StringComparer.Ordinal.Compare(first, second) <= 0)
            {
                low = first;
                high = second;
            }
            else
            {
                low = second;
                high = first;
            }
            string edgeKey = $"{low}|{high}";
            if (edges.TryGetValue(edgeKey, out Tuple<string, string, int> edge))
            {
                edges[edgeKey] = Tuple.Create(edge.Item1, edge.Item2, edge.Item3 + 1);
            }
            else
            {
                edges.Add(edgeKey, Tuple.Create(low, high, 1));
            }
        }

        private static bool HullTrianglesAreCoplanar(HullTriangle first, HullTriangle second)
        {
            return Vector3.Dot(first.Normal, second.Normal) >= kHullNormalDotTolerance &&
                Mathf.Abs(first.PlaneDistance - second.PlaneDistance) <= kHullPlaneTolerance;
        }

        private static string HullPointKey(Vector3 point)
        {
            return $"{Mathf.RoundToInt(point.x / kHullPointTolerance)}:" +
                $"{Mathf.RoundToInt(point.y / kHullPointTolerance)}:" +
                $"{Mathf.RoundToInt(point.z / kHullPointTolerance)}";
        }

        private static int CompareHullPoints(Vector3 first, Vector3 second)
        {
            int x = first.x.CompareTo(second.x);
            if (x != 0) return x;
            int y = first.y.CompareTo(second.y);
            return y != 0 ? y : first.z.CompareTo(second.z);
        }

        private static string HullFaceSortKey(JObject face)
        {
            return face.ToString(Formatting.None);
        }

        private static GeometryPool.VertexLayout GetVertexLayout(
            Stroke stroke,
            BrushDescriptor brush)
        {
            if (stroke.m_Type == Stroke.Type.BatchedBrushStroke &&
                stroke.m_BatchSubset?.m_ParentBatch?.Geometry != null)
            {
                return stroke.m_BatchSubset.m_ParentBatch.Geometry.Layout;
            }
            return brush.VertexLayout;
        }

        private static Mesh CreateLiveMesh(Stroke stroke)
        {
            if (stroke.m_Type == Stroke.Type.BatchedBrushStroke &&
                stroke.m_BatchSubset?.m_ParentBatch != null)
            {
                BatchSubset subset = stroke.m_BatchSubset;
                var mesh = new Mesh { name = $"fixture-{stroke.m_BrushGuid:D}-live" };
                subset.m_ParentBatch.Geometry.CopyToMesh(
                    mesh,
                    subset.m_StartVertIndex,
                    subset.m_VertLength,
                    subset.m_iTriIndex,
                    subset.m_nTriIndex);
                return mesh;
            }

            if (stroke.m_Type == Stroke.Type.BrushStroke && stroke.m_Object != null)
            {
                Mesh source = stroke.m_Object.GetComponent<MeshFilter>()?.sharedMesh;
                if (source != null)
                {
                    Mesh mesh = Object.Instantiate(source);
                    mesh.name = $"fixture-{stroke.m_BrushGuid:D}-live";
                    return mesh;
                }
            }

            throw new InvalidOperationException(
                $"{kLogPrefix} Stroke {stroke.m_Guid:D} has no readable mesh.");
        }

        private static Material GetStrokeMaterial(Stroke stroke)
        {
            if (stroke.m_Type == Stroke.Type.BatchedBrushStroke)
            {
                return stroke.m_BatchSubset?.m_ParentBatch?.InstantiatedMaterial;
            }
            return stroke.m_Object?.GetComponent<MeshRenderer>()?.sharedMaterial;
        }

        private static JObject BuildStrokeInput(Stroke stroke)
        {
            var controlPoints = new JArray();
            foreach (PointerManager.ControlPoint point in stroke.m_ControlPoints)
            {
                controlPoints.Add(new JObject
                {
                    ["position"] = Vector3Array(point.m_Pos),
                    ["orientation"] = QuaternionArray(point.m_Orient),
                    ["pressure"] = point.m_Pressure,
                    ["timestampMs"] = point.m_TimestampMs,
                });
            }

            return new JObject
            {
                ["brushGuid"] = stroke.m_BrushGuid.ToString("D"),
                ["color"] = ColorArray(stroke.m_Color),
                ["brushSize"] = stroke.m_BrushSize,
                ["brushScale"] = stroke.m_BrushScale,
                ["flags"] = Convert.ToUInt64(stroke.m_Flags),
                ["seed"] = stroke.m_Seed,
                ["group"] = stroke.Group.ToString(),
                ["controlPoints"] = controlPoints,
                ["localToWorldMatrix"] = MatrixArray(stroke.StrokeTransform.localToWorldMatrix),
            };
        }

        private static JObject BuildVertexLayout(GeometryPool.VertexLayout layout)
        {
            var texcoords = new JArray();
            for (int channel = 0; channel < GeometryPool.kNumTexcoords; ++channel)
            {
                GeometryPool.TexcoordInfo info = layout.GetTexcoordInfo(channel);
                texcoords.Add(new JObject
                {
                    ["channel"] = channel,
                    ["itemSize"] = info.size,
                    ["semantic"] = info.semantic.ToString(),
                });
            }
            return new JObject
            {
                ["usesNormals"] = layout.bUseNormals,
                ["normalSemantic"] = layout.normalSemantic.ToString(),
                ["usesColors"] = layout.bUseColors,
                ["usesTangents"] = layout.bUseTangents,
                ["usesVertexIds"] = layout.bUseVertexIds,
                ["fbxExportsNormalAsTexcoord1"] = layout.bFbxExportNormalAsTexcoord1,
                ["texcoords"] = texcoords,
            };
        }

        private static JObject BuildMaterial(Material material)
        {
            if (material == null) return null;
            var keywords = new JArray(
                material.shaderKeywords.OrderBy(keyword => keyword, StringComparer.Ordinal));
            var state = new JObject
            {
                ["name"] = material.name,
                ["shader"] = material.shader != null ? material.shader.name : null,
                ["renderQueue"] = material.renderQueue,
                ["enableInstancing"] = material.enableInstancing,
                ["doubleSidedGi"] = material.doubleSidedGI,
                ["keywords"] = keywords,
            };
            var numericProperties = new JObject();
            foreach (string propertyName in new[]
            {
                "_SrcBlend", "_DstBlend", "_ZWrite", "_Cull", "_CullMode", "_ColorMask", "_Mode"
            })
            {
                if (material.HasFloat(propertyName))
                {
                    numericProperties[propertyName] = material.GetFloat(propertyName);
                }
            }
            state["numericProperties"] = numericProperties;
            return state;
        }

        private static JObject BuildBrushBakerMapping(
            bool mappingFound,
            bool processAttempted,
            BrushBaker.ComputeShaderMapping mapping,
            float squeezeAmount)
        {
            var modifiedAttributes = new JArray();
            if (mappingFound)
            {
                if (mapping.ModifyColor) modifiedAttributes.Add("color");
                if (mapping.ModifyNormal) modifiedAttributes.Add("normal");
                if (mapping.ModifyUv0) modifiedAttributes.Add("texcoord0");
                if (mapping.ModifyUv1) modifiedAttributes.Add("texcoord1");
                if (mapping.ModifyUv2) modifiedAttributes.Add("texcoord2");
            }
            return new JObject
            {
                ["mappingFound"] = mappingFound,
                ["processAttempted"] = processAttempted,
                ["squeezeAmount"] = squeezeAmount,
                ["name"] = mappingFound ? mapping.name : null,
                ["computeShader"] = mappingFound && mapping.computeShader != null
                    ? mapping.computeShader.name
                    : null,
                ["modifiedAttributes"] = modifiedAttributes,
            };
        }

        private static JObject BuildMeshStage(
            Mesh mesh,
            GeometryPool.VertexLayout sourceLayout)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            mesh.RecalculateBounds();
            int vertexCount = mesh.vertexCount;
            var attributes = new JObject
            {
                ["position"] = BuildAttribute(
                    3,
                    GeometryPool.Semantic.Position.ToString(),
                    Flatten(mesh.vertices)),
            };

            Vector3[] normals = mesh.normals;
            if (normals.Length == vertexCount)
            {
                attributes["normal"] = BuildAttribute(
                    3,
                    sourceLayout.normalSemantic.ToString(),
                    Flatten(normals));
            }

            Vector4[] tangents = mesh.tangents;
            if (tangents.Length == vertexCount)
            {
                attributes["tangent"] = BuildAttribute(4, "Vector", Flatten(tangents));
            }

            Color[] colors = mesh.colors;
            if (colors.Length == vertexCount)
            {
                attributes["color"] = BuildAttribute(4, "Color", Flatten(colors));
            }

            for (int channel = 0; channel < GeometryPool.kNumTexcoords; ++channel)
            {
                VertexAttribute vertexAttribute =
                    (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
                if (!mesh.HasVertexAttribute(vertexAttribute)) continue;

                var values = new List<Vector4>();
                mesh.GetUVs(channel, values);
                if (values.Count != vertexCount) continue;

                int itemSize = mesh.GetVertexAttributeDimension(vertexAttribute);
                attributes[$"texcoord{channel}"] = BuildAttribute(
                    itemSize,
                    sourceLayout.GetTexcoordInfo(channel).semantic.ToString(),
                    Flatten(values, itemSize));
            }

            int[] indices = mesh.triangles;
            Bounds bounds = mesh.bounds;
            return new JObject
            {
                ["vertexCount"] = vertexCount,
                ["indexCount"] = indices.Length,
                ["indexFormat"] = mesh.indexFormat.ToString(),
                ["subMeshCount"] = mesh.subMeshCount,
                ["attributes"] = attributes,
                ["indices"] = new JArray(indices),
                ["bounds"] = new JObject
                {
                    ["min"] = Vector3Array(bounds.min),
                    ["max"] = Vector3Array(bounds.max),
                },
            };
        }

        private static JObject BuildAttribute(int itemSize, string semantic, JArray data)
        {
            return new JObject
            {
                ["itemSize"] = itemSize,
                ["semantic"] = semantic,
                ["componentType"] = "float32",
                ["data"] = data,
            };
        }

        private static JArray Flatten(IReadOnlyList<Vector3> values)
        {
            var result = new JArray();
            foreach (Vector3 value in values)
            {
                result.Add(value.x);
                result.Add(value.y);
                result.Add(value.z);
            }
            return result;
        }

        private static JArray Flatten(IReadOnlyList<Vector4> values, int itemSize = 4)
        {
            var result = new JArray();
            foreach (Vector4 value in values)
            {
                result.Add(value.x);
                result.Add(value.y);
                if (itemSize >= 3) result.Add(value.z);
                if (itemSize >= 4) result.Add(value.w);
            }
            return result;
        }

        private static JArray Flatten(IReadOnlyList<Color> values)
        {
            var result = new JArray();
            foreach (Color value in values)
            {
                result.Add(value.r);
                result.Add(value.g);
                result.Add(value.b);
                result.Add(value.a);
            }
            return result;
        }

        private static JArray Vector3Array(Vector3 value)
        {
            return new JArray(value.x, value.y, value.z);
        }

        private static JArray QuaternionArray(Quaternion value)
        {
            return new JArray(value.x, value.y, value.z, value.w);
        }

        private static JArray ColorArray(Color value)
        {
            return new JArray(value.r, value.g, value.b, value.a);
        }

        private static JArray MatrixArray(Matrix4x4 value)
        {
            var result = new JArray();
            for (int row = 0; row < 4; ++row)
            {
                for (int column = 0; column < 4; ++column)
                {
                    result.Add(value[row, column]);
                }
            }
            return result;
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character =>
                invalid.Contains(character) ? '_' : character).ToArray());
        }
    }
}
