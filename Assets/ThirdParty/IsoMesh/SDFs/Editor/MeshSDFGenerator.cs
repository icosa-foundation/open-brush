using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UnityEngine.Rendering;

namespace IsoMesh.Editor
{
    public class MeshSDFGenerator : EditorWindow
    {
        private const string ASSET_SAVE_PATH = "Assets/Data/SDFMeshes/";

        [SerializeField]
        private ComputeShader m_meshSampleComputeShader;

        [SerializeField]
        private ComputeShader m_tesselationComputeShader;

        [SerializeField]
        private Mesh m_mesh;

        private bool MissingMesh => m_mesh == null;

        [SerializeField]
        [Min(1)]
        private int m_size = 128;

        [SerializeField]
        private float m_padding = 0.2f;

        [SerializeField]
        private Vector3 m_translation = Vector3.zero;

        [SerializeField]
        private Vector3 m_rotation = Vector3.zero;

        [SerializeField]
        private Vector3 m_scale = Vector3.one;

        private Matrix4x4 ModelTransform => Matrix4x4.TRS(m_translation, Quaternion.Euler(m_rotation), m_scale);

        [SerializeField]
        private bool m_tesselateMesh = false;

        [SerializeField]
        private Mesh m_tessellatedMesh;

        [SerializeField]
        private int m_subdivisions = 1;

        [SerializeField]
        private float m_minimumEdgeLength = 0.15f;

        [SerializeField]
        private bool m_sampleUVs = true;

        [SerializeField]
        private bool m_autosaveOnComplete = false;

        private float[] m_samples;
        private float[] m_packedUVs;

        private Vector3 m_minBounds;
        private Vector3 m_maxBounds;

        private bool CanSave => !MissingMesh && !m_samples.IsNullOrEmpty();

        private int m_lastSubdivisionLevel = 0;

        private static MeshSDFGenerator m_window;
        private SerializedObject m_serializedObject;
        private SerializedProperties m_serializedProperties;

        private class SerializedProperties
        {
            public SerializedProperty Padding { get; }
            public SerializedProperty Translation { get; }
            public SerializedProperty Rotation { get; }
            public SerializedProperty Scale { get; }
            public SerializedProperty MinimumEdgeLength { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                Padding = serializedObject.FindProperty("m_padding");
                Translation = serializedObject.FindProperty("m_translation");
                Rotation = serializedObject.FindProperty("m_rotation");
                Scale = serializedObject.FindProperty("m_scale");
                MinimumEdgeLength = serializedObject.FindProperty("m_minimumEdgeLength");
            }
        }

        [MenuItem("Tools/Mesh to SDF")]
        public static void ShowWindow()
        {
            m_window = GetWindow<MeshSDFGenerator>("Mesh SDF Generator");
            m_window.m_serializedObject = new SerializedObject(m_window);
            m_window.m_serializedProperties = new SerializedProperties(m_window.m_serializedObject);
        }

        private void Generate()
        {
            m_tessellatedMesh = null;

            using (ComputeSession session = new ComputeSession(m_meshSampleComputeShader, m_tesselationComputeShader, m_size, m_padding, m_sampleUVs, ModelTransform))
            {
                System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();

                if (m_tesselateMesh)
                    session.DispatchWithTesselation(m_mesh, m_subdivisions, m_minimumEdgeLength, out m_samples, out m_packedUVs, out m_minBounds, out m_maxBounds, out m_tessellatedMesh);
                else
                    session.Dispatch(m_mesh, out m_samples, out m_packedUVs, out m_minBounds, out m_maxBounds);

                m_lastSubdivisionLevel = m_tesselateMesh ? m_subdivisions : 0;

                stopwatch.Stop();
                Debug.Log($"That took {stopwatch.Elapsed.ToString("g")}. [h/m/s/ms]");
            }

            if (m_autosaveOnComplete)
                Save();
        }

