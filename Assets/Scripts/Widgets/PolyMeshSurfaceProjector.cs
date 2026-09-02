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

using System.Collections.Generic;
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush
{
    /// Projects points onto the polygon faces of a PolyMesh without reducing those faces to the
    /// triangles used by Unity for rendering and collision.
    public sealed class PolyMeshSurfaceProjector
    {
        private sealed class FaceData
        {
            public Face Face;
            public Vector3 Normal;
            public Vector3 Origin;
            public Vector3 TangentX;
            public Vector3 TangentY;
            public Vector3[] Vertices;
            public Vector2[] Vertices2d;
            public Bounds Bounds;
        }

        private readonly FaceData[] m_Faces;
        private readonly Dictionary<Face, int> m_FaceIndices;

        public int FaceCount => m_Faces.Length;

        public PolyMeshSurfaceProjector(PolyMesh poly)
        {
            m_Faces = new FaceData[poly.Faces.Count];
            m_FaceIndices = new Dictionary<Face, int>(poly.Faces.Count);

            for (int faceIndex = 0; faceIndex < poly.Faces.Count; ++faceIndex)
            {
                Face face = poly.Faces[faceIndex];
                List<Vertex> faceVertices = face.GetVertices();
                var vertices = new Vector3[faceVertices.Count];
                for (int vertexIndex = 0; vertexIndex < faceVertices.Count; ++vertexIndex)
                {
                    vertices[vertexIndex] = faceVertices[vertexIndex].Position;
                }

                Vector3 origin = face.Centroid;
                (Vector3 tangentX, Vector3 tangentY) = face.GetTangents();
                var vertices2d = new Vector2[vertices.Length];
                for (int vertexIndex = 0; vertexIndex < vertices.Length; ++vertexIndex)
                {
                    Vector3 fromOrigin = vertices[vertexIndex] - origin;
                    vertices2d[vertexIndex] = new Vector2(
                        Vector3.Dot(fromOrigin, tangentX),
                        Vector3.Dot(fromOrigin, tangentY));
                }

                Bounds bounds = vertices.Length > 0
                    ? new Bounds(vertices[0], Vector3.zero)
                    : new Bounds(origin, Vector3.zero);
                for (int vertexIndex = 1; vertexIndex < vertices.Length; ++vertexIndex)
                {
                    bounds.Encapsulate(vertices[vertexIndex]);
                }

                m_Faces[faceIndex] = new FaceData
                {
                    Face = face,
                    Normal = face.Normal,
                    Origin = origin,
                    TangentX = tangentX,
                    TangentY = tangentY,
                    Vertices = vertices,
                    Vertices2d = vertices2d,
                    Bounds = bounds
                };
                m_FaceIndices.Add(face, faceIndex);
            }
        }

        public int FindClosestFace(Vector3 point, out Vector3 closestPoint)
        {
            int closestFaceIndex = -1;
            float closestDistanceSq = float.PositiveInfinity;
            closestPoint = point;

            for (int faceIndex = 0; faceIndex < m_Faces.Length; ++faceIndex)
            {
                FaceData face = m_Faces[faceIndex];
                if (face.Bounds.SqrDistance(point) > closestDistanceSq)
                {
                    continue;
                }

                Vector3 candidate = ClosestPointOnFace(face, point);
                float candidateDistanceSq = (candidate - point).sqrMagnitude;
                if (candidateDistanceSq < closestDistanceSq)
                {
                    closestFaceIndex = faceIndex;
                    closestDistanceSq = candidateDistanceSq;
                    closestPoint = candidate;
                }
            }

            return closestFaceIndex;
        }

        public Vector3 ClosestPointOnFace(int faceIndex, Vector3 point)
        {
            return ClosestPointOnFace(m_Faces[faceIndex], point);
        }

        public Vector3 GetFaceNormal(int faceIndex)
        {
            return m_Faces[faceIndex].Normal;
        }

        public bool CanTransition(int fromFaceIndex, int toFaceIndex, float maxDihedralAngle)
        {
            if (fromFaceIndex == toFaceIndex)
            {
                return true;
            }

            // Face changes normally occur across one edge. Handle that without allocating the
            // traversal buffers used when pointer sampling skips one or more intermediate faces.
            foreach (Halfedge edge in m_Faces[fromFaceIndex].Face.GetHalfedges())
            {
                if (edge.Pair != null &&
                    m_FaceIndices.TryGetValue(edge.Pair.Face, out int adjacentFaceIndex) &&
                    adjacentFaceIndex == toFaceIndex)
                {
                    return edge.DihedralAngle <= maxDihedralAngle;
                }
            }

            var visited = new bool[m_Faces.Length];
            var pending = new int[m_Faces.Length];
            int readIndex = 0;
            int writeIndex = 0;
            visited[fromFaceIndex] = true;
            pending[writeIndex++] = fromFaceIndex;

            while (readIndex < writeIndex)
            {
                int faceIndex = pending[readIndex++];
                foreach (Halfedge edge in m_Faces[faceIndex].Face.GetHalfedges())
                {
                    if (edge.Pair == null || edge.DihedralAngle > maxDihedralAngle ||
                        !m_FaceIndices.TryGetValue(edge.Pair.Face, out int adjacentFaceIndex) ||
                        visited[adjacentFaceIndex])
                    {
                        continue;
                    }

                    if (adjacentFaceIndex == toFaceIndex)
                    {
                        return true;
                    }

                    visited[adjacentFaceIndex] = true;
                    pending[writeIndex++] = adjacentFaceIndex;
                }
            }

            return false;
        }

        private static Vector3 ClosestPointOnFace(FaceData face, Vector3 point)
        {
            if (face.Vertices.Length == 0 || face.Normal.sqrMagnitude == 0f)
            {
                return face.Origin;
            }

            Vector3 projected = point - face.Normal * Vector3.Dot(point - face.Origin, face.Normal);
            Vector3 projectedFromOrigin = projected - face.Origin;
            Vector2 projected2d = new Vector2(
                Vector3.Dot(projectedFromOrigin, face.TangentX),
                Vector3.Dot(projectedFromOrigin, face.TangentY));

            if (IsPointInPolygon(projected2d, face.Vertices2d))
            {
                return projected;
            }

            Vector3 closest = face.Vertices[0];
            float closestDistanceSq = float.PositiveInfinity;
            for (int vertexIndex = 0; vertexIndex < face.Vertices.Length; ++vertexIndex)
            {
                Vector3 edgeStart = face.Vertices[vertexIndex];
                Vector3 edgeEnd = face.Vertices[(vertexIndex + 1) % face.Vertices.Length];
                Vector3 edge = edgeEnd - edgeStart;
                float edgeLengthSq = edge.sqrMagnitude;
                float edgeT = edgeLengthSq > 0f
                    ? Mathf.Clamp01(Vector3.Dot(projected - edgeStart, edge) / edgeLengthSq)
                    : 0f;
                Vector3 candidate = edgeStart + edge * edgeT;
                float candidateDistanceSq = (candidate - point).sqrMagnitude;
                if (candidateDistanceSq < closestDistanceSq)
                {
                    closest = candidate;
                    closestDistanceSq = candidateDistanceSq;
                }
            }

            return closest;
        }

        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            bool inside = false;
            for (int current = 0, previous = polygon.Length - 1;
                 current < polygon.Length;
                 previous = current++)
            {
                Vector2 currentPoint = polygon[current];
                Vector2 previousPoint = polygon[previous];
                bool crossesScanline = (currentPoint.y > point.y) != (previousPoint.y > point.y);
                if (crossesScanline)
                {
                    float intersectionX =
                        (previousPoint.x - currentPoint.x) *
                        (point.y - currentPoint.y) /
                        (previousPoint.y - currentPoint.y) + currentPoint.x;
                    if (point.x < intersectionX)
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }
    }
} // namespace TiltBrush
