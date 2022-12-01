using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IsoMesh
{
    /// <summary>
    /// This class contains data representing a signed distance field of a mesh.
    /// </summary>
    [CreateAssetMenu]
    public class SDFMeshAsset : ScriptableObject
    {
        [SerializeField]
        [ReadOnly]
        //[PreviewField]
        private Mesh m_sourceMesh;
        public Mesh SourceMesh => m_sourceMesh;

        [SerializeField]
        [HideInInspector]
        private float[] m_samples;

        [SerializeField]
        [HideInInspector]
        private float[] m_packedUVs;

        public bool HasUVs => !m_packedUVs.IsNullOrEmpty();

        [SerializeField]
        [ReadOnly]
        //[ShowIf("IsTessellated")]
        private int m_tessellationLevel = 0;

        public bool IsTessellated => m_tessellationLevel > 0;

        [SerializeField]
        [ReadOnly]
        private int m_size;
        public int Size => m_size;

        public int CellsPerSide => m_size - 1;
        public int PointsPerSide => m_size;

        [SerializeField]
        [ReadOnly]
        private float m_padding;
        public float Padding => m_padding;

        [SerializeField]
        [ReadOnly]
        private Vector3 m_minBounds;
        public Vector3 MinBounds => m_minBounds;

        [SerializeField]
        [ReadOnly]
        private Vector3 m_maxBounds;
        public Vector3 MaxBounds => m_maxBounds;

        public Vector3 Centre => (m_maxBounds + m_minBounds) * 0.5f;

        public Bounds Bounds => new Bounds(Centre, MaxBounds - MinBounds);

        public int TotalSize => m_size * m_size * m_size;

        public static void Create(string path, string name, float[] samples, float[] packedUVs, int tessellationLevel, int size, float padding, Mesh sourceMesh, Vector3 minBounds, Vector3 maxBounds)
        {
            SDFMeshAsset asset = Utils.CreateAsset<SDFMeshAsset>(path, name + "_" + size);
            asset.m_sourceMesh = sourceMesh;
            asset.m_minBounds = minBounds;
            asset.m_maxBounds = maxBounds;
            asset.m_size = size;
            asset.m_tessellationLevel = tessellationLevel;
            asset.m_padding = padding;
            asset.m_samples = samples;
            asset.m_packedUVs = packedUVs;

#if UNITY_EDITOR
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
#endif
        }

        #region Public methods

        // note that the true purpose of this class is as a data container for information to be passed to the gpu.
        // these methods are basically clones of hlsl methods for debugging purposes.

        /// <summary>
        /// Given a point anywhere in space, return the point nearest it within the bounds of the volume.
        /// Optionally can provide another parameter which just adjusts the size of this volume.
        /// </summary>
        public Vector3 ClampToVolume(Vector3 input, float boundsOffset = 0f)
        {
            return new Vector3(
                Mathf.Clamp(input.x, MinBounds.x + boundsOffset, MaxBounds.x - boundsOffset),
                Mathf.Clamp(input.y, MinBounds.y + boundsOffset, MaxBounds.y - boundsOffset),
                Mathf.Clamp(input.z, MinBounds.z + boundsOffset, MaxBounds.z - boundsOffset)
                );
        }

        /// <summary>
        /// Given a point anywhere in space, return the point nearest it within the bounds of the volume,
        /// normalized to the range [0, 1] on all axes.
        /// 
        /// Optionally can provide another parameter which just adjusts the size of this volume.
        /// </summary>
        public Vector3 ClampAndNormalizeToVolume(Vector3 p, float boundsOffset = 0f)
        {
            // clamp so we're inside the volume
            p = ClampToVolume(p, boundsOffset);

            (float x, float y, float z) = (p.x, p.y, p.z);

            x = Mathf.InverseLerp(MinBounds.x, MaxBounds.x, x);
            y = Mathf.InverseLerp(MinBounds.y, MaxBounds.y, y);
            z = Mathf.InverseLerp(MinBounds.z, MaxBounds.z, z);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// Given a point anywhere in space, return the coordinates of the nearest cell, as well as the fractional component
        /// from that cell to the next cell on all 3 axes.
        /// 
        /// Optionally can provide another parameter which just adjusts the size of this volume.
        /// </summary>
        public (int, int, int) GetNearestCoordinates(Vector3 p, out Vector3 frac, float boundsOffset = 0f)
        {
            p = ClampAndNormalizeToVolume(p, boundsOffset);

            (int x, int y, int z) result = p.PiecewiseOp(f => Mathf.FloorToInt(f * CellsPerSide));
            result = result.PiecewiseOp(i => Mathf.Min(i, CellsPerSide - 1));

            frac = p.PiecewiseOp(f => (f * CellsPerSide) % 1f);

            return result;
        }

        /// <summary>
        /// Given a cell coordinate, return the distance to the nearest triangle.
        /// </summary>
        public float GetSignedDistance(int x, int y, int z)
        {
            int index = CellCoordinateToIndex(x, y, z);
            return GetSignedDistanceAtIndex(index);
        }

        public float GetSignedDistanceAtIndex(int index)
        {
            return m_samples[index];
        }

        /// <summary>
        /// Given a point anywhere in space, clamp the point to be within the volume and then return the distance to the mesh.
        /// </summary>
        public float Sample(Vector3 p)
        {
            (int x, int y, int z) = GetNearestCoordinates(p, out Vector3 frac);

            float sampleA = GetSignedDistance(x, y, z);
            float sampleB = GetSignedDistance(x + 1, y, z);
            float sampleC = GetSignedDistance(x, y + 1, z);
            float sampleD = GetSignedDistance(x + 1, y + 1, z);
            float sampleE = GetSignedDistance(x, y, z + 1);
            float sampleF = GetSignedDistance(x + 1, y, z + 1);
            float sampleG = GetSignedDistance(x, y + 1, z + 1);
            float sampleH = GetSignedDistance(x + 1, y + 1, z + 1);

            return Utils.TrilinearInterpolate(frac, sampleA, sampleB, sampleC, sampleD, sampleE, sampleF, sampleG, sampleH);
        }

        // publicly exposing an array just feels wrong to me hehe
        public void GetDataArrays(out float[] samples, out float[] packedUVs)
        {
            samples = m_samples;
            packedUVs = m_packedUVs;
        }

        #endregion

        #region General Helper Methods

        /// <summary>
        /// Convert a 1-dimensional cell index into the object space position it corresponds to.
        /// </summary>
        public Vector3 IndexToVertex(int index)
        {
            (int x, int y, int z) = IndexToCellCoordinate(index);
            return CellCoordinateToVertex(x, y, z);
        }

        /// <summary>
        /// Convert a 3-dimensional cell coordinate into the object space position it corresponds to.
        /// </summary>
        public Vector3 CellCoordinateToVertex(int x, int y, int z)
        {
            float gridSize = CellsPerSide;
            float xPos = Mathf.Lerp(MinBounds.x, MaxBounds.x, x / gridSize);
            float yPos = Mathf.Lerp(MinBounds.y, MaxBounds.y, y / gridSize);
            float zPos = Mathf.Lerp(MinBounds.z, MaxBounds.z, z / gridSize);

            return new Vector3(xPos, yPos, zPos);
        }

        /// <summary>
        /// Convert a 1-dimensional cell index into the 3-dimensional cell coordinate it corresponds to.
        /// </summary>
        public (int x, int y, int z) IndexToCellCoordinate(int index)
        {
            int z = index / (PointsPerSide * PointsPerSide);
            index -= (z * PointsPerSide * PointsPerSide);
            int y = index / PointsPerSide;
            int x = index % PointsPerSide;

            return (x, y, z);
        }

        /// <summary>
        /// Convert a 3-dimensional cell coordinate into the 1-dimensional cell index it corresponds to.
        /// </summary>
        public int CellCoordinateToIndex(int x, int y, int z) => (x + y * PointsPerSide + z * PointsPerSide * PointsPerSide);

        /// <summary>
        /// Get the metadata to be sent to the gpu. Doesn't include the actual sample data, or the point at which this asset's sample data starts in the total sample buffer.
        /// </summary>
        //public SurfaceNetGPU.SDFMeshData GetMetadataStruct()
        //{
        //    return new SurfaceNetGPU.SDFMeshData()
        //    {
        //        Size = m_size,
        //        MinBounds = m_minBounds,
        //        MaxBounds = m_maxBounds
        //    };
        //}

        #endregion
    }
}