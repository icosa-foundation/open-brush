using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace IsoMesh.Chunking
{
    /// <summary>
    /// This class allows you to control a 3D grid of aligned <see cref="SDFGroupMeshGenerator"/> components together.
    /// </summary>
    [ExecuteInEditMode]
    public class ChunkGrid : MonoBehaviour, ISerializationCallbackReceiver, IInstantiator<SDFGroupMeshGenerator>, IActivator<SDFGroupMeshGenerator>
    {
        [SerializeField]
        private SDFGroup m_group;
        public SDFGroup Group
        {
            get
            {
                if (!m_group)
                    m_group = GetComponent<SDFGroup>();

                return m_group;
            }
        }

        [SerializeField]
        private MainSettings m_mainSettings = new MainSettings();

        [SerializeField]
        private VoxelSettings m_voxelSettings = new VoxelSettings();
        public VoxelSettings VoxelSettings => m_voxelSettings;

        [SerializeField]
        private AlgorithmSettings m_algorithmSettings = new AlgorithmSettings();

        [SerializeField]
        private bool m_addMeshRenderers = false;

        [SerializeField]
        private bool m_addMeshColliders = false;

        [SerializeField]
        private Material m_meshRendererMaterial;

        private readonly Dictionary<Vector3Int, Chunk> m_grid = new Dictionary<Vector3Int, Chunk>();

        private readonly HashSet<Vector3Int> m_tempSeenCoordinates = new HashSet<Vector3Int>();

        [SerializeField]
        private List<Chunk> m_rawList = new List<Chunk>();
        
        [SerializeField]
        private Pool<SDFGroupMeshGenerator> m_meshGenPool;

        private Pool<SDFGroupMeshGenerator> Pool
        {
            get
            {
                if (m_meshGenPool == null)
                    m_meshGenPool = new Pool<SDFGroupMeshGenerator>(this, this, transform);

                return m_meshGenPool;
            }
        }


        /// <summary>
        /// Enumerate every possible neighbouring coordinate offset.
        /// </summary>
        private static readonly Vector3Int[] m_neighbouringCoordinates = new Vector3Int[]
        {
            new Vector3Int(-1, -1, -1),
            new Vector3Int(0, -1, -1),
            new Vector3Int(1, -1, -1),
            new Vector3Int(-1, 0, -1),
            new Vector3Int(0, 0, -1),
            new Vector3Int(1, 0, -1),
            new Vector3Int(-1, 1, -1),
            new Vector3Int(0, 1, -1),
            new Vector3Int(1, 1, -1),
            new Vector3Int(-1, -1, 0),
            new Vector3Int(0, -1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(1, 1, 0),
            new Vector3Int(-1, -1, 1),
            new Vector3Int(0, -1, 1),
            new Vector3Int(1, -1, 1),
            new Vector3Int(-1, 0, 1),
            new Vector3Int(0, 0, 1),
            new Vector3Int(1, 0, 1),
            new Vector3Int(-1, 1, 1),
            new Vector3Int(0, 1, 1),
            new Vector3Int(1, 1, 1)
        };

        /// <summary>
        /// Enumerate every possible neighbouring coordinate offset, not including diagonals.
        /// </summary>
        private static readonly Vector3Int[] m_axisAlignedNeighbouringCoordinates = new Vector3Int[]
        {
            new Vector3Int(0, 0, -1),
            new Vector3Int(0, -1, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, 0, 1),
        };

        /// <summary>
        /// Return all occupied chunks.
        /// </summary>
        public IList<Chunk> Chunks => m_rawList.AsReadOnly();

        /// <summary>
        /// The number of chunks controlled by this object.
        /// </summary>
        public int Count => m_rawList.Count;

        /// <summary>
        /// Return all the coordinates adjacent to this one.
        /// </summary>
        public IEnumerable<Vector3Int> GetNeighbourCoordinates(Vector3Int coordinate)
        {
            for (int i = 0; i < m_neighbouringCoordinates.Length; i++)
                yield return coordinate + m_neighbouringCoordinates[i];
        }

        /// <summary>
        /// Return all the occupied chunks adjacent to the given chunk.
        /// </summary>
        public IEnumerable<Chunk> GetNeighbours(Chunk chunk) => GetNeighbours(chunk.Coordinate);

        /// <summary>
        /// Return all the occupied chunks adjacent to the given coordinate.
        /// </summary>
        public IEnumerable<Chunk> GetNeighbours(Vector3Int coordinate)
        {
            for (int i = 0; i < m_neighbouringCoordinates.Length; i++)
                if (TryGetChunk(coordinate + m_neighbouringCoordinates[i], out Chunk chunk))
                    yield return chunk;
        }

        /// <summary>
        /// Get all empty spaces adjacent to the given coordinate.
        /// </summary>
        public IEnumerable<Vector3Int> GetUnoccupiedNeighbourCoordinates(Vector3Int coordinate)
        {
            for (int i = 0; i < m_neighbouringCoordinates.Length; i++)
            {
                Vector3Int neighbourCoordinate = coordinate + m_neighbouringCoordinates[i];
                if (!IsOccupied(neighbourCoordinate))
                    yield return neighbourCoordinate;
            }
        }

        /// <summary>
        /// Return all the coordinates adjacent to this one, not including diagonals.
        /// </summary>
        public IEnumerable<Vector3Int> GetAxisAlignedNeighbourCoordinates(Vector3Int coordinate)
        {
            for (int i = 0; i < m_axisAlignedNeighbouringCoordinates.Length; i++)
                yield return coordinate + m_axisAlignedNeighbouringCoordinates[i];
        }

        /// <summary>
        /// Return all the occupied chunks adjacent to the given chunk, not including diagonals.
        /// </summary>
        public IEnumerable<Chunk> GetAxisAlignedNeighbours(Chunk chunk) => GetAxisAlignedNeighbours(chunk.Coordinate);

        /// <summary>
        /// Return all the occupied chunks adjacent to the given coordinate, not including diagonals.
        /// </summary>
        public IEnumerable<Chunk> GetAxisAlignedNeighbours(Vector3Int coordinate)
        {
            foreach (Vector3Int neigbourCoordinate in GetAxisAlignedNeighbourCoordinates(coordinate))
                if (TryGetChunk(neigbourCoordinate, out Chunk chunk))
                    yield return chunk;
        }

        /// <summary>
        /// Get all empty spaces adjacent to the given coordinate, not including diagonals.
        /// </summary>
        public IEnumerable<Vector3Int> GetUnoccupiedAxisAlignedNeighbourCoordinates(Vector3Int coordinate)
        {
            foreach (Vector3Int neigbourCoordinate in GetAxisAlignedNeighbourCoordinates(coordinate))
                if (!IsOccupied(neigbourCoordinate))
                    yield return neigbourCoordinate;
        }

        private readonly List<Vector3Int> m_unoccupiedNeighbourCoordinates = new List<Vector3Int>();

        /// <summary>
        /// Returns the coordinate of each empty space directly adjacent to an occupied space, for the entire grid.
        /// </summary>
        public IList<Vector3Int> UnoccupiedNeighbourCoordinates => m_unoccupiedNeighbourCoordinates.AsReadOnly();

        private readonly List<Vector3Int> m_unoccupiedAxisAlignedNeighbourCoordinates = new List<Vector3Int>();

        /// <summary>
        /// Returns the coordinate of each empty space directly adjacent to an occupied space, for the entire grid.
        /// </summary>
        public IList<Vector3Int> UnoccupiedAxisAlignedNeighbourCoordinates => m_unoccupiedAxisAlignedNeighbourCoordinates.AsReadOnly();

        /// <summary>
        /// Returns the coordinate of each occupied chunk.
        /// </summary>
        public IEnumerable<Vector3Int> GetAllOccupiedCoordinates()
        {
            for (int i = 0; i < m_rawList.Count; i++)
                yield return m_rawList[i].Coordinate;
        }

        /// <summary>
        /// Convert an integer grid coordinate to a local position, which accounts for the chunk size.
        /// </summary>
        public Vector3 CoordinateToLocalPosition(Vector3Int coordinate) => (Vector3)coordinate * m_voxelSettings.OffsetDistance;

        /// <summary>
        /// Convert an integer grid coordinate to a world position, which accounts for the chunk size and the transform of the grid object.
        /// </summary>
        public Vector3 CoordinateToWorldPosition(Vector3Int coordinate) => transform.TransformPoint(CoordinateToLocalPosition(coordinate));

        /// <summary>
        /// Returns true if the given coordinate has a chunk.
        /// </summary>
        public bool IsOccupied(Vector3Int coordinate) => m_grid.ContainsKey(coordinate);

        /// <summary>
        /// If there is a chunk at the given coordinate, get it and return true.
        /// </summary>
        public bool TryGetChunk(Vector3Int coordinate, out Chunk chunk) => m_grid.TryGetValue(coordinate, out chunk);

        /// <summary>
        /// Provided the given coordinate is empty, create a chunk and place it at that coordinate.
        /// </summary>
        public void AddChunk(Vector3Int coordinate) => TryAddChunk(coordinate, out _);

        /// <summary>
        /// Provided the given coordinate is empty, create a chunk and place it at that coordinate. Returns whether the operation was a success, and the new chunk.
        /// </summary>
        public bool TryAddChunk(Vector3Int coordinate, out Chunk chunk)
        {
            if (IsOccupied(coordinate))
            {
                Debug.LogError("Grid already contains a chunk at coordinate: " + coordinate);
                chunk = default;
                return false;
            }

            SDFGroupMeshGenerator meshGen = Pool.GetNew();
            meshGen.transform.localPosition = (Vector3)coordinate * m_voxelSettings.OffsetDistance;
            meshGen.gameObject.name = "MeshGen " + coordinate;
            meshGen.gameObject.SetActive(true);

            chunk = new Chunk(coordinate, meshGen);

            m_rawList.Add(chunk);
            m_grid.Add(coordinate, chunk);

            OnChunksChanged();

            return true;
        }

        /// <summary>
        /// Try to remove the chunk at the given coordinate. Returns true on success.
        /// </summary>
        public bool RemoveChunk(Vector3Int coordinate)
        {
            if (!m_grid.TryGetValue(coordinate, out Chunk chunk))
                return false;

            SDFGroupMeshGenerator meshGen = chunk.MeshGen;

            if (meshGen)
            {
                if (!meshGen.transform.IsChildOf(transform))
                    Pool.Free(meshGen);
                else
                    Pool.ReturnToPool(meshGen);
            }

            m_grid.Remove(coordinate);
            m_rawList.Remove(chunk);

            OnChunksChanged();

            return true;
        }

        /// <summary>
        /// Instantly remove every chunk! The nuclear option!!
        /// </summary>
        public void RemoveAllChunks()
        {
            for (int i = m_rawList.Count - 1; i >= 0; --i)
                RemoveChunk(m_rawList[i].Coordinate);
        }

        #region Private Methods

        private void OnChunksChanged()
        {
            RebuildAllUnoccupiedAxisAlignedNeighbourCoordinates();
            RebuildAllUnoccupiedNeighbourCoordinates();
        }

        private void RebuildAllUnoccupiedAxisAlignedNeighbourCoordinates()
        {
            m_unoccupiedAxisAlignedNeighbourCoordinates.Clear();

            if (Count == 0)
            {
                // might remove this if it has any impact. for now, it ensures there's an add handle in the scene gui when you create an empty grid
                m_unoccupiedAxisAlignedNeighbourCoordinates.Add(Vector3Int.zero);
            }
            else
            {
                m_tempSeenCoordinates.Clear();

                for (int i = 0; i < m_rawList.Count; i++)
                {
                    Chunk chunk = m_rawList[i];
                    Vector3Int coordinate = chunk.Coordinate;
                    
                    for (int j = 0; j < m_axisAlignedNeighbouringCoordinates.Length; j++)
                    {
                        Vector3Int neighbourCoordinate = coordinate + m_axisAlignedNeighbouringCoordinates[j];
                        if (!IsOccupied(neighbourCoordinate))
                        {
                            if (!m_tempSeenCoordinates.Contains(neighbourCoordinate))
                            {
                                m_unoccupiedAxisAlignedNeighbourCoordinates.Add(neighbourCoordinate);
                                m_tempSeenCoordinates.Add(neighbourCoordinate);
                            }
                        }
                    }
                }
            }
        }

        private void RebuildAllUnoccupiedNeighbourCoordinates()
        {
            m_unoccupiedNeighbourCoordinates.Clear();

            if (Count == 0)
            {
                // might remove this if it has any impact. for now, it ensures there's an add handle in the scene gui when you create an empty grid
                m_unoccupiedNeighbourCoordinates.Add(Vector3Int.zero);
            }
            else
            {
                m_tempSeenCoordinates.Clear();

                for (int i = 0; i < m_rawList.Count; i++)
                {
                    Chunk chunk = m_rawList[i];
                    Vector3Int coordinate = chunk.Coordinate;

                    for (int j = 0; j < m_neighbouringCoordinates.Length; j++)
                    {
                        Vector3Int neighbourCoordinate = coordinate + m_neighbouringCoordinates[j];
                        if (!IsOccupied(neighbourCoordinate))
                        {
                            if (!m_tempSeenCoordinates.Contains(neighbourCoordinate))
                            {
                                m_unoccupiedNeighbourCoordinates.Add(neighbourCoordinate);
                                m_tempSeenCoordinates.Add(neighbourCoordinate);
                            }
                        }
                    }
                }
            }
        }

        #endregion
        
        #region MonoBehaviour Stuff

        private void OnEnable()
        {
            OnChunksChanged();

#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed += OnUndo;
#endif
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.Undo.undoRedoPerformed -= OnUndo;
#endif
        }

        private void Update()
        {
            // check for objects deleted outside the context of this class, 
            // believe it or not this is apparently how you're "supposed" to do it :/
            for (int i = m_rawList.Count - 1; i >= 0; --i)
            {
                SDFGroupMeshGenerator meshGen = m_rawList[i].MeshGen;
                if (!meshGen || !meshGen.transform.IsChildOf(transform))
                    RemoveChunk(m_rawList[i].Coordinate);
            }

        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            // since dictionaries don't serialize, we store a serialized list of chunks and restore it

            m_grid.Clear();

            for (int i = 0; i < m_rawList.Count; i++)
                m_grid.Add(m_rawList[i].Coordinate, m_rawList[i]);

            // cant serialize these references :(
            Pool.SetInstantiator(this);
            Pool.SetActivator(this);
        }

        #endregion

        #region Editor Functions

        // note - im astonished that all this stuff below works. and the first time, too. :) so used to unity editor stuff being a nightmare

        private void OnUndo()
        {
            // ensure each chunk is positioned correctly.
            foreach (Chunk chunk in m_rawList)
            {
                if (chunk.MeshGen)
                {
                    chunk.MeshGen.transform.localPosition = (Vector3)chunk.Coordinate * m_voxelSettings.OffsetDistance;
                }
            }
        }

        /// <summary>
        /// Returns all objects to dirty before an operation is applied to all chunks. 
        /// </summary>
        private Object[] GetChunkObjectsToDirty()
        {
            IEnumerable<Chunk> nonNulls = m_rawList.Where((Chunk c) => c.MeshGen);
            return nonNulls.Select((Chunk c) => c.MeshGen as Object).Union(nonNulls.Select((Chunk c) => c.MeshGen.transform as Object)).ToArray();
        }

        public void OnVoxelSettingChanged()
        {
#if UNITY_EDITOR
            // I do it like this instead of calling Undo.RecordObject on each individually so this counts as one action, and is undone all together
            UnityEditor.Undo.RecordObjects(GetChunkObjectsToDirty(), "Set Voxel Settings");
#endif

            foreach (Chunk chunk in m_rawList)
            {
                if (chunk.MeshGen)
                {
                    chunk.MeshGen.SetVoxelSettings(m_voxelSettings);
                    chunk.MeshGen.transform.localPosition = (Vector3)chunk.Coordinate * m_voxelSettings.OffsetDistance;

#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(chunk.MeshGen);
                    UnityEditor.EditorUtility.SetDirty(chunk.MeshGen.transform);
#endif
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void OnMainSettingsChanged()
        {
#if UNITY_EDITOR
            // I do it like this instead of calling Undo.RecordObject on each individually so this counts as one action, and is undone all together
            UnityEditor.Undo.RecordObjects(GetChunkObjectsToDirty(), "Set Main Settings");
#endif
            foreach (Chunk chunk in m_rawList)
            {
                if (chunk.MeshGen)
                {
                    chunk.MeshGen.SetMainSettings(m_mainSettings);

#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(chunk.MeshGen);
                    UnityEditor.EditorUtility.SetDirty(chunk.MeshGen.transform);
#endif
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void OnAlgorithmSettingsChanged()
        {
#if UNITY_EDITOR
            // I do it like this instead of calling Undo.RecordObject on each individually so this counts as one action, and is undone all together
            UnityEditor.Undo.RecordObjects(GetChunkObjectsToDirty(), "Set Algorithm Settings");
#endif
            foreach (Chunk chunk in m_rawList)
            {
                if (chunk.MeshGen)
                {
                    chunk.MeshGen.SetAlgorithmSettings(m_algorithmSettings);

#if UNITY_EDITOR
                    UnityEditor.EditorUtility.SetDirty(chunk.MeshGen);
                    UnityEditor.EditorUtility.SetDirty(chunk.MeshGen.transform);
#endif
                }
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public SDFGroupMeshGenerator Create()
        {
            GameObject cloneObject = new GameObject();
            cloneObject.transform.SetParent(transform);
            cloneObject.gameObject.SetActive(false);

            SDFGroupMeshGenerator meshGen = cloneObject.AddComponent<SDFGroupMeshGenerator>();

#if UNITY_EDITOR
            // Register Undo
            UnityEditor.Undo.RegisterCreatedObjectUndo(cloneObject, "Created SDF Mesh Generator");
#endif

            return meshGen;
        }

        public void Activate(SDFGroupMeshGenerator meshGen)
        {
            SDFGroupMeshGenerator.CloneSettings(meshGen, transform, m_group, m_mainSettings, m_algorithmSettings, m_voxelSettings, m_addMeshRenderers, m_addMeshColliders, m_meshRendererMaterial);
            meshGen.SetSettingsControlledByGrid(true);
        }

        public void Deactivate(SDFGroupMeshGenerator meshGen)
        {
            meshGen.MainSettings.AutoUpdate = false;
            //meshGen.gameObject.SetActive(false);
            //meshGen.SetSettingsControlledByGrid(false);
        }

        #endregion
    }

    [System.Serializable]
    public struct Chunk
    {
        [SerializeField]
        private Vector3Int m_coordinate;
        public Vector3Int Coordinate => m_coordinate;

        [SerializeField]
        private SDFGroupMeshGenerator m_meshGen;
        public SDFGroupMeshGenerator MeshGen => m_meshGen;

        /// <summary>
        /// Chunks are cuboids, but this returns the radius of the sphere which fully encapsulates the chunk.
        /// </summary>
        public float Radius
        {
            get
            {
                if (!m_meshGen)
                    return 0f;

                return m_meshGen.VoxelSettings.Extents.magnitude;
            }
        }

        public Chunk(Vector3Int coordinate, SDFGroupMeshGenerator meshGen)
        {
            m_coordinate = coordinate;
            m_meshGen = meshGen;
        }
    }
}