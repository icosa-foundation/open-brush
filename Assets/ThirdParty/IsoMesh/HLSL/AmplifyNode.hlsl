StructuredBuffer<float3> _MeshVertices;
StructuredBuffer<float3> _MeshNormals;
StructuredBuffer<int> _MeshTriangles;

void SDFMeshProcedural_CustomCode(in int VertexID, out float3 Position, out float3 Normal)
{
    int id = _MeshTriangles[VertexID];
    
    Position = _MeshVertices[id];
    Normal = _MeshNormals[id];

}