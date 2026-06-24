using System;
using UnityEngine;
using System.Collections.Generic;

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
        public bool ModifyUv2;
    }

    void Start()
    {
        m_Instance = this;
    }

    private bool TryGetComputeShaderMapping(string brushGuid, out ComputeShaderMapping mapping)
    {
        if (computeShaders != null)
        {
            for (int i = 0; i < computeShaders.Count; i++)
            {
                if (computeShaders[i].brushGuid == brushGuid)
                {
                    mapping = computeShaders[i];
                    return true;
                }
            }
        }

        mapping = default;
        return false;
    }

    public Mesh ProcessMesh(Mesh mesh, string brushGuid)
    {
        if (mesh == null) return null;

        if (!TryGetComputeShaderMapping(brushGuid, out var mapping))
        {
            return mesh;
        }

        var computeShader = mapping.computeShader;
        if (computeShader == null)
        {
            Debug.LogWarning($"Mapping for brushGuid {brushGuid} has no compute shader assigned");
            return mesh;
        }

        int vertexCount = mesh.vertexCount;
        if (vertexCount == 0) return mesh;

        var verticesArray = mesh.vertices;
        if (verticesArray == null || verticesArray.Length == 0) return mesh;

        var normalsArray = mesh.normals;
        if (normalsArray == null || normalsArray.Length == 0) return mesh;

        var colors = new List<Color>();
        mesh.GetColors(colors);
        if (colors.Count == 0) return mesh;

        var uvs = new List<Vector3>();
        mesh.GetUVs(0, uvs);
        if (uvs.Count == 0) return mesh;

        computeShader.SetMatrix("TransformObjectToWorld", transform.localToWorldMatrix);
        computeShader.SetMatrix("TransformWorldToObject", transform.worldToLocalMatrix);

        ComputeBuffer vertexBuffer = null;
        ComputeBuffer normalBuffer = null;
        ComputeBuffer colorBuffer = null;
        ComputeBuffer uvBuffer = null;
        ComputeBuffer uv1Buffer = null;
        ComputeBuffer uv2Buffer = null;

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

            bool needsUv1Buffer = mapping.ModifyUv1 || mesh.uv2.Length > 0;
            if (needsUv1Buffer)
            {
                var uv1s = new List<Vector4>();
                mesh.GetUVs(1, uv1s);
                if (uv1s.Count != vertexCount)
                    uv1s = new List<Vector4>(new Vector4[vertexCount]);
                uv1Buffer = new ComputeBuffer(vertexCount, sizeof(float) * 4);
                uv1Buffer.SetData(uv1s);
                computeShader.SetBuffer(0, "uv1Buffer", uv1Buffer);
            }

            bool needsUv2Buffer = mapping.ModifyUv2 || mesh.uv3.Length > 0;
            if (needsUv2Buffer)
            {
                var uv2s = new List<Vector2>();
                mesh.GetUVs(2, uv2s);
                if (uv2s.Count != vertexCount)
                    uv2s = new List<Vector2>(new Vector2[vertexCount]);
                uv2Buffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);
                uv2Buffer.SetData(uv2s);
                computeShader.SetBuffer(0, "uv2Buffer", uv2Buffer);
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

            if (mapping.ModifyUv1 && uv1Buffer != null)
            {
                var newUv1s = new Vector4[vertexCount];
                uv1Buffer.GetData(newUv1s);
                mesh.SetUVs(1, newUv1s);
            }

            if (mapping.ModifyUv2 && uv2Buffer != null)
            {
                var newUv2s = new Vector2[vertexCount];
                uv2Buffer.GetData(newUv2s);
                mesh.SetUVs(2, newUv2s);
            }
        }
        finally
        {
            uv2Buffer?.Release();
            uv1Buffer?.Release();
            uvBuffer?.Release();
            colorBuffer?.Release();
            normalBuffer?.Release();
            vertexBuffer?.Release();
        }

        return mesh;
    }
}
