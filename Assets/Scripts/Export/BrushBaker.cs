using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        if (mesh == null) return null;

        var match = computeShaders.FirstOrDefault(x => x.brushGuid == brushGuid);
        if (match.computeShader == null)
        {
            Debug.LogWarning($"No compute shader mapping found for brushGuid {brushGuid}");
            return mesh;
        }
        var mapping = match;
        var computeShader = mapping.computeShader;

        int vertexCount = mesh.vertexCount;
        if (vertexCount == 0) return mesh;

        var verticesArray = mesh.vertices;
        if (verticesArray == null || verticesArray.Length != vertexCount) return mesh;

        var normalsArray = mesh.normals;
        if (normalsArray == null || normalsArray.Length != vertexCount) return mesh;

        var colors = new List<Color>();
        mesh.GetColors(colors);
        if (colors.Count != vertexCount) return mesh;

        var uvs = new List<Vector3>();
        mesh.GetUVs(0, uvs);
        if (uvs.Count != vertexCount) return mesh;

        computeShader.SetMatrix("TransformObjectToWorld", transform.localToWorldMatrix);
        computeShader.SetMatrix("TransformWorldToObject", transform.worldToLocalMatrix);

        ComputeBuffer vertexBuffer = null;
        ComputeBuffer normalBuffer = null;
        ComputeBuffer colorBuffer = null;
        ComputeBuffer uvBuffer = null;
        ComputeBuffer uv1Buffer = null;

        try
        {
            vertexBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
            vertexBuffer.SetData(verticesArray);

            normalBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
            normalBuffer.SetData(normalsArray);

            colorBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 4);
            colorBuffer.SetData(colors);

            uvBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 3);
            uvBuffer.SetData(uvs);

            computeShader.SetBuffer(0, "vertexBuffer", vertexBuffer);
            computeShader.SetBuffer(0, "normalBuffer", normalBuffer);
            computeShader.SetBuffer(0, "colorBuffer", colorBuffer);
            computeShader.SetBuffer(0, "uvBuffer", uvBuffer);
            computeShader.SetFloat("_SqueezeAmount", squeezeAmount);
            computeShader.SetInt("_VertexCount", vertexCount);

            if (mapping.ModifyUv1)
            {
                var uv1s = new List<Vector4>();
                mesh.GetUVs(1, uv1s);
                if (uv1s.Count != vertexCount)
                    uv1s = new List<Vector4>(new Vector4[vertexCount]);
                uv1Buffer = new ComputeBuffer(vertexCount, sizeof(float) * 4);
                uv1Buffer.SetData(uv1s);
                computeShader.SetBuffer(0, "uv1Buffer", uv1Buffer);
            }

            int threadGroups = Mathf.CeilToInt(vertexCount / 8f);
            computeShader.Dispatch(0, threadGroups, 1, 1);

            var newVerts = new Vector3[vertexCount];
            vertexBuffer.GetData(newVerts);
            mesh.vertices = newVerts;

            if (mapping.ModifyColor)
            {
                var newColors = new Color[vertexCount];
                colorBuffer.GetData(newColors);
                mesh.colors = newColors;
            }

            if (mapping.ModifyNormal)
            {
                var newNormals = new Vector3[vertexCount];
                normalBuffer.GetData(newNormals);
                mesh.normals = newNormals;
            }

            if (mapping.ModifyUv0)
            {
                var newUvs = new Vector3[vertexCount];
                uvBuffer.GetData(newUvs);
                mesh.SetUVs(0, newUvs);
            }

            if (mapping.ModifyUv1)
            {
                var newUv1s = new Vector4[vertexCount];
                uv1Buffer.GetData(newUv1s);
                mesh.SetUVs(1, newUv1s);
            }
        }
        finally
        {
            uv1Buffer?.Release();
            uvBuffer?.Release();
            colorBuffer?.Release();
            normalBuffer?.Release();
            vertexBuffer?.Release();
        }

        return mesh;
    }
}
