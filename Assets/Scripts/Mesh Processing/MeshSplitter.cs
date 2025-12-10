using System.Collections.Generic;
using UnityEngine;

namespace TiltBrush
{
    public static class MeshSplitter
    {
        public static List<MeshFilter> DoSplit(MeshFilter sourceMf, float weldTolerance = 0.0001f)
        {
            if (!sourceMf)
            {
                Debug.LogError("Source mesh filter for splitting is null");
                return new List<MeshFilter>();
            }
            List<MeshFilter> splits = new List<MeshFilter>();

            // System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            // stopwatch.Start();

            // Step 1: Extract all mesh data on main thread
            var mesh = sourceMf.sharedMesh ?? sourceMf.mesh;
            var renderer = sourceMf.GetComponent<MeshRenderer>();
            var materials = renderer.sharedMaterials;

            // Extract triangles from each submesh with material mapping
            var trianglesList = new List<int>();
            var materialIndices = new List<int>();

            for (int submeshIndex = 0; submeshIndex < mesh.subMeshCount; submeshIndex++)
            {
                var submeshTriangles = mesh.GetTriangles(submeshIndex);
                foreach (var tri in submeshTriangles)
                {
                    trianglesList.Add(tri);
                    materialIndices.Add(submeshIndex);
                }
            }

            var meshData = new MeshSplitInput
            {
                meshFilter = sourceMf,
                vertices = mesh.vertices,
                triangles = trianglesList.ToArray(),
                materialIndices = materialIndices.ToArray(),
                materials = materials,
                normals = mesh.normals,
                tangents = mesh.tangents,
                colors = mesh.colors,
                uv = mesh.uv,
                uv2 = mesh.uv2,
                uv3 = mesh.uv3,
                uv4 = mesh.uv4
            };

            var splitResults = SplitMeshData(meshData, weldTolerance);

            var meshFilter = meshData.meshFilter;
            int partNum = 0;
            foreach (var part in splitResults.parts)
            {
                // Group triangles by material
                var submeshTriangles = new Dictionary<int, List<int>>();
                for (int i = 0; i < part.triangles.Length; i++)
                {
                    int matIndex = part.materialIndices[i];
                    if (!submeshTriangles.TryGetValue(matIndex, out var tris))
                    {
                        tris = new List<int>();
                        submeshTriangles[matIndex] = tris;
                    }
                    tris.Add(part.triangles[i]);
                }

                Mesh newMesh = new Mesh
                {
                    vertices = part.vertices
                };

                // Set submeshes
                newMesh.subMeshCount = submeshTriangles.Count;
                var submeshMaterials = new List<Material>();
                int submeshIndex = 0;
                foreach (var kvp in submeshTriangles)
                {
                    int matIndex = kvp.Key;
                    var tris = kvp.Value;
                    newMesh.SetTriangles(tris.ToArray(), submeshIndex);
                    submeshMaterials.Add(materials[matIndex]);
                    submeshIndex++;
                }

                if (part.normals != null && part.normals.Length == part.vertices.Length)
                    newMesh.normals = part.normals;
                else
                    newMesh.RecalculateNormals();
                if (part.tangents != null && part.tangents.Length == part.vertices.Length)
                    newMesh.tangents = part.tangents;
                if (part.colors != null && part.colors.Length == part.vertices.Length)
                    newMesh.colors = part.colors;
                if (part.uv != null && part.uv.Length == part.vertices.Length)
                    newMesh.uv = part.uv;
                if (part.uv2 != null && part.uv2.Length == part.vertices.Length)
                    newMesh.uv2 = part.uv2;
                if (part.uv3 != null && part.uv3.Length == part.vertices.Length)
                    newMesh.uv3 = part.uv3;
                if (part.uv4 != null && part.uv4.Length == part.vertices.Length)
                    newMesh.uv4 = part.uv4;
                newMesh.RecalculateBounds();

                GameObject go = new GameObject($"{meshFilter.name}[obsplit:{partNum++}]");
                var split = go.AddComponent<MeshFilter>();
                split.mesh = newMesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = submeshMaterials.ToArray();
                go.transform.SetParent(meshFilter.transform, false);
                splits.Add(split);
            }

            // stopwatch.Stop();
            // Debug.Log($"Mesh splitting completed in {stopwatch.ElapsedMilliseconds / 1000f} seconds.");

            return splits;
        }

        // Structs for passing mesh data and results between threads
        struct MeshSplitInput
        {
            public MeshFilter meshFilter;
            public Vector3[] vertices;
            public int[] triangles;
            public int[] materialIndices;
            public Material[] materials;
            public Vector3[] normals;
            public Vector4[] tangents;
            public Color[] colors;
            public Vector2[] uv;
            public Vector2[] uv2;
            public Vector2[] uv3;
            public Vector2[] uv4;
        }

        struct MeshSplitResult
        {
            public List<MeshPart> parts;
        }

        struct MeshPart
        {
            public Vector3[] vertices;
            public int[] triangles;
            public int[] materialIndices;
            public Vector3[] normals;
            public Vector4[] tangents;
            public Color[] colors;
            public Vector2[] uv;
            public Vector2[] uv2;
            public Vector2[] uv3;
            public Vector2[] uv4;
        }

