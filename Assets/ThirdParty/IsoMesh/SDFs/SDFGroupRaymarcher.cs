using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IsoMesh
{
    /// <summary>
    /// This class generates a cube mesh, gives it a raymarching material, and passes the contents of the SDF group into that material.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    [ExecuteInEditMode]
    public class SDFGroupRaymarcher : MonoBehaviour, ISDFGroupComponent
    {
        #region Fields and Properties

        private static class MaterialProperties
        {
            public static int Settings_StructuredBuffer = Shader.PropertyToID("_Settings");

            public static readonly int SDFData_StructuredBuffer = Shader.PropertyToID("_SDFData");
            public static readonly int SDFMaterials_StructuredBuffer = Shader.PropertyToID("_SDFMaterials");
            public static readonly int SDFDataCount_Int = Shader.PropertyToID("_SDFDataCount");

            public static int Diffuse_Colour = Shader.PropertyToID("_DiffuseColour");
            public static int Ambient_Colour = Shader.PropertyToID("_AmbientColour");
            public static int GlossPower_Float = Shader.PropertyToID("_GlossPower");
            public static int GlossMultiplier_Float = Shader.PropertyToID("_GlossMultiplier");
        }

        [SerializeField]
        private Material m_material;
        private Material m_materialInstance;

        [SerializeField]
        private SDFGroup m_group;
        public SDFGroup Group => m_group;

        [SerializeField]
        [HideInInspector]
        private MeshRenderer m_renderer;
        public MeshRenderer Renderer => m_renderer;

        [SerializeField]
        [HideInInspector]
        private MeshFilter m_filter;

        [SerializeField]
        private Vector3 m_size = new Vector3(10f, 10f, 10f);
        public Vector3 Size => m_size;

        [SerializeField]
        private Color m_diffuseColour = Color.red;
        public Color DiffuseColour => m_diffuseColour;

        [SerializeField]
        private Color m_ambientColour = Color.black;
        public Color AmbientColour => m_ambientColour;

        [SerializeField]
        private float m_glossPower = 0.1f;
        public float GlossPower => m_glossPower;

        [SerializeField]
        private float m_glossMultiplier = 0.5f;
        public float GlossMultiplier => m_glossMultiplier;

        private MaterialPropertyBlock m_propertyBlock;

        #endregion

        #region MonoBehaviour Callbacks

        private void Reset()
        {
            m_group = GetComponent<SDFGroup>();

            if (!m_group)
                GetComponentInParent<SDFGroup>();

            m_renderer = gameObject.GetOrAddComponent<MeshRenderer>();
            m_filter = gameObject.GetOrAddComponent<MeshFilter>();
            UpdateCubeMesh();

            if (m_materialInstance == null)
                m_materialInstance = new Material(m_material);

            m_renderer.material = m_materialInstance;
        }

        private void OnEnable()
        {
            if (!m_group)
                GetComponentInParent<SDFGroup>();

            if (m_materialInstance == null)
                m_materialInstance = new Material(m_material);

            OnVisualsChanged();
        }

        private void OnDisable()
        {
            DestroyImmediate(m_materialInstance);
        }

        #endregion

        #region Setters

        public void SetSize(Vector3 size)
        {
            m_size = size;
            UpdateCubeMesh();
        }

        public void SetDiffuseColour(Color diffuseColour)
        {
            m_diffuseColour = diffuseColour;
            OnVisualsChanged();
        }

        public void SetAmbientColour(Color ambientColour)
        {
            m_ambientColour = ambientColour;
            OnVisualsChanged();
        }

        public void SetGlossPower(float glossPower)
        {
            m_glossPower = glossPower;
            OnVisualsChanged();
        }

        public void SetGlossMultiplier(float glossMultiplier)
        {
            m_glossMultiplier = glossMultiplier;
            OnVisualsChanged();
        }

        #endregion

        #region SDF Group Methods

        // this is more of a passive process, we don't need to 'run' anything as it's all done on the gpu side
        public void Run() { }

        public void UpdateSettingsBuffer(ComputeBuffer computeBuffer)
        {
            if (m_propertyBlock == null)
                m_propertyBlock = new MaterialPropertyBlock();

            m_propertyBlock.SetBuffer(MaterialProperties.Settings_StructuredBuffer, computeBuffer);

            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

        public void UpdateDataBuffer(ComputeBuffer computeBuffer, ComputeBuffer materialBuffer, int count)
        {
            if (m_propertyBlock == null)
                m_propertyBlock = new MaterialPropertyBlock();

            if (computeBuffer != null && computeBuffer.IsValid())
                m_propertyBlock.SetBuffer(MaterialProperties.SDFData_StructuredBuffer, computeBuffer);

            if (materialBuffer != null && materialBuffer.IsValid())
                m_propertyBlock.SetBuffer(MaterialProperties.SDFMaterials_StructuredBuffer, materialBuffer);

            m_propertyBlock.SetInt(MaterialProperties.SDFDataCount_Int, count);

            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

        public void OnEmpty() => m_renderer.enabled = false;

        public void OnNotEmpty() => m_renderer.enabled = true;

        #endregion

        #region Helper Methods

        public void OnVisualsChanged()
        {
            if (m_propertyBlock == null)
                m_propertyBlock = new MaterialPropertyBlock();

            m_propertyBlock.SetColor(MaterialProperties.Diffuse_Colour, m_diffuseColour);
            m_propertyBlock.SetColor(MaterialProperties.Ambient_Colour, m_ambientColour);
            m_propertyBlock.SetFloat(MaterialProperties.GlossPower_Float, m_glossPower);
            m_propertyBlock.SetFloat(MaterialProperties.GlossMultiplier_Float, m_glossMultiplier);

            m_renderer.SetPropertyBlock(m_propertyBlock);
        }

        public void UpdateCubeMesh()
        {
            Vector3 size = m_size * 0.5f;

            Vector3[] vertices = {
            new Vector3 (0, 0, 0),
            new Vector3 (1, 0, 0),
            new Vector3 (1, 1, 0),
            new Vector3 (0, 1, 0),
            new Vector3 (0, 1, 1),
            new Vector3 (1, 1, 1),
            new Vector3 (1, 0, 1),
            new Vector3 (0, 0, 1),
        };

            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = Utils.Remap(Vector3.zero, Vector3.one, -size, size, vertices[i]);

            int[] triangles = {
            0, 2, 1, //face front
	        0, 3, 2,
            2, 3, 4, //face top
	        2, 4, 5,
            1, 2, 5, //face right
	        1, 5, 6,
            0, 7, 4, //face left
	        0, 4, 3,
            5, 4, 7, //face back
	        5, 7, 6,
            0, 6, 7, //face bottom
	        0, 1, 6
        };

            Mesh mesh = new Mesh
            {
                vertices = vertices,
                triangles = triangles
            };

            mesh.Optimize();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            m_filter.mesh = mesh;
        }

        #endregion
    }
}
