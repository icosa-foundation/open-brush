// Run in an initialized Main-scene Play session after timing has finished:
// unity command run_script --file Support/Benchmarks/ColdBatchAppendParity.cs --entry TiltBrush.Benchmarks.ColdBatchAppendParity.Run --args '["Logs/BrushMeshBenchmarks/cold-append-parity.json"]' --project-path $PWD --json
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using VertexLayout = TiltBrush.GeometryPool.VertexLayout;
using TexcoordInfo = TiltBrush.GeometryPool.TexcoordInfo;

namespace TiltBrush.Benchmarks
{
    public static class ColdBatchAppendParity
    {
        // Call this entry first, then wait for the app to finish loading before Run.
        public static string LoadFixture()
        {
            if (!Application.isPlaying || UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false))
                throw new InvalidOperationException("Use an idle initialized Play session");
            var info = new DiskSceneFileInfo(Path.GetFullPath("Support/Sketches/Promoted/TiltMech.tilt"));
            info.ReadMetadata(); SketchControlsScript.m_Instance.LoadSketch(info, true);
            return "[ColdBatchAppendParity] Loading TiltMech fixture";
        }

        public static string Run(string output)
        {
            if (!Application.isPlaying || App.CurrentState != App.AppState.Standard ||
                SketchMemoryScript.m_Instance.IsPlayingBack || SketchMemoryScript.AllStrokesCount() == 0 ||
                UnityEditor.SessionState.GetBool("BatchWorkloadRunning", false))
                throw new InvalidOperationException("Use an idle initialized Play session");
            var passed = new List<string>();
            foreach (int uvSize in new[] { 0, 2, 3, 4 })
                foreach (bool wideIndices in new[] { false, true })
                    foreach (bool cachedIndices in new[] { false, true })
                        foreach (bool transformed in new[] { false, true })
                        {
                            var layout = new VertexLayout
                            {
                                bUseNormals = true, bUseColors = true, bUseTangents = true,
                                texcoord0 = new TexcoordInfo { size = uvSize },
                                texcoord1 = new TexcoordInfo { size = uvSize },
                                texcoord2 = new TexcoordInfo { size = uvSize }
                            };
                            var source = new GeometryPool { Layout = layout };
                            var mesh = new Mesh { indexFormat = wideIndices ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                            try
                            {
                                source.NumVerts = 16;
                                for (int i = 0; i < 16; i++)
                                {
                                    source.m_Vertices[i] = new Vector3(i * 0.1f, Mathf.Sin(i), -i);
                                    source.m_Normals[i] = new Vector3(0.5f, 0.25f, 0.75f);
                                    source.m_Tangents[i] = new Vector4(0.5f, 0.25f, 0.75f, -1);
                                    source.m_Colors[i] = new Color32((byte)(i * 13), (byte)(255 - i), (byte)(i * 7), (byte)(i * 15));
                                    for (int channel = 0; channel < 3; channel++)
                                    {
                                        var uv = source.GetTexcoordData(channel);
                                        if (uvSize == 2) uv.v2[i] = new Vector2(i, channel);
                                        if (uvSize == 3) uv.v3[i] = new Vector3(i, channel, 0.25f);
                                        if (uvSize == 4) uv.v4[i] = new Vector4(i, channel, 0.25f, 0.75f);
                                    }
                                }
                                source.m_Tris.AddRange(new[] { 0, 1, 2, 4, 5, 6, 4, 6, 7, 12, 13, 14 });
                                source.CopyToMesh(mesh);
                                if (cachedIndices) source.m_Tris[3] = 7; // Newer than the mesh snapshot.
                                Compare(source, mesh, 4, 8, 3, 6, transformed, cachedIndices);
                                passed.Add($"uv{uvSize}/index{(wideIndices ? 32 : 16)}/cached{cachedIndices}/transform{transformed}");
                            }
                            finally { source.Destroy(); Object.DestroyImmediate(mesh); }
                        }
            // An unsupported raw color format must retain the existing converted reload path.
            var floatMesh = new Mesh();
            var floatSource = new GeometryPool { Layout = new VertexLayout { bUseColors = true } };
            var floatExpected = new GeometryPool { Layout = floatSource.Layout };
            var floatActual = new GeometryPool { Layout = floatSource.Layout };
            try
            {
                floatMesh.vertices = new[] { Vector3.zero, Vector3.up, Vector3.right };
                floatMesh.colors = new[] { Color.red, Color.green, Color.blue };
                floatMesh.triangles = new[] { 0, 1, 2 };
                floatSource.NumVerts = 3; floatSource.m_Tris.AddRange(new[] { 0, 1, 2 });
                floatSource.MakeGeometryNotResident(floatMesh); floatSource.EnsureGeometryResident();
                floatExpected.Append(floatSource, 0, 3, 0, 3);
                floatSource.MakeGeometryNotResident(floatMesh);
                floatActual.Append(floatSource, 0, 3, 0, 3);
                if (!floatSource.IsGeometryResident) throw new Exception("Unsupported format did not use fallback");
                Equal(floatExpected.m_Vertices, floatActual.m_Vertices, "fallback positions");
                Equal(floatExpected.m_Colors, floatActual.m_Colors, "fallback colors");
                Equal(floatExpected.m_Tris, floatActual.m_Tris, "fallback indices");
                passed.Add("float-color-fallback");
            }
            finally { floatSource.Destroy(); floatExpected.Destroy(); floatActual.Destroy(); Object.DestroyImmediate(floatMesh); }

            // Exercise actual loaded brush layouts as well as synthetic attribute/index cases.
            foreach (var stroke in SketchMemoryScript.AllStrokes()
                .Where(s => s.m_Type == Stroke.Type.BatchedBrushStroke)
                .GroupBy(s => s.m_BrushGuid).Select(g => g.First()))
            {
                var subset = stroke.m_BatchSubset;
                var batch = subset.m_ParentBatch;
                var mesh = batch.GetComponent<MeshFilter>().sharedMesh;
                batch.Geometry.EnsureGeometryResident();
                Compare(batch.Geometry, mesh, subset.m_StartVertIndex, subset.m_VertLength,
                    subset.m_iTriIndex, subset.m_nTriIndex, true, false);
                passed.Add($"fixture/{stroke.m_BrushGuid}");
                CheckBatchBounds(subset);
                passed.Add($"bounds/{stroke.m_BrushGuid}");
            }
            File.WriteAllText(output, UnityEngine.JsonUtility.ToJson(new Result { passed = passed.ToArray() }, true));
            return $"[ColdBatchAppendParity] Passed {passed.Count} complete geometry comparisons";
        }

        [Serializable]
        public class Result { public string status = "passed"; public string[] passed; }

        static void CheckBatchBounds(BatchSubset source)
        {
            var batch = source.m_ParentBatch;
            var geometry = batch.Geometry;
            var expected = new GeometryPool { Layout = geometry.Layout };
            var root = new GameObject("ColdBatchAppendParity canvas");
            root.transform.SetParent(App.Scene.transform, false);
            var canvas = root.AddComponent<CanvasScript>();
            try
            {
                var transform = TrTransform.TRS(new Vector3(0.2f, -0.4f, 0.3f), Quaternion.Euler(10, 20, 30), 1.5f);
                geometry.EnsureGeometryResident();
                expected.Append(geometry, source.m_StartVertIndex, source.m_VertLength,
                    source.m_iTriIndex, source.m_nTriIndex, transform);
                geometry.MakeGeometryNotResident(batch.GetComponent<MeshFilter>().sharedMesh);
                var result = canvas.BatchManager.CreateSubset(source, transform);
                var bounds = new Bounds(expected.m_Vertices[0], Vector3.zero);
                foreach (var vertex in expected.m_Vertices) bounds.Encapsulate(vertex);
                if ((result.m_Bounds.min - bounds.min).sqrMagnitude > 1e-8f ||
                    (result.m_Bounds.max - bounds.max).sqrMagnitude > 1e-8f)
                    throw new Exception("Transferred batch bounds differ from transformed vertices");
                if (geometry.IsGeometryResident) throw new Exception("Bounds calculation restored cold source vertices");
            }
            finally { expected.Destroy(); canvas.BatchManager.ResetPools(); Object.DestroyImmediate(root); }
        }

        static void Compare(GeometryPool source, Mesh mesh, int vertexStart, int vertexCount,
            int indexStart, int indexCount, bool transformed, bool cachedIndices)
        {
            var expected = new GeometryPool { Layout = source.Layout };
            var actual = new GeometryPool { Layout = source.Layout };
            try
            {
                // Nonzero destination offsets must be included when rebasing the index buffer.
                expected.NumVerts = actual.NumVerts = 3;
                expected.m_Tris.AddRange(new[] { 0, 1, 2 }); actual.m_Tris.AddRange(new[] { 0, 1, 2 });
                TrTransform? transform = transformed
                    ? TrTransform.TRS(new Vector3(0.2f, -0.4f, 0.3f), Quaternion.Euler(10, 20, 30), 1.5f)
                    : (TrTransform?)null;
                expected.Append(source, vertexStart, vertexCount, indexStart, indexCount, transform);
                int[] newestIndices = cachedIndices ? source.m_Tris.ToArray() : null;
                source.MakeGeometryNotResident(mesh);
                if (cachedIndices)
                {
                    source.EnsureIndicesResident(); source.m_Tris.Clear(); source.m_Tris.AddRange(newestIndices);
                }
                actual.Append(source, vertexStart, vertexCount, indexStart, indexCount, transform);
                if (source.IsGeometryResident || source.m_Vertices != null)
                    throw new Exception("Cold append reloaded source vertex cache");
                Equal(expected.m_Vertices, actual.m_Vertices, "positions");
                Equal(expected.m_Normals, actual.m_Normals, "normals");
                Equal(expected.m_Tangents, actual.m_Tangents, "tangents");
                Equal(expected.m_Colors, actual.m_Colors, "colors");
                Equal(expected.m_Tris, actual.m_Tris, "indices");
                for (int channel = 0; channel < 3; channel++)
                {
                    var e = expected.GetTexcoordData(channel); var a = actual.GetTexcoordData(channel);
                    Equal(e.v2, a.v2, $"uv{channel}/2"); Equal(e.v3, a.v3, $"uv{channel}/3"); Equal(e.v4, a.v4, $"uv{channel}/4");
                }
            }
            finally { expected.Destroy(); actual.Destroy(); }
        }

        static void Equal<T>(List<T> expected, List<T> actual, string channel)
        {
            if ((expected == null) != (actual == null) || (expected != null && !expected.SequenceEqual(actual)))
                throw new Exception($"Cold append differs from resident append: {channel}");
        }
    }
}
