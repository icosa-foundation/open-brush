// Copyright 2017 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System;

/// A class to hold the duplicated mesh of a mesh filter or a terrain, as well as the surface
/// materials assigned to the triangles.
public class ResonanceAudioAcousticMesh {
  /// The duplicated mesh.
  /// @note The vertices are in world space.
  public Mesh mesh { get; private set; }

  /// The source object from which this acoustic mesh is generated. Can be
  ///   1. A game object with a mesh filter, or
  ///   2. A terrain object.
  public GameObject sourceObject { get; private set; }

  /// Whether the source object of this acoustic mesh is included by the material mapper's object
  /// filtering mechanism. Combined with other criteria such as whether the game object is
  /// activated, the final verdict of whether this acoustic mesh should be included in reverb
  /// computation can be computed (se IsIncluded() below).
  public bool isIncludedByObjectFiltering = false;

  // Mapping from sub-meshes to surface materials. A sub-mesh is a subset of triangles that
  // share a common (visual) material.
  private ResonanceAudioRoomManager.SurfaceMaterial[] surfaceMaterialsFromSubMesh = null;

  // Start and end triangle indices for sub-meshes. All sub-meshes' triangle indices are contiguous,
  // so it is sufficient to store the starting and ending triangle indices.
  // For example:
  //   Sub-mesh  Triangle indices    Triangle range (non-inclusive ending)
  //   -------------------------------------------------------------------
  //   0         {0, 1, 2, 3, 4, 5}  {0, 6}
  //   1         {6, 7, 8}           {6, 9}
  //   2         {9, 10, 11}         {9, 11}
  //
  // Because a sub-mesh's triangles all have the same visual material, it is useful for material
  // mapping, i.e. to assign an (acoustic) surface material to all triangles in it.
  private RangeInt[] triangleRangesFromSubMesh = null;

  // Maximum number of vertices per mesh that Unity allows.
  private const int unityMaxNumVerticesPerMesh = 65000;

  // Maximum number of sub-meshes per mesh that we support.
  private const int maxNumSubMeshes = 256;

  // Unity material to store the shader that visualizes the surface materials as color-coded
  // triangles.
  private Material visualizationMaterial = null;

  /// Generates an acoustic mesh from a source object's mesh filter. Returns null if the
  /// generation failed.
  public static ResonanceAudioAcousticMesh GenerateFromMeshFilter(MeshFilter meshFilter,
                                                                  Shader surfaceMaterialShader) {
    var sourceObject = meshFilter.gameObject;
    var sourceMesh = meshFilter.sharedMesh;
    if (sourceMesh == null) {
      Debug.LogWarning("GameObject: " + sourceObject.name + " has no mesh and will not be " +
                       "included in reverb baking.");
      return null;
    }

    int numTriangleIndices = CountTriangleIndices(sourceMesh);
    int numVertices = sourceMesh.vertexCount;

    ResonanceAudioAcousticMesh acousticMesh = new ResonanceAudioAcousticMesh();
    int[] triangles = null;
    Vector3[] vertices = null;
    acousticMesh.InitializeMesh(numTriangleIndices, numVertices, out triangles, out vertices);

    // Duplicate the source object's mesh. The vertices are transformed to world space.
    acousticMesh.FillVerticesAndTrianglesFromMesh(sourceMesh, sourceObject.transform, ref vertices,
                                                  ref triangles);

    acousticMesh.mesh.vertices = vertices;
    acousticMesh.mesh.triangles = triangles;
    acousticMesh.mesh.RecalculateNormals();

    acousticMesh.InitializeSubMeshMaterials();
    acousticMesh.InitializeVisualizationMaterial(surfaceMaterialShader);

    acousticMesh.sourceObject = sourceObject;
    return acousticMesh;
  }

