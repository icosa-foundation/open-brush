using UnityEngine;

namespace IsoMesh
{
    [System.Serializable]
    public class VoxelSettings
    {
        [SerializeField]
        private CellSizeMode m_cellSizeMode = CellSizeMode.Fixed;
        public CellSizeMode CellSizeMode => m_cellSizeMode;

        [SerializeField]
        private float m_cellSize = 0.2f;

        public float CellSize
        {
            get
            {
                if (m_cellSizeMode == CellSizeMode.Density)
                    return m_volumeSize / m_cellDensity;

                return m_cellSize;
            }
        }

        [SerializeField]
        private int m_cellCount = 50;

        public int CellCount
        {
            get
            {
                if (m_cellSizeMode == CellSizeMode.Density)
                    return Mathf.FloorToInt(m_volumeSize * m_cellDensity);

                return m_cellCount;
            }
        }

        [SerializeField]
        private float m_volumeSize = 5f;
        public float VolumeSize => m_volumeSize;

        [SerializeField]
        private float m_cellDensity = 1f;
        public float CellDensity => m_cellDensity;

        public int SamplesPerSide => CellCount + 1;
        public int TotalSampleCount
        {
            get
            {
                int samplesPerSide = CellCount + 1;
                return samplesPerSide * samplesPerSide * samplesPerSide;
            }
        }

        public Vector3 Extents => Vector3.one * CellCount * CellSize;

        /// <summary>
        /// Chunks are cuboids, but this returns the radius of the sphere which fully encapsulates the chunk.
        /// </summary>
        public float Radius
        {
            get
            {
                return Extents.magnitude;
            }
        }

        /// <summary>
        /// Returns the distance, along any axis, along which an additional volume must be positioned in order to perfectly overlap with this one.
        /// For example, if this volume is at the origin, with a cellcount of 50 and a cellsize of 0.1, a volume to the left must be placed at (-4.8, 0, 0).
        /// </summary>
        public float OffsetDistance => (CellCount - 2) * CellSize;

        public void CopySettings(VoxelSettings source)
        {
            m_cellSizeMode = source.m_cellSizeMode;
            m_cellSize = source.m_cellSize;
            m_cellCount = source.m_cellCount;
            m_volumeSize = source.m_volumeSize;
            m_cellDensity = source.m_cellDensity;
        }
    }

}