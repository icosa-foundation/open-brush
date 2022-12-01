using UnityEngine;
using UnityEditor;
using IsoMesh.Editor;
using System.Collections.Generic;

namespace IsoMesh.Chunking.Editor
{
    [CustomEditor(typeof(ChunkGrid))]
    [CanEditMultipleObjects]
    public class ChunkGridEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent SDFGroup = new GUIContent("SDF Group", "The signed distance field group this component will be converting into meshes.");
            public static GUIContent OutputMode = new GUIContent("Output Mode", "This mesh can be passed directly to a material as a triangle and index buffer in 'Procedural' mode, or transfered to the CPU and sent to a MeshFilter in 'Mesh' mode.");
            public static GUIContent IsAsynchronous = new GUIContent("Is Asynchronous", "If true, the main thread won't wait for the GPU to finish generating the mesh.");
            public static GUIContent ProceduralMaterial = new GUIContent("Procedural Material", "Mesh data will be passed directly to this material as vertex and index buffers.");
            public static GUIContent VoxelSettings = new GUIContent("Voxel Settings", "These settings control the size/amount/density of voxels.");
            public static GUIContent CellSizeMode = new GUIContent("Cell Size Mode", "Fixed = the number of cells doesn't change. Density = the size of the volume doesn't change.");
            public static GUIContent CellSize = new GUIContent("Cell Size", "The size of an indidual cell (or 'voxel').");
            public static GUIContent CellCount = new GUIContent("Cell Count", "The number of cells (or 'voxels') on each side.");
            public static GUIContent VolumeSize = new GUIContent("Volume Size", "The size of each side of the whole volume in which a mesh will be generated.");
            public static GUIContent CellDensity = new GUIContent("Cell Density", "The number of cells per side. (Rounded.)");
            public static GUIContent AlgorithmSettings = new GUIContent("Algorithm Settings", "These settings control how the mesh vertices and normals are calculated from the sdf data");
            public static GUIContent MaxAngleTolerance = new GUIContent("Max Angle Tolerance", "If the angle between the vertex normal and the triangle normal exceeds this value (degrees), the vertex will be split off and given the triangle normal. This is important for sharp edges.");
            public static GUIContent VisualNormalSmoothing = new GUIContent("Visual Normal Smoothing", "The sample size for determining the surface normals of the mesh. Higher values produce smoother normals.");
            public static GUIContent IsosurfaceExtractionType = new GUIContent("Isosurface Extraction Type", "What algorithm is used to convert the SDF data to a mesh.\nSurface Nets = cheap but bad at sharp edges and corners.\nDual Contouring = similar to surface nets but uses a more advanced technique for positioning the vertices, which is more expensive but produces nice sharp edges and corners.");
            public static GUIContent ConstrainToCellUnits = new GUIContent("Constrain to Cell Units", "Dual contouring can sometimes produce vertex positions outside of their cells. This value defines the max of how far outside the cell the vertex can be before it falls back to the surface nets solution.");
            public static GUIContent OverrideQEFSettings = new GUIContent("Override QEF Settings", "Advanced controls for dual contouring's technique for finding the vertex position.");
            public static GUIContent QEFSweeps = new GUIContent("QEF Sweeps");
            public static GUIContent QEFPseudoInverseThreshold = new GUIContent("QEF Pseudo Inverse Threshold");
            public static GUIContent EdgeIntersectionType = new GUIContent("Edge Intersection Type", "Part of the isosurface extraction algorithm involves finding the intersection between each voxel edge and the underlying isosurface.\nInterpolation = a cheap approximate solution.\nBinary Search = Iteratively search for the point of intersection.");
            public static GUIContent BinarySearchIterations = new GUIContent("Binary Search Iterations", "The number of iterations for the binary search for the edge intersection. Higher values are more expensive and accurate.");
            public static GUIContent ApplyGradientDescent = new GUIContent("Apply Gradient Descent", "The found vertex position can sometimes be slightly off the true 0-isosurface. This final step will nudge it back towards the surface.");
            public static GUIContent GradientDescentIterations = new GUIContent("Gradient Descent Iterations", "The number of times to iteratively apply the gradient descent step. 1 is usually enough.");
            public static GUIContent NudgeVerticesToAverageNormalScalar = new GUIContent("Nudge Vertices to Average Normal Scalar", "Giving vertices a further nudge in the direction of the average normal of each of the voxels edge intersections can improve edges and corners but also can produce artefacts at interior angles. This scalar value is simply multiplied by the sum of these normals. Best used at very small values and alongside gradient descent.");
            public static GUIContent NudgeMaxMagnitude = new GUIContent("Nudge Max", "Limits the magnitude of the nudge vector. (See above.)");

