using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace IsoMesh.Chunking
{
    [RequireComponent(typeof(ChunkGrid))]
    [ExecuteInEditMode]
    public class AutoChunker : MonoBehaviour, ISDFGroupComponent
    {
        [SerializeField]
        private ChunkGrid m_chunkGrid;

        private readonly List<Vector3Int> m_chunksToSpawn = new List<Vector3Int>();
        private readonly List<Vector3Int> m_chunksToRemove = new List<Vector3Int>();

        private void Reset() => m_chunkGrid = GetComponent<ChunkGrid>();
        private void OnEnable() => m_chunkGrid = GetComponent<ChunkGrid>();

        public void Run() => ManageChunks();

        private bool m_running = false;

        public void ManageChunks()
        {
            if (m_running)
                return;

            m_running = true;

            m_chunksToSpawn.Clear();
            m_chunksToRemove.Clear();

            const float padding = 1f;

            Vector3 halfExtents = m_chunkGrid.VoxelSettings.Extents * 0.5f * padding;

            IList<Vector3Int> allUnoccupiedNeighbourCoordinates = m_chunkGrid.UnoccupiedNeighbourCoordinates;
            IList<Chunk> chunks = m_chunkGrid.Chunks;
            
            for (int i = 0; i < allUnoccupiedNeighbourCoordinates.Count; i++)
            {
                Vector3Int coordinate = allUnoccupiedNeighbourCoordinates[i];
                Vector3 centre = m_chunkGrid.CoordinateToWorldPosition(coordinate);

                if (m_chunkGrid.Group.OverlapBox(centre, halfExtents))
                {
                    // we gotta spawn something
                    m_chunksToSpawn.Add(coordinate);
                }
            }

            for (int i = 0; i < chunks.Count; i++)
            {
                Vector3Int coordinate = chunks[i].Coordinate;
                Vector3 centre = m_chunkGrid.CoordinateToWorldPosition(coordinate);

                if (!m_chunkGrid.Group.OverlapBox(centre, halfExtents))
                {
                    // we gotta remove something
                    m_chunksToRemove.Add(coordinate);
                }
            }

            for (int i = 0; i < m_chunksToSpawn.Count; i++)
                m_chunkGrid.AddChunk(m_chunksToSpawn[i]);

            for (int i = 0; i < m_chunksToRemove.Count; i++)
                m_chunkGrid.RemoveChunk(m_chunksToRemove[i]);

            m_running = false;
        }
        
        public void UpdateDataBuffer(ComputeBuffer computeBuffer, ComputeBuffer materialBuffer, int count) { }
        public void UpdateSettingsBuffer(ComputeBuffer computeBuffer) { }

        public void OnEmpty() { }
        public void OnNotEmpty() { }
    }

}