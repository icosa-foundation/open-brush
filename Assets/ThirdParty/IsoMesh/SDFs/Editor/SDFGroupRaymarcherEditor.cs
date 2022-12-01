using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace IsoMesh.Editor
{
    [CustomEditor(typeof(SDFGroupRaymarcher))]
    [CanEditMultipleObjects]
    public class SDFGroupRaymarcherEditor : UnityEditor.Editor
    {
        private static class Labels
        {
            public static GUIContent VisualSettings = new GUIContent("Visual Settings");
            public static GUIContent Material = new GUIContent("Material", "This is the material the sdf data is sent to.");
            public static GUIContent SDFGroup = new GUIContent("SDF Group", "An SDF group is a collection of sdf primitives, meshes, and operations which mutually interact.");
            public static GUIContent Size = new GUIContent("Size", "The size and shape of the raymarching volume.");
            public static GUIContent DiffuseColour = new GUIContent("Diffuse Colour", "The diffuse colour of the raymarched shapes.");
            public static GUIContent AmbientColour = new GUIContent("Ambient Colour", "The ambient, or 'base' colour of the raymarched shapes.");
            public static GUIContent GlossPower = new GUIContent("Gloss Power", "The gloss/specular power of the raymarched shapes.");
            public static GUIContent GlossMultiplier = new GUIContent("Gloss Multiplier", "A multiplier for the contribution of the glossiness.");
        }

        private class SerializedProperties
        {
            public SerializedProperty SDFGroup { get; }
            public SerializedProperty Material { get; }
            public SerializedProperty Size { get; }
            public SerializedProperty DiffuseColour { get; }
            public SerializedProperty AmbientColour { get; }
            public SerializedProperty GlossPower { get; }
            public SerializedProperty GlossMultiplier { get; }

            public SerializedProperties(SerializedObject serializedObject)
            {
                SDFGroup = serializedObject.FindProperty("m_group");
                Material = serializedObject.FindProperty("m_material");
                Size = serializedObject.FindProperty("m_size");
                DiffuseColour = serializedObject.FindProperty("m_diffuseColour");
                AmbientColour = serializedObject.FindProperty("m_ambientColour");
                GlossPower = serializedObject.FindProperty("m_glossPower");
                GlossMultiplier = serializedObject.FindProperty("m_glossMultiplier");
            }
        }

        private SDFGroupRaymarcher m_raymarcher;

        private SerializedProperties m_serializedProperties;
        private bool m_isVisualSettingsOpen = true;
        private SerializedPropertySetter m_setter;

        private void OnEnable()
        {
            m_raymarcher = target as SDFGroupRaymarcher;
            m_serializedProperties = new SerializedProperties(serializedObject);
            m_setter = new SerializedPropertySetter(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            m_setter.Clear();
            serializedObject.DrawScript();

            GUI.enabled = false;
            EditorGUILayout.PropertyField(m_serializedProperties.SDFGroup, Labels.SDFGroup);
            EditorGUILayout.PropertyField(m_serializedProperties.Material, Labels.Material);
            GUI.enabled = true;

            if (m_isVisualSettingsOpen = EditorGUILayout.Foldout(m_isVisualSettingsOpen, Labels.VisualSettings, true))
            {
                using (EditorGUILayout.VerticalScope box = new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
                    {
                        m_setter.DrawVector3Setting(Labels.Size, m_serializedProperties.Size, onValueChangedCallback: m_raymarcher.UpdateCubeMesh);
                        m_setter.DrawColourSetting(Labels.DiffuseColour, m_serializedProperties.DiffuseColour, m_raymarcher.OnVisualsChanged);
                        m_setter.DrawColourSetting(Labels.AmbientColour, m_serializedProperties.AmbientColour, m_raymarcher.OnVisualsChanged);
                        m_setter.DrawFloatSetting(Labels.GlossPower, m_serializedProperties.GlossPower, onValueChangedCallback: m_raymarcher.OnVisualsChanged);
                        m_setter.DrawFloatSetting(Labels.GlossMultiplier, m_serializedProperties.GlossMultiplier, onValueChangedCallback: m_raymarcher.OnVisualsChanged);
                    }
                }
            }

            m_setter.Update();
        }

        private void OnSceneGUI()
        {
            Handles.color = Color.white;
            Handles.matrix = m_raymarcher.transform.localToWorldMatrix;
            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            Handles.DrawWireCube(Vector3.zero, m_raymarcher.Size);
        }
    }
}