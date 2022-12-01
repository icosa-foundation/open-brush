using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace IsoMesh
{
    [System.Serializable]
    public struct SDFMaterial
    {
        public const float MIN_SMOOTHING = 0.0001f;

        [SerializeField]
        private MaterialType m_type;
        public MaterialType Type => m_type;

        [SerializeField]
        private Texture2D m_texture;
        public Texture2D Texture => m_texture;

        [SerializeField]
        [ColorUsage(showAlpha: false)]
        private Color m_colour;
        public Color Colour => m_colour;

        [SerializeField]
        [ColorUsage(showAlpha: false, hdr: true)]
        private Color m_emission;
        public Color Emission => m_emission;

        [SerializeField]
        private float m_materialSmoothing;
        public float MaterialSmoothing => m_materialSmoothing;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_metallic;
        public float Metallic => m_metallic;

        [SerializeField]
        [Range(0f, 1f)]
        private float m_smoothness;
        public float Smoothness => m_smoothness;

        [SerializeField]
        [ColorUsage(showAlpha: false)]
        private Color m_subsurfaceColour;
        public Color SubsurfaceColour => m_subsurfaceColour;

        [SerializeField]
        [Min(0f)]
        private float m_subsurfaceScatteringPower;
        public float SubsurfaceScatteringPower => m_subsurfaceScatteringPower;

        public enum MaterialType
        {
            None,
            Colour,
            Texture
        }

        public SDFMaterial(Color mainCol, Color emission, float metallic, float smoothness, Color subsurfaceColour, float subsurfaceScatteringPower, float materialSmoothing)
        {
            m_type = MaterialType.Colour;
            m_texture = default;
            m_colour = mainCol;
            m_emission = emission;
            m_metallic = metallic;
            m_smoothness = smoothness;
            m_subsurfaceColour = subsurfaceColour;
            m_subsurfaceScatteringPower = subsurfaceScatteringPower;
            m_materialSmoothing = materialSmoothing;
        }
    }

    [System.Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct SDFMaterialGPU
    {
        public static int Stride => sizeof(float) * 14 + sizeof(int) * 2;

        public int MaterialType;
        public int TextureIndex;
        public Vector3 Colour;
        public Vector3 Emission;
        public float Metallic;
        public float Smoothness;
        public float Thickness;
        public Vector3 SubsurfaceColour;
        public float SubsurfaceScatteringPower;
        public float MaterialSmoothing;

        public SDFMaterialGPU(SDFMaterial material)
        {
            MaterialType = (int)material.Type;
            TextureIndex = 0;
            Colour = (Vector4)material.Colour;
            Emission = (Vector4)material.Emission;
            Metallic = Mathf.Clamp01(material.Metallic);
            Smoothness = Mathf.Clamp01(material.Smoothness);
            Thickness = 0f;
            SubsurfaceColour = (Vector4)material.SubsurfaceColour;
            SubsurfaceScatteringPower = material.SubsurfaceScatteringPower;//Mathf.Lerp(5f, 0f, material.SubsurfaceScatteringPower);
            MaterialSmoothing = material.MaterialSmoothing;
        }
    }
}