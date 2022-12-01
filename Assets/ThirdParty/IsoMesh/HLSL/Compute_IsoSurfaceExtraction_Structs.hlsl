#ifndef SURFACE_NET_STRUCTS
#define SURFACE_NET_STRUCTS

struct CellData
{
    int VertexID;
    float3 SurfacePoint;
    
    bool HasSurfacePoint()
    {
        return VertexID >= 0;
    }
};

struct VertexData
{
    int Index;
    int CellID;
    float3 Vertex;
    float3 Normal;
};

struct TriangleData
{
    int P_1;
    int P_2;
    int P_3;
};

struct NewVertexData
{
    int Index;
    float3 Vertex;
    float3 Normal;
};


// This struct represents a single SDF object, to be sent as an instruction to the GPU.
struct SDFGPUData
{
    int Type; // negative if operation, 0 if mesh, else it's an enum value
    float4 Data; // if primitive, this could be anything. if mesh, it's (size, sample start index, uv start index, 0)
    float4x4 Transform; // translation/rotation/scale
    int Operation; // how this sdf is combined with previous 
    int Flip; // whether to multiply by -1, turns inside out
    float3 MinBounds; // only used by sdfmesh, near bottom left
    float3 MaxBounds; // only used by sdfmesh, far top right
    float Smoothing;
    
    bool IsMesh()
    {
        return Type == 0;
    }
    
    bool IsOperation()
    {
        return Type < 0;
    }
    
    bool IsPrimitive()
    {
        return Type > 0;
    }
    
    int Size()
    {
        return (int) Data.x;
    }
    
    int SampleStartIndex()
    {
        return (int) Data.y;
    }
    
    int UVStartIndex()
    {
        return (int) Data.z;
    }
};

struct SDFMaterialGPU
{
    int MaterialType;
    int TextureIndex;
    float3 Colour;
    float3 Emission;
    float Metallic;
    float Smoothness;
    float Thickness;
    float3 SubsurfaceColour;
    float SubsurfaceScatteringPower;
    float MaterialSmoothing;
};

// interpolate two materials, but note that 'materialtype' and 'textureindex' cant be interpolated
SDFMaterialGPU lerpMaterial(SDFMaterialGPU a, SDFMaterialGPU b, float t)
{
    SDFMaterialGPU result;
    result.MaterialType = 1;
    result.TextureIndex = 0;
    result.Colour = lerp(a.Colour, b.Colour, t);
    result.Emission = lerp(a.Emission, b.Emission, t);
    result.Metallic = lerp(a.Metallic, b.Metallic, t);
    result.Smoothness = lerp(a.Smoothness, b.Smoothness, t);
    result.Thickness = lerp(a.Thickness, b.Thickness, t);
    result.SubsurfaceColour = lerp(a.SubsurfaceColour, b.SubsurfaceColour, t);
    result.SubsurfaceScatteringPower = lerp(a.SubsurfaceScatteringPower, b.SubsurfaceScatteringPower, t);
    result.MaterialSmoothing = lerp(a.MaterialSmoothing, b.MaterialSmoothing, t);
    return result;
}

SDFMaterialGPU negate(SDFMaterialGPU mat)
{
    SDFMaterialGPU result;
    result.MaterialType = 1;
    result.TextureIndex = 0;
    result.Colour = -mat.Colour;
    result.Emission = -mat.Emission;
    result.Metallic = -mat.Metallic;
    result.Smoothness = -mat.Smoothness;
    result.Thickness = -mat.Thickness;
    result.SubsurfaceColour = -mat.SubsurfaceColour;
    result.SubsurfaceScatteringPower = -mat.SubsurfaceScatteringPower;
    result.MaterialSmoothing = -mat.MaterialSmoothing;
    return result;
}

struct Settings
{
    float NormalSmoothing; // the 'epsilon' value for computing the gradient, affects how smoothed out the normals are
    float ThicknessMaxDistance;
    float ThicknessFalloff;
};


#endif // SURFACE_NET_STRUCTS