        private void Save()
        {
            if (!CanSave)
                return;

            SDFMeshAsset.Create(ASSET_SAVE_PATH, "SDFMesh_" + m_mesh.name, m_samples, m_packedUVs, m_lastSubdivisionLevel, m_size, m_padding, m_mesh, m_minBounds, m_maxBounds);
        }

        private bool m_transformBoxOpened = false;
        private UnityEditor.Editor m_inputMeshPreview;
        private UnityEditor.Editor m_tesselatedMeshPreview;
        private Vector2 m_scrollPos;

        private void OnGUI()
        {
            using (EditorGUILayout.ScrollViewScope scroll = new EditorGUILayout.ScrollViewScope(m_scrollPos))
            {
                m_scrollPos = scroll.scrollPosition;

                Mesh newMesh = (Mesh)EditorGUILayout.ObjectField("Mesh", m_mesh, typeof(Mesh), allowSceneObjects: false);

                if (m_mesh != newMesh)
                {
                    m_mesh = newMesh;

                    if (m_inputMeshPreview != null)
                    {
                        DestroyImmediate(m_inputMeshPreview);
                        m_inputMeshPreview = null;
                    }
                }

                if (m_mesh != null)
                {
                    if (m_inputMeshPreview == null)
                        m_inputMeshPreview = UnityEditor.Editor.CreateEditor(m_mesh);

                    m_inputMeshPreview.DrawPreview(GUILayoutUtility.GetRect(200, 200));
                }

                m_size = Mathf.Max(1, EditorGUILayout.IntField("Size", m_size));
                m_padding = Mathf.Max(0f, EditorGUILayout.FloatField("Padding", m_padding));

                //this.DrawIntField("Size", ref m_size, min: 1);
                ////this.DrawFloatField("Padding", ref m_padding, out _, min: 0f);
                //this.DrawFloatField("Padding", m_serializedProperties.Padding, out m_padding, min: 0f);

                m_transformBoxOpened = EditorGUILayout.Foldout(m_transformBoxOpened, "Transform", true);

                if (m_transformBoxOpened)
                {
                    using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                        {
                            m_translation = EditorGUILayout.Vector3Field("Translation", m_translation);
                            m_rotation = EditorGUILayout.Vector3Field("Rotation", m_rotation);
                            m_scale = EditorGUILayout.Vector3Field("Scale", m_scale);
                        }
                    }
                }

                m_tesselateMesh = EditorGUILayout.Toggle("Tessellate Mesh First", m_tesselateMesh);
                m_sampleUVs = EditorGUILayout.Toggle("Sample UVs", m_tesselateMesh);

                if (m_tesselateMesh)
                {
                    using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                        {
                            if (m_tessellatedMesh != null)
                            {
                                if (m_tesselatedMeshPreview != null && m_tesselatedMeshPreview.serializedObject.targetObject as Mesh != m_tessellatedMesh)
                                {
                                    DestroyImmediate(m_tesselatedMeshPreview);
                                    m_tesselatedMeshPreview = null;
                                }

                                if (m_tesselatedMeshPreview == null)
                                    m_tesselatedMeshPreview = UnityEditor.Editor.CreateEditor(m_tessellatedMesh);

                                m_tesselatedMeshPreview.DrawPreview(GUILayoutUtility.GetRect(200, 200));
                            }

                            m_subdivisions = Mathf.Clamp(EditorGUILayout.IntField("Subdivisions", m_subdivisions), 0, 4);
                            m_minimumEdgeLength = Mathf.Max(EditorGUILayout.FloatField("Minimum Edge Length", m_minimumEdgeLength), 0f);


                            //this.DrawIntField("Subdivisions", ref m_subdivisions, min: 0, max: 4);
                            ////this.DrawFloatField("Minimum Edge Length", ref m_minimumEdgeLength, min: 0);
                            //this.DrawFloatField("Minimum Edge Length", m_serializedProperties.MinimumEdgeLength, out m_minimumEdgeLength, min: 0f);
                        }
                    }
                }

                m_autosaveOnComplete = EditorGUILayout.Toggle("Autosave On Complete", m_autosaveOnComplete);
                //this.DrawBoolField("Autosave On Complete", ref m_autosaveOnComplete);

