// Run in an idle Main-scene Play session after ColdBatchAppendParity.LoadFixture has settled.
// Covers resident/cold removal, non-empty tail truncation, empty upload and reuse for real brush layouts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TiltBrush.Benchmarks
{
    public static class ColdBatchRemovalParity
    {
        [Serializable]
        public class Result { public string status = "passed"; public string[] passed; }

        public static string Run(string output)
        {
            if (!Application.isPlaying || App.CurrentState != App.AppState.Standard ||
                SketchMemoryScript.m_Instance.IsPlayingBack || SketchMemoryScript.AllStrokesCount() == 0 ||
                UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false))
                throw new InvalidOperationException("Use an idle initialized Play session with a loaded sketch");
            var passed = new List<string>();
            foreach (var stroke in SketchMemoryScript.AllStrokes()
                .Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke && s.m_BatchSubset.m_VertLength > 0)
                .GroupBy(s => s.m_BrushGuid).Select(g => g.OrderBy(s => s.m_BatchSubset.m_VertLength).First()))
            {
                foreach (bool cold in new[] { false, true })
                    foreach (bool frontFirst in new[] { false, true })
                    {
                        Check(stroke.m_BatchSubset, cold, frontFirst);
                        passed.Add($"{stroke.m_BrushGuid}/cold={cold}/frontFirst={frontFirst}");
                    }
            }
            if (passed.Count == 0) throw new Exception("No brush layouts tested");
            File.WriteAllText(output, JsonUtility.ToJson(new Result { passed = passed.ToArray() }, true));
            return $"[ColdBatchRemovalParity] Passed {passed.Count} removal, empty-upload and reuse cases";
        }

        static void Check(BatchSubset source, bool cold, bool frontFirst)
        {
            var expected = new GeometryPool { Layout = source.m_ParentBatch.Geometry.Layout };
            var expectedMesh = new Mesh();
            var root = new GameObject("ColdBatchRemovalParity canvas");
            root.transform.SetParent(App.Scene.transform, false);
            var canvas = root.AddComponent<CanvasScript>();
            try
            {
                expected.Append(source.m_ParentBatch.Geometry, source.m_StartVertIndex, source.m_VertLength,
                    source.m_iTriIndex, source.m_nTriIndex);
                expected.CopyToMesh(expectedMesh);
                var first = canvas.BatchManager.CreateSubset(source.m_ParentBatch.Brush, expected);
                var batch = first.m_ParentBatch;
                var second = batch.AddSubset(expected);
                batch.m_EditorDebug = true;
                batch.FlushMeshUpdates();
                if (cold) batch.ClearCachedGeometry();
                if (cold && batch.Geometry.IsGeometryResident) throw new Exception("ClearCachedGeometry did not evict vertices");
                var removed = frontFirst ? first : second;
                var remaining = frontFirst ? second : first;
                batch.RemoveSubset(removed);
                if (removed.m_ParentBatch != null || batch.m_Groups.Count != 1 || !remaining.m_Active)
                    throw new Exception("First removal changed subset ownership or active state");
                if (batch.Geometry.NumVerts != expected.NumVerts * (frontFirst ? 2 : 1) ||
                    batch.Geometry.NumTriIndices != expected.NumTriIndices * (frontFirst ? 2 : 1))
                    throw new Exception("Non-empty removal changed geometry lengths");
                batch.FlushMeshUpdates();
                if (cold && (batch.Geometry.IsGeometryResident || VertexCapacity(batch.Geometry) != 0))
                    throw new Exception($"Partial cold removal restored vertex channels: {source.m_ParentBatch.Brush.name}, frontFirst={frontFirst}");
                if (!frontFirst) EqualMesh(expectedMesh, batch.GetComponent<MeshFilter>().sharedMesh, allowUnusedTail: cold);
                if (cold) batch.ClearCachedGeometry();
                batch.RemoveSubset(remaining);
                if (remaining.m_ParentBatch != null || batch.m_Groups.Count != 0 ||
                    !batch.Geometry.IsGeometryResident || batch.Geometry.NumVerts != 0 ||
                    batch.Geometry.NumTriIndices != 0 || !batch.Geometry.Layout.Equals(expected.Layout))
                    throw new Exception("Empty batch state or layout differs");
                if (cold && VertexCapacity(batch.Geometry) != 0)
                    throw new Exception("Removing the final cold subset restored discarded vertex arrays");
                batch.FlushMeshUpdates();
                var mesh = batch.GetComponent<MeshFilter>().sharedMesh;
                if (mesh.vertexCount != 0 || mesh.triangles.Length != 0)
                    throw new Exception("Empty batch still contains uploaded geometry");
                var reused = batch.AddSubset(expected);
                batch.FlushMeshUpdates();
                if (reused.m_StartVertIndex != 0 || reused.m_iTriIndex != 0 || !reused.m_Active)
                    throw new Exception("Reused batch has stale subset offsets or visibility");
                EqualMesh(expectedMesh, mesh);
                batch.DisableSubset(reused); batch.FlushMeshUpdates();
                batch.EnableSubset(reused); batch.FlushMeshUpdates();
                EqualMesh(expectedMesh, mesh);
            }
            finally { expected.Destroy(); Object.DestroyImmediate(expectedMesh); canvas.BatchManager.ResetPools(); Object.DestroyImmediate(root); }
        }

        static long VertexCapacity(GeometryPool geometry)
        {
            long total = geometry.m_Vertices?.Capacity ?? 0;
            total += geometry.m_Normals?.Capacity ?? 0;
            total += geometry.m_Tangents?.Capacity ?? 0;
            total += geometry.m_Colors?.Capacity ?? 0;
            for (int channel = 0; channel < 3; channel++)
            {
                // GetTexcoordData restores cold geometry; inspect the cached fields directly.
                var uv = channel == 0 ? geometry.m_Texcoord0 : channel == 1 ? geometry.m_Texcoord1 : geometry.m_Texcoord2;
                total += (uv.v2?.Capacity ?? 0) + (uv.v3?.Capacity ?? 0) + (uv.v4?.Capacity ?? 0);
            }
            return total;
        }

        static bool EqualChannel<T>(T[] expected, T[] actual, int unusedVertices)
        {
            return (actual.Length == expected.Length ||
                (expected.Length > 0 && actual.Length == expected.Length + unusedVertices)) &&
                expected.SequenceEqual(actual.Take(expected.Length));
        }

        static void EqualMesh(Mesh expected, Mesh actual, bool allowUnusedTail = false)
        {
            int unused = allowUnusedTail ? actual.vertexCount - expected.vertexCount : 0;
            if (unused < 0 || !EqualChannel(expected.vertices, actual.vertices, unused) || !EqualChannel(expected.normals, actual.normals, unused) ||
                !EqualChannel(expected.tangents, actual.tangents, unused) || !EqualChannel(expected.colors32, actual.colors32, unused) ||
                !expected.triangles.SequenceEqual(actual.triangles))
                throw new Exception("Reused/truncated mesh differs from expected geometry");
            for (int channel = 0; channel < 3; channel++)
            {
                var left = new List<Vector4>(); var right = new List<Vector4>();
                expected.GetUVs(channel, left); actual.GetUVs(channel, right);
                if (!EqualChannel(left.ToArray(), right.ToArray(), unused)) throw new Exception($"Reused/truncated UV{channel} differs");
            }
        }
    }
}
