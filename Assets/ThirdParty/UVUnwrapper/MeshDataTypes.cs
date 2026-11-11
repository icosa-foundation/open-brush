using System.Collections.Generic;
using UnityEngine;

namespace Prowl.Unwrapper
{
    public struct UvRect
    {
        public float left;
        public float top;
        public float width;
        public float height;

        public readonly Vector2 Position => new(left, top);
        public readonly Vector2 Size => new(width, height);
    }

    public struct Face
    {
        public int[] indices;
    }

    public struct TextureCoord
    {
        public float[] uv;
        public TextureCoord(float u, float v) => uv = new [] { u, v };
    }

    public struct FaceTextureCoords
    {
        public TextureCoord[] coords;
    }

    public class UVMesh
    {
        public List<Vector3> vertices = new();
        public List<Face> faces = new();
        public List<Vector3> faceNormals = new();
        public List<int> facePartitions = new();
    }
}