using System.Runtime.InteropServices;
using UnityEngine;

public class GenerateModelUV : MonoBehaviour
{
    [Range(0, 3)]
    public int mappingMode; // Planar, box, cylinder, spherical
    [Range(0.5f, 1)]
    public float capThreshold = 0.75f;
    public ComputeShader uvGeneratorShader;

    private Mesh mesh;

    [ContextMenu("Go")]
    void Go()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null || uvGeneratorShader == null) return;
        mesh = meshFilter.mesh;
        int vertexCount = mesh.vertexCount;
        if (vertexCount == 0) return;
        int sizePerVertex = 3 * 4;

        ComputeBuffer positionBuffer = null;
        ComputeBuffer uvBuffer = null;
        try
        {
            positionBuffer = new ComputeBuffer(vertexCount, sizePerVertex);
            uvBuffer = new ComputeBuffer(vertexCount, sizeof(float) * 2);
            positionBuffer.SetData(mesh.vertices);
    
            // Find the kernel ID
            int kernelID = uvGeneratorShader.FindKernel("CSMain");
    
            // Bind the compute buffers to the shader
            uvGeneratorShader.SetBuffer(kernelID, "positionBuffer", positionBuffer);
            uvGeneratorShader.SetBuffer(kernelID, "uvBuffer", uvBuffer);
            uvGeneratorShader.SetInt("mappingMode", mappingMode);
    
            // Set shader parameters for bounding box
            Bounds bounds = mesh.bounds;
            uvGeneratorShader.SetFloat("minX", bounds.min.x);
            uvGeneratorShader.SetFloat("maxX", bounds.max.x);
            uvGeneratorShader.SetFloat("minY", bounds.min.y);
            uvGeneratorShader.SetFloat("maxY", bounds.max.y);
            uvGeneratorShader.SetFloat("minZ", bounds.min.z);
            uvGeneratorShader.SetFloat("maxZ", bounds.max.z);
            uvGeneratorShader.SetFloat("capThreshold", capThreshold);
            uvGeneratorShader.SetInt("vertexCount", vertexCount);
    
            // Dispatch the compute shader
            int groups = (vertexCount + 63) / 64; // This ensures rounding up
            uvGeneratorShader.Dispatch(kernelID, groups, 1, 1);
    
            // Retrieve data from the buffer
            Vector2[] uvs = new Vector2[vertexCount];
            uvBuffer.GetData(uvs);
    
            // Apply the generated UVs to the mesh
            mesh.uv = uvs;
    
            }
        finally
        {
            positionBuffer?.Release();
            uvBuffer?.Release();
        }
    }
}
