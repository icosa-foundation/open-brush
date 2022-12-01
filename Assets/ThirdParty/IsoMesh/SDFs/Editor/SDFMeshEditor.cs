using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFMesh))]
    [CanEditMultipleObjects]
    public class SDFMeshEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent MeshAsset = new GUIContent("Mesh Asset", "An SDFMeshAsset ScriptableObject. You can create these in 'Tools/Mesh to SDF'");
            public static GUIContent Operation = new GUIContent("Operation", "How this primitive is combined with the previous SDF objects in the hierarchy.");
            public static GUIContent Flip = new GUIContent("Flip", "Turn this object inside out.");
            public static GUIContent Smoothing = new GUIContent("Smoothing", "How smoothly this sdf blends with the previous SDFs.");

            public static GUIContent Material = new GUIContent("Material", "The visual properties of this SDF object.");
            public static GUIContent MaterialType = new GUIContent("Type", "Whether this object has no effect on the group colours, just a pure colour, or is fully textured.");
            public static GUIContent MaterialTexture = new GUIContent("Texture", "The texture which will be applied to this object via the UVs of the original mesh.");
            public static GUIContent Colour = new GUIContent("Colour", "Colour of this SDF object.");
            public static GUIContent Emission = new GUIContent("Emission", "Emission of this primitive, must be used alongside post processing (bloom).");
            public static GUIContent MaterialSmoothing = new GUIContent("Material Smoothing", "How sharply this material is combined with other SDF objects.");
            public static GUIContent Metallic = new GUIContent("Metallic", "Metallicity of this object's material.");
            public static GUIContent Smoothness = new GUIContent("Smoothness", "Smoothness of this object's material.");
            public static GUIContent SubsurfaceColour = new GUIContent("Subsurface Colour", "Colour of the inside of this SDF object, used by subsurface scattering.");
            public static GUIContent SubsurfaceScatteringPower = new GUIContent("Subsurface Scattering Power", "Strength of the subsurface scattering effect.");
            
            public static string MeshAssetRequiredMessage = "SDF Mesh objects must have a reference to an SDFMeshAsset ScriptableObject. You can create these in 'Tools/Mesh to SDF'";
        }

        private class SerializedProperties
        {
            public SerializedProperty MeshAsset { get; }
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
                MeshAsset = serializedObject.FindProperty("m_asset");
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
        private SDFMesh m_sdfMesh;
        private SerializedPropertySetter m_setter;

        private bool m_isMaterialOpen = true;

        private void OnEnable()
        {
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_sdfMesh = target as SDFMesh;
            m_setter = new SerializedPropertySetter(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.DrawScript();
            m_setter.Clear();
            
            m_setter.DrawProperty(Labels.MeshAsset, m_serializedProperties.MeshAsset);

            bool hasMeshAsset = m_serializedProperties.MeshAsset.objectReferenceValue;

            if (!hasMeshAsset)
                EditorGUILayout.HelpBox(Labels.MeshAssetRequiredMessage, MessageType.Warning);

            GUI.enabled = hasMeshAsset;

            m_setter.DrawProperty(Labels.Operation, m_serializedProperties.Operation);
            m_setter.DrawProperty(Labels.Flip, m_serializedProperties.Flip);
            m_setter.DrawFloatSetting(Labels.Smoothing, m_serializedProperties.Smoothing, min: 0f);
            
            if (m_isMaterialOpen = EditorGUILayout.Foldout(m_isMaterialOpen, Labels.Material, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawProperty(Labels.MaterialType, m_serializedProperties.MaterialType);

                        if (m_sdfMesh.Material.Type != SDFMaterial.MaterialType.None)
                        {
                            if (m_sdfMesh.Material.Type == SDFMaterial.MaterialType.Texture)
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

            GUI.enabled = true;
        }
    }
}