  /// Generates an acoustic mesh from a terrain.
  public static ResonanceAudioAcousticMesh GenerateFromTerrain(Terrain terrain,
                                                               Shader surfaceMaterialShader) {
    var terrainData = terrain.terrainData;
    var heightMap = terrainData.GetHeights(0, 0, terrainData.heightmapWidth,
                                           terrainData.heightmapHeight);

    // First sub-sample the height map.
    int m;
    int n;
    int subSampledNumTriangleIndices;
    int subSampleStep;
    SubSampleHeightMap(heightMap.GetLength(0), heightMap.GetLength(1), out m, out n,
                       out subSampleStep, out subSampledNumTriangleIndices);

    ResonanceAudioAcousticMesh acousticMesh = new ResonanceAudioAcousticMesh();
    int[] triangles;
    Vector3[] vertices;
    acousticMesh.InitializeMesh(subSampledNumTriangleIndices, subSampledNumTriangleIndices,
                                out triangles, out vertices);

    // Create triangles and vertices from the height map. The vertices are transformed to world
    // space.
    acousticMesh.FillTrianglesAndVerticesFromHeightMap(
        terrain.transform.position, terrainData.size, heightMap, m, n, subSampleStep,
        ref triangles, ref vertices);

    acousticMesh.mesh.vertices = vertices;
    acousticMesh.mesh.triangles = triangles;
    acousticMesh.mesh.RecalculateNormals();

    acousticMesh.InitializeSubMeshMaterials();
    acousticMesh.InitializeVisualizationMaterial(surfaceMaterialShader);

    acousticMesh.sourceObject = terrain.gameObject;
    return acousticMesh;
  }

  /// Gets the mapping from triangles to surface material indices. This will be passed to the
  /// ray-tracing engine to model sound reflecting from a surface.
  public int[] GetSurfaceMaterialIndicesFromTriangle() {
    int[] surfaceMaterialIndicesFromTriangle = new int[mesh.triangles.Length / 3];

    // For each sub-mesh, find the surface material that it maps to.
    for (int subMeshIndex = 0; subMeshIndex < surfaceMaterialsFromSubMesh.Length; ++subMeshIndex) {
      int surfaceMaterialIndex = (int) surfaceMaterialsFromSubMesh[subMeshIndex];

      // Assign the mapped surface material to all the triangles within the range of the sub-mesh.
      for (int triangleIndex = triangleRangesFromSubMesh[subMeshIndex].start;
           triangleIndex < triangleRangesFromSubMesh[subMeshIndex].end; ++triangleIndex) {
        surfaceMaterialIndicesFromTriangle[triangleIndex] = surfaceMaterialIndex;
      }
    }
    return surfaceMaterialIndicesFromTriangle;
  }

  /// Sets the surface material to all sub-meshes. This is particularly useful for assigning surface
  /// materials to terrains, because we model a terrain as a uniform surface.
  public void SetSurfaceMaterialToAllSubMeshes(
      ResonanceAudioRoomManager.SurfaceMaterial surfaceMaterial) {
    for (int subMeshIndex = 0; subMeshIndex < surfaceMaterialsFromSubMesh.Length; ++subMeshIndex) {
      surfaceMaterialsFromSubMesh[subMeshIndex] = surfaceMaterial;
    }
    SetSubMeshSurfaceMaterials();
  }

  /// Sets the surface material to a sub-mesh. This is useful for assigning surface materials to
  /// game objects with a mesh filter. Because in a mesh filter, a sub-mesh contains triangles
  /// sharing a common Unity Material, which will eventually be assigned to the same surface
  /// material.
  public void SetSurfaceMaterialToSubMesh(ResonanceAudioRoomManager.SurfaceMaterial surfaceMaterial,
                                          int subMeshIndex) {
    if (subMeshIndex < 0 || subMeshIndex >= triangleRangesFromSubMesh.Length) {
      Debug.LogError("subMeshIndex= " + subMeshIndex + " out of range [0, " +
                     triangleRangesFromSubMesh.Length + "]");
      return;
    }

    surfaceMaterialsFromSubMesh[subMeshIndex] = surfaceMaterial;
    SetSubMeshSurfaceMaterials();
  }

  /// Renders the acoustic mesh. Returns false if the mesh does not exist.
  public bool Render() {
    if (mesh == null) {
      return false;
    }

    Graphics.DrawMesh(mesh, Matrix4x4.identity, visualizationMaterial, 0);
    return true;
  }

