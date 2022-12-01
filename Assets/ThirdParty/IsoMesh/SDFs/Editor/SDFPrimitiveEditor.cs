using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFPrimitive))]
    [CanEditMultipleObjects]
    public class SDFPrimitiveEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent Type = new GUIContent("Type", "Which primitive this object represents.");
            public static GUIContent Operation = new GUIContent("Operation", "How this primitive is combined with the previous SDF objects in the hierarchy.");
            public static GUIContent Flip = new GUIContent("Flip", "Turn this object inside out.");
            public static GUIContent Bounds = new GUIContent("Bounds", "The xyz bounds of the cuboid.");
            public static GUIContent Roundedness = new GUIContent("Roundedness", "How curved are the cuboids corners and edges.");
            public static GUIContent Radius = new GUIContent("Radius", "The radius of the sphere.");
            public static GUIContent CylinderRadius = new GUIContent("Radius", "The radius of the cylinder.");
            public static GUIContent CylinderLength = new GUIContent("Length", "The length of the cylinder.");
            public static GUIContent MajorRadius = new GUIContent("Major Radius", "The radius of the whole torus.");
            public static GUIContent MinorRadius = new GUIContent("Minor Radius", "The radius of the tube of the torus.");
            public static GUIContent Thickness = new GUIContent("Thickness", "The thickness of the frame.");
            public static GUIContent Smoothing = new GUIContent("Smoothing", "How smoothly this sdf blends with the previous SDFs.");

            public static GUIContent Material = new GUIContent("Material", "The visual properties of this SDF object.");
            public static GUIContent MaterialSmoothing = new GUIContent("Material Smoothing", "How sharply this material is combined with other SDF objects.");
            public static GUIContent MaterialType = new GUIContent("Type", "Whether this object has no effect on the group colours, just a pure colour, or is fully textured.");
            public static GUIContent MaterialTexture = new GUIContent("Texture", "The texture which will be applied to this object via procedural UVs.");
            public static GUIContent Colour = new GUIContent("Colour", "Colour of this primitive.");
            public static GUIContent Emission = new GUIContent("Emission", "Emission of this primitive, must be used alongside post processing (bloom).");
            public static GUIContent Metallic = new GUIContent("Metallic", "Metallicity of this object's material.");
            public static GUIContent Smoothness = new GUIContent("Smoothness", "Smoothness of this object's material.");
            public static GUIContent SubsurfaceColour = new GUIContent("Subsurface Colour", "Colour of the inside of this primitive, used by subsurface scattering.");
            public static GUIContent SubsurfaceScatteringPower = new GUIContent("Subsurface Scattering Power", "Strength of the subsurface scattering effect.");
        }

        private class SerializedProperties
        {
            public SerializedProperty Type { get; }
            public SerializedProperty Data { get; }
            public SerializedProperty Operation { get; }
            public SerializedProperty Flip { get; }
            public SerializedProperty Smoothing { get; }

            public SerializedProperty Material { get; }
            public SerializedProperty MaterialType { get; }
            public SerializedProperty MaterialTexture { get; }
            public SerializedProperty Colour { get; }
            public SerializedProperty Emission { get; }
            public SerializedProperty MaterialSmoothing { get; }
            public SerializedProperty Metallic { get; }
            public SerializedProperty Smoothness { get; }
            public SerializedProperty SubsurfaceColour { get; }
            public SerializedProperty SubsurfaceScatteringPower { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                Type = serializedObject.FindProperty("m_type");
                Data = serializedObject.FindProperty("m_data");
                Operation = serializedObject.FindProperty("m_operation");
                Flip = serializedObject.FindProperty("m_flip");
                Smoothing = serializedObject.FindProperty("m_smoothing");

                Material = serializedObject.FindProperty("m_material");
                MaterialType = Material.FindPropertyRelative("m_type");
                MaterialTexture = Material.FindPropertyRelative("m_texture");
                Colour = Material.FindPropertyRelative("m_colour");
                Emission = Material.FindPropertyRelative("m_emission");
                MaterialSmoothing = Material.FindPropertyRelative("m_materialSmoothing");
                Metallic = Material.FindPropertyRelative("m_metallic");
                Smoothness = Material.FindPropertyRelative("m_smoothness");
                SubsurfaceColour = Material.FindPropertyRelative("m_subsurfaceColour");
                SubsurfaceScatteringPower = Material.FindPropertyRelative("m_subsurfaceScatteringPower");
            }
        }


        private SerializedProperties m_serializedProperties;
        private SDFPrimitive m_sdfPrimitive;
        private SerializedPropertySetter m_setter;

        private bool m_isMaterialOpen = true;

        private void OnEnable()
        {
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_sdfPrimitive = target as SDFPrimitive;
            m_setter = new SerializedPropertySetter(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.DrawScript();
            m_setter.Clear();

            m_setter.DrawProperty(Labels.Type, m_serializedProperties.Type);
            m_setter.DrawProperty(Labels.Operation, m_serializedProperties.Operation);
            m_setter.DrawProperty(Labels.Flip, m_serializedProperties.Flip);
            m_setter.DrawFloatSetting(Labels.Smoothing, m_serializedProperties.Smoothing, min: 0f);

            using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                switch (m_sdfPrimitive.Type)
                {
                    case SDFPrimitiveType.Sphere:
                        m_setter.DrawVectorSettingX(Labels.Radius, m_serializedProperties.Data, min: 0f);
                        break;
                    case SDFPrimitiveType.Torus:
                        m_setter.DrawVectorSettingX(Labels.MajorRadius, m_serializedProperties.Data, min: 0f);
                        m_setter.DrawVectorSettingY(Labels.MinorRadius, m_serializedProperties.Data, min: 0f);
                        break;
                    case SDFPrimitiveType.Cuboid:
                        m_setter.DrawVector3Setting(Labels.Bounds, m_serializedProperties.Data, min: 0f);
                        m_setter.DrawVectorSettingW(Labels.Roundedness, m_serializedProperties.Data, min: 0f);
                        break;
                    case SDFPrimitiveType.BoxFrame:
                        m_setter.DrawVector3Setting(Labels.Bounds, m_serializedProperties.Data, min: 0f);
                        m_setter.DrawVectorSettingW(Labels.Thickness, m_serializedProperties.Data, min: 0f);
                        break;
                    case SDFPrimitiveType.Cylinder:
                        m_setter.DrawVectorSettingX(Labels.CylinderRadius, m_serializedProperties.Data, min: 0f);
                        m_setter.DrawVectorSettingY(Labels.CylinderLength, m_serializedProperties.Data, min: 0f);
                        break;
                }
            }

            if (m_isMaterialOpen = EditorGUILayout.Foldout(m_isMaterialOpen, Labels.Material, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawProperty(Labels.MaterialType, m_serializedProperties.MaterialType);

                        if (m_sdfPrimitive.Material.Type != SDFMaterial.MaterialType.None)
                        {
                            if (m_sdfPrimitive.Material.Type == SDFMaterial.MaterialType.Texture)
                                m_setter.DrawProperty(Labels.MaterialTexture, m_serializedProperties.MaterialTexture);

                            m_setter.DrawProperty(Labels.Colour, m_serializedProperties.Colour);
                            m_setter.DrawFloatSetting(Labels.MaterialSmoothing, m_serializedProperties.MaterialSmoothing, min: 0f);
                            m_setter.DrawProperty(Labels.Emission, m_serializedProperties.Emission);
                            m_setter.DrawProperty(Labels.Metallic, m_serializedProperties.Metallic);
                            m_setter.DrawProperty(Labels.Smoothness, m_serializedProperties.Smoothness);
                            m_setter.DrawProperty(Labels.SubsurfaceColour, m_serializedProperties.SubsurfaceColour);
                            m_setter.DrawProperty(Labels.SubsurfaceScatteringPower, m_serializedProperties.SubsurfaceScatteringPower);
                        }
                    }
                }
            }

            m_setter.Update();
        }
    }
}