                GUI.enabled = m_mesh != null;

                if (GUI.Button(GUILayoutUtility.GetRect(200, 80), "Generate"))
                    Generate();

                if (GUI.Button(GUILayoutUtility.GetRect(200, 80), "Save"))
                    Save();

                GUI.enabled = true;
            }
        }

        /// <summary>
        /// This class was meant to make my life easier while dealing with compute shaders.
        /// 
        /// At one point, I frankensteined 2 different classes together which might be why it's a little confusing! basically there are 2 entry points,
        /// you can dispatch with or without a step of mesh tesselation first.
        /// </summary>
        public class ComputeSession : IDisposable
        {
            public static class Properties
            {
                public static int InputVertices_StructuredBuffer = Shader.PropertyToID("_InputVertices");
                public static int InputNormals_StructuredBuffer = Shader.PropertyToID("_InputNormals");
                public static int InputTriangles_StructuredBuffer = Shader.PropertyToID("_InputTriangles");
                public static int InputTangents_StructuredBuffer = Shader.PropertyToID("_InputTangents");
                public static int InputUVs_StructuredBuffer = Shader.PropertyToID("_InputUVs");

                public static int OutputVertices_StructuredBuffer = Shader.PropertyToID("_OutputVertices");
                public static int OutputNormals_StructuredBuffer = Shader.PropertyToID("_OutputNormals");
                public static int OutputTriangles_StructuredBuffer = Shader.PropertyToID("_OutputTriangles");
                public static int OutputTangents_StructuredBuffer = Shader.PropertyToID("_OutputTangents");
                public static int OutputUVs_StructuredBuffer = Shader.PropertyToID("_OutputUVs");

                public static int TriangleCount_Int = Shader.PropertyToID("_TriangleCount");
                public static int VertexCount_Int = Shader.PropertyToID("_VertexCount");

                public static int MinimumEdgeLength_Float = Shader.PropertyToID("_MinimumEdgeLength");

                public static int MinBounds_Vector3 = Shader.PropertyToID("_MinBounds");
                public static int MaxBounds_Vector3 = Shader.PropertyToID("_MaxBounds");
                public static int Padding_Float = Shader.PropertyToID("_Padding");
                public static int Size_Int = Shader.PropertyToID("_Size");
                public static int ModelTransformMatrix_Matrix = Shader.PropertyToID("_ModelTransformMatrix");

                public static int Samples_RWStructuredBuffer = Shader.PropertyToID("_Samples");
                public static int PackedUVs_RWStructuredBuffer = Shader.PropertyToID("_PackedUVs");
                public static int MeshBounds_RWStructuredBuffer = Shader.PropertyToID("_BoundsBuffer");
            }

            private ComputeBuffer InputVerticesBuffer;
            private ComputeBuffer InputTrianglesBuffer;
            private ComputeBuffer InputNormalsBuffer;
            private ComputeBuffer InputTangentsBuffer;
            private ComputeBuffer InputUVsBuffer;

            private ComputeBuffer OutputVerticesBuffer;
            private ComputeBuffer OutputTrianglesBuffer;
            private ComputeBuffer OutputNormalsBuffer;
            private ComputeBuffer OutputTangentsBuffer;
            private ComputeBuffer OutputUVsBuffer;

            private ComputeBuffer SamplesBuffer { get; }
            private ComputeBuffer PackedUVsBuffer { get; }
            private ComputeBuffer BoundsBuffer { get; }

            public const string ComputeBoundsKernelName = "CS_ComputeMeshBounds";
            public const string GetTextureWholeKernelName = "CS_SampleMeshDistances";
            public const string TessellateKernelName = "CS_Tessellate";
            public const string PreprocessMeshKernelName = "CS_PreprocessMesh";

            public const string WriteUVsKeyword = "WRITE_UVS";

            //public int GetTextureSliceKernel { get; }
            public int GetTextureWholeKernel { get; }
            public int ComputeBoundsKernel { get; }
            public int TessellateKernel { get; }
            public int PreprocessMeshKernel { get; }

            public ComputeShader MeshSampleComputeShader { get; }
            public ComputeShader TessellationComputeShader { get; }

            private bool m_sampleUVs;

            private int[] m_triangles;
            private Vector3[] m_vertices;
            private Vector3[] m_normals;
            private Vector4[] m_tangents;
            private Vector2[] m_uvs;

            private int m_size;
            private float m_padding;
            private Matrix4x4 m_transform;
            private float[] m_samples;
            private float[] m_packedUVs;

            public int Dimensions => m_size * m_size * m_size;

            public ComputeSession(ComputeShader meshSampleComputeShader, ComputeShader tesselationComputeShader, int size, float padding, bool sampleUVs, Matrix4x4 transform)
            {
                MeshSampleComputeShader = Instantiate(meshSampleComputeShader);
                TessellationComputeShader = Instantiate(tesselationComputeShader);

                GetTextureWholeKernel = MeshSampleComputeShader.FindKernel(GetTextureWholeKernelName);
                ComputeBoundsKernel = MeshSampleComputeShader.FindKernel(ComputeBoundsKernelName);
                TessellateKernel = TessellationComputeShader.FindKernel(TessellateKernelName);
                PreprocessMeshKernel = TessellationComputeShader.FindKernel(PreprocessMeshKernelName);

                m_sampleUVs = sampleUVs;
                m_size = size;
                m_padding = padding;
                m_transform = transform;
                m_samples = new float[Dimensions];
                m_packedUVs = new float[Dimensions];

                BoundsBuffer = new ComputeBuffer(6, sizeof(int));
                MeshSampleComputeShader.SetBuffer(ComputeBoundsKernel, Properties.MeshBounds_RWStructuredBuffer, BoundsBuffer);

                SamplesBuffer = new ComputeBuffer(Dimensions, sizeof(float));
                MeshSampleComputeShader.SetBuffer(GetTextureWholeKernel, Properties.Samples_RWStructuredBuffer, SamplesBuffer);

                PackedUVsBuffer = new ComputeBuffer(Dimensions, sizeof(float));
                MeshSampleComputeShader.SetBuffer(GetTextureWholeKernel, Properties.PackedUVs_RWStructuredBuffer, PackedUVsBuffer);

                MeshSampleComputeShader.SetInt(Properties.Size_Int, m_size);
                MeshSampleComputeShader.SetFloat(Properties.Padding_Float, m_padding);
                MeshSampleComputeShader.SetMatrix(Properties.ModelTransformMatrix_Matrix, m_transform);
            }

            public void Dispose()
            {
                BoundsBuffer?.Dispose();
                SamplesBuffer?.Dispose();
                PackedUVsBuffer?.Dispose();

                InputVerticesBuffer?.Dispose();
                InputTrianglesBuffer?.Dispose();
                InputNormalsBuffer?.Dispose();
                InputTangentsBuffer?.Dispose();
                InputUVsBuffer?.Dispose();

                OutputVerticesBuffer?.Dispose();
                OutputTrianglesBuffer?.Dispose();
                OutputNormalsBuffer?.Dispose();
                OutputTangentsBuffer?.Dispose();
                OutputUVsBuffer?.Dispose();

                DestroyImmediate(MeshSampleComputeShader);
                DestroyImmediate(TessellationComputeShader);
            }

            public void Dispatch(Mesh mesh, out float[] samples, out float[] packedUVs, out Vector3 minBounds, out Vector3 maxBounds)
            {
                m_triangles = mesh.triangles;
                m_vertices = mesh.vertices;
                m_normals = mesh.normals;
                m_uvs = mesh.uv;

                InputTrianglesBuffer = new ComputeBuffer(m_triangles.Length, sizeof(int), ComputeBufferType.Structured);
                InputVerticesBuffer = new ComputeBuffer(m_vertices.Length, sizeof(float) * 3, ComputeBufferType.Structured);
                InputNormalsBuffer = new ComputeBuffer(m_normals.Length, sizeof(float) * 3, ComputeBufferType.Structured);

                InputTrianglesBuffer.SetData(m_triangles);
                InputNormalsBuffer.SetData(m_normals);
                InputVerticesBuffer.SetData(m_vertices);

                MeshSampleComputeShader.SetBuffer(ComputeBoundsKernel, Properties.InputTriangles_StructuredBuffer, InputTrianglesBuffer);
                MeshSampleComputeShader.SetBuffer(ComputeBoundsKernel, Properties.InputVertices_StructuredBuffer, InputVerticesBuffer);

                MeshSampleComputeShader.SetBuffer(GetTextureWholeKernel, Properties.InputTriangles_StructuredBuffer, InputTrianglesBuffer);
                MeshSampleComputeShader.SetBuffer(GetTextureWholeKernel, Properties.InputNormals_StructuredBuffer, InputNormalsBuffer);
                MeshSampleComputeShader.SetBuffer(GetTextureWholeKernel, Properties.InputVertices_StructuredBuffer, InputVerticesBuffer);

                bool hasUVs = !m_uvs.IsNullOrEmpty();
                if (m_sampleUVs && hasUVs)
                {
                    MeshSampleComputeShader.EnableKeyword(WriteUVsKeyword);
                    InputUVsBuffer = new ComputeBuffer(m_uvs.Length, sizeof(float) * 2, ComputeBufferType.Structured);
                    InputUVsBuffer.SetData(m_uvs);
                    MeshSampleComputeShader.SetBuffer(GetTextureWholeKernel, Properties.InputUVs_StructuredBuffer, InputUVsBuffer);
                }
                else
                {
                    MeshSampleComputeShader.DisableKeyword(WriteUVsKeyword);
                }

                MeshSampleComputeShader.SetInt(Properties.TriangleCount_Int, m_triangles.Length);
                MeshSampleComputeShader.SetInt(Properties.VertexCount_Int, m_vertices.Length);

                RunBoundsPhase(mesh, out minBounds, out maxBounds);
                RunSamplePhase(hasUVs, out samples, out packedUVs, minBounds, maxBounds);
            }

            public void DispatchWithTesselation(Mesh mesh, int subdivisions, float minimumEdgeLength, out float[] samples, out float[] packedUVs, out Vector3 minBounds, out Vector3 maxBounds, out Mesh tessellatedMesh)
            {
                m_triangles = mesh.triangles;
                m_vertices = mesh.vertices;
                m_normals = mesh.normals;
                m_tangents = mesh.tangents;
                m_uvs = mesh.uv;

                tessellatedMesh = RunTesselationPhase(mesh, subdivisions, minimumEdgeLength);
                Dispatch(tessellatedMesh, out samples, out packedUVs, out minBounds, out maxBounds);
            }

            private void RunBoundsPhase(Mesh mesh, out Vector3 minBounds, out Vector3 maxBounds)
            {
                int[] meshBounds = new int[6];
                BoundsBuffer.SetData(meshBounds);
                MeshSampleComputeShader.Dispatch(ComputeBoundsKernel, Mathf.CeilToInt(mesh.triangles.Length / 64f), 1, 1);
                BoundsBuffer.GetData(meshBounds);

                const float packingMultiplier = 1000f;

                minBounds = new Vector3(meshBounds[0] / packingMultiplier, meshBounds[1] / packingMultiplier, meshBounds[2] / packingMultiplier);
                maxBounds = new Vector3(meshBounds[3] / packingMultiplier, meshBounds[4] / packingMultiplier, meshBounds[5] / packingMultiplier);

                minBounds -= m_padding * Vector3.one;
                maxBounds += m_padding * Vector3.one;
            }

            /// <summary>
            /// Note: I chose to use buffers instead of writing to texture 3ds directly on the GPU because for some reason it's
            /// stupidly complicated to write to a texture3d on the gpu and then get that data back to the cpu for serialization.
            /// </summary>
            private void RunSamplePhase(bool hasUVs, out float[] samples, out float[] packedUVs, Vector3 minBounds, Vector3 maxBounds)
            {
                MeshSampleComputeShader.SetVector(Properties.MinBounds_Vector3, minBounds);
                MeshSampleComputeShader.SetVector(Properties.MaxBounds_Vector3, maxBounds);

                int threadGroups = Mathf.CeilToInt(m_size / 8f);
                MeshSampleComputeShader.Dispatch(GetTextureWholeKernel, threadGroups, threadGroups, threadGroups);

                SamplesBuffer.GetData(m_samples);
                samples = m_samples;

                if (hasUVs)
                {
                    PackedUVsBuffer.GetData(m_packedUVs);
                    packedUVs = m_packedUVs;
                }
                else
                {
                    packedUVs = null;
                }
            }

            public Mesh RunTesselationPhase(Mesh inputMesh, int subdivisions, float minimumEdgeLength)
            {
                // triangle count is the big number here for dispatch sizes, and the final mesh
                // will have (triangle count * 4^subdivisions) for triangles, vertices, and normals
                int inputTriangleCount = m_triangles.Length;

                Preprocess(inputTriangleCount);
                Tessellate(inputTriangleCount, subdivisions, minimumEdgeLength);

                int finalOutputSize = inputTriangleCount * (int)Mathf.Pow(4, subdivisions);

                Vector3[] outputVertices = new Vector3[finalOutputSize];
                Vector3[] outputNormals = new Vector3[finalOutputSize];
                Vector4[] outputTangents = new Vector4[finalOutputSize];
                Vector2[] outputUVs = new Vector2[finalOutputSize];
                int[] outputTriangles = new int[finalOutputSize];

                OutputVerticesBuffer.GetData(outputVertices);
                OutputNormalsBuffer.GetData(outputNormals);
                OutputTangentsBuffer.GetData(outputTangents);
                OutputTrianglesBuffer.GetData(outputTriangles);
                OutputUVsBuffer.GetData(outputUVs);

                return new Mesh
                {
                    indexFormat = IndexFormat.UInt32,
                    vertices = outputVertices,
                    normals = outputNormals,
                    tangents = outputTangents,
                    uv = outputUVs,
                    triangles = outputTriangles
                };
            }

            private void Preprocess(int triangleCount)
            {
                void SetInputPreprocess<T>(T[] array, int stride, int nameID, ref ComputeBuffer buffer)
                {
                    buffer = new ComputeBuffer(array.Length, stride, ComputeBufferType.Structured);
                    buffer.SetData(array);
                    TessellationComputeShader.SetBuffer(PreprocessMeshKernel, nameID, buffer);
                }

                void SetOutputPreprocess(int stride, int nameID, ref ComputeBuffer buffer)
                {
                    buffer = new ComputeBuffer(triangleCount, stride, ComputeBufferType.Structured);
                    TessellationComputeShader.SetBuffer(PreprocessMeshKernel, nameID, buffer);
                }

                SetInputPreprocess(m_vertices, sizeof(float) * 3, Properties.InputVertices_StructuredBuffer, ref InputVerticesBuffer);
                SetInputPreprocess(m_normals, sizeof(float) * 3, Properties.InputNormals_StructuredBuffer, ref InputNormalsBuffer);
                SetInputPreprocess(m_tangents, sizeof(float) * 4, Properties.InputTangents_StructuredBuffer, ref InputTangentsBuffer);
                SetInputPreprocess(m_uvs, sizeof(float) * 2, Properties.InputUVs_StructuredBuffer, ref InputUVsBuffer);
                SetInputPreprocess(m_triangles, sizeof(int), Properties.InputTriangles_StructuredBuffer, ref InputTrianglesBuffer);

                SetOutputPreprocess(sizeof(float) * 3, Properties.OutputVertices_StructuredBuffer, ref OutputVerticesBuffer);
                SetOutputPreprocess(sizeof(float) * 3, Properties.OutputNormals_StructuredBuffer, ref OutputNormalsBuffer);
                SetOutputPreprocess(sizeof(float) * 4, Properties.OutputTangents_StructuredBuffer, ref OutputTangentsBuffer);
                SetOutputPreprocess(sizeof(float) * 2, Properties.OutputUVs_StructuredBuffer, ref OutputUVsBuffer);
                SetOutputPreprocess(sizeof(int), Properties.OutputTriangles_StructuredBuffer, ref OutputTrianglesBuffer);

                TessellationComputeShader.SetInt(Properties.TriangleCount_Int, triangleCount);

                int threadGroupX = Mathf.CeilToInt((triangleCount / 3f) / 64f);

                TessellationComputeShader.Dispatch(PreprocessMeshKernel, threadGroupX, 1, 1);
            }

            private void Tessellate(int triangles, int subdivisions, float minimumEdgeLength)
            {
                TessellationComputeShader.SetFloat(Properties.MinimumEdgeLength_Float, minimumEdgeLength);

                for (int i = 0; i < subdivisions; i++)
                {
                    int inputTriangles = triangles;
                    int outputTriangles = triangles * 4;

                    // output from preprocess goes to input of tessellate
                    TessellationComputeShader.SetBuffer(TessellateKernel, Properties.InputVertices_StructuredBuffer, OutputVerticesBuffer);
                    TessellationComputeShader.SetBuffer(TessellateKernel, Properties.InputNormals_StructuredBuffer, OutputNormalsBuffer);
                    TessellationComputeShader.SetBuffer(TessellateKernel, Properties.InputTangents_StructuredBuffer, OutputTangentsBuffer);
                    TessellationComputeShader.SetBuffer(TessellateKernel, Properties.InputUVs_StructuredBuffer, OutputUVsBuffer);
                    TessellationComputeShader.SetBuffer(TessellateKernel, Properties.InputTriangles_StructuredBuffer, OutputTrianglesBuffer);

                    void SetOutputTessellate(int stride, int nameID, ref ComputeBuffer buffer)
                    {
                        buffer = new ComputeBuffer(outputTriangles, stride, ComputeBufferType.Structured);
                        TessellationComputeShader.SetBuffer(TessellateKernel, nameID, buffer);
                    }

                    ComputeBuffer oldOutputVertices = OutputVerticesBuffer;
                    ComputeBuffer oldOutputNormals = OutputNormalsBuffer;
                    ComputeBuffer oldOutputTangents = OutputTangentsBuffer;
                    ComputeBuffer oldOutputUVs = OutputUVsBuffer;
                    ComputeBuffer oldOutputTriangles = OutputTrianglesBuffer;

                    SetOutputTessellate(sizeof(float) * 3, Properties.OutputVertices_StructuredBuffer, ref OutputVerticesBuffer);
                    SetOutputTessellate(sizeof(float) * 3, Properties.OutputNormals_StructuredBuffer, ref OutputNormalsBuffer);
                    SetOutputTessellate(sizeof(float) * 4, Properties.OutputTangents_StructuredBuffer, ref OutputTangentsBuffer);
                    SetOutputTessellate(sizeof(float) * 2, Properties.OutputUVs_StructuredBuffer, ref OutputUVsBuffer);
                    SetOutputTessellate(sizeof(int), Properties.OutputTriangles_StructuredBuffer, ref OutputTrianglesBuffer);

                    TessellationComputeShader.SetInt(Properties.TriangleCount_Int, inputTriangles);

                    int threadGroupX = Mathf.CeilToInt((inputTriangles / 3f) / 64f);
                    TessellationComputeShader.Dispatch(TessellateKernel, threadGroupX, 1, 1);

                    oldOutputVertices?.Dispose();
                    oldOutputNormals?.Dispose();
                    oldOutputTangents?.Dispose();
                    oldOutputUVs?.Dispose();
                    oldOutputTriangles?.Dispose();

                    triangles = outputTriangles;
                }
            }
        }
    }
}