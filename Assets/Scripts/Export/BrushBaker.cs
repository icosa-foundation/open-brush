using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class BrushBaker : MonoBehaviour
{
    private static readonly bool kDropUnusedWideUvComponentsForGltf = false;

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

    public bool TryGetMapping(string brushGuid, out ComputeShaderMapping mapping)
    {
        mapping = default;
        if (computeShaders == null) return false;
        foreach (ComputeShaderMapping candidate in computeShaders)
        {
            if (string.Equals(candidate.brushGuid, brushGuid, StringComparison.OrdinalIgnoreCase))
            {
                mapping = candidate;
                return true;
            }
        }
        return false;
    }

    public IEnumerable<ComputeShaderMapping> Mappings
    {
        get { return computeShaders ?? Enumerable.Empty<ComputeShaderMapping>(); }
    }

    public Mesh ProcessMesh(Mesh mesh, string brushGuid)
    {
        if (mesh == null) return null;

        ComputeShaderMapping mapping;
        if (!TryGetMapping(brushGuid, out mapping))
        {
            Debug.LogWarning($"No mapping found for brushGuid {brushGuid}");
            return mesh;
        }

        ComputeShader computeShader = mapping.computeShader;
        if (computeShader == null) return mesh;

        int vertexCount = mesh.vertexCount;
        if (vertexCount == 0) return mesh;

        var verticesArray = mesh.vertices;
        if (verticesArray == null || verticesArray.Length != vertexCount) return mesh;

        var normalsArray = mesh.normals;
        if (normalsArray == null || normalsArray.Length != vertexCount) return mesh;

        var colors = new List<Color>();
        mesh.GetColors(colors);
        if (colors.Count == 0)
        {
            colors = Enumerable.Repeat(Color.white, vertexCount).ToList();
        }
        else if (colors.Count != vertexCount)
        {
            return mesh;
        }

        var uvs = new List<Vector3>();
        mesh.GetUVs(0, uvs);
        if (uvs.Count == 0)
        {
            uvs = Enumerable.Repeat(Vector3.zero, vertexCount).ToList();
        }
        else if (uvs.Count != vertexCount)
        {
            return mesh;
        }

        ComputeBuffer vertexBuffer = null;
        ComputeBuffer normalBuffer = null;
        ComputeBuffer colorBuffer = null;
        ComputeBuffer uvBuffer = null;
        ComputeBuffer uv1Buffer = null;
        ComputeBuffer uv2Buffer = null;

        try
        {
            computeShader.SetMatrix("TransformObjectToWorld", transform.localToWorldMatrix);
            computeShader.SetMatrix("TransformWorldToObject", transform.worldToLocalMatrix);

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
                {
                    uv1s = Enumerable.Repeat(Vector4.zero, vertexCount).ToList();
                }
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
                {
                    uv2s = Enumerable.Repeat(Vector2.zero, vertexCount).ToList();
                }
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

        if (kDropUnusedWideUvComponentsForGltf)
        {
            DropWideUvComponents(mesh);
        }

        return mesh;
    }

    private static void DropWideUvComponents(Mesh mesh)
    {
        // Disabled by default. UnityGLTF already exports uv0/uv1 through Vector2[] accessors,
        // so this full mesh rewrite is only worth enabling for a proven uv2+ wide-texcoord export issue.
        for (int channel = 0; channel < 8; channel++)
        {
            var source = new List<Vector4>();
            mesh.GetUVs(channel, source);
            if (source.Count == 0) continue;

            var truncated = new List<Vector2>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                Vector4 uv = source[i];
                truncated.Add(new Vector2(uv.x, uv.y));
            }
            mesh.SetUVs(channel, truncated);
        }
    }
}