  /// Whether this acoustic mesh is included in reverb computation.
  public bool IsIncluded() {
    // Not included by the material mapper's object filtering.
    if (!isIncludedByObjectFiltering) {
      return false;
    }

    // Mesh is not initialized.
    if (mesh == null) {
      return false;
    }

    // The source object is null or inactive.
    if (sourceObject == null || !sourceObject.activeInHierarchy) {
      return false;
    }

    return true;
  }

  /// Whether the sub-mesh is a triangular mesh.
  public bool IsSubMeshTriangular(int subMeshIndex) {
    return triangleRangesFromSubMesh[subMeshIndex].length > 0;
  }

  // Finds how many indices are used for triangles in |sourceMesh|, which may contain other kinds
  // of sub-meshes such as lines and points.
  private static int CountTriangleIndices(Mesh sourceMesh) {
    int numTriangleIndices = 0;
    for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; ++subMeshIndex) {
      var topology = sourceMesh.GetTopology(subMeshIndex);
      if (topology == MeshTopology.Triangles) {
        numTriangleIndices += (int) sourceMesh.GetIndexCount(subMeshIndex);
      }
    }
    return numTriangleIndices;
  }

  // Finds how to sub-sample the height map so that the total number of vertices is no greater than
  // 65,000. The dimensions of the sub-sampled heightMap will be m-by-n, with each cell being
  // |subSampleStep| times larger than the original cell, and will have
  // |subSampledNumTriangleIndices| vertices.
  private static void SubSampleHeightMap(int originalM, int originalN, out int m, out int n,
                                         out int subSampleStep,
                                         out int subSampledNumTriangleIndices) {
    m = originalM;
    n = originalN;

    // We need 6 triangle indices for every square cell of the height map (2 triangles * 3 indices
    // per triangle).
    subSampledNumTriangleIndices = (m - 1) * (n - 1) * 6;
    subSampleStep = 1;

    // Grow the cell size by [2, 2] in each iteration and thereby reducing the dimensions [m, n].
    // Note Unity requires that the m and n are of the form 2^N + 1.
    while (subSampledNumTriangleIndices >= unityMaxNumVerticesPerMesh) {
      subSampleStep *= 2;

      // After one iteration, m (or n) is reduced from 2^N + 1 to 2^(N-1) + 1.
      m = (m - 1) / 2 + 1;
      n = (n - 1) / 2 + 1;
      subSampledNumTriangleIndices =  (m - 1) * (n - 1) * 6;
    }
  }

  // Allocates the mesh, its triangles and vertices, and resizes them as needed.
  private void InitializeMesh(int numTriangleIndices, int numVertices,
                              out int[] triangles, out Vector3[] vertices) {
    if (mesh == null) {
      mesh = new Mesh();
    }
    triangles = mesh.triangles;
    Array.Resize(ref triangles, numTriangleIndices);
    vertices = mesh.vertices;
    Array.Resize(ref vertices, numVertices);
  }

  // Initializes surface materials for all sub-meshes to transparent (i.e. perfectly absorbent).
  private void InitializeSubMeshMaterials() {
    // Do nothing if the sub-mesh-to-surface-material mapping is already allocated with the correct
    // length.
    int numSubMeshes = triangleRangesFromSubMesh.Length;
    if (surfaceMaterialsFromSubMesh != null && surfaceMaterialsFromSubMesh.Length == numSubMeshes) {
      return;
    }

    surfaceMaterialsFromSubMesh = new ResonanceAudioRoomManager.SurfaceMaterial[numSubMeshes];
    for (int i = 0; i < numSubMeshes; ++i) {
      surfaceMaterialsFromSubMesh[i] = ResonanceAudioRoomManager.SurfaceMaterial.Transparent;
    }
  }

  // Initializes a Unity material that holds the shader that visualizes the surface materials.
  private void InitializeVisualizationMaterial(Shader surfaceMaterialShader) {
    if (visualizationMaterial == null) {
      visualizationMaterial = new Material(surfaceMaterialShader);
    }

    SetSubMeshEnds();
  }

  // Fills the vertices array (the vertices are transformed to world space first) and the triangle
  // indices from a mesh. Reverse the ordering if necessary.
  private void FillVerticesAndTrianglesFromMesh(Mesh sourceMesh, Transform sourceObjectTransform,
                                                ref Vector3[] vertices, ref int[] triangles) {
    // Copy all vertices.
    var sourceObjectVertices = sourceMesh.vertices;
    for (int i = 0; i < sourceObjectVertices.Length; ++i) {
      vertices[i] = sourceObjectTransform.TransformPoint(sourceObjectVertices[i]);
    }

    // Group the triangles (recorded as index ranges) by sub-meshes.
    Array.Resize(ref triangleRangesFromSubMesh, sourceMesh.subMeshCount);

    // Reverse the order of the vertices in triangles if the mesh is "flipped", i.e., an odd number
    // of its three scales (in the local x-, y-, z-directions respectively) are negative.
    var sourceObjectScale = sourceObjectTransform.lossyScale;
    bool reverseTriangleOrder =
        (sourceObjectScale.x * sourceObjectScale.y * sourceObjectScale.z) < 0;

    int triangleIndex = 0;
    for (int subMeshIndex = 0; subMeshIndex < sourceMesh.subMeshCount; ++subMeshIndex) {
      triangleRangesFromSubMesh[subMeshIndex].start = triangleIndex / 3;
      if (sourceMesh.GetTopology(subMeshIndex) != MeshTopology.Triangles) {
        triangleRangesFromSubMesh[subMeshIndex].length = 0;
        continue;
      }

      var subMeshTriangles = sourceMesh.GetTriangles(subMeshIndex);
      for (int j = 0; j < subMeshTriangles.Length; j += 3) {
        if (reverseTriangleOrder) {
          triangles[triangleIndex + 0] = subMeshTriangles[j + 2];
          triangles[triangleIndex + 1] = subMeshTriangles[j + 1];
          triangles[triangleIndex + 2] = subMeshTriangles[j + 0];
        } else {
          triangles[triangleIndex + 0] = subMeshTriangles[j + 0];
          triangles[triangleIndex + 1] = subMeshTriangles[j + 1];
          triangles[triangleIndex + 2] = subMeshTriangles[j + 2];
        }
        triangleIndex += 3;
      }

      triangleRangesFromSubMesh[subMeshIndex].length = subMeshTriangles.Length / 3;
    }
  }

  // Fills the triangles and vertices arrays using sub-sampled points on the height map.
  private void FillTrianglesAndVerticesFromHeightMap(Vector3 terrainPosition, Vector3 terrainSize,
                                                     float [,] heightMap, int m, int n,
                                                     int subSampleStep, ref int[] triangles,
                                                     ref Vector3[] vertices) {
    // Split a square cell to two triangles with six vertices.
    // Indices:           =>   Vertices:
    // (i, j)      (i, j+1)    0         2 3
    //   |-----------|         |---------//|
    //   |           |         |        // |
    //   |           |         |       //  |
    //   |           |         |      //   |
    //   |           |         |     //    |
    //   |           |    =>   |    //     |
    //   |           |         |   //      |
    //   |           |         |  //       |
    //   |           |         | //        |
    //   |-----------|         |//---------|
    // (i+1, j)    (i+1, j+1)  1 4         5
    //
    // So for example, for a cell with
    // top-left corner (i, j), the first triangle is made of the following vertices:
    //     (i, j)   = (i, j) + (0, 0)
    //     (i, j+1) = (i, j) + (0, 1)
    //     (i+1, j) = (i, j) + (1, 0)
    // Which can be expressed as (i, j) + offsets[k] with k in [0, 6), where 6 is the number of
    // vertices per cell (2 triangles per cell * 3 vertices per triangle).
    int numVerticesPerCell = 6;
    int[,] offsets = {
      {0, 0},  // Vertex 0
      {1, 0},  // Vertex 1
      {0, 1},  // Vertex 2
      {0, 1},  // Vertex 3
      {1, 0},  // Vertex 4
      {1, 1},  // Vertex 5
    };

    // The height map's 2D coordinates are normalized between 0 and 1. So the mapping of a point
    // (I, J) and its height(I, J) to a point (x, y, z) in world space is:
    //     x = terrainPosition.x + J            * terrainSize.x / (N - 1)
    //     y = terrainPosition.x + height(I, J) * terrainSize.y
    //     z = terrainPosition.x + I            * terrainSize.z / (M - 1)
    // where (I, J) are the original (before sub-sampling) point indices, and (M, N) the original
    // height map dimensions.
    //
    // We formulate the above equations into a vector operation:
    //     worldPoint = terrainPosition = heightMapPoint * heightMapScaler,
    // where
    //    worldPoint = (x, y, z)
    //    heightMapPoint = (J, height(I, J), I)
    //    heightMapScaler = (terrainSize.x / (N - 1), terrainSize.y, terrainSize.z / (M - 1))
    var originalM = heightMap.GetLength(0);
    var originalN = heightMap.GetLength(1);
    Vector3 heightMapScaler =
        Vector3.Scale(terrainSize, new Vector3(1.0f / (float) (originalN - 1),
                                               1.0f,
                                               1.0f / (float) (originalM - 1)));

    Vector3 heightMapPoint = new Vector3();
    for (int i = 0; i < m - 1; ++i) {
      for (int j = 0; j < n - 1; ++j) {
        int subSampledCellIndex = i * (n - 1) + j;
        for (int k = 0; k < numVerticesPerCell; ++k) {
          // Triangles.
          int triangleIndex = numVerticesPerCell * subSampledCellIndex + k;
          triangles[triangleIndex] = triangleIndex;

          // The 2D-array |heightMap| is sub-sampled to dimensions m and n. The original indices
          // can be obtained by multiplying the |subSampleStep| to a sub-sampled point indexed at
          // (i, j) + offsets[k].
          int originalI = (i + offsets[k, 0]) * subSampleStep;
          int originalJ = (j + offsets[k, 1]) * subSampleStep;

          heightMapPoint.Set(originalJ, heightMap[originalI, originalJ], originalI);

          // The vector operation above:
          //     worldPoint = terrainPosition + heightMapPoint * heightMapScaler,
          vertices[triangleIndex] = terrainPosition + Vector3.Scale(heightMapPoint,
                                                                    heightMapScaler);
        }
      }
    }

    // We consider all triangles of a terrain as belonging to a single sub-mesh.
    Array.Resize(ref triangleRangesFromSubMesh, 1);
    triangleRangesFromSubMesh[0].start = 0;
    triangleRangesFromSubMesh[0].length = triangles.Length / 3;
  }

  // Sets the index of the ending triangle of each sub-mesh. The array will be used by the shader to
  // determine which sub-mesh a specific triangle belongs to.
  private void SetSubMeshEnds() {
    int numSubMeshes = surfaceMaterialsFromSubMesh.Length;

    if (numSubMeshes > maxNumSubMeshes) {
      Debug.LogError("Too many sub-meshes: " + sourceObject.name + " has " + numSubMeshes +
                     " sub-meshes. Sub-meshes more than " + maxNumSubMeshes + " are not allowed.");
      return;
    }

    float[] endsArray = new float[numSubMeshes];
    for (int i = 0; i < numSubMeshes; ++i) {
      endsArray[i] = (float) triangleRangesFromSubMesh[i].end;
    }
    visualizationMaterial.SetFloatArray("_SubMeshEnds", endsArray);
    visualizationMaterial.SetInt("_NumSubMeshes", numSubMeshes);
  }

  // Sets the surface materials as an array indexed by sub-mesh indices. The array will be used by
  // the shader to display the color coding of the material mapping.
  private void SetSubMeshSurfaceMaterials() {
    int numSubMeshes = surfaceMaterialsFromSubMesh.Length;
    float[] materialsArray = visualizationMaterial.GetFloatArray("_SubMeshMaterials");
    if (materialsArray == null) {
      materialsArray = new float[numSubMeshes];
    }

    for (int i = 0; i < surfaceMaterialsFromSubMesh.Length; ++i) {
      materialsArray[i] = (float) surfaceMaterialsFromSubMesh[i];
    }
    visualizationMaterial.SetFloatArray("_SubMeshSurfaceMaterials", materialsArray);
  }
}
