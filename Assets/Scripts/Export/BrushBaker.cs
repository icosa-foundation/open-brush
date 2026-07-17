using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;

public class BrushBaker : MonoBehaviour
{
    private static readonly bool kDropUnusedWideUvComponentsForGltf = false;

    public List<ComputeShaderMapping> computeShaders;
    public List<ComputeShaderMapping> staticComputeShaders;
    public List<StaticMeshPolicyMapping> staticMeshPolicies;
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

    public enum StaticMeshBakeMode
    {
        None,
        FacetedFaceColors,
        ToonVertexShading,
    }

    [Serializable]
    public struct StaticMeshPolicyMapping
    {
        public string name;
        public string brushGuid;
        public StaticMeshBakeMode Mode;
    }

    public enum TextureBakeMode
    {
        None,
        UvBaseColor,
        UvUnlit,
        UvEmission,
        Unsupported,
        PetalGradient,
    }

    [Serializable]
    public struct TextureBakePolicyMapping
    {
        public string name;
        public string brushGuid;
        public TextureBakeMode Mode;
        public int BakePass;
        public bool ForceUnlit;
        public string Reason;
    }

    void Start()
    {
        m_Instance = this;
    }

    public bool TryGetMapping(string brushGuid, out ComputeShaderMapping mapping)
    {
        return TryGetMapping(computeShaders, brushGuid, out mapping);
    }

    public bool TryGetStaticMapping(string brushGuid, out ComputeShaderMapping mapping)
    {
        return TryGetMapping(staticComputeShaders, brushGuid, out mapping);
    }

    public bool TryGetStaticMeshPolicy(
        string brushGuid, out StaticMeshPolicyMapping policy)
    {
        policy = default;
        if (staticMeshPolicies == null) return false;
        foreach (StaticMeshPolicyMapping candidate in staticMeshPolicies)
        {
            if (string.Equals(candidate.brushGuid, brushGuid, StringComparison.OrdinalIgnoreCase))
            {
                policy = candidate;
                return true;
            }
        }
        return false;
    }

