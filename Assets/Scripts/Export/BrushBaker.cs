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
        public string name;
        public Shader shader;
        public string brushGuid;
        public ComputeShader computeShader;
        public bool ModifyColor;
        public bool ModifyNormal;
        public bool ModifyUv0;
        public bool ModifyUv1;
    }

    void Start()
    {
        m_Instance = this;
    }

    public Mesh ProcessMesh(Mesh mesh, string brushGuid)
    {
        ComputeShaderMapping mapping;
        ComputeShader computeShader;
        if (mesh == null) return null;

        try
        {
            mapping = computeShaders.First(x => x.brushGuid == brushGuid);
            computeShader = mapping.computeShader;
        }
        catch (InvalidOperationException e)
        {
            Debug.LogWarning($"No mapping found for brushGuid {brushGuid}: {e.Message}");
            return mesh;
        }

        int vertexCount = mesh.vertexCount;
        if (computeShader == null) return mesh;
        if (vertexCount == 0) return mesh;

        var verticesArray = mesh.vertices;
        if (verticesArray == null || verticesArray.Length != vertexCount) return mesh;

        var normalsArray = mesh.normals;
        if (normalsArray == null || normalsArray.Length != vertexCount) return mesh;

        List<Color> colors = new List<Color>();
        mesh.GetColors(colors);
        if (colors.Count != vertexCount) return mesh;

        List<Vector3> uvs = new List<Vector3>();
        mesh.GetUVs(0, uvs);
        if (uvs.Count != vertexCount) return mesh;

        // Get the transformation matrix from the GameObject's transform
        Matrix4x4 localToWorldtransformMatrix = transform.localToWorldMatrix;
        Matrix4x4 worldToLocaltransformMatrix = transform.worldToLocalMatrix;

        // Set the matrix as a shader parameter
        computeShader.SetMatrix("TransformObjectToWorld", localToWorldtransformMatrix);
        computeShader.SetMatrix("TransformWorldToObject", worldToLocaltransformMatrix);

        NativeArray<Vector3> vertices = new NativeArray<Vector3>(verticesArray, Allocator.TempJob);
        ComputeBuffer vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        vertexBuffer.SetData(vertices);

        NativeArray<Vector3> normals = new NativeArray<Vector3>(normalsArray, Allocator.TempJob);
        ComputeBuffer normalBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3);
        normalBuffer.SetData(normals);

        ComputeBuffer colorBuffer = new ComputeBuffer(colors.Count, sizeof(float) * 4);
        colorBuffer.SetData(colors);

        ComputeBuffer uvBuffer = new ComputeBuffer(uvs.Count, sizeof(float) * 3);
        uvBuffer.SetData(uvs);

        computeShader.SetBuffer(0, "vertexBuffer", vertexBuffer);
        computeShader.SetBuffer(0, "normalBuffer", normalBuffer);
        computeShader.SetBuffer(0, "colorBuffer", colorBuffer);
        computeShader.SetBuffer(0, "uvBuffer", uvBuffer);
        computeShader.SetFloat("_SqueezeAmount", squeezeAmount);
        computeShader.SetInt("_VertexCount", vertexCount);

        ComputeBuffer uv1Buffer = null;
        bool needsUv1Buffer = mapping.ModifyUv1 || mesh.uv2.Length > 0;
        if (needsUv1Buffer)
        {
            List<Vector4> uv1s = new List<Vector4>();
            mesh.GetUVs(1, uv1s);
            if (uv1s.Count != mesh.vertexCount)
            {
                uv1s = Enumerable.Repeat(Vector4.zero, mesh.vertexCount).ToList();
            }
            uv1Buffer = new ComputeBuffer(uv1s.Count, sizeof(float) * 4);
            uv1Buffer.SetData(uv1s);
            computeShader.SetBuffer(0, "uv1Buffer", uv1Buffer);
        }

        int threadGroups = Mathf.CeilToInt(vertexCount / 8f);
        computeShader.Dispatch(0, threadGroups, 1, 1);

        var newVerts = new Vector3[mesh.vertexCount];
        vertexBuffer.GetData(newVerts);
        mesh.vertices = newVerts;

        if (mapping.ModifyColor)
        {
            var newColors = new Color[mesh.vertexCount];
            colorBuffer.GetData(newColors);
            mesh.colors = newColors;
        }

        if (mapping.ModifyNormal)
        {
            var newNormals = new Vector3[mesh.vertexCount];
            normalBuffer.GetData(newNormals);
            mesh.normals = newNormals;
        }

        if (mapping.ModifyUv0)
        {
            var newUvs = new Vector3[mesh.vertexCount];
            uvBuffer.GetData(newUvs);
            mesh.SetUVs(0, newUvs);
        }

        if (mapping.ModifyUv1 && uv1Buffer != null)
        {
            var newUv1s = new Vector4[mesh.vertexCount];
            uv1Buffer.GetData(newUv1s);
            mesh.SetUVs(1, newUv1s);
        }

        vertexBuffer.Release();
        normalBuffer.Release();
        uvBuffer.Release();
        if (uv1Buffer != null)
        {
            uv1Buffer.Release();
        }
        vertices.Dispose();
        normals.Dispose();

        return mesh;
    }
}