        // This method does not touch UnityEngine objects, so it's safe for parallel execution
        static MeshSplitResult SplitMeshData(MeshSplitInput input, float weldTolerance)
        {
            var vertices = input.vertices;
            var triangles = input.triangles;
            var materialIndices = input.materialIndices;
            var normals = input.normals;
            var tangents = input.tangents;
            var colors = input.colors;
            var uv = input.uv;
            var uv2 = input.uv2;
            var uv3 = input.uv3;
            var uv4 = input.uv4;

            int vertCount = vertices.Length;
            int triCount = triangles.Length;

            var spatialHash = new Dictionary<Vector3Int, List<int>>(vertCount / 4);
            int[] vertexMap = new int[vertCount];
            int uniqueCount = 0;
            float weldSqr = weldTolerance * weldTolerance;

            for (int i = 0; i < vertCount; i++)
            {
                Vector3 v = vertices[i];
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(v.x / weldTolerance),
                    Mathf.RoundToInt(v.y / weldTolerance),
                    Mathf.RoundToInt(v.z / weldTolerance)
                );

                if (!spatialHash.TryGetValue(key, out var bucket))
                {
                    bucket = new List<int>();
                    spatialHash[key] = bucket;
                }

                bool found = false;
                for (int j = 0, bCount = bucket.Count; j < bCount; j++)
                {
                    int idx = bucket[j];
                    if ((vertices[idx] - v).sqrMagnitude <= weldSqr)
                    {
                        vertexMap[i] = vertexMap[idx];
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    vertexMap[i] = uniqueCount;
                    bucket.Add(i);
                    uniqueCount++;
                }
            }

            UnionFind uf = new UnionFind(uniqueCount);
            for (int i = 0; i < triCount; i += 3)
            {
                int a = vertexMap[triangles[i]];
                int b = vertexMap[triangles[i + 1]];
                int c = vertexMap[triangles[i + 2]];
                uf.Union(a, b);
                uf.Union(b, c);
            }

            var componentTriangles = new Dictionary<int, List<int>>(uniqueCount);
            var componentMaterialIndices = new Dictionary<int, List<int>>(uniqueCount);
            for (int i = 0; i < triCount; i += 3)
            {
                int a = vertexMap[triangles[i]];
                int componentId = uf.Find(a);

                if (!componentTriangles.TryGetValue(componentId, out var tris))
                {
                    tris = new List<int>();
                    componentTriangles[componentId] = tris;
                }
                if (!componentMaterialIndices.TryGetValue(componentId, out var matIndices))
                {
                    matIndices = new List<int>();
                    componentMaterialIndices[componentId] = matIndices;
                }
                tris.Add(triangles[i]);
                tris.Add(triangles[i + 1]);
                tris.Add(triangles[i + 2]);
                // All three vertices of a triangle share the same material
                int triMaterialIndex = materialIndices[i];
                matIndices.Add(triMaterialIndex);
                matIndices.Add(triMaterialIndex);
                matIndices.Add(triMaterialIndex);
            }

            var parts = new List<MeshPart>(componentTriangles.Count);
            foreach (var comp in componentTriangles)
            {
                var compTriangles = comp.Value;
                var compMatIndices = componentMaterialIndices[comp.Key];
                int compTriCount = compTriangles.Count;
                var vertexRemap = new Dictionary<int, int>(compTriCount / 3);

                var newVerts = new List<Vector3>();
                var newNormals = normals != null && normals.Length == vertCount ? new List<Vector3>() : null;
                var newTangents = tangents != null && tangents.Length == vertCount ? new List<Vector4>() : null;
                var newColors = colors != null && colors.Length == vertCount ? new List<Color>() : null;
                var newUV = uv != null && uv.Length == vertCount ? new List<Vector2>() : null;
                var newUV2 = uv2 != null && uv2.Length == vertCount ? new List<Vector2>() : null;
                var newUV3 = uv3 != null && uv3.Length == vertCount ? new List<Vector2>() : null;
                var newUV4 = uv4 != null && uv4.Length == vertCount ? new List<Vector2>() : null;
                var newTris = new List<int>();
                var newMatIndices = new List<int>();

                int newIndex = 0;
                for (int i = 0; i < compTriCount; i++)
                {
                    int idx = compTriangles[i];
                    if (!vertexRemap.TryGetValue(idx, out int remapped))
                    {
                        remapped = newIndex++;
                        vertexRemap[idx] = remapped;
                        newVerts.Add(vertices[idx]);
                        if (newNormals != null) newNormals.Add(normals[idx]);
                        if (newTangents != null) newTangents.Add(tangents[idx]);
                        if (newColors != null) newColors.Add(colors[idx]);
                        if (newUV != null) newUV.Add(uv[idx]);
                        if (newUV2 != null) newUV2.Add(uv2[idx]);
                        if (newUV3 != null) newUV3.Add(uv3[idx]);
                        if (newUV4 != null) newUV4.Add(uv4[idx]);
                    }
                    newTris.Add(remapped);
                    newMatIndices.Add(compMatIndices[i]);
                }

                parts.Add(new MeshPart
                {
                    vertices = newVerts.ToArray(),
                    triangles = newTris.ToArray(),
                    materialIndices = newMatIndices.ToArray(),
                    normals = newNormals?.ToArray(),
                    tangents = newTangents?.ToArray(),
                    colors = newColors?.ToArray(),
                    uv = newUV?.ToArray(),
                    uv2 = newUV2?.ToArray(),
                    uv3 = newUV3?.ToArray(),
                    uv4 = newUV4?.ToArray()
                });
            }

            return new MeshSplitResult { parts = parts };
        }
    }
}