            public static GUIContent AddMeshRenderers = new GUIContent("Add Mesh Renderers", "Automatically add a mesh renderer to new chunks.");
            public static GUIContent AddMeshColliders = new GUIContent("Add Mesh Colliders", "Automatically add a mesh collider to new chunks.");
            public static GUIContent MeshRendererMaterial = new GUIContent("Mesh Renderer Material", "Material to add to new mesh renderers.");

            public static string GroupRequiredError = "This component must reference an SDFGroup!";
        }

        private class SerializedProperties
        {
            public SerializedProperty SDFGroup { get; }

            public SerializedProperty MainSettings { get; }
            public SerializedProperty VoxelSettings { get; }
            public SerializedProperty AlgorithmSettings { get; }

            public SerializedProperty AutoUpdate { get; }
            public SerializedProperty OutputMode { get; }
            public SerializedProperty IsAsynchronous { get; }
            public SerializedProperty ProceduralMaterial { get; }
            public SerializedProperty CellSizeMode { get; }
            public SerializedProperty CellSize { get; }
            public SerializedProperty CellCount { get; }
            public SerializedProperty VolumeSize { get; }
            public SerializedProperty CellDensity { get; }
            public SerializedProperty MaxAngleTolerance { get; }
            public SerializedProperty VisualNormalSmoothing { get; }
            public SerializedProperty IsosurfaceExtractionType { get; }
            public SerializedProperty ConstrainToCellUnits { get; }
            public SerializedProperty OverrideQEFSettings { get; }
            public SerializedProperty QEFSweeps { get; }
            public SerializedProperty QEFPseudoInverseThreshold { get; }
            public SerializedProperty EdgeIntersectionType { get; }
            public SerializedProperty BinarySearchIterations { get; }
            public SerializedProperty ApplyGradientDescent { get; }
            public SerializedProperty GradientDescentIterations { get; }
            public SerializedProperty NudgeVerticesToAverageNormalScalar { get; }
            public SerializedProperty NudgeMaxMagnitude { get; }

            public SerializedProperty AddMeshRenderers { get; }
            public SerializedProperty AddMeshColliders { get; }
            public SerializedProperty MeshRendererMaterial { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                SDFGroup = serializedObject.FindProperty("m_group");
                MainSettings = serializedObject.FindProperty("m_mainSettings");
                // AutoUpdate = MainSettings.FindPropertyRelative("m_autoUpdate");
                OutputMode = MainSettings.FindPropertyRelative("m_outputMode");
                IsAsynchronous = MainSettings.FindPropertyRelative("m_isAsynchronous");
                ProceduralMaterial = MainSettings.FindPropertyRelative("m_proceduralMaterial");

                AlgorithmSettings = serializedObject.FindProperty("m_algorithmSettings");
                MaxAngleTolerance = AlgorithmSettings.FindPropertyRelative("m_maxAngleTolerance");
                VisualNormalSmoothing = AlgorithmSettings.FindPropertyRelative("m_visualNormalSmoothing");
                IsosurfaceExtractionType = AlgorithmSettings.FindPropertyRelative("m_isosurfaceExtractionType");
                ConstrainToCellUnits = AlgorithmSettings.FindPropertyRelative("m_constrainToCellUnits");
                OverrideQEFSettings = AlgorithmSettings.FindPropertyRelative("m_overrideQEFSettings");
                QEFSweeps = AlgorithmSettings.FindPropertyRelative("m_qefSweeps");
                QEFPseudoInverseThreshold = AlgorithmSettings.FindPropertyRelative("m_qefPseudoInverseThreshold");
                EdgeIntersectionType = AlgorithmSettings.FindPropertyRelative("m_edgeIntersectionType");
                BinarySearchIterations = AlgorithmSettings.FindPropertyRelative("m_binarySearchIterations");
                ApplyGradientDescent = AlgorithmSettings.FindPropertyRelative("m_applyGradientDescent");
                GradientDescentIterations = AlgorithmSettings.FindPropertyRelative("m_gradientDescentIterations");
                NudgeVerticesToAverageNormalScalar = AlgorithmSettings.FindPropertyRelative("m_nudgeVerticesToAverageNormalScalar");
                NudgeMaxMagnitude = AlgorithmSettings.FindPropertyRelative("m_nudgeMaxMagnitude");

                VoxelSettings = serializedObject.FindProperty("m_voxelSettings");
                CellSizeMode = VoxelSettings.FindPropertyRelative("m_cellSizeMode");
                CellSize = VoxelSettings.FindPropertyRelative("m_cellSize");
                CellCount = VoxelSettings.FindPropertyRelative("m_cellCount");
                VolumeSize = VoxelSettings.FindPropertyRelative("m_volumeSize");
                CellDensity = VoxelSettings.FindPropertyRelative("m_cellDensity");

                AddMeshRenderers = serializedObject.FindProperty("m_addMeshRenderers");
                AddMeshColliders = serializedObject.FindProperty("m_addMeshColliders");
                MeshRendererMaterial = serializedObject.FindProperty("m_meshRendererMaterial");
            }
        }

        private const float HOVER_DISTANCE = 20f;
        private const float HANDLE_SIZE = 0.1f;

        private ChunkGrid m_grid;

        private SerializedProperties m_serializedProperties;
        private SerializedPropertySetter m_setter;
        private bool m_isVoxelSettingsOpen = true;
        private bool m_isAlgorithmSettingsOpen = true;

        private Vector3Int? m_clickedCoordinate = null;
        private Vector3Int? m_hoveredCoordinate = null;
        
        private readonly Dictionary<int, Vector3Int> m_emptyNeighbourControlIDs = new Dictionary<int, Vector3Int>();

        private void OnEnable()
        {
            m_isDeleteMode = false;
            m_clickedCoordinate = null;
            m_hoveredCoordinate = null;

            m_grid = target as ChunkGrid;
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_setter = new SerializedPropertySetter(serializedObject);

            m_emptyNeighbourControlIDs.Clear();

            foreach (Vector3Int coordinate in m_grid.UnoccupiedAxisAlignedNeighbourCoordinates)
                m_emptyNeighbourControlIDs.Add(GUIUtility.GetControlID(FocusType.Passive), coordinate);
        }

        public override void OnInspectorGUI()
        {
            m_setter.Clear();

            //Debug.Log(m_grid.VoxelSettings.Extents);
            //Debug.Log(m_grid.VoxelSettings.Radius);


            serializedObject.DrawScript();

            m_setter.DrawProperty(Labels.SDFGroup, m_serializedProperties.SDFGroup);

            bool hasGroup = m_serializedProperties.SDFGroup.objectReferenceValue;

            if (!hasGroup)
                EditorGUILayout.HelpBox(Labels.GroupRequiredError, MessageType.Error);

            GUI.enabled = hasGroup;

            m_setter.DrawEnumSetting<OutputMode>(Labels.OutputMode, m_serializedProperties.OutputMode, onValueChangedCallback: m_grid.OnMainSettingsChanged);

            OutputMode outputMode = (OutputMode)m_serializedProperties.OutputMode.enumValueIndex;

            if (outputMode == OutputMode.Procedural)
            {
                m_setter.DrawProperty(Labels.ProceduralMaterial, m_serializedProperties.ProceduralMaterial, onValueChangedCallback: m_grid.OnMainSettingsChanged);
            }
            else if (outputMode == OutputMode.MeshFilter)
            {
                m_setter.DrawProperty(Labels.AddMeshRenderers, m_serializedProperties.AddMeshRenderers);

                if (m_serializedProperties.AddMeshRenderers.boolValue)
                    m_setter.DrawProperty(Labels.MeshRendererMaterial, m_serializedProperties.MeshRendererMaterial);

                m_setter.DrawProperty(Labels.AddMeshColliders, m_serializedProperties.AddMeshColliders);

                m_setter.DrawProperty(Labels.IsAsynchronous, m_serializedProperties.IsAsynchronous, onValueChangedCallback: m_grid.OnMainSettingsChanged);
            }

            if (m_isVoxelSettingsOpen = EditorGUILayout.Foldout(m_isVoxelSettingsOpen, Labels.VoxelSettings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawProperty(Labels.CellSizeMode, m_serializedProperties.CellSizeMode);

                        CellSizeMode cellSizeMode = (CellSizeMode)m_serializedProperties.CellSizeMode.enumValueIndex;

                        if (cellSizeMode == CellSizeMode.Fixed)
                        {
                            m_setter.DrawFloatSetting(Labels.CellSize, m_serializedProperties.CellSize, min: 0.005f, onValueChangedCallback: m_grid.OnVoxelSettingChanged);
                            m_setter.DrawIntSetting(Labels.CellCount, m_serializedProperties.CellCount, min: 2, max: 200, onValueChangedCallback: m_grid.OnVoxelSettingChanged);
                        }
                        else if (cellSizeMode == CellSizeMode.Density)
                        {
                            m_setter.DrawFloatSetting(Labels.VolumeSize, m_serializedProperties.VolumeSize, min: 0.05f, onValueChangedCallback: m_grid.OnVoxelSettingChanged);
                            m_setter.DrawFloatSetting(Labels.CellDensity, m_serializedProperties.CellDensity, min: 0.05f, onValueChangedCallback: m_grid.OnVoxelSettingChanged);
                        }
                    }
                }
            }

            if (m_isAlgorithmSettingsOpen = EditorGUILayout.Foldout(m_isAlgorithmSettingsOpen, Labels.AlgorithmSettings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawEnumSetting<IsosurfaceExtractionType>(Labels.IsosurfaceExtractionType, m_serializedProperties.IsosurfaceExtractionType, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                        if ((IsosurfaceExtractionType)m_serializedProperties.IsosurfaceExtractionType.enumValueIndex == IsosurfaceExtractionType.DualContouring)
                            m_setter.DrawFloatSetting(Labels.ConstrainToCellUnits, m_serializedProperties.ConstrainToCellUnits, min: 0f, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Normal Settings", EditorStyles.boldLabel);

                        m_setter.DrawFloatSetting(Labels.MaxAngleTolerance, m_serializedProperties.MaxAngleTolerance, min: 0f, max: 180f, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);
                        m_setter.DrawFloatSetting(Labels.VisualNormalSmoothing, m_serializedProperties.VisualNormalSmoothing, min: 1e-5f, max: 10f, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                        if ((IsosurfaceExtractionType)m_serializedProperties.IsosurfaceExtractionType.enumValueIndex == IsosurfaceExtractionType.DualContouring)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("QEF Settings", EditorStyles.boldLabel);

                            m_setter.DrawBoolSetting(Labels.OverrideQEFSettings, m_serializedProperties.OverrideQEFSettings, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                            if (m_serializedProperties.OverrideQEFSettings.boolValue)
                            {
                                m_setter.DrawIntSetting(Labels.QEFSweeps, m_serializedProperties.QEFSweeps, min: 1, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);
                                m_setter.DrawFloatSetting(Labels.QEFPseudoInverseThreshold, m_serializedProperties.QEFPseudoInverseThreshold, min: 1e-7f, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);
                            }

                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField("Nudge Settings", EditorStyles.boldLabel);

                            m_setter.DrawFloatSetting(Labels.NudgeVerticesToAverageNormalScalar, m_serializedProperties.NudgeVerticesToAverageNormalScalar, min: 0f, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);
                            m_setter.DrawFloatSetting(Labels.NudgeMaxMagnitude, m_serializedProperties.NudgeMaxMagnitude, min: 0f, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);
                        }

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Edge Intersection Settings", EditorStyles.boldLabel);

                        m_setter.DrawEnumSetting<EdgeIntersectionType>(Labels.EdgeIntersectionType, m_serializedProperties.EdgeIntersectionType, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                        if ((EdgeIntersectionType)m_serializedProperties.EdgeIntersectionType.enumValueIndex == EdgeIntersectionType.BinarySearch)
                            m_setter.DrawIntSetting(Labels.BinarySearchIterations, m_serializedProperties.BinarySearchIterations, min: 1, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                        EditorGUILayout.Space();
                        EditorGUILayout.LabelField("Gradient Descent Settings", EditorStyles.boldLabel);

                        m_setter.DrawBoolSetting(Labels.ApplyGradientDescent, m_serializedProperties.ApplyGradientDescent, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);

                        if (m_serializedProperties.ApplyGradientDescent.boolValue)
                            m_setter.DrawIntSetting(Labels.GradientDescentIterations, m_serializedProperties.GradientDescentIterations, min: 1, onValueChangedCallback: m_grid.OnAlgorithmSettingsChanged);
                    }
                }
            }

            GUI.enabled = true;

            m_setter.Update();
        }

        private bool m_isDeleteMode = false;
        private readonly List<Vector3Int> m_copiedCoordinates = new List<Vector3Int>();

        private void OnSceneGUI()
        {
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            
            foreach (Chunk chunk in m_grid.Chunks)
            {
                if (chunk.MeshGen)
                {
                    Handles.matrix = chunk.MeshGen.transform.localToWorldMatrix;
                    Handles.color = Color.black;
                    Handles.DrawWireCube(Vector3.zero, m_grid.VoxelSettings.Extents);
                }
            }

            Handles.matrix = m_grid.transform.localToWorldMatrix;

            Event e = Event.current;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.LeftControl)
                {
                    m_isDeleteMode = true;
                    m_clickedCoordinate = null;
                    m_hoveredCoordinate = null;
                }
            }
            else if (e.type == EventType.KeyUp)
            {
                m_isDeleteMode = false;
                m_clickedCoordinate = null;
                m_hoveredCoordinate = null;
            }
            else if (e.type == EventType.Repaint || e.type == EventType.Layout)
            {
                m_copiedCoordinates.Clear();
                m_copiedCoordinates.AddRange((m_isDeleteMode ? m_grid.GetAllOccupiedCoordinates() : m_grid.UnoccupiedAxisAlignedNeighbourCoordinates));
                    
                // first we need to find the closest
                UpdateHoveredCoordinate(m_copiedCoordinates);

                if (m_isDeleteMode)
                {
                    DrawCoordinateHandles(m_copiedCoordinates, Color.red, Color.red, Color.red.SetAlpha(0.4f), (Vector3Int v) => { m_grid.RemoveChunk(v); });
                }
                else
                {

                    DrawCoordinateHandles(m_copiedCoordinates, Color.green, Color.blue, Color.cyan.SetAlpha(0.4f), m_grid.AddChunk);
                }

            }
            else if (e.type == EventType.MouseDown && e.button == 0)
            {
                m_copiedCoordinates.Clear();
                m_copiedCoordinates.AddRange((m_isDeleteMode ? m_grid.GetAllOccupiedCoordinates() : m_grid.UnoccupiedAxisAlignedNeighbourCoordinates));

                UpdateHoveredCoordinate(m_copiedCoordinates);

                if (m_hoveredCoordinate.HasValue)
                    m_clickedCoordinate = m_hoveredCoordinate.Value;
                
                HandleUtility.Repaint();
            }
            else if (e.type == EventType.MouseUp && e.button == 0)
            {
                m_clickedCoordinate = null;

                Event.current.Use();

                HandleUtility.Repaint();
            }
            else if (e.type == EventType.MouseMove)
            {
                HandleUtility.Repaint();
            }
        }

        private void DrawCoordinateHandles(IEnumerable<Vector3Int> enumerable, Color clickedColour, Color hoveredColour, Color defaultColour, System.Action<Vector3Int> onClicked)
        {
            foreach (Vector3Int coordinate in enumerable)
            {
                Vector3 pos = m_grid.CoordinateToLocalPosition(coordinate);

                float size = HandleUtility.GetHandleSize(pos) * HANDLE_SIZE;
                float dist = HandleUtility.DistanceToCircle(pos, size);

                if (m_clickedCoordinate.HasValue && m_clickedCoordinate.Value == coordinate)
                {
                    Handles.color = clickedColour.SetAlpha(0.1f);
                    Handles.CubeHandleCap(0, pos, Quaternion.identity, m_grid.VoxelSettings.Extents.x, Event.current.type);

                    Handles.color = clickedColour;
                    Handles.DrawWireCube(pos, m_grid.VoxelSettings.Extents);

                    onClicked?.Invoke(coordinate);

                    m_clickedCoordinate = null;
                }
                else if (m_hoveredCoordinate.HasValue && m_hoveredCoordinate.Value == coordinate)
                {
                    Handles.color = hoveredColour.SetAlpha(0.1f);
                    Handles.CubeHandleCap(0, pos, Quaternion.identity, m_grid.VoxelSettings.Extents.x, Event.current.type);

                    Handles.color = hoveredColour;
                    Handles.DrawWireCube(pos, m_grid.VoxelSettings.Extents);
                }
                else
                {
                    Handles.color = defaultColour;
                }

                Handles.CubeHandleCap(0, pos, Quaternion.identity, size, Event.current.type);
            }
        }

        private void UpdateHoveredCoordinate(IEnumerable<Vector3Int> enumerable)
        {
            float leastDistToMouse = Mathf.Infinity;
            m_hoveredCoordinate = null;

            foreach (Vector3Int coordinate in enumerable)
            {
                Vector3 pos = m_grid.CoordinateToLocalPosition(coordinate);

                float size = HandleUtility.GetHandleSize(pos) * HANDLE_SIZE;
                float dist = HandleUtility.DistanceToCircle(pos, size);

                if (dist < leastDistToMouse && dist <= HOVER_DISTANCE)
                {
                    leastDistToMouse = dist;
                    m_hoveredCoordinate = coordinate;
                }
            }
        }
    }
}