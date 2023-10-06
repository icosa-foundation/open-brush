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

using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

/// Resonance Audio reverb computer that communicates with the native reverb computation engine.
public static class ResonanceAudioReverbComputer {
  /// Computes the reverb for reverb probes, given the scene geometry defined by acoustic
  /// meshes, and stores the results in the reverb probes. Returns false if failed.
  public static bool ComputeReverb(List<ResonanceAudioAcousticMesh> acousticMeshes) {
    // First extract scene geometry (vertices, triangles, and materials) from all acoustic meshes.
    if (!ExtractSceneGeometryFromAcousticMeshes(acousticMeshes)) {
      Debug.LogError("Geometry not valid; aborted.");
      return false;
    }

    // Actually compute the reverb and store the results in |reverbProbes|.
    if (!ComputeReverbForSceneGeometry()) {
      return false;
    }

    Debug.Log("Reverb baking is completed successfully.");
    return true;
  }

  /// The list of selected reverb probes to bake reverb to.
  public static List<ResonanceAudioReverbProbe> selectedReverbProbes =
      new List<ResonanceAudioReverbProbe>();

  // Extracts triangles, vertices, and materials from acoustic meshes. Returns false if failed.
  private static bool ExtractSceneGeometryFromAcousticMeshes(
      List<ResonanceAudioAcousticMesh> acousticMeshes) {
    // The triangles, vertices, and materials are stored in three global arrays that combine all
    // data from individual acoustic meshes.
    // First pre-allocate the global arrays.
    PreAllocateArrays(acousticMeshes);

    // Fill triangles, vertices, and materials from acoustic meshes one after another to the global
    // arrays. |lastVertexIndex| and |lastTriangleIndex| are to keep track of where in the global
    // arrays we are currently filling.
    int lastVertexIndex = 0;
    int lastTriangleIndex = 0;
    for (int i = 0; i < acousticMeshes.Count; ++i) {
      var acousticMesh = acousticMeshes[i];
      if (!FillArraysFromAcousticMesh(acousticMesh.GetSurfaceMaterialIndicesFromTriangle(),
                                      acousticMesh.mesh.vertices, acousticMesh.mesh.triangles,
                                      ref lastVertexIndex, ref lastTriangleIndex)) {
        return false;
      }
    }

    return true;
  }

  // Computes the size of the global triangles, vertices, and materials arrays by adding up
  // numbers from all acoustic meshes.
  private static void PreAllocateArrays(List<ResonanceAudioAcousticMesh> acousticMeshes) {
    int verticesLength = 0;
    int trianglesLength = 0;
    int materialsLength = 0;
    for (int i = 0; i < acousticMeshes.Count; ++i) {
      int vertexCount = acousticMeshes[i].mesh.vertexCount;
      int triangleCount = acousticMeshes[i].mesh.triangles.Length / 3;
      verticesLength += 3 * vertexCount;     // 3 floats for every vertex.
      trianglesLength += triangleCount * 3;  // 3 ints for every triangle.
      materialsLength += triangleCount;      // 1 material per triangle.
    }

    Array.Resize(ref vertices, verticesLength);
    Array.Resize(ref triangles, trianglesLength);
    Array.Resize(ref materials, materialsLength);
  }

  // Fills data from an acoustic mesh to the global triangles, vertices, and materials arrays.
  // Returns false if failed.
  private static bool FillArraysFromAcousticMesh(int[] meshMaterials, Vector3[] meshVertices,
                                                 int[] meshTriangles, ref int lastVertexIndex,
                                                 ref int lastTriangleIndex) {
    if (meshMaterials.Length != meshTriangles.Length / 3) {
      Debug.LogError("The number of materials assigned to triangles (" + meshMaterials.Length +
                     ") should be equal to the number of triangles (" + meshTriangles.Length / 3 +
                     ")");
      return false;
    }

    // Vertices.
    for (int i = 0; i < meshVertices.Length; ++i) {
      var vertex = meshVertices[i];
      int vertexIndexOffset = 3 * (lastVertexIndex + i);
      vertices[vertexIndexOffset + 0] = vertex.x;
      vertices[vertexIndexOffset + 1] = vertex.y;
      vertices[vertexIndexOffset + 2] = vertex.z;
    }

    // Triangles.
    for (int i = 0; i < meshTriangles.Length; ++i) {
      triangles[lastTriangleIndex + i] = meshTriangles[i] + lastVertexIndex;
    }

    // Surface materials.
    meshMaterials.CopyTo(materials, lastTriangleIndex / 3);

    // Update the global array index trackers.
    lastTriangleIndex += meshTriangles.Length;
    lastVertexIndex += meshVertices.Length;
    return true;
  }

  // Computes reverb using already extracted scene geometry. Calls various native functions.
  // Returns false if failed.
  private static bool ComputeReverbForSceneGeometry() {
    // Initializes the reverb computation engine: creates the scene, sets up the surface materials,
    // and prepares the ray tracer.
    ResonanceAudio.InitializeReverbComputer(vertices, triangles, materials, scatteringCoefficient);
    // Iterate through the selected reverb probes.
    for (int i = 0; i < selectedReverbProbes.Count; ++i) {
      var reverbProbe = selectedReverbProbes[i];
      Undo.RecordObject(reverbProbe, "Bake Reverb To Reverb Probes");
      // Compute the RT60s and estimate the proxy room.
      if (!ResonanceAudio.ComputeRt60sAndProxyRoom(reverbProbe, totalNumPaths, numPathsPerBatch,
                                                   maxDepth, energyThreshold,
                                                   listenerSphereRadius)) {
        Debug.LogError("Failed to compute reverb probe[" + i + "] " + reverbProbe.gameObject.name);
        return false;
      }
    }
    return true;
  }

  // Vertices, triangles, and materials. These are all arrays and will be passed to the native
  // reverb computation engine.
  private static float[] vertices = null;
  private static int[] triangles = null;
  private static int[] materials = null;

  // Ray-tracing related parameters.
  // Total number of ray-paths.
  private const int totalNumPaths = 50000;

  // The ray tracing is done in batches, How many ray-paths per batch.
  private const int numPathsPerBatch = 1000;

  // Maximum depth of ray tracing, i.e., the maximum number reflections for a single ray path.
  private const int maxDepth = 100;

  // The relative energy decrease below which the ray tracing terminates.
  private const float energyThreshold = 1e-12f;

  // The global scattering coefficient for rays reflecting from surfaces.
  private const float scatteringCoefficient = 1.0f;

  // The radius of the listener sphere, which is used to intersect rays and collect energies.
  private const float listenerSphereRadius = 0.1f;
}
