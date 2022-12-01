using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IsoMesh
{
    /// <summary>
    /// This struct represents a single SDF object, to be sent as an instruction to the GPU.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    [System.Serializable]
    public struct SDFGPUData
    {
        public static int Stride => sizeof(int) * 3 + sizeof(float) * 11 + sizeof(float) * 16;

        public int Type; // negative if operation, 0 if mesh, else it's an enum value
        public Vector4 Data; // if primitive, this could be anything. if mesh, it's (size, sample start index, uv start index, 0)
        public Matrix4x4 Transform; // translation/rotation/scale
        public int CombineType; // how this sdf is combined with previous 
        public int Flip; // whether to multiply by -1, turns inside out
        public Vector3 MinBounds; // only used by sdfmesh, near bottom left
        public Vector3 MaxBounds;// only used by sdfmesh, far top right
        public float Smoothing; // the input to the smooth min function, how smoothly this sdf blends with the previous ones

        public bool IsMesh => Type == 0;
        public bool IsOperation => Type < 0;
        public bool IsPrimitive => Type > 0;
        public int Size => (int)Data.x;
        public int SampleStartIndex => (int)Data.y;
        public int UVStartIndex => (int)Data.z;

        public SDFPrimitiveType PrimitiveType => (SDFPrimitiveType)(Type - 1);
        public SDFOperationType OperationType => (SDFOperationType)(-Type - 1);

        public override string ToString()
        {
            if (IsMesh)
            {
                return $"[Mesh] Size = {(int)Data.x}, MinBounds = {MinBounds}, MaxBounds = {MaxBounds}, StartIndex = {(int)Data.y}, UVStartIndex = {(int)Data.z}";
            }
            else if (IsOperation)
            {
                return $"[{OperationType}] Data = {Data}";
            }
            else
            {
                return $"[{PrimitiveType}] Data = {Data}";
            }
        }
    }
}