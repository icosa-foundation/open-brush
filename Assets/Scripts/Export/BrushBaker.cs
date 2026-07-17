using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

public class BrushBaker : MonoBehaviour
{
    private static readonly bool kDropUnusedWideUvComponentsForGltf = false;

    public List<ComputeShaderMapping> computeShaders;
    public List<TextureBakePolicyMapping> textureBakePolicies;
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

    public enum TextureBakeMode
    {
        None,
        UvBaseColor,
        UvUnlit,
        UvEmission,
        Unsupported,
    }

    [Serializable]
    public struct TextureBakePolicyMapping
    {
        public string name;
        public string brushGuid;
        public TextureBakeMode Mode;
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

    public bool TryGetTextureBakeMode(string brushGuid, out TextureBakeMode mode)
    {
        mode = TextureBakeMode.None;
        if (textureBakePolicies == null) return false;
        foreach (TextureBakePolicyMapping candidate in textureBakePolicies)
        {
            if (string.Equals(candidate.brushGuid, brushGuid, StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate.Mode;
                return true;
            }
        }
        return false;
    }

    public Mesh ProcessMesh(Mesh mesh, string brushGuid)
    {
        ComputeShaderMapping mapping;
        if (!TryGetMapping(brushGuid, out mapping))
        {
            Debug.LogWarning($"No mapping found for brushGuid {brushGuid}");
            return mesh;
        }

        ComputeShader computeShader = mapping.computeShader;
        if (computeShader == null) return mesh;

        // Get the transformation matrix from the GameObject's transform
        Matrix4x4 localToWorldtransformMatrix = transform.localToWorldMatrix;
        Matrix4x4 worldToLocaltransformMatrix = transform.worldToLocalMatrix;

        // Set the matrix as a shader parameter
        computeShader.SetMatrix("TransformObjectToWorld", localToWorldtransformMatrix);
        computeShader.SetMatrix("TransformWorldToObject", worldToLocaltransformMatrix);

        NativeArray<Vector3> vertices = new NativeArray<Vector3>(mesh.vertices, Allocator.TempJob);
        ComputeBuffer vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
        vertexBuffer.SetData(vertices);

        NativeArray<Vector3> normals = new NativeArray<Vector3>(mesh.normals, Allocator.TempJob);
        ComputeBuffer normalBuffer = new ComputeBuffer(normals.Length, sizeof(float) * 3);
        normalBuffer.SetData(normals);

        // get color buffer as well
        List<Color> colors = new List<Color>();
        mesh.GetColors(colors);
        ComputeBuffer colorBuffer = new ComputeBuffer(colors.Count, sizeof(float) * 4);
        colorBuffer.SetData(colors);

        List<Vector3> uvs = new List<Vector3>();
        mesh.GetUVs(0, uvs);
        ComputeBuffer uvBuffer = new ComputeBuffer(uvs.Count, sizeof(float) * 3);
        uvBuffer.SetData(uvs);

        computeShader.SetBuffer(0, "vertexBuffer", vertexBuffer);
        computeShader.SetBuffer(0, "normalBuffer", normalBuffer);
        computeShader.SetBuffer(0, "colorBuffer", colorBuffer);
        computeShader.SetBuffer(0, "uvBuffer", uvBuffer);
        computeShader.SetFloat("_SqueezeAmount", squeezeAmount);
        computeShader.SetInt("_VertexCount", mesh.vertexCount);

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

        ComputeBuffer uv2Buffer = null;
        bool needsUv2Buffer = mapping.ModifyUv2 || mesh.uv3.Length > 0;
        if (needsUv2Buffer)
        {
            List<Vector2> uv2s = new List<Vector2>();
            mesh.GetUVs(2, uv2s);
            if (uv2s.Count != mesh.vertexCount)
            {
                uv2s = Enumerable.Repeat(Vector2.zero, mesh.vertexCount).ToList();
            }
            uv2Buffer = new ComputeBuffer(uv2s.Count, sizeof(float) * 2);
            uv2Buffer.SetData(uv2s);
            computeShader.SetBuffer(0, "uv2Buffer", uv2Buffer);
        }

        int threadGroups = Mathf.CeilToInt(mesh.vertices.Length / 8f);
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

        if (mapping.ModifyUv2 && uv2Buffer != null)
        {
            var newUv2s = new Vector2[mesh.vertexCount];
            uv2Buffer.GetData(newUv2s);
            mesh.SetUVs(2, newUv2s);
        }

        if (kDropUnusedWideUvComponentsForGltf)
        {
            DropWideUvComponents(mesh);
        }

        vertexBuffer.Release();
        normalBuffer.Release();
        colorBuffer.Release();
        uvBuffer.Release();
        if (uv1Buffer != null)
        {
            uv1Buffer.Release();
        }
        if (uv2Buffer != null)
        {
            uv2Buffer.Release();
        }
        vertices.Dispose();
        normals.Dispose();

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