    private static bool TryGetMapping(
        List<ComputeShaderMapping> mappings, string brushGuid, out ComputeShaderMapping mapping)
    {
        mapping = default;
        if (mappings == null) return false;
        foreach (ComputeShaderMapping candidate in mappings)
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

    public IEnumerable<ComputeShaderMapping> StaticMappings
    {
        get { return staticComputeShaders ?? Enumerable.Empty<ComputeShaderMapping>(); }
    }

    public bool TryGetTextureBakePolicy(
        string brushGuid, out TextureBakePolicyMapping policy)
    {
        policy = default;
        if (textureBakePolicies == null) return false;
        foreach (TextureBakePolicyMapping candidate in textureBakePolicies)
        {
            if (string.Equals(candidate.brushGuid, brushGuid, StringComparison.OrdinalIgnoreCase))
            {
                policy = candidate;
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

        return ProcessMesh(mesh, mapping);
    }

    public Mesh ProcessMeshForStaticExport(
        Mesh mesh, string brushGuid, Material material, Matrix4x4 localToWorldMatrix)
    {
        bool foundMapping = false;
        if (TryGetMapping(brushGuid, out var commonMapping))
        {
            mesh = ProcessMesh(mesh, commonMapping);
            foundMapping = true;
        }

        if (TryGetStaticMapping(brushGuid, out var staticMapping))
        {
            Debug.Log($"[OB_STATIC_MESH] Applying {staticMapping.name} to brush {brushGuid}");
            mesh = ProcessMesh(mesh, staticMapping);
            foundMapping = true;
        }

        if (TryGetStaticMeshPolicy(brushGuid, out var staticPolicy))
        {
            Debug.Log($"[OB_STATIC_MESH] Applying {staticPolicy.name} to brush {brushGuid}");
            mesh = ApplyStaticMeshPolicy(mesh, material, localToWorldMatrix, staticPolicy.Mode);
            foundMapping = true;
        }

        if (!foundMapping)
        {
            Debug.LogWarning($"[OB_STATIC_MESH] No mapping found for brush {brushGuid}");
        }
        return mesh;
    }

    private static Mesh ApplyStaticMeshPolicy(
        Mesh mesh, Material material, Matrix4x4 localToWorldMatrix,
        StaticMeshBakeMode mode)
    {
        switch (mode)
        {
            case StaticMeshBakeMode.FacetedFaceColors:
                return BakeFacetedFaceColors(mesh, material, localToWorldMatrix);
            case StaticMeshBakeMode.ToonVertexShading:
                return BakeToonVertexShading(mesh, localToWorldMatrix);
            default:
                return mesh;
        }
    }

    private static Mesh BakeToonVertexShading(Mesh mesh, Matrix4x4 localToWorldMatrix)
    {
        Vector3[] normals = mesh.normals;
        if (normals.Length != mesh.vertexCount)
        {
            Debug.LogWarning($"[OB_STATIC_MESH] Toon mesh {mesh.name} has no vertex normals");
            return mesh;
        }

        Color[] colors = mesh.colors;
        if (colors.Length != mesh.vertexCount)
        {
            colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
        }

        Matrix4x4 normalMatrix = localToWorldMatrix.inverse.transpose;
        for (int i = 0; i < colors.Length; i++)
        {
            float light = normalMatrix.MultiplyVector(normals[i]).normalized.y * 0.2f;
            Color color = colors[i];
            color.r = Mathf.Clamp01(color.r + light);
            color.g = Mathf.Clamp01(color.g + light);
            color.b = Mathf.Clamp01(color.b + light);
            colors[i] = color;
        }
        mesh.colors = colors;
        Debug.Log($"[OB_STATIC_MESH] Baked Toon vertex shading into {colors.Length} vertices");
        return mesh;
    }

    private static Mesh BakeFacetedFaceColors(
        Mesh mesh, Material material, Matrix4x4 localToWorldMatrix)
    {
        if (material == null || !material.HasProperty("_ColorX") ||
            !material.HasProperty("_ColorY") || !material.HasProperty("_ColorZ"))
        {
            Debug.LogWarning(
                $"[OB_STATIC_MESH] Faceted material properties are unavailable on {material?.name}");
            return mesh;
        }

        var sourceVertices = mesh.vertices;
        var sourceNormals = mesh.normals;
        var sourceTangents = mesh.tangents;
        var sourceColors = mesh.colors;
        var sourceUvs = new List<Vector4>[8];
        for (int channel = 0; channel < sourceUvs.Length; channel++)
        {
            sourceUvs[channel] = new List<Vector4>();
            mesh.GetUVs(channel, sourceUvs[channel]);
        }

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var tangents = new List<Vector4>();
        var colors = new List<Color>();
        var uvs = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
        var submeshTriangles = new List<int>[mesh.subMeshCount];

        Color colorX = material.GetColor("_ColorX");
        Color colorY = material.GetColor("_ColorY");
        Color colorZ = material.GetColor("_ColorZ");
        Matrix4x4 normalMatrix = localToWorldMatrix.inverse.transpose;

        for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
        {
            int[] sourceTriangles = mesh.GetTriangles(submesh);
            var triangles = new List<int>(sourceTriangles.Length);
            submeshTriangles[submesh] = triangles;
            for (int triangle = 0; triangle < sourceTriangles.Length; triangle += 3)
            {
                int index0 = sourceTriangles[triangle];
                int index1 = sourceTriangles[triangle + 1];
                int index2 = sourceTriangles[triangle + 2];
                Vector3 edge1 = sourceVertices[index1] - sourceVertices[index0];
                Vector3 edge2 = sourceVertices[index2] - sourceVertices[index0];
                Vector3 faceNormal = Vector3.Cross(edge1, edge2).normalized;
                if (sourceNormals.Length == sourceVertices.Length)
                {
                    Vector3 averageNormal =
                        sourceNormals[index0] + sourceNormals[index1] + sourceNormals[index2];
                    if (Vector3.Dot(faceNormal, averageNormal) < 0)
                    {
                        faceNormal = -faceNormal;
                    }
                }
                Vector3 worldNormal = normalMatrix.MultiplyVector(faceNormal).normalized;
                Color faceColor = new Color(
                    Mathf.Clamp01(colorX.r * worldNormal.x + colorY.r * worldNormal.y +
                        colorZ.r * worldNormal.z),
                    Mathf.Clamp01(colorX.g * worldNormal.x + colorY.g * worldNormal.y +
                        colorZ.g * worldNormal.z),
                    Mathf.Clamp01(colorX.b * worldNormal.x + colorY.b * worldNormal.y +
                        colorZ.b * worldNormal.z),
                    1);

                AppendFacetedVertex(index0, faceColor, triangles);
                AppendFacetedVertex(index1, faceColor, triangles);
                AppendFacetedVertex(index2, faceColor, triangles);
            }
        }

        string meshName = mesh.name;
        mesh.Clear();
        mesh.name = meshName;
        mesh.indexFormat = vertices.Count > ushort.MaxValue
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        if (normals.Count == vertices.Count) mesh.SetNormals(normals);
        if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
        mesh.SetColors(colors);
        for (int channel = 0; channel < uvs.Length; channel++)
        {
            if (uvs[channel].Count == vertices.Count)
            {
                mesh.SetUVs(channel, uvs[channel]);
            }
        }
        mesh.subMeshCount = submeshTriangles.Length;
        for (int submesh = 0; submesh < submeshTriangles.Length; submesh++)
        {
            mesh.SetTriangles(submeshTriangles[submesh], submesh, false);
        }
        mesh.RecalculateBounds();
        Debug.Log($"[OB_STATIC_MESH] Baked faceted colors: {sourceVertices.Length} source vertices, {vertices.Count} split vertices");
        return mesh;

        void AppendFacetedVertex(
            int sourceIndex, Color faceColor, List<int> destinationTriangles)
        {
            int destinationIndex = vertices.Count;
            vertices.Add(sourceVertices[sourceIndex]);
            destinationTriangles.Add(destinationIndex);
            if (sourceNormals.Length == sourceVertices.Length)
            {
                normals.Add(sourceNormals[sourceIndex]);
            }
            if (sourceTangents.Length == sourceVertices.Length)
            {
                tangents.Add(sourceTangents[sourceIndex]);
            }
            float alpha = sourceColors.Length == sourceVertices.Length
                ? sourceColors[sourceIndex].a : 1;
            faceColor.a = alpha;
            colors.Add(faceColor);
            for (int channel = 0; channel < sourceUvs.Length; channel++)
            {
                if (sourceUvs[channel].Count == sourceVertices.Length)
                {
                    uvs[channel].Add(sourceUvs[channel][sourceIndex]);
                }
            }
        }
    }

    private Mesh ProcessMesh(Mesh mesh, ComputeShaderMapping mapping)
    {

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
