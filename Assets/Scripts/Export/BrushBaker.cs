using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

public class BrushBaker : MonoBehaviour
{
    public List<ComputeShaderMapping> computeShaders;
    public float squeezeAmount = 1.0f; // Set this to your desired squeeze amount
    public static BrushBaker m_Instance;

    [Serializable]
    public struct ComputeShaderMapping
    {
        public string brushGuid;
        public ComputeShader computeShader;
    }

    void Start()
    {
        m_Instance = this;
    }

    public Mesh ProcessMesh(Mesh mesh, string brushGuid)
    {
        ComputeShader computeShader;
        try
        {
            computeShader = computeShaders.First(x => x.brushGuid == brushGuid).computeShader;
        }
        catch (InvalidOperationException e)
        {
            return mesh;
        }
        if (computeShader == null) return mesh;

        // Get the transformation matrix from the GameObject's transform
        Matrix4x4 transformMatrix = transform.localToWorldMatrix;

        // Set the matrix as a shader parameter
        computeShader.SetMatrix("TransformObjectToWorld", transformMatrix);

        NativeArray<Vector3> vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
        ComputeBuffer vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        vertexBuffer.SetData(vertices);

        NativeArray<Vector3> normals = new NativeArray<Vector3>(mesh.normals, Allocator.TempJob);
        ComputeBuffer normalBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3);
        normalBuffer.SetData(normals);

        List<Vector3> uvs = new List<Vector3>();
        mesh.GetUVs(0, uvs);
        ComputeBuffer uvBuffer = new ComputeBuffer(uvs.Count, sizeof(float) * 3);
        uvBuffer.SetData(uvs);

        // if we need texcoord1
        if (mesh.uv2.Length > 0)
        {
            List<Vector3> uv1s = new List<Vector3>();
            mesh.GetUVs(1, uv1s);
            ComputeBuffer uv1Buffer = new ComputeBuffer(uv1s.Count, sizeof(float) * 3);
            uv1Buffer.SetData(uv1s);
            computeShader.SetBuffer(0, "uv1Buffer", uv1Buffer);
        }

        computeShader.SetBuffer(0, "vertexBuffer", vertexBuffer);
        computeShader.SetBuffer(0, "normalBuffer", normalBuffer);
        computeShader.SetBuffer(0, "uvBuffer", uvBuffer);
        computeShader.SetFloat("_SqueezeAmount", squeezeAmount);

        int threadGroups = Mathf.CeilToInt(mesh.vertices.Length / 8f);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        var newVerts = new Vector3[mesh.vertexCount];
        vertexBuffer.GetData(newVerts);
        mesh.vertices = newVerts;

        vertexBuffer.Release();
        normalBuffer.Release();
        uvBuffer.Release();

        vertices.Dispose();
        normals.Dispose();

        return mesh;
    }
}
